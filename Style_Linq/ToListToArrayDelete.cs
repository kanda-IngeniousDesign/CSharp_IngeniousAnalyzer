using System.Collections.Generic;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using CSharp_IngeniousAnalyzer.Style__Common;

namespace CSharp_IngeniousAnalyzer.Style_Linq;

// ToList/ToArray を削除すると、チェーン（Where/Select等）のラムダの実行が「確定させた行」から
// 「foreach で列挙する時点」へ後ろにずれ、ループ本体と交互に実行されるようになる。
// また ToList() だけを削除すると、変数が列挙元と同じインスタンスを指すようになる。
// そのため Fix で動作が変わらないことを保証するには、確定させてからループが終わるまでの間に、
// チェーンの結果を変えうる処理が一切ないことを確認する必要がある。
//
// 次の前提は解析では確認できないため、成り立つものとして扱う：
// ・プロパティ/インデクサーの getter、演算子、型変換、ToString/Equals/GetHashCode/CompareTo/Deconstruct は副作用を持たない
// ・列挙元そのものの列挙（GetEnumerator/MoveNext/Current。yield を使った独自の拡張メソッドや、
//   Distinct 等に渡す IEqualityComparer/IComparer を含む）は副作用を持たない
// ・静的コンストラクターは、実行されるタイミングがずれても結果に影響する副作用を持たない
// ・他のスレッドから列挙元やチェーンが参照する値を変更しない
// ・チェーンの評価が例外を投げない（投げる場合、例外の発生タイミングや、ループを途中で抜けたときに例外が起きるかどうかが変わる）
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public class ToListToArrayDelete : CommonAnalyzer
{
    public const string DiagnosticId = "LINQ002";
    private const string Category = "Performance";
    private static readonly LocalizableString Title = CreateLocalStr(nameof(ResourceEnum.LINQ002_Title));
    private static readonly LocalizableString MessageFormat = CreateLocalStr(nameof(ResourceEnum.LINQ002_Message));

    protected override DiagnosticDescriptor Rule { get; } = new(
        DiagnosticId, Title, MessageFormat, Category, DiagnosticSeverity.Warning, isEnabledByDefault: true);

    protected override SyntaxKind[] TargetKinds => [SyntaxKind.InvocationExpression];

    protected override void AnalyzeNode(SyntaxNodeAnalysisContext context)
    {
        if (IsGeneratedFile(context)) return;
        var invocation = (InvocationExpressionSyntax)context.Node;
        var model = context.SemanticModel;

        if (!IsMaterializationMethod(invocation, model, context.CancellationToken))
        {
            return;
        }

        // null条件演算子（?.）経由の呼び出しは、Fixで書き換えられず列挙元も解析できないため対象外
        if (invocation.Expression is not MemberAccessExpressionSyntax)
        {
            return;
        }

        // foreach直接、または安全性が証明できる変数経由のどちらかであれば警告する
        bool isRedundant = IsDirectForEachUsage(invocation, model, context.CancellationToken) ||
                           IsRedundantVariableAssignment(invocation, model, context.CancellationToken);

        if (isRedundant)
        {
            var receiverName = GetReceiverName(invocation);
            context.ReportDiagnostic(Diagnostic.Create(Rule, invocation.GetLocation(), receiverName));
        }
    }

    /// <summary>
    /// 呼び出しが System.Linq.Enumerable の ToList または ToArray であるかを厳密に判定します
    /// </summary>
    private static bool IsMaterializationMethod(InvocationExpressionSyntax invocation, SemanticModel model, CancellationToken cancellationToken)
    {
        var methodSymbol = model.GetSymbolInfo(invocation, cancellationToken).Symbol as IMethodSymbol;
        if (methodSymbol == null)
        {
            return false;
        }

        var containingType = methodSymbol.ContainingType;
        if (containingType == null || containingType.ToDisplayString() != "System.Linq.Enumerable")
        {
            return false;
        }

        // Enumerable.ToList(src) のような静的メソッド形式は、Fixで「src.」の部分を取り出せないため対象外
        if (methodSymbol.MethodKind != MethodKind.ReducedExtension)
        {
            return false;
        }

        var name = methodSymbol.Name;
        return name == "ToList" || name == "ToArray";
    }

    /// <summary>
    /// foreach ステートメントの式として直接使用されており、かつ削除しても動作が変わらないかを判定します
    /// </summary>
    private static bool IsDirectForEachUsage(InvocationExpressionSyntax invocation, SemanticModel model, CancellationToken cancellationToken)
    {
        if (invocation.Parent is not ForEachStatementSyntax forEach || forEach.Expression != invocation)
        {
            return false;
        }

        // 直接 foreach に渡している場合、確定から列挙までの間に処理は挟まらないため、ループ本体だけを確認すればよい
        // （外側のループがあっても、周回ごとにチェーンごと作り直されるため ToList の有無で差は出ない）
        return IsSafeToDefer(invocation, forEach, forEach.Statement.DescendantNodesAndSelf(), model, cancellationToken);
    }

    /// <summary>
    /// 変数宣言を経由している場合でも、後続で安全に書き換えられず foreach で1回だけ使われるケースを検証します
    /// </summary>
    private static bool IsRedundantVariableAssignment(
        InvocationExpressionSyntax invocation,
        SemanticModel model,
        CancellationToken cancellationToken)
    {
        var declarator = FindVariableDeclarator(invocation);
        if (declarator == null)
        {
            return false;
        }

        if (!TryGetTargetSymbolAndType(declarator, model, cancellationToken, out var targetSymbol, out var targetVariableType, out var isVar))
        {
            return false;
        }

        // 型の暗黙的変換が成立するか確認（varの場合は型一致なのでOK）
        if (!isVar && !CanBeAssignedWithoutToList(invocation, targetVariableType!, model, cancellationToken))
        {
            return false;
        }

        // 定義された変数が、後続で再代入されず、かつ foreach の式として1回だけ参照されているか
        return HasSingleSafeForEachReference(invocation, targetSymbol!, model, cancellationToken);
    }

    /// <summary>
    /// 呼び出しの祖先から変数宣言子（VariableDeclaratorSyntax）を特定します
    /// </summary>
    private static VariableDeclaratorSyntax? FindVariableDeclarator(InvocationExpressionSyntax invocation)
    {
        SyntaxNode? current = invocation.Parent;
        while (current != null)
        {
            if (current is VariableDeclaratorSyntax declarator)
            {
                return declarator;
            }
            if (current is StatementSyntax || current is MemberDeclarationSyntax)
            {
                break;
            }
            current = current.Parent;
        }
        return null;
    }

    /// <summary>
    /// 対象シンボルと変数の型、var宣言であるかを取得します
    /// </summary>
    private static bool TryGetTargetSymbolAndType(
        VariableDeclaratorSyntax declarator,
        SemanticModel model,
        CancellationToken cancellationToken,
        out ISymbol? symbol,
        out ITypeSymbol? type,
        out bool isVar)
    {
        symbol = model.GetDeclaredSymbol(declarator, cancellationToken);
        type = null;
        isVar = false;

        if (symbol == null)
        {
            return false;
        }

        if (declarator.Parent is VariableDeclarationSyntax varDeclSyntax)
        {
            isVar = varDeclSyntax.Type.IsVar;
            type = model.GetTypeInfo(varDeclSyntax.Type, cancellationToken).Type;
        }

        return true;
    }

    /// <summary>
    /// ToList/ToArray を除外した元の式の型が代入先の型へ暗黙的に変換可能であるかを判定します
    /// </summary>
    private static bool CanBeAssignedWithoutToList(
        InvocationExpressionSyntax invocation,
        ITypeSymbol targetVariableType,
        SemanticModel model,
        CancellationToken cancellationToken)
    {
        if (invocation.Expression is MemberAccessExpressionSyntax memberAccess)
        {
            var expressionWithoutToList = memberAccess.Expression;
            var conversion = model.ClassifyConversion(expressionWithoutToList, targetVariableType);
            return conversion.IsImplicit;
        }

        return false;
    }

    /// <summary>
    /// 変数が定義以降に再代入されず、非代入参照として foreach で1回だけ安全に使用されているかを検証します
    /// （LINQ や List などの無駄なアロケーションを排除）
    /// </summary>
    private static bool HasSingleSafeForEachReference(
        InvocationExpressionSyntax invocation,
        ISymbol targetSymbol,
        SemanticModel model,
        CancellationToken cancellationToken)
    {
        // 祖先から MethodDeclarationSyntax または ParenthesizedLambdaExpressionSyntax などのボディスコープを特定
        SyntaxNode? methodBody = null;
        var current = invocation.Parent;
        while (current != null)
        {
            if (current is MethodDeclarationSyntax methodDecl)
            {
                methodBody = methodDecl.Body;
                break;
            }
            if (current is AccessorDeclarationSyntax accessorDecl)
            {
                methodBody = accessorDecl.Body;
                break;
            }
            if (current is LocalFunctionStatementSyntax localFunc)
            {
                methodBody = localFunc.Body;
                break;
            }
            if (current is ConstructorDeclarationSyntax ctorDecl)
            {
                methodBody = ctorDecl.Body;
                break;
            }
            current = current.Parent;
        }

        if (methodBody == null)
        {
            return false;
        }

        var invocationSpanStart = invocation.SpanStart;

        // LINQ の Where / ToList などを排除し、foreach 参照のカウンティングと再代入チェックを単一のループで効率的に実行
        int nonAssignmentRefCount = 0;
        ForEachStatementSyntax? foundForEach = null;

        foreach (var node in methodBody.DescendantNodes())
        {
            if (node is IdentifierNameSyntax id && id.SpanStart > invocationSpanStart)
            {
                if (SymbolEqualityComparer.Default.Equals(model.GetSymbolInfo(id, cancellationToken).Symbol, targetSymbol))
                {
                    // 再代入チェック（代入式の左辺である場合）
                    if (id.Parent is AssignmentExpressionSyntax assign && assign.Left == id)
                    {
                        return false; // 再代入されている場合は即座に安全ではないと判定
                    }

                    // 非代入参照のカウント
                    nonAssignmentRefCount++;
                    if (nonAssignmentRefCount > 1)
                    {
                        return false; // 2回以上参照されている場合はNG
                    }

                    // 参照元が foreach の式であるか確認
                    if (id.Parent is ForEachStatementSyntax forEachStmt && forEachStmt.Expression == id)
                    {
                        foundForEach = forEachStmt;
                    }
                    else
                    {
                        return false; // foreach 以外の場所で参照されている場合はNG
                    }
                }
            }
        }

        if (nonAssignmentRefCount != 1 || foundForEach == null)
        {
            return false;
        }

        // goto があると、テキスト上の順序と実行順序が一致せず、確定からループ終了までの区間を特定できないため警告しない
        if (methodBody.DescendantNodes().OfType<GotoStatementSyntax>().Any())
        {
            return false;
        }

        // foreach が宣言とは別のラムダ/ローカル関数の中にある場合、そのラムダがいつ何回呼ばれるか分からず、
        // 間に列挙元を変更する処理が挟まる可能性を排除できないため警告しない
        foreach (var ancestor in foundForEach.Ancestors())
        {
            if (ancestor == methodBody || ancestor.Span.Contains(invocation.Span))
            {
                break;
            }
            if (ancestor is AnonymousFunctionExpressionSyntax || ancestor is LocalFunctionStatementSyntax)
            {
                return false;
            }
        }

        // 1回確定させた結果を foreach が何度も列挙しうる場合は警告しない。
        // ToList/ToArray を削除すると列挙のたびにチェーンを評価し直すことになり、性能が悪化するうえ、
        // ラムダで作った配列などが列挙のたびに別のインスタンスになり、周回の間で結果が変わりうるため
        if (MayEnumerateMoreThanOnce(invocation, foundForEach))
        {
            return false;
        }

        // 確定させてから、ループが終わるまでの区間
        var regionStart = invocation.Span.End;
        var regionEnd = foundForEach.Span.End;
        var regionNodes = methodBody.DescendantNodes().Where(n => regionStart <= n.SpanStart && n.SpanStart < regionEnd);

        return IsSafeToDefer(invocation, foundForEach, regionNodes, model, cancellationToken);
    }

    /// <summary>
    /// foreach が、確定させる処理を繰り返さないループ（宣言を含まない外側のループや、for の初期化子で宣言したループ）の中にあり、
    /// 1回の確定に対して複数回実行されうるかを判定します
    /// </summary>
    private static bool MayEnumerateMoreThanOnce(InvocationExpressionSyntax invocation, ForEachStatementSyntax foundForEach)
    {
        foreach (var ancestor in foundForEach.Ancestors())
        {
            var isLoop = ancestor is ForStatementSyntax || ancestor is CommonForEachStatementSyntax ||
                         ancestor is WhileStatementSyntax || ancestor is DoStatementSyntax;

            // 宣言と foreach の共通の祖先より外側は、宣言と foreach をまとめて繰り返すため問題ない。
            // 共通の祖先がループそのものになるのは for の初期化子で宣言した場合だけで、
            // そのときは宣言が1回だけで、本体の foreach は周回ごとに実行される
            if (ancestor.Span.Contains(invocation.Span))
            {
                return isLoop;
            }

            // 宣言を含まないループの中に foreach がある
            if (isLoop)
            {
                return true;
            }
        }
        return false;
    }

    /// <summary>
    /// ToList/ToArray を削除してチェーンの評価を列挙時まで遅らせても、結果が変わらないことを確認します
    /// </summary>
    private static bool IsSafeToDefer(
        InvocationExpressionSyntax invocation,
        ForEachStatementSyntax targetForEach,
        IEnumerable<SyntaxNode> regionNodes,
        SemanticModel model,
        CancellationToken cancellationToken)
    {
        var chain = ((MemberAccessExpressionSyntax)invocation.Expression).Expression;
        if (!EnumeratesSameElements(invocation, chain, model, cancellationToken))
        {
            return false;
        }

        if (!IsChainFreeOfSideEffects(chain, model, cancellationToken))
        {
            return false;
        }

        var chainSymbols = CollectChainSymbols(invocation, chain, model, cancellationToken);
        return !RegionMayAffectChain(regionNodes, targetForEach, chainSymbols, model, cancellationToken);
    }

    /// <summary>
    /// ToList/ToArray を削除して foreach が受け手を直接列挙するようになっても、同じ型の要素を同じ方法で列挙するかを判定します。
    /// ToList&lt;object&gt;() のように型引数を明示している場合や、受け手の公開 GetEnumerator() が IEnumerable&lt;T&gt; の実装と
    /// 異なる場合（MatchCollection 等）は、ループ変数の型や列挙される内容が変わるため対象外とします
    /// </summary>
    private static bool EnumeratesSameElements(InvocationExpressionSyntax invocation, ExpressionSyntax chain, SemanticModel model, CancellationToken cancellationToken)
    {
        if (model.GetSymbolInfo(invocation, cancellationToken).Symbol is not IMethodSymbol { TypeArguments.Length: 1 } materialization)
        {
            return false;
        }
        var elementType = materialization.TypeArguments[0];

        // 受け手を直接 foreach した場合にコンパイラーが選ぶ GetEnumerator と要素の型を、仮の foreach 文で求める
        var probe = SyntaxFactory.ForEachStatement(
            SyntaxFactory.IdentifierName("var"),
            SyntaxFactory.Identifier("linq002Probe"),
            chain.WithoutTrivia(),
            SyntaxFactory.Block());
        if (!model.TryGetSpeculativeSemanticModel(invocation.SpanStart, probe, out var speculativeModel))
        {
            return false;
        }

        var info = speculativeModel.GetForEachStatementInfo(probe);
        if (!SymbolEqualityComparer.Default.Equals(info.ElementType, elementType))
        {
            return false;
        }

        var getEnumerator = info.GetEnumeratorMethod;
        if (getEnumerator == null)
        {
            return false;
        }

        // 公開 GetEnumerator() が IEnumerable<T>.GetEnumerator() の実装そのものか、.NET の標準コレクション
        // （配列や IEnumerable<T> 等のインターフェイス経由の列挙を含む）であれば同じ内容を列挙する
        var enumerableOfT = model.Compilation.GetTypeByMetadataName("System.Collections.Generic.IEnumerable`1")?.Construct(elementType);
        var interfaceGetEnumerator = enumerableOfT?.GetMembers("GetEnumerator").FirstOrDefault();
        var receiverType = model.GetTypeInfo(chain, cancellationToken).Type;
        if (interfaceGetEnumerator != null && receiverType != null &&
            SymbolEqualityComparer.Default.Equals(receiverType.FindImplementationForInterfaceMember(interfaceGetEnumerator), getEnumerator))
        {
            return true;
        }
        return IsInSystemNamespace(getEnumerator.ContainingType);
    }

    /// <summary>
    /// 型が .NET 標準ライブラリの System 名前空間（またはその配下）に属しているかを判定します
    /// </summary>
    private static bool IsInSystemNamespace(INamedTypeSymbol type)
    {
        var ns = type.ContainingNamespace;
        while (ns?.ContainingNamespace != null && !ns.ContainingNamespace.IsGlobalNamespace)
        {
            ns = ns.ContainingNamespace;
        }
        return ns != null && !ns.IsGlobalNamespace && ns.Name == "System";
    }

    /// <summary>
    /// チェーンを遅延評価にしても結果や副作用の順序が変わらないよう、チェーン自体が副作用を持たないかを判定します。
    /// ラムダの外（Where/Select等の呼び出し自体や、Take(n) の n など）は確定時に1回だけ評価されるため対象外とし、
    /// 列挙時に評価されるラムダの中身と、デリゲートとして渡される引数を確認します
    /// </summary>
    private static bool IsChainFreeOfSideEffects(ExpressionSyntax chain, SemanticModel model, CancellationToken cancellationToken)
    {
        // DBクエリ等の IQueryable は、列挙時に外部へ問い合わせるため、評価を遅らせると結果が変わりうる
        for (ExpressionSyntax? current = chain; current != null; current = GetReceiver(current))
        {
            if (ImplementsQueryable(model.GetTypeInfo(current, cancellationToken).Type))
            {
                return false;
            }
        }

        foreach (var node in chain.DescendantNodesAndSelf())
        {
            // クエリ構文は暗黙のラムダを含み、メソッド呼び出しとして解析できないため安全側に倒す
            if (node is QueryExpressionSyntax)
            {
                return false;
            }

            // メソッドグループやデリゲート変数を渡すと、列挙時に実行される処理の中身を確認できない
            if (node is ArgumentSyntax argument &&
                argument.Expression is not AnonymousFunctionExpressionSyntax &&
                model.GetTypeInfo(argument.Expression, cancellationToken).ConvertedType?.TypeKind == TypeKind.Delegate)
            {
                return false;
            }

            if (IsInsideLambda(node, chain) && HasSideEffectInLambda(node, model, cancellationToken))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// メソッドチェーン上の1つ手前の受け手（a.Where(...) なら a）を取得します
    /// </summary>
    private static ExpressionSyntax? GetReceiver(ExpressionSyntax expression)
    {
        return expression switch
        {
            InvocationExpressionSyntax { Expression: MemberAccessExpressionSyntax memberAccess } => memberAccess.Expression,
            MemberAccessExpressionSyntax memberAccess => memberAccess.Expression,
            ParenthesizedExpressionSyntax parenthesized => parenthesized.Expression,
            _ => null,
        };
    }

    /// <summary>
    /// 型が System.Linq.IQueryable を実装しているかを判定します
    /// </summary>
    private static bool ImplementsQueryable(ITypeSymbol? type)
    {
        if (type == null)
        {
            return false;
        }
        if (type.ToDisplayString() == "System.Linq.IQueryable")
        {
            return true;
        }
        return type.AllInterfaces.Any(i => i.ToDisplayString() == "System.Linq.IQueryable");
    }

    /// <summary>
    /// ノードがチェーン内のラムダ（列挙時に実行される部分）の中にあるかを判定します
    /// </summary>
    private static bool IsInsideLambda(SyntaxNode node, ExpressionSyntax chain)
    {
        foreach (var ancestor in node.Ancestors())
        {
            if (ancestor == chain)
            {
                return false;
            }
            if (ancestor is AnonymousFunctionExpressionSyntax)
            {
                return true;
            }
        }
        return false;
    }

    /// <summary>
    /// チェーン内のラムダの中で、副作用を持ちうる、または評価するたびに結果が変わりうる処理であるかを判定します
    /// </summary>
    private static bool HasSideEffectInLambda(SyntaxNode node, SemanticModel model, CancellationToken cancellationToken)
    {
        switch (node)
        {
            case InvocationExpressionSyntax invocation:
                // LINQ のメソッド自体は副作用を持たない（渡すデリゲートは ArgumentSyntax 側で確認する）
                return !IsNameOf(invocation, model, cancellationToken) &&
                       (model.GetSymbolInfo(invocation, cancellationToken).Symbol as IMethodSymbol)?.ContainingType?.ToDisplayString() != "System.Linq.Enumerable";
            case BaseObjectCreationExpressionSyntax:
            case WithExpressionSyntax:
            case CollectionExpressionSyntax:
            case AwaitExpressionSyntax:
            case AssignmentExpressionSyntax:
            case PositionalPatternClauseSyntax:
            case CommonForEachStatementSyntax:
            case UsingStatementSyntax:
                return true;
            case PrefixUnaryExpressionSyntax prefix:
                return IsIncrementOrDecrement(prefix.Kind());
            case PostfixUnaryExpressionSyntax postfix:
                return IsIncrementOrDecrement(postfix.Kind());
            case IdentifierNameSyntax id:
                // DateTime.Now の Now 部分も、using static System.DateTime; による単独の Now も識別子として現れる
                return IsNonDeterministicProperty(model.GetSymbolInfo(id, cancellationToken).Symbol);
            default:
                return false;
        }
    }

    /// <summary>
    /// 現在時刻など、評価するタイミングによって値が変わるプロパティであるかを判定します
    /// </summary>
    private static bool IsNonDeterministicProperty(ISymbol? symbol)
    {
        if (symbol is not IPropertySymbol property || property.ContainingType == null)
        {
            return false;
        }

        return property.ContainingType.ToDisplayString() switch
        {
            "System.DateTime" => property.Name is "Now" or "UtcNow" or "Today",
            "System.DateTimeOffset" => property.Name is "Now" or "UtcNow",
            "System.Environment" => property.Name is "TickCount" or "TickCount64",
            "System.Diagnostics.Stopwatch" => property.Name is "Elapsed" or "ElapsedMilliseconds" or "ElapsedTicks",
            _ => false,
        };
    }

    /// <summary>
    /// チェーンが参照している変数（列挙元と、ラムダが参照するローカル変数・引数）を収集します
    /// </summary>
    private static HashSet<ISymbol> CollectChainSymbols(
        InvocationExpressionSyntax invocation,
        ExpressionSyntax chain,
        SemanticModel model,
        CancellationToken cancellationToken)
    {
        var symbols = new HashSet<ISymbol>(SymbolEqualityComparer.Default);

        var rootSymbol = GetChainRootSymbol(invocation, model, cancellationToken);
        if (rootSymbol != null)
        {
            symbols.Add(rootSymbol);
        }

        foreach (var id in chain.DescendantNodesAndSelf().OfType<IdentifierNameSyntax>())
        {
            var symbol = model.GetSymbolInfo(id, cancellationToken).Symbol;
            if (symbol is ILocalSymbol || symbol is IParameterSymbol)
            {
                symbols.Add(symbol);
            }
        }

        return symbols;
    }

    /// <summary>
    /// 確定させてからループが終わるまでの区間に、チェーンの結果を変えうる処理が含まれているかを判定します。
    /// 呼び出し先で何をしているかは解析できないため、メソッド呼び出しは内容を問わず変更の可能性ありとみなします
    /// </summary>
    private static bool RegionMayAffectChain(
        IEnumerable<SyntaxNode> regionNodes,
        ForEachStatementSyntax targetForEach,
        HashSet<ISymbol> chainSymbols,
        SemanticModel model,
        CancellationToken cancellationToken)
    {
        foreach (var node in regionNodes)
        {
            switch (node)
            {
                case InvocationExpressionSyntax invocation when !IsNameOf(invocation, model, cancellationToken):
                case BaseObjectCreationExpressionSyntax:
                case WithExpressionSyntax:
                case CollectionExpressionSyntax:
                case AwaitExpressionSyntax:
                case QueryExpressionSyntax:
                case PositionalPatternClauseSyntax:
                case UsingStatementSyntax:
                case YieldStatementSyntax yield when yield.IsKind(SyntaxKind.YieldReturnStatement):
                case LocalDeclarationStatementSyntax localDeclaration when localDeclaration.UsingKeyword != default:
                    return true;
                case CommonForEachStatementSyntax forEach when forEach != targetForEach:
                    return true;
                case AssignmentExpressionSyntax assignment when !IsLocalWriteTarget(assignment.Left, model, cancellationToken):
                    return true;
                case PrefixUnaryExpressionSyntax prefix when IsIncrementOrDecrement(prefix.Kind()) && !IsLocalWriteTarget(prefix.Operand, model, cancellationToken):
                    return true;
                case PostfixUnaryExpressionSyntax postfix when IsIncrementOrDecrement(postfix.Kind()) && !IsLocalWriteTarget(postfix.Operand, model, cancellationToken):
                    return true;
                case IdentifierNameSyntax id when model.GetSymbolInfo(id, cancellationToken).Symbol is { } symbol && chainSymbols.Contains(symbol):
                    return true;
            }
        }

        return false;
    }

    /// <summary>
    /// 書き込み先が、ほかの場所から見えないローカル変数・値渡しの引数・破棄（_）だけであるかを判定します。
    /// ref ローカル、プライマリコンストラクターの引数（実体はフィールド）、ラムダ/ローカル関数に捕まえられている変数、
    /// アドレスを取られている変数は、別の場所で作られた遅延クエリ等から参照されうるため対象外とします
    /// </summary>
    private static bool IsLocalWriteTarget(ExpressionSyntax target, SemanticModel model, CancellationToken cancellationToken)
    {
        switch (target)
        {
            case TupleExpressionSyntax tuple:
                return tuple.Arguments.All(a => IsLocalWriteTarget(a.Expression, model, cancellationToken));
            case DeclarationExpressionSyntax:
                return true;
            case IdentifierNameSyntax id:
                return model.GetSymbolInfo(id, cancellationToken).Symbol switch
                {
                    IDiscardSymbol => true,
                    ILocalSymbol local => !local.IsRef && !IsVisibleElsewhere(local, model, cancellationToken),
                    IParameterSymbol parameter => parameter.RefKind == RefKind.None &&
                                                  !IsPrimaryConstructorParameter(parameter) &&
                                                  !IsVisibleElsewhere(parameter, model, cancellationToken),
                    _ => false,
                };
            default:
                return false;
        }
    }

    /// <summary>
    /// 引数がクラス/構造体のプライマリコンストラクターの引数であるかを判定します
    /// </summary>
    private static bool IsPrimaryConstructorParameter(IParameterSymbol parameter)
    {
        return parameter.DeclaringSyntaxReferences.Any(r => r.GetSyntax() is ParameterSyntax { Parent.Parent: TypeDeclarationSyntax });
    }

    /// <summary>
    /// 変数が、宣言されたスコープ以外のラムダ/ローカル関数に捕まえられているか、アドレスを取られているかを判定します
    /// （例: 先に作った遅延クエリ src.Where(x =&gt; x &gt; limit) が limit を捕まえている場合）
    /// </summary>
    private static bool IsVisibleElsewhere(ISymbol variable, SemanticModel model, CancellationToken cancellationToken)
    {
        foreach (var reference in variable.DeclaringSyntaxReferences)
        {
            var declaration = reference.GetSyntax(cancellationToken);
            var root = declaration.SyntaxTree.GetRoot(cancellationToken);
            foreach (var id in root.DescendantNodes().OfType<IdentifierNameSyntax>())
            {
                if (id.Identifier.ValueText != variable.Name ||
                    !SymbolEqualityComparer.Default.Equals(model.GetSymbolInfo(id, cancellationToken).Symbol, variable))
                {
                    continue;
                }

                if (id.Parent is PrefixUnaryExpressionSyntax prefix && prefix.IsKind(SyntaxKind.AddressOfExpression))
                {
                    return true;
                }

                var closure = id.Ancestors().FirstOrDefault(a => a is AnonymousFunctionExpressionSyntax || a is LocalFunctionStatementSyntax);
                if (closure != null && !closure.Span.Contains(declaration.Span))
                {
                    return true;
                }
            }
        }
        return false;
    }

    /// <summary>
    /// インクリメント/デクリメント演算子であるかを判定します
    /// </summary>
    private static bool IsIncrementOrDecrement(SyntaxKind kind)
    {
        return kind is SyntaxKind.PreIncrementExpression or SyntaxKind.PreDecrementExpression
                    or SyntaxKind.PostIncrementExpression or SyntaxKind.PostDecrementExpression;
    }

    /// <summary>
    /// コンパイル時に定数へ置き換わる nameof 式であるかを判定します（同名のメソッド呼び出しは除く）
    /// </summary>
    private static bool IsNameOf(InvocationExpressionSyntax invocation, SemanticModel model, CancellationToken cancellationToken)
    {
        return invocation.Expression is IdentifierNameSyntax { Identifier.ValueText: "nameof" } &&
               model.GetSymbolInfo(invocation, cancellationToken).Symbol == null;
    }

    /// <summary>
    /// LINQ チェーンの起点となる、列挙元の式のシンボルを取得します（例: srcList.Where(...).ToList() の srcList）
    /// 単純なメンバーアクセスチェーン以外（null条件演算子など）は解決できず null を返すことがあります
    /// </summary>
    private static ISymbol? GetChainRootSymbol(InvocationExpressionSyntax invocation, SemanticModel model, CancellationToken cancellationToken)
    {
        if (invocation.Expression is not MemberAccessExpressionSyntax memberAccess)
        {
            return null;
        }

        ExpressionSyntax current = memberAccess.Expression;
        while (current is InvocationExpressionSyntax innerInvocation && innerInvocation.Expression is MemberAccessExpressionSyntax innerMemberAccess)
        {
            current = innerMemberAccess.Expression;
        }

        return model.GetSymbolInfo(current, cancellationToken).Symbol;
    }

    /// <summary>
    /// レシーバーの名前文字列を取得します
    /// </summary>
    private static string GetReceiverName(InvocationExpressionSyntax invocation)
    {
        if (invocation.Expression is MemberAccessExpressionSyntax memberAccess)
        {
            return memberAccess.Expression.ToString();
        }
        return "expression";
    }
}

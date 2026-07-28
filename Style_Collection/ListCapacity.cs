using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using System.Linq;
using System.Threading;
using CSharp_IngeniousAnalyzer.Style__Common;

namespace CSharp_IngeniousAnalyzer.Style_Collection;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public class ListCapacity : CommonAnalyzer
{
    public const string DiagnosticId = "COLL001";
    private const string Category = "Performance";
    private static readonly LocalizableString Title = CreateLocalStr(nameof(ResourceEnum.COLL001_Title));
    private static readonly LocalizableString MessageFormat = CreateLocalStr(nameof(ResourceEnum.COLL001_Message));

    protected override DiagnosticDescriptor Rule { get; } = new(
        DiagnosticId, Title, MessageFormat, Category, DiagnosticSeverity.Warning, isEnabledByDefault: true);

    protected override SyntaxKind[] TargetKinds => [SyntaxKind.ObjectCreationExpression];

    protected override void AnalyzeNode(SyntaxNodeAnalysisContext context)
    {
        if (IsGeneratedFile(context)) return;
        var objectCreation = (ObjectCreationExpressionSyntax)context.Node;

        // 1. 生成されている型が「List<T>」であるかを検証する
        if (context.SemanticModel.GetTypeInfo(objectCreation, context.CancellationToken).Type is not INamedTypeSymbol typeSymbol || typeSymbol.OriginalDefinition.ToDisplayString() != "System.Collections.Generic.List<T>") return;

        // 2. すでに初期サイズが指定されている、あるいはコレクション初期化子がある場合はスルーする
        if ((objectCreation.ArgumentList != null && objectCreation.ArgumentList.Arguments.Count > 0) || objectCreation.Initializer != null) return;

        // 3. ループ上限式を安全逆引きする（取れない場合はスルーする）
        var limitExpression = GetLimitExpressionOrNull(objectCreation, context.SemanticModel, context.CancellationToken);
        if (limitExpression is null) return;

        // 4. 上限値が「ローカル変数」であり、かつリスト生成よりも「後」に宣言されている場合は警告対象外とする
        if (IsVariableDeclaredAfter(limitExpression, objectCreation, context.SemanticModel, context.CancellationToken))
        {
            return;
        }

        // 安全かつ確実と確定したケース（ローカル変数または定数）のみ警告を通知する
        var diagnostic = Diagnostic.Create(Rule, objectCreation.GetLocation());
        context.ReportDiagnostic(diagnostic);
    }

    /// <summary>
    /// ループ上限値がローカル変数であり、かつリスト生成よりも後で宣言されているかを判定する
    /// </summary>
    private static bool IsVariableDeclaredAfter(ExpressionSyntax limitExpr, ObjectCreationExpressionSyntax objectCreation, SemanticModel semanticModel, CancellationToken cancellationToken)
    {
        var symbolInfo = semanticModel.GetSymbolInfo(limitExpr, cancellationToken);
        var symbol = symbolInfo.Symbol;

        // ローカル変数またはパラメータの場合のみ宣言位置を検証する
        if (symbol is ILocalSymbol)
        {
            var syntaxRef = symbol.DeclaringSyntaxReferences.FirstOrDefault();
            if (syntaxRef != null)
            {
                // 変数の宣言位置が、リストのインスタンス化よりも「後」にある場合は真（＝危険なので弾く）
                return syntaxRef.Span.Start > objectCreation.SpanStart;
            }
        }

        // プロパティアクセスや複雑な式、フィールドなどは追跡困難なため安全側に倒して false（除外対象外＝ここでは警告しない条件には該当させないが、GetLimitExpressionOrNullで既に弾かれている）
        return false;
    }

    /// <summary>
    /// ループ上限を逆引きする安全検証ロジック（ローカル変数・定数のみ許可）
    /// </summary>
    private static ExpressionSyntax? GetLimitExpressionOrNull(ObjectCreationExpressionSyntax objectCreation, SemanticModel semanticModel, CancellationToken cancellationToken)
    {
        var variableDeclarator = objectCreation.AncestorsAndSelf().OfType<VariableDeclaratorSyntax>().FirstOrDefault();
        if (variableDeclarator is null) return null;

        var listSymbol = semanticModel.GetDeclaredSymbol(variableDeclarator, cancellationToken);
        if (listSymbol is null) return null;

        var methodBlock = objectCreation.Ancestors().OfType<BlockSyntax>().FirstOrDefault();
        if (methodBlock is null) return null;

        ForStatementSyntax? targetForLoop = null;

        var allForLoops = methodBlock.DescendantNodes().OfType<ForStatementSyntax>();

        foreach (var forLoop in allForLoops)
        {
            var invocations = forLoop.Statement.DescendantNodes().OfType<InvocationExpressionSyntax>();
            foreach (var invocation in invocations)
            {
                if (invocation.Expression is MemberAccessExpressionSyntax memberAccess)
                {
                    var objSymbol = semanticModel.GetSymbolInfo(memberAccess.Expression, cancellationToken).Symbol;
                    if (SymbolEqualityComparer.Default.Equals(objSymbol, listSymbol))
                    {
                        targetForLoop = forLoop;
                        break;
                    }
                }
            }

            if (targetForLoop != null) break;
        }

        if (targetForLoop is null) return null;

        if (targetForLoop.Statement.DescendantNodes().OfType<ForStatementSyntax>().Any())
        {
            return null;
        }

        if (targetForLoop.Condition is BinaryExpressionSyntax binaryExpression &&
            binaryExpression.OperatorToken.IsKind(SyntaxKind.LessThanToken))
        {
            var limitExpr = binaryExpression.Right;

            // 上限の右辺が「ローカル変数（ILocalSymbol）」または「定数・リテラル」であるものに厳しく限定する
            var symbolInfo = semanticModel.GetSymbolInfo(limitExpr, cancellationToken);
            if (symbolInfo.Symbol != null && symbolInfo.Symbol is not ILocalSymbol && symbolInfo.Symbol is not IFieldSymbol)
            {
                return null;
            }
            // プロパティアクセス（MemberAccessExpressionSyntax）などは完全に除外する
            if (limitExpr is MemberAccessExpressionSyntax)
            {
                return null;
            }

            if (IsVariableDeclaredAfter(limitExpr, objectCreation, semanticModel, cancellationToken))
            {
                return null;
            }

            return limitExpr;
        }

        return null;
    }
}
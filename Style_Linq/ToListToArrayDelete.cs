using System.Collections.Generic;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using CSharp_IngeniousAnalyzer.Style__Common;

namespace CSharp_IngeniousAnalyzer.Style_Linq;

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

        // foreach直接、または安全性が証明できる変数経由のどちらかであれば警告する
        bool isRedundant = IsDirectForEachUsage(invocation) || 
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

        var name = methodSymbol.Name;
        return name == "ToList" || name == "ToArray";
    }

    /// <summary>
    /// foreach ステートメントの式として直接使用されているかを判定します
    /// </summary>
    private static bool IsDirectForEachUsage(InvocationExpressionSyntax invocation)
    {
        return invocation.Parent is ForEachStatementSyntax forEach && forEach.Expression == invocation;
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

        return nonAssignmentRefCount == 1 && foundForEach != null;
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
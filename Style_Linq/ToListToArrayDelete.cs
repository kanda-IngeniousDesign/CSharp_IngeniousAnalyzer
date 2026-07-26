using System.Linq;
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
        var invocation = (InvocationExpressionSyntax)context.Node;
        var model = context.SemanticModel;

        if (!IsMaterializationMethod(invocation, model, context.CancellationToken))
        {
            return;
        }

        bool isRedundant = IsDirectForEachUsage(invocation) || 
                           IsRedundantVariableAssignment(invocation, model, context.CancellationToken);

        if (isRedundant)
        {
            var receiverName = GetReceiverName(invocation);
            context.ReportDiagnostic(Diagnostic.Create(Rule, invocation.GetLocation(), receiverName));
        }
    }

    /// <summary>
    /// 呼び出しが ToList または ToArray であるかを判定します
    /// </summary>
    private static bool IsMaterializationMethod(InvocationExpressionSyntax invocation, SemanticModel model, CancellationToken cancellationToken)
    {
        var methodSymbol = model.GetSymbolInfo(invocation, cancellationToken).Symbol as IMethodSymbol;
        if (methodSymbol == null || methodSymbol.ContainingType.ToDisplayString() != "System.Linq.Enumerable")
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
    /// 変数宣言または再代入を経由して、不要な実体化が行われているかを検証します
    /// </summary>
    private static bool IsRedundantVariableAssignment(InvocationExpressionSyntax invocation, SemanticModel model, CancellationToken cancellationToken)
    {
        var declaratorOrAssignment = FindDeclaratorOrAssignment(invocation);
        if (declaratorOrAssignment == null)
        {
            return false;
        }

        if (!TryGetTargetSymbolAndType(declaratorOrAssignment, model, cancellationToken, out var targetSymbol, out var targetVariableType, out var isVar))
        {
            return false;
        }

        if (!isVar && !CanBeAssignedWithoutToList(invocation, targetVariableType!, model, cancellationToken))
        {
            return false;
        }

        return HasSingleForEachReference(invocation, targetSymbol!, model, cancellationToken);
    }

    /// <summary>
    /// 呼び出しの祖先から変数宣言または代入式を特定します
    /// </summary>
    private static SyntaxNode? FindDeclaratorOrAssignment(InvocationExpressionSyntax invocation)
    {
        SyntaxNode? current = invocation.Parent;
        while (current != null)
        {
            if (current is VariableDeclaratorSyntax || current is AssignmentExpressionSyntax)
            {
                return current;
            }
            current = current.Parent;
        }
        return null;
    }

    /// <summary>
    /// 対象シンボルと変数の型、およびvar宣言であるかを取得します
    /// </summary>
    private static bool TryGetTargetSymbolAndType(
        SyntaxNode declaratorOrAssignment, 
        SemanticModel model, 
        CancellationToken cancellationToken, 
        out ISymbol? symbol, 
        out ITypeSymbol? type,
        out bool isVar)
    {
        symbol = null;
        type = null;
        isVar = false;

        if (declaratorOrAssignment is VariableDeclaratorSyntax varDecl)
        {
            symbol = model.GetDeclaredSymbol(varDecl, cancellationToken);
            if (varDecl.Parent is VariableDeclarationSyntax varDeclSyntax)
            {
                // var 宣言であるかを判定
                isVar = varDeclSyntax.Type.IsVar;
                type = model.GetTypeInfo(varDeclSyntax.Type, cancellationToken).Type;
            }
        }
        else if (declaratorOrAssignment is AssignmentExpressionSyntax assignExpr && assignExpr.Left is IdentifierNameSyntax leftId)
        {
            symbol = model.GetSymbolInfo(leftId, cancellationToken).Symbol;
            type = model.GetTypeInfo(assignExpr.Left, cancellationToken).Type;
        }

        return symbol != null;
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
    /// 変数が定義以降に非代入参照として foreach で1回だけ使用されているかを検証します
    /// </summary>
    private static bool HasSingleForEachReference(
        InvocationExpressionSyntax invocation, 
        ISymbol targetSymbol, 
        SemanticModel model, 
        CancellationToken cancellationToken)
    {
        var methodBody = invocation.Ancestors().OfType<MethodDeclarationSyntax>().FirstOrDefault()?.Body;
        if (methodBody == null)
        {
            return false;
        }

        var invocationSpanStart = invocation.SpanStart;

        var subsequentRefs = methodBody.DescendantNodes().OfType<IdentifierNameSyntax>()
            .Where(id => SymbolEqualityComparer.Default.Equals(model.GetSymbolInfo(id, cancellationToken).Symbol, targetSymbol))
            .Where(r => r.SpanStart > invocationSpanStart)
            .ToList();

        var nonAssignmentRefs = subsequentRefs.Where(r => 
            !(r.Parent is AssignmentExpressionSyntax assign && assign.Left == r)).ToList();

        if (nonAssignmentRefs.Count == 1)
        {
            var onlyRef = nonAssignmentRefs[0];
            return onlyRef.Parent is ForEachStatementSyntax forEachStmt && forEachStmt.Expression == onlyRef;
        }

        return false;
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
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
    /// （再代入への代入は誤検知リスクが高いため、初回の変数宣言のみに絞る）
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
            // 文の境界を越えたら探索を打ち切る
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
    /// </summary>
    private static bool HasSingleSafeForEachReference(
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

        // 代入の左辺（再代入）が含まれている場合は安全とは言えないため除外
        var hasReassignment = subsequentRefs.Any(r => r.Parent is AssignmentExpressionSyntax assign && assign.Left == r);
        if (hasReassignment)
        {
            return false;
        }

        var nonAssignmentRefs = subsequentRefs.Where(r => 
            !(r.Parent is AssignmentExpressionSyntax assign && assign.Left == r)).ToList();

        // 参照がちょうど1回であり、それが foreach の式であれば安全
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
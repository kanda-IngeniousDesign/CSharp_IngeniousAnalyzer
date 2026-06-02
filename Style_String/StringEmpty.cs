using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using CSharp_IngeniousAnalyzer.Core;
using CSharp_IngeniousAnalyzer.Style__Common;

namespace CSharp_IngeniousAnalyzer.Style_String;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public class StringEmpty : CommonAnalyzer
{
    public const string DiagnosticId = "STR001";
    private const string Category = "Style";
    private static readonly LocalizableString Title = CreateLocalStr(nameof(ResourceEnum.STR001_Title));
    private static readonly LocalizableString MessageFormat = CreateLocalStr(nameof(ResourceEnum.STR001_Message));

    protected override DiagnosticDescriptor Rule { get; } = new(
        DiagnosticId, Title, MessageFormat, Category, DiagnosticSeverity.Warning, isEnabledByDefault: true);

    // 監視ターゲットを宣言するだけ！
    protected override SyntaxKind[] TargetKinds => [SyntaxKind.EqualsExpression, SyntaxKind.NotEqualsExpression];

    protected override void AnalyzeNode(SyntaxNodeAnalysisContext context)
    {
        if (IsGeneratedFile(context)) return;
        var binaryExpr = (BinaryExpressionSyntax)context.Node;

        // 左右のどちらかが大文字の「String.Empty」であるかチェック
        var isLeftTarget = IsLargeStringEmpty(binaryExpr.Left);
        var isRightTarget = IsLargeStringEmpty(binaryExpr.Right);

        if (!isLeftTarget && !isRightTarget) return;

        var targetNode = isLeftTarget ? binaryExpr.Left : binaryExpr.Right;
        var diagnostic = Diagnostic.Create(Rule, targetNode.GetLocation(), targetNode.ToString().Trim());
        context.ReportDiagnostic(diagnostic);
    }

    private static bool IsLargeStringEmpty(ExpressionSyntax expression)
    {
        return expression is MemberAccessExpressionSyntax ma && 
               ma.Name.Identifier.ValueText == "Empty" && 
               ma.Expression.ToString().Trim() == "String";
    }
}
using System.Collections.Immutable;
using CSharp_IngeniousAnalyzer.Style__Common;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace CSharp_IngeniousAnalyzer.Style_Null;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public class IsNull : CommonAnalyzer
{
    public const string DiagnosticId = "NULL001";
    private const string Category = "Style";
    private static readonly LocalizableString Title = CreateLocalStr(nameof(ResourceEnum.NULL001_Title));
    private static readonly LocalizableString MessageFormat = CreateLocalStr(nameof(ResourceEnum.NULL001_Message));

    protected override DiagnosticDescriptor Rule { get; } = new(
        DiagnosticId, Title, MessageFormat, Category, DiagnosticSeverity.Warning, isEnabledByDefault: true);

    // == と != の式を監視対象にする
    protected override SyntaxKind[] TargetKinds => [SyntaxKind.EqualsExpression, SyntaxKind.NotEqualsExpression];

    protected override void AnalyzeNode(SyntaxNodeAnalysisContext context)
    {
        var binaryExpr = (BinaryExpressionSyntax)context.Node;

        // 左辺または右辺が「nullリテラル」であるかチェック
        bool isLeftNull = binaryExpr.Left.IsKind(SyntaxKind.NullLiteralExpression);
        bool isRightNull = binaryExpr.Right.IsKind(SyntaxKind.NullLiteralExpression);

        // 片方がnullリテラルであれば、もう片方が対象変数（name == null など）
        if (isLeftNull || isRightNull)
        {
            // 警告（波線）を発生させる
            var diagnostic = Diagnostic.Create(Rule, binaryExpr.GetLocation(), "is null");
            context.ReportDiagnostic(diagnostic);
        }
    }
}
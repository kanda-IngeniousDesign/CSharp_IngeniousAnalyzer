using CSharp_IngeniousAnalyzer.Style__Common;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace CSharp_IngeniousAnalyzer.Style_Compare;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public class Inequality : CommonAnalyzer
{
    public const string DiagnosticId = "COMP001";
    private const string Category = "Style";
    private static readonly LocalizableString Title = CreateLocalStr(nameof(ResourceEnum.COMP001_Title));
    private static readonly LocalizableString MessageFormat = CreateLocalStr(nameof(ResourceEnum.COMP001_Message));

    protected override DiagnosticDescriptor Rule { get; } = new(
        DiagnosticId, Title, MessageFormat, Category, DiagnosticSeverity.Warning, isEnabledByDefault: true);

    // 監視対象
    protected override SyntaxKind[] TargetKinds => 
    [
        SyntaxKind.GreaterThanExpression, 
        SyntaxKind.GreaterThanOrEqualExpression,
        SyntaxKind.LessThanExpression, 
        SyntaxKind.LessThanOrEqualExpression
    ];

    protected override void AnalyzeNode(SyntaxNodeAnalysisContext context)
    {
        var binaryExpr = (BinaryExpressionSyntax)context.Node;
        var kind = binaryExpr.Kind();

        // '<' と '<=' 以外はすべて警告（つまり > と >= を警告）
        if (kind == SyntaxKind.GreaterThanExpression || kind == SyntaxKind.GreaterThanOrEqualExpression)
        {
            //if (IsSimpleOperand(binaryExpr.Left) && IsSimpleOperand(binaryExpr.Right))
            {
                context.ReportDiagnostic(Diagnostic.Create(Rule, binaryExpr.GetLocation()));
            }
        }
    }

    private static bool IsSimpleOperand(ExpressionSyntax expression)
    {
        // 1. 関数呼び出しは除外 (例: GetCount() > 10 は対象外)
        if (expression is InvocationExpressionSyntax) return false;

        // 2. 変数、リテラルは対象
        if (expression is IdentifierNameSyntax || expression is LiteralExpressionSyntax) return true;

        // 3. 演算（BinaryExpression）は「再帰的に」単純なものかチェックする
        //    例: 1 + 2 > 10 などは、その構成要素が単純であれば対象に含める
        if (expression is BinaryExpressionSyntax binary)
        {
            return IsSimpleOperand(binary.Left) && IsSimpleOperand(binary.Right);
        }

        return false;
    }}
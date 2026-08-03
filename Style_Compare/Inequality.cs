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

    // 監視対象：'>' と '>=' のみに絞ることで、不要なコールバックを削減
    protected override SyntaxKind[] TargetKinds => 
    [
        SyntaxKind.GreaterThanExpression, 
        SyntaxKind.GreaterThanOrEqualExpression
    ];

    protected override void AnalyzeNode(SyntaxNodeAnalysisContext context)
    {
        if (IsGeneratedFile(context)) return;
        
        var binaryExpr = (BinaryExpressionSyntax)context.Node;
        
        // '>' または '>=' の場合に警告を通知
        context.ReportDiagnostic(Diagnostic.Create(Rule, binaryExpr.GetLocation()));
    }
}
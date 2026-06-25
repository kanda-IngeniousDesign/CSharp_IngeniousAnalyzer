using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using CSharp_IngeniousAnalyzer.Style__Common;

namespace CSharp_IngeniousAnalyzer.Style_Complexity;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public class MethodComplexity : CommonAnalyzer
{
    private const int DefaultThreshold = 17;

    public const string DiagnosticId = "CPX001";
    private const string Category = "Complexity";
    private static readonly LocalizableString Title = CreateLocalStr(nameof(ResourceEnum.CPX001_Title));
    private static readonly LocalizableString MessageFormat = CreateLocalStr(nameof(ResourceEnum.CPX001_Message));

    protected override DiagnosticDescriptor Rule { get; } = new(
        DiagnosticId, Title, MessageFormat, Category, DiagnosticSeverity.Warning, isEnabledByDefault: true);

    // メソッド宣言を監視
    protected override SyntaxKind[] TargetKinds => [SyntaxKind.MethodDeclaration];

    protected override void AnalyzeNode(SyntaxNodeAnalysisContext context)
    {
        // もし MethodDeclaration 以外なら処理を止める
        if (!context.Node.IsKind(SyntaxKind.MethodDeclaration)) return;

        if (IsGeneratedFile(context)) return;
        var method = (MethodDeclarationSyntax)context.Node;
        if (method.Body == null) return;

        // セマンティックモデルを使って複雑度を算出
        var complexity = CalculateComplexity(method, context.SemanticModel);

        if (complexity > DefaultThreshold)
        {
            var diagnostic = Diagnostic.Create(Rule, method.Identifier.GetLocation(), method.Identifier.Text, complexity);
            context.ReportDiagnostic(diagnostic);
        }
    }

    private static int CalculateComplexity(MethodDeclarationSyntax method, SemanticModel model)
    {
        int count = 1;

        // LINQの判定（これは固定値でOK）
        var invocations = method.DescendantNodes().OfType<InvocationExpressionSyntax>();
        foreach (var inv in invocations)
        {
            var symbol = model.GetSymbolInfo(inv).Symbol as IMethodSymbol;
            if (symbol?.ContainingNamespace.ToDisplayString() == "System.Linq") count++;
        }

        // ネストの深さを考慮したカウント
        var complexNodes = method.DescendantNodes().Where(n => 
            n is IfStatementSyntax or ForStatementSyntax or ForEachStatementSyntax or 
            WhileStatementSyntax or DoStatementSyntax or SwitchSectionSyntax or CatchClauseSyntax or
            ConditionalExpressionSyntax);

        foreach (var node in complexNodes)
        {
            // 親をたどって、メソッド宣言までの距離（＝ネストの深さ）を取得
            int depth = 0;
            var parent = node.Parent;
            while (parent != null && parent != method)
            {
                if (parent is IfStatementSyntax or ForStatementSyntax or ForEachStatementSyntax or 
                    WhileStatementSyntax or DoStatementSyntax or SwitchSectionSyntax or CatchClauseSyntax)
                {
                    depth++;
                }
                parent = parent.Parent;
            }
            
            // 深ければ深いほど、複雑度を倍掛け（あるいは加算）する
            count += (1 + depth); 
        }

        return count;
    }
}
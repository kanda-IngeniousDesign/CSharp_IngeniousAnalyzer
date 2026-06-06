using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using CSharp_IngeniousAnalyzer.Style__Common;

namespace CSharp_IngeniousAnalyzer.Style_Linq;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public class LinqOptimize : CommonAnalyzer
{
    public const string DiagnosticId = "LINQ001";
    private const string Category = "Performance";
    private static readonly LocalizableString Title = CreateLocalStr(nameof(ResourceEnum.LINQ001_Title));
    private static readonly LocalizableString MessageFormat = CreateLocalStr(nameof(ResourceEnum.LINQ001_Message));

    protected override DiagnosticDescriptor Rule { get; } = new(
        DiagnosticId, Title, MessageFormat, Category, DiagnosticSeverity.Warning, isEnabledByDefault: true);

    // 監視ターゲットは「メソッド呼び出し（Invocation）」のみに絞る
    protected override SyntaxKind[] TargetKinds => [SyntaxKind.InvocationExpression];

    private static readonly HashSet<string> TargetMethods = ["FirstOrDefault", "Any", "Last"];

    protected override void AnalyzeNode(SyntaxNodeAnalysisContext context)
    {
        if (IsGeneratedFile(context)) return;
        var invocation = (InvocationExpressionSyntax)context.Node;

        // 1. 自分自身のメソッド名が FirstOrDefault / Any / Last のいずれかか
        if (invocation.Expression is not MemberAccessExpressionSyntax ma || !TargetMethods.Contains(ma.Name.Identifier.ValueText)) return;

        // 【作戦Aの鉄壁ガード】すでに右辺のメソッドに引数がある場合は完全スルー
        if (invocation.ArgumentList.Arguments.Count > 0) return;

        // 2. 左辺（ ma.Expression ）がまた別のメソッド呼び出し（Invocation）か
        if (ma.Expression is not InvocationExpressionSyntax leftInv) return;

        // 3. その左辺のメソッド名が「Where」であるか
        if (leftInv.Expression is not MemberAccessExpressionSyntax leftMa || leftMa.Name.Identifier.ValueText != "Where") return;

        // 4. セマンティックモデルで、これが「本物のSystem.Linq」であるかを厳密に証明（ダミーの完全排除）
        var methodSymbol = context.SemanticModel.GetSymbolInfo(invocation, context.CancellationToken).Symbol as IMethodSymbol;
        if (methodSymbol is null || methodSymbol.ContainingType?.ContainingNamespace?.ToDisplayString() != "System.Linq") return;

        // すべての条件をクリアしたら警告を通知
        var diagnostic = Diagnostic.Create(Rule, invocation.GetLocation(), ma.Name.Identifier.ValueText);
        context.ReportDiagnostic(diagnostic);
    }
}
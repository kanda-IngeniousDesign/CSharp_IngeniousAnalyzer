using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using CSharp_IngeniousAnalyzer.Style__Common;

namespace CSharp_IngeniousAnalyzer.Style_Complexity;

// 巨大メソッドの分割自体を行うCodeFixProviderは用意していない。
// 機械的に安全な変換が定義できず、100%の精度を保証できないため、手動での対応を前提としている。
// レガシーコード等で意図的に許容する場合は、CPX001と同様に "Ignore CPX002" のFix（FatMethodFix）を
// 実行することで "// Ignore CPX002" コメントを自動挿入し、抑制できる。
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public class FatMethod : CommonAnalyzer
{
    private const int MaxLineThreshold = 300;
    private const int MethodCallThreshold = 100;
    public const string DiagnosticId = "CPX002";
    private const string Category = "Complexity";
    private static readonly LocalizableString Title = CreateLocalStr(nameof(ResourceEnum.CPX002_Title));
    private static readonly LocalizableString MessageFormat = CreateLocalStr(nameof(ResourceEnum.CPX002_Message));

    protected override DiagnosticDescriptor Rule { get; } = new(
        DiagnosticId, Title, MessageFormat, Category, DiagnosticSeverity.Warning, isEnabledByDefault: true);

    protected override SyntaxKind[] TargetKinds => [SyntaxKind.MethodDeclaration];

    protected override void AnalyzeNode(SyntaxNodeAnalysisContext context)
    {
        if (IsGeneratedFile(context)) return;

        var method = (MethodDeclarationSyntax)context.Node;
        if (method.Body == null) return;

        if (method.HasIgnoreComment(DiagnosticId)) return;

        var span = method.Body.GetLocation().GetLineSpan();
        int lineCount = span.EndLinePosition.Line - span.StartLinePosition.Line + 1;

        // 特定行を超えた場合のみ判定を開始
        if (lineCount > MaxLineThreshold)
        {
            // DescendantNodes() と LINQ の Count() による全探索とアロケーションを排除し、
            // foreach ループで直接カウントして高速化
            int invocationCount = 0;
            foreach (var node in method.Body.DescendantNodes())
            {
                if (node.IsKind(SyntaxKind.InvocationExpression))
                {
                    invocationCount++;
                }
            }

            // 呼び出し比率が特定値未満であれば警告
            if (invocationCount < (lineCount / MethodCallThreshold))
            {
                context.ReportDiagnostic(Diagnostic.Create(Rule,
                                                           method.Identifier.GetLocation(),
                                                           method.Identifier.Text,
                                                           lineCount,
                                                           invocationCount));
            }
        }
    }
}
using CSharp_IngeniousAnalyzer.Style__Common;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace CSharp_IngeniousAnalyzer.Style_Exception;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public class RethrowLosesStackTrace : CommonAnalyzer
{
    public const string DiagnosticId = "EXC002";
    private const string Category = "Exception";
    private static readonly LocalizableString Title = CreateLocalStr(nameof(ResourceEnum.EXC002_Title));
    private static readonly LocalizableString MessageFormat = CreateLocalStr(nameof(ResourceEnum.EXC002_Message));

    protected override DiagnosticDescriptor Rule { get; } = new(
        DiagnosticId, Title, MessageFormat, Category, DiagnosticSeverity.Warning, isEnabledByDefault: true);

    protected override SyntaxKind[] TargetKinds => [SyntaxKind.ThrowStatement];

    protected override void AnalyzeNode(SyntaxNodeAnalysisContext context)
    {
        if (IsGeneratedFile(context)) return;

        var throwStmt = (ThrowStatementSyntax)context.Node;

        // "throw ex;" のような単純な識別子の再スローのみを対象とする（throw; や throw new(...) は対象外）
        if (throwStmt.Expression is not IdentifierNameSyntax identifier) return;

        // ラムダ式・ローカル関数・メソッド境界を越えた先にあるcatch句は対象外とする
        // （境界を越えると、その場所では bare な throw; に書き換えられないため）
        CatchClauseSyntax? catchClause = null;
        for (SyntaxNode? node = throwStmt.Parent; node != null; node = node.Parent)
        {
            if (node is CatchClauseSyntax cc)
            {
                catchClause = cc;
                break;
            }

            if (node is AnonymousFunctionExpressionSyntax or LocalFunctionStatementSyntax or BaseMethodDeclarationSyntax)
            {
                return;
            }
        }

        // 直近のcatch句が宣言する例外変数と同名の識別子を再スローしている場合のみ検知する。
        // C#の言語仕様上、同名のローカル変数によるシャドーイングは許可されないため、
        // 名前が一致すれば必ずこのcatch句の例外変数を指していると判断できる（シンボル解決は不要）。
        if (catchClause?.Declaration?.Identifier.Text != identifier.Identifier.Text) return;

        context.ReportDiagnostic(Diagnostic.Create(Rule, throwStmt.GetLocation(), identifier.Identifier.Text));
    }
}

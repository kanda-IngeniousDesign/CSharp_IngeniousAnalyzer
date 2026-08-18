using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using CSharp_IngeniousAnalyzer.Style__Common;

namespace CSharp_IngeniousAnalyzer.Style_Exception;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public class EmptyCatch : CommonAnalyzer
{
    public const string DiagnosticId = "EXC001";
    private const string Category = "Exception";
    private static readonly LocalizableString Title = CreateLocalStr(nameof(ResourceEnum.EXC001_Title));
    private static readonly LocalizableString MessageFormat = CreateLocalStr(nameof(ResourceEnum.EXC001_Message));

    protected override DiagnosticDescriptor Rule { get; } = new(
        DiagnosticId, Title, MessageFormat, Category, DiagnosticSeverity.Warning, isEnabledByDefault: true);

    protected override SyntaxKind[] TargetKinds => [SyntaxKind.CatchClause];

    protected override void AnalyzeNode(SyntaxNodeAnalysisContext context)
    {
        if (IsGeneratedFile(context)) return;
        var catchClause = (CatchClauseSyntax)context.Node;

        // 1. ブロック内に文が1つでもあれば対象外
        if (catchClause.Block.Statements.Count > 0) return;

        // 2. コメントが1つでもあれば「意図的に何もしない」とみなし対象外
        //    「{ // ... \n }」（開き括弧と同じ行）「{ \n // ... \n }」（閉じ括弧の手前）の両方の書き方を許容する
        if (HasComment(catchClause.Block.OpenBraceToken.TrailingTrivia) ||
            HasComment(catchClause.Block.CloseBraceToken.LeadingTrivia))
        {
            return;
        }

        // 型指定なしの `catch { }` の場合は "Exception" として通知する
        var exceptionTypeName = catchClause.Declaration?.Type.ToString() ?? "Exception";

        var diagnostic = Diagnostic.Create(Rule, catchClause.CatchKeyword.GetLocation(), exceptionTypeName);
        context.ReportDiagnostic(diagnostic);
    }

    private static bool HasComment(SyntaxTriviaList triviaList)
    {
        foreach (var trivia in triviaList)
        {
            if (trivia.IsKind(SyntaxKind.SingleLineCommentTrivia) ||
                trivia.IsKind(SyntaxKind.MultiLineCommentTrivia) ||
                trivia.IsKind(SyntaxKind.SingleLineDocumentationCommentTrivia) ||
                trivia.IsKind(SyntaxKind.MultiLineDocumentationCommentTrivia))
            {
                return true;
            }
        }

        return false;
    }
}

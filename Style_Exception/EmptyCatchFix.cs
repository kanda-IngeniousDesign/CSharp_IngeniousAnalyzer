using System.Collections.Immutable;
using System.Composition;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CSharp_IngeniousAnalyzer.Style__Common;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace CSharp_IngeniousAnalyzer.Style_Exception;

[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(EmptyCatchFix)), Shared]
public class EmptyCatchFix : CodeFixProvider
{
    public sealed override ImmutableArray<string> FixableDiagnosticIds => [EmptyCatch.DiagnosticId];

    public sealed override FixAllProvider GetFixAllProvider() => WellKnownFixAllProviders.BatchFixer;

    public sealed override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
        var diagnostic = context.Diagnostics.First();
        var catchClause = root?.FindNodeAtSpan<CatchClauseSyntax>(diagnostic.Location.SourceSpan);
        if (catchClause is null) return;

        context.RegisterCodeFix(
            CodeAction.Create(
                title: "Fix : TODOコメントを追加する",
                createChangedDocument: c => InsertCommentAsync(context.Document, catchClause, c),
                equivalenceKey: nameof(EmptyCatchFix)),
            diagnostic);
    }

    private async Task<Document> InsertCommentAsync(Document document, CatchClauseSyntax catchClause, CancellationToken cancellationToken)
    {
        var sourceText = await document.GetTextAsync(cancellationToken).ConfigureAwait(false);

        // catch句と同じ行のインデントを基準に、1段階深いインデントを組み立てる
        var catchLine = sourceText.Lines.GetLineFromPosition(catchClause.SpanStart);
        var baseIndent = new string(catchLine.ToString().TakeWhile(char.IsWhiteSpace).ToArray());
        var innerIndent = baseIndent + "    ";

        var textStr = sourceText.ToString();
        var newLine = textStr.Contains("\r\n") ? "\r\n" : "\n";

        // 開き括弧と閉じ括弧の間（単一行 "{ }" の場合は空白のみ、複数行の場合は改行+インデント）を
        // まるごと置き換えることで、既存の書式に関わらず閉じ括弧が確実に独立した行に来るようにする。
        // ゼロ幅挿入だと、単一行の "catch (Exception ex) { }" のようなケースで閉じ括弧がTODOコメントに
        // 巻き込まれ、コメントアウトされてしまう（コンパイルエラーになる）ため。
        var openBrace = catchClause.Block.OpenBraceToken;
        var closeBrace = catchClause.Block.CloseBraceToken;
        var replaceSpan = TextSpan.FromBounds(openBrace.Span.End, closeBrace.SpanStart);
        var comment = $"{newLine}{innerIndent}// TODO: 例外処理を検討してください{newLine}{baseIndent}";

        var textChange = new TextChange(replaceSpan, comment);
        return document.WithText(sourceText.WithChanges(textChange));
    }
}

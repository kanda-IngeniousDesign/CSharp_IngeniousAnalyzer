using System.Collections.Immutable;
using System.Composition;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;

namespace CSharp_IngeniousAnalyzer.Style_Unicode;

// 不可視文字を書き換えるFixは提供しない（理由は InvisibleUnicode のクラスコメントを参照）。
// 提供するのは、文字を含む直近の文・メンバー宣言の直前に「// Ignore UNI001」を挿入する抑制専用のFixのみ。
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(InvisibleUnicodeFix)), Shared]
public class InvisibleUnicodeFix : CodeFixProvider
{
    public sealed override ImmutableArray<string> FixableDiagnosticIds => [InvisibleUnicode.DiagnosticId];

    public sealed override FixAllProvider GetFixAllProvider() => WellKnownFixAllProviders.BatchFixer;

    public sealed override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
        if (root is null) return;

        var diagnostic = context.Diagnostics.First();

        // using・名前空間より前（ファイル先頭のコメント等）で挿入先がない場合は、#pragma 等で抑制してもらう
        var anchor = InvisibleUnicode.FindIgnoreAnchor(root, diagnostic.Location.SourceSpan.Start);
        if (anchor is null) return;

        context.RegisterCodeFix(
            CodeAction.Create(
                title: "Ignore : 不可視Unicode文字のチェックを無視する",
                createChangedDocument: c => InsertIgnoreCommentAsync(context.Document, anchor, c),
                equivalenceKey: "IgnoreInvisibleUnicode"),
            diagnostic);
    }

    /// <summary>
    /// 指定ノードの直前の行に「// Ignore UNI001」を挿入する。
    /// 挿入位置はノードの先行トリビア内（トークンの外側）のため、文字列リテラルやコメントの中に入り込むことはない。
    /// </summary>
    private static async Task<Document> InsertIgnoreCommentAsync(Document document, SyntaxNode anchor, CancellationToken cancellationToken)
    {
        var text = await document.GetTextAsync(cancellationToken).ConfigureAwait(false);

        // 既存の説明コメントやドキュメントコメントより下、実コードの行頭インデントの手前に挿入する
        var leadingTrivia = anchor.GetLeadingTrivia();
        var lastWhitespace = leadingTrivia.LastOrDefault(t => t.IsKind(SyntaxKind.WhitespaceTrivia));
        var insertPosition = lastWhitespace.IsKind(SyntaxKind.WhitespaceTrivia) && lastWhitespace.FullSpan.End == anchor.SpanStart
            ? lastWhitespace.FullSpan.Start
            : anchor.SpanStart;

        var line = text.Lines.GetLineFromPosition(insertPosition);
        var newLine = DetectNewLine(text, line);
        var indent = BuildIndent(text, line);
        var comment = $"// Ignore {InvisibleUnicode.DiagnosticId}";

        var isLineStart = string.IsNullOrWhiteSpace(text.ToString(TextSpan.FromBounds(line.Start, insertPosition)));
        if (isLineStart)
        {
            return document.WithText(text.WithChanges(new TextChange(new TextSpan(insertPosition, 0), indent + comment + newLine)));
        }

        // 直前のトークンと同じ行にある場合（例: "int a = 1; int b = 2;" の b）は、
        // コメントが直前トークンの後続トリビアとして解釈されないよう、改行してから挿入する。
        // その際、改行の手前に残る半角スペース・タブ（直前トークンの後続トリビア）は行末の空白にならないよう取り除く
        var replaceStart = insertPosition;
        while (replaceStart > line.Start && text[replaceStart - 1] is ' ' or '\t')
        {
            replaceStart--;
        }
        var insertion = newLine + indent + comment + newLine + indent;

        return document.WithText(text.WithChanges(new TextChange(TextSpan.FromBounds(replaceStart, insertPosition), insertion)));
    }

    /// <summary>
    /// 行頭のインデントを、タブはタブ・それ以外の空白は半角スペースに揃えて返す（不可視文字をコピーしないため）
    /// </summary>
    private static string BuildIndent(SourceText text, TextLine line)
    {
        var builder = new StringBuilder();
        for (var i = line.Start; i < line.End && char.IsWhiteSpace(text[i]); i++)
        {
            builder.Append(text[i] == '\t' ? '\t' : ' ');
        }
        return builder.ToString();
    }

    /// <summary>
    /// 挿入行 → ファイル全体の最初の改行、の順に改行コードを検出する。改行が1つもなければ "\r\n" にフォールバックする。
    /// U+2028 等も行区切りとして扱われるが、それ自体が検知対象の不可視文字のため、CR・LFによる改行のみを採用する。
    /// </summary>
    private static string DetectNewLine(SourceText text, TextLine line)
    {
        var newLine = GetLineBreak(text, line);
        if (newLine != null) return newLine;

        foreach (var l in text.Lines)
        {
            newLine = GetLineBreak(text, l);
            if (newLine != null) return newLine;
        }
        return "\r\n";
    }

    private static string? GetLineBreak(SourceText text, TextLine line)
    {
        var lineBreak = text.ToString(TextSpan.FromBounds(line.End, line.EndIncludingLineBreak));
        return lineBreak is "\r\n" or "\n" or "\r" ? lineBreak : null;
    }
}

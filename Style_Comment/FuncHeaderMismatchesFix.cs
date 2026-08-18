using System.Collections.Immutable;
using System.Composition;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace CSharp_IngeniousAnalyzer.Style_Comment;

[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(FuncHeaderMismatchesFix)), Shared]
public class FuncHeaderMismatchesFix : CodeFixProvider
{
    public sealed override ImmutableArray<string> FixableDiagnosticIds => [FuncHeaderMismatches.DiagnosticId];

    public sealed override FixAllProvider GetFixAllProvider() => WellKnownFixAllProviders.BatchFixer;

    public sealed override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
        var diagnostic = context.Diagnostics.First();
        var methodDecl = root?.FindToken(diagnostic.Location.SourceSpan.Start).Parent?.AncestorsAndSelf().OfType<MethodDeclarationSyntax>().First();

        if (methodDecl == null) return;

        context.RegisterCodeFix(
            CodeAction.Create(
                title: "Fix : 関数ヘッダーのパラメータを同期する",
                createChangedDocument: c => SyncParamsAsync(context.Document, methodDecl, c),
                equivalenceKey: nameof(FuncHeaderMismatchesFix)),
            diagnostic);
    }

private async Task<Document> SyncParamsAsync(Document document, MethodDeclarationSyntax methodDecl, CancellationToken cancellationToken)
    {
        var sourceText = await document.GetTextAsync(cancellationToken);

        // 1. メソッドの直前にあるドキュメントコメントを取得（COMM002の検知ロジックと同じ判定基準に揃える）
        var (docTrivia, rawDocCommentTrivia) = DocCommentScanner.TryGetDocComment(methodDecl);

        TextSpan commentSpan;
        string originalComment;

        if (docTrivia != null)
        {
            commentSpan = docTrivia.Span;
            originalComment = docTrivia.ToString();
        }
        else if (rawDocCommentTrivia != null && rawDocCommentTrivia.Count > 0)
        {
            // GenerateDocumentationFile が無効なビルドでは /// コメントが構造化されないため
            // （IDE上の解析では常に構造化されるため、ビルド時のみFixが無効化されるのを防ぐ）、
            // 生テキストの範囲を直接特定して同じ処理にフォールバックする
            var firstTrivia = rawDocCommentTrivia[0];
            var lastTrivia = rawDocCommentTrivia[rawDocCommentTrivia.Count - 1];

            // 先頭行のインデントを保持するため、直前の空白トリビアも範囲に含める
            var leadingTrivia = methodDecl.GetLeadingTrivia();
            var firstIndex = leadingTrivia.IndexOf(firstTrivia);
            var startTrivia = firstTrivia;
            if (firstIndex > 0 && leadingTrivia[firstIndex - 1].IsKind(SyntaxKind.WhitespaceTrivia))
            {
                startTrivia = leadingTrivia[firstIndex - 1];
            }

            commentSpan = TextSpan.FromBounds(startTrivia.SpanStart, lastTrivia.Span.End);
            originalComment = sourceText.ToString(commentSpan);
        }
        else
        {
            return document;
        }

        var lineBreak = originalComment.Contains("\r\n") ? "\r\n" : "\n";
        var lines = originalComment.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
        
        // 2. プレフィックス（インデント + "///"）の検出
        var sampleLine = lines.FirstOrDefault(l => l.Contains("///")) ?? "    /// ";
        int slashIndex = sampleLine.IndexOf("///");
        var indent = slashIndex >= 0 ? sampleLine.Substring(0, slashIndex) : "    ";
        var prefix = indent + "///";

        // 3. 既存の <param> タグを完全に排除
        var cleanedLines = RemoveExistingParams(lines.ToList());

        // 4. 挿入位置の特定
        int insertIndex = DetermineInsertIndex(cleanedLines);

        // 5. メソッドの引数の順番通りに <param> タグを生成・挿入
        var finalLines = InsertParameters(cleanedLines, methodDecl.ParameterList.Parameters, prefix, insertIndex);

        var newCommentText = string.Join(lineBreak, finalLines);

        // 6. 【超重要】docTrivia.Span ではなく、メソッド自体の SpanStart から逆算するか、
        // あるいは sourceText から安全に位置を特定してテキスト変更を行うことで誤爆を根絶する
        // docTrivia.FullSpan や Span に余分な改行が含まれる場合があるため、
        // 該当コメントの開始位置から文字数を正確に合わせた TextChange を適用します。
        var textChange = new TextChange(commentSpan, newCommentText);
        return document.WithText(sourceText.WithChanges(textChange));
    }

    private List<string> RemoveExistingParams(List<string> lines)
    {
        var result = new List<string>();
        foreach (var line in lines)
        {
            var trimmed = line.Trim();
            if (trimmed.Contains("<param")) continue;
            result.Add(line);
        }
        return result;
    }

    private int DetermineInsertIndex(List<string> lines)
    {
        // </summary> があればその直後
        int index = lines.FindIndex(l => l.Contains("</summary>"));
        if (index >= 0) return index + 1;

        // <summary> がない場合（装飾行のみなど）は、最初の装飾行（---）の下
        index = lines.FindIndex(l => l.Contains("---"));
        if (index >= 0) return index + 1;

        // それもなければ最初の意味のある行の直後、または先頭
        index = lines.FindIndex(l => !string.IsNullOrWhiteSpace(l));
        return index >= 0 ? index + 1 : 0;
    }

    private List<string> InsertParameters(List<string> lines, SeparatedSyntaxList<ParameterSyntax> parameters, string prefix, int insertIndex)
    {
        var result = new List<string>(lines);

        foreach (var param in parameters)
        {
            var paramName = param.Identifier.ValueText;
            var paramLine = $"{prefix} <param name=\"{paramName}\"></param>";
            
            if (insertIndex >= 0 && insertIndex <= result.Count)
            {
                result.Insert(insertIndex, paramLine);
            }
            else
            {
                result.Add(paramLine);
            }
            insertIndex++;
        }

        return result;
    }
}
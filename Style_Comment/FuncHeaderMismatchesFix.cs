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
        
        // 1. メソッドの直前にあるトリビアから DocumentationCommentTriviaSyntax を取得
        var leadingTrivia = methodDecl.GetLeadingTrivia();
        SyntaxTrivia docTrivia = default;
        foreach (var trivia in leadingTrivia)
        {
            if (trivia.GetStructure() is DocumentationCommentTriviaSyntax)
            {
                docTrivia = trivia;
                break;
            }
        }

        if (docTrivia == default) return document;

        var originalComment = docTrivia.ToString();
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
        var textChange = new TextChange(docTrivia.Span, newCommentText);
        return document.WithText(sourceText.WithChanges(textChange));
    }

    private string DetectLineBreak(string comment)
    {
        return comment.Contains("\r\n") ? "\r\n" : "\n";
    }

    private List<string> SplitIntoLines(string comment)
    {
        return comment.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None).ToList();
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
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
    
    var xmlTrivia = methodDecl.GetLeadingTrivia()
        .Select(i => i.GetStructure())
        .OfType<DocumentationCommentTriviaSyntax>()
        .FirstOrDefault();

    if (xmlTrivia == null) return document;

    var originalComment = xmlTrivia.ToString();
    string lineBreak = originalComment.Contains("\r\n") ? "\r\n" : "\n";
    var lines = originalComment.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
    
    // 3. paramタグ以外の行を保持（paramを含む行はここで排除します）
    var newCommentLines = new List<string>();
    foreach (var line in lines)
    {
        // paramが含まれる行は無視して、それ以外（summaryなど）を保持
        if (line.Contains("<param")) continue;
        
        if (line.Contains("<"))
        {
            if (line.Contains("///"))
                newCommentLines.Add(line);
            else
                newCommentLines.Add("///" + line);
            continue;
        }                
        newCommentLines.Add(line);
    }
    
    // 4. </summary> の直後を特定
    int insertIndex = newCommentLines.FindIndex(l => l.Contains("</summary>")) + 1;
    if (insertIndex <= 0) insertIndex = newCommentLines.Count;

    // 5. 引数の順番通りにすべて挿入（これで順番が保証されます）
    foreach (var param in methodDecl.ParameterList.Parameters)
    {
        var paramName = param.Identifier.ValueText;
        newCommentLines.Insert(insertIndex, $"    /// <param name=\"{paramName}\"></param>");
        insertIndex++;
    }

    // 5. 文字列を再結合して置換
    var newComment = string.Join(lineBreak, newCommentLines);
    var newSourceText = sourceText.Replace(xmlTrivia.FullSpan, newComment);
    
    return document.WithText(newSourceText);
}



}
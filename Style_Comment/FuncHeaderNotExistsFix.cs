using System.Collections.Immutable;
using System.Composition;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace CSharp_IngeniousAnalyzer.Style_Comment;

[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(FuncHeaderNotExistsFix)), Shared]
public class FuncHeaderNotExistsFix : CodeFixProvider
{
    public sealed override ImmutableArray<string> FixableDiagnosticIds => [FuncHeaderNotExists.DiagnosticId];

    public sealed override FixAllProvider GetFixAllProvider() => WellKnownFixAllProviders.BatchFixer;

    public sealed override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
        var diagnostic = context.Diagnostics.First();
        var methodDecl = root?.FindToken(diagnostic.Location.SourceSpan.Start).Parent?.AncestorsAndSelf().OfType<MethodDeclarationSyntax>().First();

        if (methodDecl == null) return;

        context.RegisterCodeFix(
            CodeAction.Create(
                title: "Fix : 関数ヘッダーを新規作成する",
                createChangedDocument: c => CreateHeaderAsync(context.Document, methodDecl, c),
                equivalenceKey: nameof(FuncHeaderNotExistsFix)),
            diagnostic);
    }

    private async Task<Document> CreateHeaderAsync(Document document, MethodDeclarationSyntax methodDecl, System.Threading.CancellationToken cancellationToken)
    {
        var sourceText = await document.GetTextAsync(cancellationToken);
        
        var line = sourceText.Lines.GetLineFromPosition(methodDecl.SpanStart);
        var indent = new string(line.ToString().TakeWhile(char.IsWhiteSpace).ToArray());
        var paramNames = methodDecl.ParameterList.Parameters.Select(p => p.Identifier.ValueText);

        var lines = new List<string>
        {
            $"{indent}/// <summary>",
            $"{indent}/// ",
            $"{indent}/// </summary>"
        };
        
        foreach (var name in paramNames)
        {
            lines.Add($"{indent}/// <param name=\"{name}\"></param>");
        }
        
        // SourceTextの文字列表現から改行コードを判定（または安全にファイルを走査）
        var textStr = sourceText.ToString();
        var newLine = textStr.Contains("\r\n") ? "\r\n" : "\n";
        var fullComment = string.Join(newLine, lines) + newLine;        
        var textChange = new TextChange(new TextSpan(line.Start, 0), fullComment);
        
        return document.WithText(sourceText.WithChanges(textChange));
    }
}
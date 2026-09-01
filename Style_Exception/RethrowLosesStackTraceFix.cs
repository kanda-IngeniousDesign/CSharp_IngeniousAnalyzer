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

namespace CSharp_IngeniousAnalyzer.Style_Exception;

[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(RethrowLosesStackTraceFix)), Shared]
public class RethrowLosesStackTraceFix : CodeFixProvider
{
    public sealed override ImmutableArray<string> FixableDiagnosticIds => [RethrowLosesStackTrace.DiagnosticId];

    public sealed override FixAllProvider GetFixAllProvider() => WellKnownFixAllProviders.BatchFixer;

    public sealed override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
        var diagnostic = context.Diagnostics.First();
        var throwStmt = root?.FindNodeAtSpan<ThrowStatementSyntax>(diagnostic.Location.SourceSpan);
        if (throwStmt is null) return;

        context.RegisterCodeFix(
            CodeAction.Create(
                title: "Fix : throw; に置き換える",
                createChangedDocument: c => ReplaceWithBareThrowAsync(context.Document, throwStmt, c),
                equivalenceKey: nameof(RethrowLosesStackTraceFix)),
            diagnostic);
    }

    private static async Task<Document> ReplaceWithBareThrowAsync(Document document, ThrowStatementSyntax throwStmt, CancellationToken cancellationToken)
    {
        var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
        if (root is null) return document;

        // "throw" の直後の空白（式との区切り）を削除してから式を取り除き、"throw;" にする
        var newThrowKeyword = throwStmt.ThrowKeyword.WithTrailingTrivia();
        var newThrow = throwStmt.WithThrowKeyword(newThrowKeyword).WithExpression(null);

        var newRoot = root.ReplaceNode(throwStmt, newThrow);
        return document.WithSyntaxRoot(newRoot);
    }
}

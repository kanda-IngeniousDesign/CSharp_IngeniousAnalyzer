using System.Collections.Immutable;
using System.Composition;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace CSharp_IngeniousAnalyzer.Style_Linq;

[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(ToListToArrayDeleteCodeFixProvider)), Shared]
public class ToListToArrayDeleteCodeFixProvider : CodeFixProvider
{
    public sealed override ImmutableArray<string> FixableDiagnosticIds => [ToListToArrayDelete.DiagnosticId];

    public sealed override FixAllProvider GetFixAllProvider() => WellKnownFixAllProviders.BatchFixer;

    public sealed override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
        var diagnostic = context.Diagnostics.First();
        var invocation = root?.FindNode(diagnostic.Location.SourceSpan).FirstAncestorOrSelf<InvocationExpressionSyntax>();

        if (invocation != null)
        {
            context.RegisterCodeFix(
                CodeAction.Create("Fix : 不要な ToList/ToArray を削除", c => RemoveMethodCallAsync(context.Document, invocation, c), "RemoveToList"),
                diagnostic);
        }
    }

    private static async Task<Document> RemoveMethodCallAsync(Document document, InvocationExpressionSyntax invocation, CancellationToken ct)
    {
        var root = await document.GetSyntaxRootAsync(ct).ConfigureAwait(false);
        if (root == null || invocation.Expression is not MemberAccessExpressionSyntax memberAccess)
            return document;

        // invocation (list.Where(...).ToList()) を、
        // memberAccess.Expression (list.Where(...)) に置き換える
        // 今回のポイント：ReplaceNode がWhereを消さないよう、最も単純なノード置換にする
        var newRoot = root.ReplaceNode(invocation, memberAccess.Expression.WithoutTrivia().WithTriviaFrom(invocation));
        
        return document.WithSyntaxRoot(newRoot);
    }
}
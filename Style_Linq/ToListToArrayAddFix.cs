using System.Collections.Immutable;
using System.Composition;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Editing;

namespace CSharp_IngeniousAnalyzer.Style_Linq;

[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(ToListToArrayAddFix)), Shared]
public class ToListToArrayAddFix : CodeFixProvider
{
    public sealed override ImmutableArray<string> FixableDiagnosticIds => [ToListToArrayAdd.DiagnosticId];

    public sealed override FixAllProvider GetFixAllProvider() => WellKnownFixAllProviders.BatchFixer;

    public sealed override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
        var diagnostic = context.Diagnostics.First();
        var diagnosticSpan = diagnostic.Location.SourceSpan;

        var declaration = root?.FindToken(diagnosticSpan.Start).Parent?.AncestorsAndSelf().OfType<VariableDeclaratorSyntax>().FirstOrDefault();
        if (declaration == null) return;

        context.RegisterCodeFix(
            CodeAction.Create(
                title: "Fix : ToList() を追加して結果を確定する",
                createChangedDocument: c => AddToListAsync(context.Document, declaration, c),
                equivalenceKey: nameof(ToListToArrayAddFix)),
            diagnostic);
    }

    private static async Task<Document> AddToListAsync(Document document, VariableDeclaratorSyntax declaration, System.Threading.CancellationToken cancellationToken)
    {
        var editor = await DocumentEditor.CreateAsync(document, cancellationToken).ConfigureAwait(false);

        if (declaration.Initializer?.Value is InvocationExpressionSyntax invocation)
        {
            // invocation (.Where(...)) を ToList() 呼び出しでラップする
            var toListInvocation = SyntaxFactory.InvocationExpression(
                SyntaxFactory.MemberAccessExpression(
                    SyntaxKind.SimpleMemberAccessExpression,
                    invocation,
                    SyntaxFactory.IdentifierName("ToList")));

            // 初期化子を新しい Invocation に置き換え
            var newInitializer = declaration.Initializer.WithValue(toListInvocation);
            editor.ReplaceNode(declaration.Initializer, newInitializer);
        }

        return editor.GetChangedDocument();
    }
}
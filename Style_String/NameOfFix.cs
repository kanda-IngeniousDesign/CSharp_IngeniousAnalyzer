using System.Collections.Immutable;
using System.Composition;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using CSharp_IngeniousAnalyzer.Style_String;
using CSharp_IngeniousAnalyzer.Style__Common;

namespace CSharp_IngeniousAnalyzer.Style_String;

[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(NameofFix)), Shared]
public class NameofFix : CodeFixProvider
{
    public sealed override ImmutableArray<string> FixableDiagnosticIds => [Nameof.DiagnosticId];

    public sealed override FixAllProvider GetFixAllProvider() => WellKnownFixAllProviders.BatchFixer;

    public sealed override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
        if (root is null) return;

        var diagnostic = context.Diagnostics.First();
        var span = diagnostic.Location.SourceSpan;

        SyntaxNode targetNode = root.FindNodeAtSpan<LiteralExpressionSyntax>(span);
        if (targetNode is null)
        {
            targetNode = root.FindNodeAtSpan<InterpolatedStringTextSyntax>(span);
        }

        if (targetNode is null) return;

        context.RegisterCodeFix(
            CodeAction.Create(
                title: "Fix : nameof に書き換える",
                createChangedDocument: c => ReplaceWithNameofAsync(context.Document, targetNode, c),
                equivalenceKey: "ReplaceWithNameof"),
            diagnostic);
    }

    private async Task<Document> ReplaceWithNameofAsync(Document document, SyntaxNode targetNode, CancellationToken cancellationToken)
    {
        var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
        if (root is null) return document;

        if (targetNode is LiteralExpressionSyntax literal)
        {
            var variableName = literal.Token.ValueText;
            var nameofIdentifier = SyntaxFactory.IdentifierName("nameof");
            var argument = SyntaxFactory.Argument(SyntaxFactory.IdentifierName(variableName));
            var argumentList = SyntaxFactory.ArgumentList(SyntaxFactory.SeparatedList([argument]));
            var nameofExpression = SyntaxFactory.InvocationExpression(nameofIdentifier, argumentList);
            var nameofWithTrivia = nameofExpression.WithTriviaFrom(targetNode);

            var newRoot = root.ReplaceNode(targetNode, nameofWithTrivia);
            return document.WithSyntaxRoot(newRoot);
        }
        else if (targetNode is InterpolatedStringTextSyntax textSyntax)
        {
            var text = textSyntax.TextToken.ValueText;
            string variableName = string.Empty;
            var words = text.Split([' ', ':', '=', ',', ';', '\t', '\r', '\n'], System.StringSplitOptions.RemoveEmptyEntries);
            foreach (var word in words)
            {
                if (SyntaxFacts.IsValidIdentifier(word))
                {
                    variableName = word;
                    break;
                }
            }

            if (string.IsNullOrEmpty(variableName)) return document;

            var nameofIdentifier = SyntaxFactory.IdentifierName("nameof");
            var argument = SyntaxFactory.Argument(SyntaxFactory.IdentifierName(variableName));
            var argumentList = SyntaxFactory.ArgumentList(SyntaxFactory.SeparatedList([argument]));
            var nameofExpression = SyntaxFactory.InvocationExpression(nameofIdentifier, argumentList);
            var interpolation = SyntaxFactory.Interpolation(nameofExpression);

            var interpolatedString = textSyntax.Ancestors().OfType<InterpolatedStringExpressionSyntax>().FirstOrDefault();
            if (interpolatedString is null) return document;

            var parts = text.Split(new[] { variableName }, 2, System.StringSplitOptions.None);
            
            var newContents = new SyntaxList<InterpolatedStringContentSyntax>();
            foreach (var content in interpolatedString.Contents)
            {
                if (content == textSyntax)
                {
                    if (!string.IsNullOrEmpty(parts[0]))
                    {
                        var token0 = SyntaxFactory.Token(
                            textSyntax.TextToken.LeadingTrivia,
                            SyntaxKind.InterpolatedStringTextToken,
                            parts[0],
                            parts[0],
                            textSyntax.TextToken.TrailingTrivia);
                        newContents = newContents.Add(SyntaxFactory.InterpolatedStringText(token0));
                    }
                    
                    newContents = newContents.Add(interpolation);

                    if (parts.Length > 1 && !string.IsNullOrEmpty(parts[1]))
                    {
                        var token1 = SyntaxFactory.Token(
                            textSyntax.TextToken.LeadingTrivia,
                            SyntaxKind.InterpolatedStringTextToken,
                            parts[1],
                            parts[1],
                            textSyntax.TextToken.TrailingTrivia);
                        newContents = newContents.Add(SyntaxFactory.InterpolatedStringText(token1));
                    }
                }
                else
                {
                    newContents = newContents.Add(content);
                }
            }

            var newInterpolatedString = interpolatedString.WithContents(newContents);
            var newRoot = root.ReplaceNode(interpolatedString, newInterpolatedString);
            return document.WithSyntaxRoot(newRoot);
        }

        return document;
    }
}
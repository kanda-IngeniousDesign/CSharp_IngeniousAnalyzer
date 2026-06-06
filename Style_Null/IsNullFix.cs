using System.Collections.Immutable;
using System.Composition;
using CSharp_IngeniousAnalyzer.Style__Common;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace CSharp_IngeniousAnalyzer.Style_Null;

[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(IsNullFix)), Shared]
public class IsNullFix : CodeFixProvider
{
    public sealed override ImmutableArray<string> FixableDiagnosticIds => [IsNull.DiagnosticId];

    public sealed override FixAllProvider GetFixAllProvider() => WellKnownFixAllProviders.BatchFixer;

    public sealed override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
        if (root is null) return;

        // 共通拡張メソッドでスマートに逆引き
        var binaryExpr = root.FindNodeAtSpan<BinaryExpressionSyntax>(context.Diagnostics.First().Location.SourceSpan);
        if (binaryExpr is null) return;

        context.RegisterCodeFix(
            CodeAction.Create(
                title: "Fix : is / is not パターンに書き換える",
                createChangedDocument: c => ReplaceWithPatternAsync(context.Document, binaryExpr, c),
                equivalenceKey: "ReplaceWithPattern"),
            context.Diagnostics.First());
    }

    private async Task<Document> ReplaceWithPatternAsync(Document document, BinaryExpressionSyntax binaryExpr, CancellationToken cancellationToken)
    {
        var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
        if (root is null) return document;

        var isLeftNull = binaryExpr.Left is LiteralExpressionSyntax leftLiteral && leftLiteral.IsKind(SyntaxKind.NullLiteralExpression);
        var targetExpression = isLeftNull ? binaryExpr.Right : binaryExpr.Left;

        ExpressionSyntax newExpression;

        if (binaryExpr.IsKind(SyntaxKind.EqualsExpression))
        {
            var isToken = SyntaxFactory.Token(SyntaxKind.IsKeyword).WithTrailingTrivia(SyntaxFactory.TriviaList(SyntaxFactory.Space));
            var nullPattern = SyntaxFactory.ConstantPattern(SyntaxFactory.LiteralExpression(SyntaxKind.NullLiteralExpression));
            newExpression = SyntaxFactory.IsPatternExpression(targetExpression, isToken, nullPattern);
        }
        else
        {
            var isToken = SyntaxFactory.Token(SyntaxKind.IsKeyword).WithTrailingTrivia(SyntaxFactory.TriviaList(SyntaxFactory.Space));
            var notToken = SyntaxFactory.Token(SyntaxKind.NotKeyword).WithTrailingTrivia(SyntaxFactory.TriviaList(SyntaxFactory.Space));
            var nullPattern = SyntaxFactory.ConstantPattern(SyntaxFactory.LiteralExpression(SyntaxKind.NullLiteralExpression));
            newExpression = SyntaxFactory.IsPatternExpression(targetExpression, isToken, SyntaxFactory.UnaryPattern(notToken, nullPattern));
        }

        // 式全体の最外殻Trivia防衛も、この1行で美しく完結！
        newExpression = newExpression.WithTriviaFrom<ExpressionSyntax>(binaryExpr);

        return document.WithSyntaxRoot(root.ReplaceNode(binaryExpr, newExpression));
    }
}
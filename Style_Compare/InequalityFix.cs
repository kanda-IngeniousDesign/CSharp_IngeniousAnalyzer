using System.Collections.Immutable;
using System.Composition;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Editing;

namespace CSharp_IngeniousAnalyzer.Style_Compare;

[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(InequalityFix)), Shared]
public class InequalityFix : CodeFixProvider
{
    public sealed override ImmutableArray<string> FixableDiagnosticIds => [Inequality.DiagnosticId];

    public sealed override FixAllProvider GetFixAllProvider() => WellKnownFixAllProviders.BatchFixer;

    public sealed override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
        var diagnostic = context.Diagnostics.First();
        var binaryExpr = root?.FindNode(diagnostic.Location.SourceSpan) as BinaryExpressionSyntax;

        if (binaryExpr == null) return;

        context.RegisterCodeFix(
            CodeAction.Create(
                title: "Fix: 不等号を右向きに変更する",
                createChangedDocument: c => ReverseInequality(context.Document, binaryExpr, c),
                equivalenceKey: "ReverseInequality"),
            diagnostic);
    }

    private async Task<Document> ReverseInequality(Document document, BinaryExpressionSyntax node, CancellationToken ct)
    {
        var editor = await DocumentEditor.CreateAsync(document, ct);
        
        // 1. 演算子を「論理的に逆の」演算子に変換
        SyntaxKind newKind = node.Kind() switch
        {
            SyntaxKind.GreaterThanExpression => SyntaxKind.LessThanExpression,
            SyntaxKind.LessThanExpression => SyntaxKind.GreaterThanExpression,
            SyntaxKind.GreaterThanOrEqualExpression => SyntaxKind.LessThanOrEqualExpression,
            SyntaxKind.LessThanOrEqualExpression => SyntaxKind.GreaterThanOrEqualExpression,
            _ => node.Kind()
        };

        // 2. 演算子だけでなく、左辺と右辺も入れ替えることで論理値を維持する
        // 例: a > b  ->  b < a
        SyntaxKind operatorTokenKind = newKind switch
        {
            SyntaxKind.GreaterThanExpression => SyntaxKind.GreaterThanToken,
            SyntaxKind.LessThanExpression => SyntaxKind.LessThanToken,
            SyntaxKind.GreaterThanOrEqualExpression => SyntaxKind.GreaterThanEqualsToken,
            SyntaxKind.LessThanOrEqualExpression => SyntaxKind.LessThanEqualsToken,
            _ => throw new InvalidOperationException()
        };

        // 3. 左右を入れ替えて生成
        var newNode = SyntaxFactory.BinaryExpression(
            newKind,
            node.Right.WithoutTrivia(),
            SyntaxFactory.Token(operatorTokenKind),
            node.Left.WithoutTrivia()
        ).WithTriviaFrom(node);

        editor.ReplaceNode(node, newNode);
        return editor.GetChangedDocument();
    }
}
using System.Collections.Immutable;
using System.Composition;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using CSharp_IngeniousAnalyzer.Core;

namespace CSharp_IngeniousAnalyzer.Style_String;

[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(StringEmptyFix)), Shared]
public class StringEmptyFix : CodeFixProvider
{
    // 🛠️ 密結合にして二重管理を廃止
    public sealed override ImmutableArray<string> FixableDiagnosticIds => [StringEmpty.DiagnosticId];

    public sealed override FixAllProvider GetFixAllProvider() => WellKnownFixAllProviders.BatchFixer;

    public sealed override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
        if (root is null) return;

        // 💡 共通の拡張メソッドでノードを一撃取得！
        var binaryExpr = root.FindNodeAtSpan<BinaryExpressionSyntax>(context.Diagnostics.First().Location.SourceSpan);
        if (binaryExpr is null) return;

        context.RegisterCodeFix(
            CodeAction.Create(
                title: "Fix : string.Empty に書き換える",
                createChangedDocument: c => ReplaceWithPatternAsync(context.Document, binaryExpr, c),
                equivalenceKey: "ReplaceWithPattern"),
            context.Diagnostics.First());
    }

    private async Task<Document> ReplaceWithPatternAsync(Document document, BinaryExpressionSyntax binaryExpr, CancellationToken cancellationToken)
    {
        var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
        if (root is null) return document;

        var isLeftTarget = binaryExpr.Left is MemberAccessExpressionSyntax leftMa && 
                           leftMa.Name.Identifier.ValueText == "Empty" &&
                           leftMa.Expression.ToString().Trim() == "String";

        var oldExpression = isLeftTarget ? binaryExpr.Left : binaryExpr.Right;

        // 小文字の「string.Empty」式を組み立てる
        var stringEmptyExpression = SyntaxFactory.MemberAccessExpression(
            SyntaxKind.SimpleMemberAccessExpression,
            SyntaxFactory.PredefinedType(SyntaxFactory.Token(SyntaxKind.StringKeyword)),
            SyntaxFactory.IdentifierName("Empty"));

        // 💡 泥臭かったTriviaの移植が、拡張メソッドで究極にシンプルに！
        var nodeWithTrivia = stringEmptyExpression.WithTriviaFrom<MemberAccessExpressionSyntax>(oldExpression);

        var newBinaryExpr = isLeftTarget 
            ? binaryExpr.WithLeft(nodeWithTrivia) 
            : binaryExpr.WithRight(nodeWithTrivia);

        return document.WithSyntaxRoot(root.ReplaceNode(binaryExpr, newBinaryExpr));
    }
}
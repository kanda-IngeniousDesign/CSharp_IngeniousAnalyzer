using System.Collections.Immutable;
using System.Composition;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using CSharp_IngeniousAnalyzer.Core;
using CSharp_IngeniousAnalyzer.Style_String;

namespace CSharp_IngeniousAnalyzer.Style_Maintainability;

[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(NameofOptimizationFix)), Shared]
public class NameofOptimizationFix : CodeFixProvider
{
    public sealed override ImmutableArray<string> FixableDiagnosticIds => [Nameof.DiagnosticId];

    public sealed override FixAllProvider GetFixAllProvider() => WellKnownFixAllProviders.BatchFixer;

    public sealed override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
        if (root is null) return;

        // 共通の拡張メソッド「FindNodeAtSpan」で文字列ノードを一撃取得！
        var stringLiteral = root.FindNodeAtSpan<LiteralExpressionSyntax>(context.Diagnostics.First().Location.SourceSpan);
        if (stringLiteral is null) return;

        context.RegisterCodeFix(
            CodeAction.Create(
                title: "Fix : nameof に書き換える",
                createChangedDocument: c => ReplaceWithNameofAsync(context.Document, stringLiteral, c),
                equivalenceKey: "ReplaceWithNameof"),
            context.Diagnostics.First());
    }

    private async Task<Document> ReplaceWithNameofAsync(Document document, LiteralExpressionSyntax stringLiteral, CancellationToken cancellationToken)
    {
        var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
        if (root is null) return document;

        var variableName = stringLiteral.Token.ValueText;

        // 🛠️ 「nameof(変数名)」というInvocationExpression（メソッド呼び出し式）を精密にビルド
        var nameofIdentifier = SyntaxFactory.IdentifierName("nameof");
        var argument = SyntaxFactory.Argument(SyntaxFactory.IdentifierName(variableName));
        var argumentList = SyntaxFactory.ArgumentList(SyntaxFactory.SeparatedList(new[] { argument }));
        
        var nameofExpression = SyntaxFactory.InvocationExpression(nameofIdentifier, argumentList);

        // 【Triviaディフェンス】元の文字列の周りにあったコメントや改行を、新しいnameof式へ完全移植！
        var nameofWithTrivia = nameofExpression.WithTriviaFrom<InvocationExpressionSyntax>(stringLiteral);

        // 構文木を安全に置換
        var newRoot = root.ReplaceNode(stringLiteral, nameofWithTrivia);
        return document.WithSyntaxRoot(newRoot);
    }
}
using System.Collections.Immutable;
using System.Composition;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CSharp_IngeniousAnalyzer.Style__Common;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace CSharp_IngeniousAnalyzer.Style_Complexity;

[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(MethodComplexityFix)), Shared]
public class MethodComplexityFix : CodeFixProvider
{
    public sealed override ImmutableArray<string> FixableDiagnosticIds => [MethodComplexity.DiagnosticId];

    public sealed override FixAllProvider GetFixAllProvider() => WellKnownFixAllProviders.BatchFixer;

    public sealed override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
        if (root is null) return;

        var method = root.FindNodeAtSpan<MethodDeclarationSyntax>(context.Diagnostics.First().Location.SourceSpan);
        if (method is null) return;

        var title = "Ignore : メソッドの複雑度チェックを無視する";

        context.RegisterCodeFix(
            CodeAction.Create(
                title: title,
                createChangedDocument: c => AddIgnoreCommentAsync(context.Document, method, c),
                equivalenceKey: "IgnoreMethodComplexity"),
            context.Diagnostics.First());
    }

    private async Task<Document> AddIgnoreCommentAsync(Document document, MethodDeclarationSyntax method, CancellationToken cancellationToken)
    {
        var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
        if (root is null || method.Body is null) return document;

        // 半角スペース8つ、コメント、改行をまとめたトリビアリストを作成
        var ignoreTriviaList = SyntaxFactory.TriviaList(
            SyntaxFactory.Whitespace("        "),
            SyntaxFactory.Comment("// Ignore CPX001"),
            SyntaxFactory.EndOfLine("\r\n")
        );

        MethodDeclarationSyntax newMethod;

        if (method.Body.Statements.Count > 0)
        {
            // 1. ボディの中に既にステートメントがある場合：先頭文の既存トリビアの先頭に一括追加
            var firstStmt = method.Body.Statements[0];
            var existingTrivia = firstStmt.GetLeadingTrivia();

            // 既存トリビアの先頭に ignoreTriviaList を結合
            var newTrivia = ignoreTriviaList.AddRange(existingTrivia);
            var newFirstStmt = firstStmt.WithLeadingTrivia(newTrivia);

            var newBody = method.Body.ReplaceNode(firstStmt, newFirstStmt);
            newMethod = method.WithBody(newBody);
        }
        else
        {
            // 2. 空のメソッドの場合：開き括弧の直後に配置
            var openBraceToken = method.Body.OpenBraceToken;
            var newOpenBraceToken = openBraceToken.WithTrailingTrivia(
                openBraceToken.TrailingTrivia.AddRange(ignoreTriviaList)
            );

            var newBody = method.Body.WithOpenBraceToken(newOpenBraceToken);
            newMethod = method.WithBody(newBody);
        }

        var newRoot = root.ReplaceNode(method, newMethod);
        return document.WithSyntaxRoot(newRoot);
    }
}
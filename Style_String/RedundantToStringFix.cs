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

namespace CSharp_IngeniousAnalyzer.Style_String;

[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(RedundantToStringFix)), Shared]
public class RedundantToStringFix : CodeFixProvider
{
    public sealed override ImmutableArray<string> FixableDiagnosticIds => [RedundantToString.DiagnosticId];

    public sealed override FixAllProvider GetFixAllProvider() => WellKnownFixAllProviders.BatchFixer;

    public sealed override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
        if (root is null) return;

        var diagnostic = context.Diagnostics.First();
        var span = diagnostic.Location.SourceSpan;

        // 警告位置のスパンに厳密に一致するノードを取得する
        // （呼び出し元自体もメソッド呼び出しである場合、祖先を辿るFindNodeAtSpan<T>だと
        //   内側の呼び出し（例: GetName().ToString() の GetName()）を誤って拾ってしまうため）
        // 呼び出しが引数として渡されている場合（例: Foo(s.ToString())）、
        // 呼び出し自体のスパンが引数を包む ArgumentSyntax と完全に一致（タイ）するため、
        // getInnermostNodeForTie: true でタイ時に内側（呼び出し自体）を優先させる
        var targetNode = root.FindNode(span, getInnermostNodeForTie: true);
        if (targetNode is not (InvocationExpressionSyntax or ConditionalAccessExpressionSyntax)) return;

        context.RegisterCodeFix(
            CodeAction.Create(
                title: "Fix : 冗長な ToString() 呼び出しを削除する",
                createChangedDocument: c => RemoveToStringAsync(context.Document, targetNode, c),
                equivalenceKey: nameof(RedundantToStringFix)),
            diagnostic);
    }

    private async Task<Document> RemoveToStringAsync(Document document, SyntaxNode targetNode, CancellationToken cancellationToken)
    {
        var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
        if (root is null) return document;

        ExpressionSyntax receiver = targetNode switch
        {
            InvocationExpressionSyntax { Expression: MemberAccessExpressionSyntax memberAccess } => memberAccess.Expression,
            ConditionalAccessExpressionSyntax conditionalAccess => conditionalAccess.Expression,
            _ => (ExpressionSyntax)targetNode
        };

        // 泥臭かったTriviaの移植が、拡張メソッドで究極にシンプルに！
        var newReceiver = receiver.WithTriviaFrom(targetNode);

        return document.WithSyntaxRoot(root.ReplaceNode(targetNode, newReceiver));
    }
}

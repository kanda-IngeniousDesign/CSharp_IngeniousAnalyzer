using System.Collections.Immutable;
using System.Composition;
using CSharp_IngeniousAnalyzer.Style__Common;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace CSharp_IngeniousAnalyzer.Style_Linq;

[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(LinqOptimizeFix)), Shared]
public class LinqOptimizeFix : CodeFixProvider
{
    public sealed override ImmutableArray<string> FixableDiagnosticIds => [LinqOptimize.DiagnosticId];

    public sealed override FixAllProvider GetFixAllProvider() => WellKnownFixAllProviders.BatchFixer;

    public sealed override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
        if (root is null) return;

        // 警告位置のスパンに厳密に一致するノードを取得する
        // （Where().Where().FirstOrDefault() のようにネストした呼び出しは開始位置が同じになるため、
        //   祖先を辿る FindNodeAtSpan<T> だと内側の呼び出しを誤って拾ってしまう）
        // 呼び出しが引数として渡されている場合（例: Foo(list.Where(x).FirstOrDefault())）、
        // 呼び出し自体のスパンが引数を包む ArgumentSyntax と完全に一致（タイ）するため、
        // getInnermostNodeForTie: true でタイ時に内側（呼び出し自体）を優先させる
        if (root.FindNode(context.Diagnostics.First().Location.SourceSpan, getInnermostNodeForTie: true) is not InvocationExpressionSyntax invocation)
        {
            return;
        }

        context.RegisterCodeFix(
            CodeAction.Create(
                title: "Fix : LINQの評価を最適化する",
                createChangedDocument: c => ReplaceWithOptimizedLinqAsync(context.Document, invocation, c),
                equivalenceKey: "OptimizeLinq"),
            context.Diagnostics.First());
    }

    private async Task<Document> ReplaceWithOptimizedLinqAsync(Document document, InvocationExpressionSyntax invocation, CancellationToken cancellationToken)
    {
        var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
        if (root is null) return document;

        // 後続チェーン（.ToString()など）に惑わされず、Where().FirstOrDefault()のペアを内視鏡特定
        var rightInvocation = invocation.AncestorsAndSelf()
            .OfType<InvocationExpressionSyntax>()
            .FirstOrDefault(i => 
                i.Expression is MemberAccessExpressionSyntax ma && 
                ma.Expression is InvocationExpressionSyntax leftInv && 
                leftInv.Expression is MemberAccessExpressionSyntax leftMa && 
                leftMa.Name.Identifier.ValueText == "Where");

        if (rightInvocation is null) return document;

        var rightMemberAccess = (MemberAccessExpressionSyntax)rightInvocation.Expression;
        var leftInvocation = (InvocationExpressionSyntax)rightMemberAccess.Expression;
        var leftMemberAccess = (MemberAccessExpressionSyntax)leftInvocation.Expression;

        var whereArguments = leftInvocation.ArgumentList;

        // 大元（list）のレシーバーへすり替え
        var newMemberAccess = rightMemberAccess.WithExpression(leftMemberAccess.Expression);

        var newInvocation = rightInvocation
            .WithExpression(newMemberAccess)
            .WithArgumentList(whereArguments);

        // 構文木の置換結果を新しいルートとして返す
        var newRoot = root.ReplaceNode(rightInvocation, newInvocation);
        return document.WithSyntaxRoot(newRoot);
    }
}
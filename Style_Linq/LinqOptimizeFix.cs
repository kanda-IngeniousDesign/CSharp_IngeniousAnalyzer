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

        // 自作の共通拡張メソッド「FindNodeAtSpan」でスッキリ逆引き
        var invocation = root.FindNodeAtSpan<InvocationExpressionSyntax>(context.Diagnostics.First().Location.SourceSpan);
        if (invocation is null) return;

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
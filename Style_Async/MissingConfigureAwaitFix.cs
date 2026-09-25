using System.Collections.Immutable;
using System.Composition;
using CSharp_IngeniousAnalyzer.Style__Common;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace CSharp_IngeniousAnalyzer.Style_Async;

// ".ConfigureAwait(false)" を追記するFixは提供しない。
// ConfigureAwait(false) は await 後の継続を元の SynchronizationContext に戻さなくするため、
// await より後ろでUI要素（WinForms/WPF）や HttpContext.Current（ASP.NET Framework）等の
// コンテキスト依存の処理を行っている場合、追記するだけで実行時例外や動作変化を引き起こす。
// アナライザー単体ではその依存を100%判定できないため、ここで提供するのは
// 「// Ignore ASYNC001」コメントを挿入するFixのみで、CPX001/CPX002と同じ考え方に基づく抑制専用の補助アクションである。
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(MissingConfigureAwaitFix)), Shared]
public class MissingConfigureAwaitFix : CodeFixProvider
{
    public sealed override ImmutableArray<string> FixableDiagnosticIds => [MissingConfigureAwait.DiagnosticId];

    public sealed override FixAllProvider GetFixAllProvider() => WellKnownFixAllProviders.BatchFixer;

    public sealed override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
        if (root is null) return;

        var diagnostic = context.Diagnostics.First();

        // ラムダ式・ローカル関数内の await であっても、それを含むメソッド単位で抑制する
        var method = root.FindNodeAtSpan<BaseMethodDeclarationSyntax>(diagnostic.Location.SourceSpan);
        if (method?.Body is null) return;

        context.RegisterCodeFix(
            CodeAction.Create(
                title: "Ignore : ConfigureAwait チェックを無視する",
                createChangedDocument: c => context.Document.InsertIgnoreCommentInMethodAsync(method, MissingConfigureAwait.DiagnosticId, c),
                equivalenceKey: "IgnoreMissingConfigureAwait"),
            diagnostic);
    }
}

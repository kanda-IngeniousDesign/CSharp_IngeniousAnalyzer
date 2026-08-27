using System.Collections.Immutable;
using System.Composition;
using CSharp_IngeniousAnalyzer.Style__Common;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace CSharp_IngeniousAnalyzer.Style_Complexity;

// CPX002自体（巨大メソッドの分割）を機械的に安全に行うFixは提供しない。
// ここで提供するのは「// Ignore CPX002」コメントを挿入するFixのみで、
// CPX001（MethodComplexityFix）と同じ考え方に基づく抑制専用の補助アクションである。
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(FatMethodFix)), Shared]
public class FatMethodFix : CodeFixProvider
{
    public sealed override ImmutableArray<string> FixableDiagnosticIds => [FatMethod.DiagnosticId];

    public sealed override FixAllProvider GetFixAllProvider() => WellKnownFixAllProviders.BatchFixer;

    public sealed override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
        if (root is null) return;

        var method = root.FindNodeAtSpan<MethodDeclarationSyntax>(context.Diagnostics.First().Location.SourceSpan);
        if (method is null) return;

        var title = "Ignore : メソッド分割チェックを無視する";

        context.RegisterCodeFix(
            CodeAction.Create(
                title: title,
                createChangedDocument: c => context.Document.InsertIgnoreCommentInMethodAsync(method, FatMethod.DiagnosticId, c),
                equivalenceKey: "IgnoreFatMethod"),
            context.Diagnostics.First());
    }
}

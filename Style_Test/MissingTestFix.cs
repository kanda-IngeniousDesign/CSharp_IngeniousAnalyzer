using System.Collections.Immutable;
using System.Composition;
using CSharp_IngeniousAnalyzer.Style__Common;
using IngeniousXUnitGenerator.Core;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace CSharp_IngeniousAnalyzer.Style_Test;

// プロダクトコードは書き換えない。テストコードの生成はテストプロジェクトへのファイル追加だけで、
// もう一方は「// Ignore TEST001」を挿入する抑制専用のFix。
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(MissingTestFix)), Shared]
public class MissingTestFix : CodeFixProvider
{
    public sealed override ImmutableArray<string> FixableDiagnosticIds => [MissingTest.DiagnosticId];

    public sealed override FixAllProvider GetFixAllProvider() => WellKnownFixAllProviders.BatchFixer;

    public sealed override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
        if (root is null) return;

        var diagnostic = context.Diagnostics.First();
        var classDecl = root.FindNodeAtSpan<ClassDeclarationSyntax>(diagnostic.Location.SourceSpan);
        if (classDecl is null) return;

        var semanticModel = await context.Document.GetSemanticModelAsync(context.CancellationToken).ConfigureAwait(false);
        if (semanticModel?.GetDeclaredSymbol(classDecl, context.CancellationToken) is { } targetType)
        {
            var document = context.Document;
            var testProjects = TestProjectFinder.Find(document.Project.Solution, document.Project);

            // テストプロジェクトがなければ、作成してから生成する Fix を出す
            var createAction = testProjects.Count == 0 ? TestProjectLocator.TryCreateAction(document, targetType, semanticModel.Compilation, isRefactoring: false) : null;
            if (createAction is not null) context.RegisterCodeFix(createAction, diagnostic);

            foreach (var testProject in testProjects)
            {
                var testProjectId = testProject.Id;
                context.RegisterCodeFix(
                    CodeAction.Create(
                        title: $"Fix : テストコードを生成する（{testProject.Name}）",
                        createChangedSolution: c => Task.FromResult(
                            TestDocumentWriter.AddTestDocument(document, targetType, semanticModel.Compilation, testProjectId)),
                        equivalenceKey: $"GenerateTest:{testProject.Name}"),
                    diagnostic);
            }
        }

        context.RegisterCodeFix(
            CodeAction.Create(
                title: "Ignore : テストコードの存在チェックを無視する",
                createChangedDocument: c => context.Document.InsertIgnoreCommentBeforeAsync(classDecl, MissingTest.DiagnosticId, c),
                equivalenceKey: "IgnoreMissingTest"),
            diagnostic);
    }
}

using System.Composition;
using IngeniousXUnitGenerator.Core;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeRefactorings;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace CSharp_IngeniousAnalyzer.Style_Test;

// TEST001 を有効にしていなくても、クラス名の上の電球メニューからテストコードを生成できるようにする
[ExportCodeRefactoringProvider(LanguageNames.CSharp, Name = nameof(GenerateTestRefactoring)), Shared]
public class GenerateTestRefactoring : CodeRefactoringProvider
{
    public sealed override async Task ComputeRefactoringsAsync(CodeRefactoringContext context)
    {
        var document = context.Document;
        var root = await document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
        if (root is null) return;

        // クラス本体のどこでも出すとメニューがうるさいため、クラス名の上に限定する
        var classDecl = root.FindToken(context.Span.Start).Parent as ClassDeclarationSyntax;
        if (classDecl is null || !classDecl.Identifier.Span.IntersectsWith(context.Span)) return;

        var semanticModel = await document.GetSemanticModelAsync(context.CancellationToken).ConfigureAwait(false);
        if (semanticModel?.GetDeclaredSymbol(classDecl, context.CancellationToken) is not { } targetType) return;

        var testProjects = TestProjectFinder.Find(document.Project.Solution, document.Project);
        var createAction = testProjects.Count == 0 ? TestProjectLocator.TryCreateAction(document, targetType, semanticModel.Compilation, isRefactoring: true) : null;
        if (createAction is not null) context.RegisterRefactoring(createAction);

        foreach (var testProject in testProjects)
        {
            // 既にテストファイルがあるテストプロジェクトには出さない（マージは未対応）
            if (TestDocumentWriter.Exists(testProject, document, targetType)) continue;

            var testProjectId = testProject.Id;
            context.RegisterRefactoring(
                CodeAction.Create(
                    title: $"このクラスのテストコードを生成（{testProject.Name}）",
                    createChangedSolution: c => Task.FromResult(
                        TestDocumentWriter.AddTestDocument(document, targetType, semanticModel.Compilation, testProjectId)),
                    equivalenceKey: $"GenerateTestRefactoring:{testProject.Name}"));
        }
    }
}

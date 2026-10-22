using System.Composition;
using IngeniousXUnitGenerator.Core;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeRefactorings;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace CSharp_IngeniousAnalyzer.Style_Test;

// TEST001 を有効にしていなくても、電球メニューからテストコードを生成・追加できるようにする。
// ・クラス名の上   : クラス全体のテストを生成する（テストファイルが既にあれば、不足しているテストだけを追加する）
// ・メソッド名の上 : そのメソッド（コンストラクタ）のテストだけを生成・追加する
// どちらもテストプロジェクトがなければ、テストプロジェクトを作成してから生成する
[ExportCodeRefactoringProvider(LanguageNames.CSharp, Name = nameof(GenerateTestRefactoring)), Shared]
public class GenerateTestRefactoring : CodeRefactoringProvider
{
    public sealed override async Task ComputeRefactoringsAsync(CodeRefactoringContext context)
    {
        var document = context.Document;
        var root = await document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
        if (root is null) return;

        // 本体のどこでも出すとメニューがうるさいため、クラス名・メソッド名の上に限定する
        var target = FindTarget(root.FindToken(context.Span.Start), context.Span);
        if (target is null) return;

        var semanticModel = await document.GetSemanticModelAsync(context.CancellationToken).ConfigureAwait(false);
        if (semanticModel is null) return;

        var (classDecl, memberDecl) = target.Value;
        if (semanticModel.GetDeclaredSymbol(classDecl, context.CancellationToken) is not { } targetType) return;

        IMethodSymbol? onlyMember = null;
        if (memberDecl is not null)
        {
            onlyMember = semanticModel.GetDeclaredSymbol(memberDecl, context.CancellationToken) as IMethodSymbol;
            if (onlyMember is null || onlyMember.DeclaredAccessibility != Accessibility.Public) return;
        }

        var compilation = semanticModel.Compilation;
        var testProjects = TestProjectFinder.Find(document.Project.Solution, document.Project);
        if (testProjects.Count == 0)
        {
            var origin = onlyMember is null ? CreateTestProjectCodeAction.Origin.ClassRefactoring : CreateTestProjectCodeAction.Origin.MethodRefactoring;
            var createAction = TestProjectLocator.TryCreateAction(document, targetType, compilation, origin, onlyMember);
            if (createAction is not null) context.RegisterRefactoring(createAction);
            return;
        }

        foreach (var testProject in testProjects)
        {
            var testProjectId = testProject.Id;
            context.RegisterRefactoring(
                CodeAction.Create(
                    title: BuildTitle(onlyMember, TestDocumentWriter.Exists(testProject, document, targetType), testProject.Name),
                    createChangedSolution: c => TestDocumentWriter.AddOrMergeTestDocumentAsync(document, targetType, compilation, testProjectId, onlyMember, c),
                    equivalenceKey: $"GenerateTestRefactoring:{testProject.Name}:{onlyMember?.Name}"));
        }
    }

    /// <summary>
    /// カーソル位置がクラス名、または（クラス直下の）メソッド・コンストラクタ名の上なら、その宣言を返す
    /// </summary>
    private static (ClassDeclarationSyntax Class, BaseMethodDeclarationSyntax? Member)? FindTarget(SyntaxToken token, Microsoft.CodeAnalysis.Text.TextSpan span)
    {
        if (!token.Span.IntersectsWith(span)) return null;

        return token.Parent switch
        {
            ClassDeclarationSyntax classDecl when classDecl.Identifier == token => (classDecl, null),
            MethodDeclarationSyntax { Parent: ClassDeclarationSyntax owner } method when method.Identifier == token => (owner, method),
            ConstructorDeclarationSyntax { Parent: ClassDeclarationSyntax owner } constructor when constructor.Identifier == token => (owner, constructor),
            _ => null,
        };
    }

    /// <summary>
    /// 対象（クラス・メソッド）と、テストファイルが既にあるかでタイトルを変える
    /// </summary>
    private static string BuildTitle(IMethodSymbol? onlyMember, bool testFileExists, string testProjectName)
    {
        if (onlyMember is not null) return $"このメソッドのテストコードを生成（{testProjectName}）";

        return testFileExists
            ? $"このクラスの不足しているテストコードを追加（{testProjectName}）"
            : $"このクラスのテストコードを生成（{testProjectName}）";
    }
}

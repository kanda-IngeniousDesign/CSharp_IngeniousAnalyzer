using System.Collections.Immutable;
using CSharp_IngeniousAnalyzer.Style_Test;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CodeRefactorings;
using Microsoft.CodeAnalysis.Text;

namespace CSharp_IngeniousAnalyzer.Tests.Style_Test;

/// <summary>
/// TEST001 の「テストコードを生成」Fix と、クラス名の上の電球メニュー（リファクタリング）が、
/// 別プロジェクト（テストプロジェクト）にテストファイルを追加することを検証するテスト
/// </summary>
public class GenerateTestFixTests
{
    private const string CalculatorSource = """
        using System;
        namespace Product.Domain;
        public class Calculator
        {
            public int Divide(int dividend, int divisor)
            {
                if (divisor == 0) throw new ArgumentOutOfRangeException(nameof(divisor));
                return dividend / divisor;
            }
        }
        """;

    private static readonly MetadataReference CorLib = MetadataReference.CreateFromFile(typeof(object).Assembly.Location);
    private static readonly MetadataReference XUnitCore = MetadataReference.CreateFromFile(typeof(FactAttribute).Assembly.Location);

    /// <summary>
    /// Fix に「テストコードを生成」と「Ignore」が出て、生成するとテストプロジェクトに CalculatorTests.cs が追加されることを確認する
    /// </summary>
    [Fact]
    public async Task Fix_TestProjectExists_AddsGeneratedTestFile()
    {
        using var workspace = new AdhocWorkspace();
        var product = AddProductProject(workspace);
        var tests = AddProject(workspace, "Product.Tests", [CorLib, XUnitCore], product.Id);

        var actions = await ComputeFixesAsync(workspace);

        Assert.Equal(["Fix : テストコードを生成する（Product.Tests）", "Ignore : テストコードの存在チェックを無視する"], actions.Select(a => a.Title));
        var newSolution = await ApplyAsync(actions[0]);
        var added = Assert.Single(newSolution.GetProject(tests.Id)!.Documents);
        Assert.Equal("CalculatorTests.cs", added.Name);
        Assert.Equal(["Domain"], added.Folders);

        var text = (await added.GetTextAsync()).ToString();
        Assert.Contains("namespace Product.Tests.Domain;", text);
        Assert.Contains("public void Divide_DivisorIsZero_ThrowsArgumentOutOfRangeException()", text);
        Assert.Contains("public void Divide_BoundaryValues_ReturnsExpected(int dividend, int divisor, int expected)", text);
    }

    /// <summary>
    /// テストプロジェクトがなければ、Ignore の Fix だけが出ることを確認する（プロジェクトの自動作成は別途対応）
    /// </summary>
    [Fact]
    public async Task Fix_NoTestProject_OffersOnlyIgnore()
    {
        using var workspace = new AdhocWorkspace();
        AddProductProject(workspace);

        var actions = await ComputeFixesAsync(workspace);

        Assert.Equal(["Ignore : テストコードの存在チェックを無視する"], actions.Select(a => a.Title));
    }

    /// <summary>
    /// クラス名の上の電球メニューから生成でき、テストファイルが既にあるテストプロジェクトには出ないことを確認する
    /// </summary>
    [Fact]
    public async Task Refactoring_OffersOnlyForTestProjectsWithoutTestFile()
    {
        using var workspace = new AdhocWorkspace();
        var product = AddProductProject(workspace);
        AddProject(workspace, "Product.Tests", [CorLib, XUnitCore], product.Id);
        var integration = AddProject(workspace, "Product.IntegrationTests", [CorLib, XUnitCore], product.Id);
        workspace.AddDocument(DocumentInfo.Create(
            DocumentId.CreateNewId(integration.Id), "CalculatorTests.cs", ["Domain"],
            loader: TextLoader.From(TextAndVersion.Create(SourceText.From("// 既存のテスト"), VersionStamp.Create()))));

        var document = GetCalculatorDocument(workspace);
        var position = (await document.GetTextAsync()).ToString().IndexOf("Calculator", StringComparison.Ordinal);
        var actions = new List<CodeAction>();
        var context = new CodeRefactoringContext(document, new TextSpan(position, 0), actions.Add, CancellationToken.None);
        await new GenerateTestRefactoring().ComputeRefactoringsAsync(context);

        Assert.Equal(["このクラスのテストコードを生成（Product.Tests）"], actions.Select(a => a.Title));
    }

    private static async Task<List<CodeAction>> ComputeFixesAsync(AdhocWorkspace workspace)
    {
        var document = GetCalculatorDocument(workspace);
        var root = (await document.GetSyntaxRootAsync())!;
        var identifier = root.DescendantTokens().First(t => t.ValueText == "Calculator");
        var descriptor = new MissingTest().SupportedDiagnostics.Single();
        var diagnostic = Diagnostic.Create(descriptor, identifier.GetLocation(), "Calculator");

        var actions = new List<CodeAction>();
        var context = new CodeFixContext(document, diagnostic, (action, _) => actions.Add(action), CancellationToken.None);
        await new MissingTestFix().RegisterCodeFixesAsync(context);
        return actions;
    }

    private static async Task<Solution> ApplyAsync(CodeAction action)
    {
        var operations = await action.GetOperationsAsync(CancellationToken.None);
        return Assert.IsType<ApplyChangesOperation>(Assert.Single(operations)).ChangedSolution;
    }

    private static Document GetCalculatorDocument(AdhocWorkspace workspace)
    {
        return workspace.CurrentSolution.Projects.Single(p => p.Name == "Product").Documents.Single();
    }

    private static Project AddProductProject(AdhocWorkspace workspace)
    {
        var project = AddProject(workspace, "Product", [CorLib]);
        workspace.AddDocument(DocumentInfo.Create(
            DocumentId.CreateNewId(project.Id), "Calculator.cs", ["Domain"],
            loader: TextLoader.From(TextAndVersion.Create(SourceText.From(CalculatorSource), VersionStamp.Create())),
            filePath: Path.Combine("/src", "Product", "Domain", "Calculator.cs")));
        return project;
    }

    private static Project AddProject(AdhocWorkspace workspace, string name, MetadataReference[] references, ProjectId? referencedProject = null)
    {
        var info = ProjectInfo.Create(
            ProjectId.CreateNewId(), VersionStamp.Create(), name, name, LanguageNames.CSharp,
            filePath: Path.Combine("/src", name, name + ".csproj"),
            metadataReferences: references,
            projectReferences: referencedProject is null ? [] : [new ProjectReference(referencedProject)])
            .WithDefaultNamespace(name);
        return workspace.AddProject(info);
    }
}

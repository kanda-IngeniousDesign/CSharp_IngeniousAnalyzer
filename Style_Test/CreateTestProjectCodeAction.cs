using IngeniousXUnitGenerator.Core;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;

namespace CSharp_IngeniousAnalyzer.Style_Test;

// テストプロジェクトがないときに、テストプロジェクトを作ってテストコードを生成する。
// Roslyn のコード修正（ApplyChangesOperation）ではプロジェクトを追加できないため、
// 独自の操作でファイルを書き出し、dotnet sln add でソリューションに追加する（ファイル操作は Core 側で行う）。
internal sealed class CreateTestProjectCodeAction : CodeAction
{
    private readonly Document _document;
    private readonly INamedTypeSymbol _targetType;
    private readonly Compilation _compilation;
    private readonly string _testProjectDirectory;

    public CreateTestProjectCodeAction(Document document, INamedTypeSymbol targetType, Compilation compilation, string testProjectDirectory)
    {
        _document = document;
        _targetType = targetType;
        _compilation = compilation;
        _testProjectDirectory = testProjectDirectory;
    }

    public override string Title => $"Fix : テストプロジェクト（{TestProjectName}）を作成してテストコードを生成する";

    public override string? EquivalenceKey => "CreateTestProject";

    private string TestProjectName => Path.GetFileName(_testProjectDirectory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));

    protected override Task<IEnumerable<CodeActionOperation>> ComputeOperationsAsync(CancellationToken cancellationToken)
    {
        var (relativePath, code) = TestDocumentWriter.GenerateForNewProject(_document, _targetType, _compilation, TestProjectName);
        var productProjectPath = _document.Project.FilePath!;
        var solutionPath = _document.Project.Solution.FilePath ?? TestProjectScaffolder.FindSolution(productProjectPath);

        IEnumerable<CodeActionOperation> operations = [new CreateTestProjectOperation(Title, _testProjectDirectory, productProjectPath, solutionPath, relativePath, code)];
        return Task.FromResult(operations);
    }

    // ディスクへの書き込みはプレビューできないため、プレビューでは何も表示しない
    protected override Task<IEnumerable<CodeActionOperation>> ComputePreviewOperationsAsync(CancellationToken cancellationToken)
    {
        return Task.FromResult(Enumerable.Empty<CodeActionOperation>());
    }

    private sealed class CreateTestProjectOperation : CodeActionOperation
    {
        private readonly string _title;
        private readonly string _testProjectDirectory;
        private readonly string _productProjectPath;
        private readonly string? _solutionPath;
        private readonly string _testFileRelativePath;
        private readonly string _testCode;

        public CreateTestProjectOperation(
            string title, string testProjectDirectory, string productProjectPath, string? solutionPath, string testFileRelativePath, string testCode)
        {
            _title = title;
            _testProjectDirectory = testProjectDirectory;
            _productProjectPath = productProjectPath;
            _solutionPath = solutionPath;
            _testFileRelativePath = testFileRelativePath;
            _testCode = testCode;
        }

        public override string Title => _title;

        public override void Apply(Workspace workspace, CancellationToken cancellationToken)
        {
            TestProjectScaffolder.Create(_testProjectDirectory, _productProjectPath, _solutionPath, _testFileRelativePath, _testCode);
        }
    }
}

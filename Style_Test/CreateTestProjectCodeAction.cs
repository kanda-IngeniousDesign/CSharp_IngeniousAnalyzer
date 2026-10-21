using IngeniousXUnitGenerator.Core;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.Text;

namespace CSharp_IngeniousAnalyzer.Style_Test;

// テストプロジェクトがないときに、テストプロジェクトを作ってテストコードを生成する。
// Roslyn のコード修正（ApplyChangesOperation）ではプロジェクトを追加できないため、環境ごとに作り方を変える。
// ・Visual Studio : 独自の操作でファイルを書き出し、dotnet sln add でソリューションに追加する（ファイル操作は Core 側で行う）
// ・VS Code       : 独自の操作は実行されない（Roslyn の言語サーバーは ApplyChangesOperation 以外を読み飛ばす）ため、
//                   追加ファイル（AdditionalDocument）として csproj とテストファイルを作らせる。ソリューションへの追加だけは手動
public sealed class CreateTestProjectCodeAction : CodeAction
{
    private readonly Document _document;
    private readonly INamedTypeSymbol _targetType;
    private readonly Compilation _compilation;
    private readonly string _testProjectDirectory;
    private readonly bool _useFileEdits;
    private readonly bool _isRefactoring;

    public CreateTestProjectCodeAction(
        Document document, INamedTypeSymbol targetType, Compilation compilation, string testProjectDirectory, bool useFileEdits, bool isRefactoring)
    {
        _document = document;
        _targetType = targetType;
        _compilation = compilation;
        _testProjectDirectory = testProjectDirectory;
        _useFileEdits = useFileEdits;
        _isRefactoring = isRefactoring;
    }

    // Fix とリファクタリングで同じタイトルにしてはならない。言語サーバーは同じタイトルのアクションを1つにまとめ、
    // 範囲を選んだ要求（ホバーの「クイック フィックス」）ではリファクタリング側が残るため、クイックフィックスの一覧から消えてしまう
    public override string Title => (_isRefactoring
            ? $"このクラスのテストプロジェクト（{TestProjectName}）を作成してテストコードを生成"
            : $"Fix : テストプロジェクト（{TestProjectName}）を作成してテストコードを生成する")
        + (_useFileEdits ? "（ソリューションへの追加は dotnet sln add で）" : string.Empty);

    // 一括適用（すべて修正）で複数のプロジェクト作成が同時に走らないよう、等価キーを持たせない
    public override string? EquivalenceKey => null;

    private string TestProjectName => Path.GetFileName(_testProjectDirectory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));

    protected override Task<IEnumerable<CodeActionOperation>> ComputeOperationsAsync(CancellationToken cancellationToken)
    {
        return Task.FromResult(ComputeOperationsCore());
    }

    private IEnumerable<CodeActionOperation> ComputeOperationsCore()
    {
        var (relativePath, code) = TestDocumentWriter.GenerateForNewProject(_document, _targetType, _compilation, TestProjectName);
        var productProjectPath = _document.Project.FilePath!;

        // VS Code（C# Dev Kit）では Solution.FilePath が内部で生成したソリューションを指すため使わず、
        // プロダクトのプロジェクトから上へたどって、利用者のソリューションを探す
        var solutionPath = TestProjectScaffolder.FindSolution(productProjectPath);

        IEnumerable<CodeActionOperation> operations = _useFileEdits
            ? [new ApplyChangesOperation(AddProjectFiles(productProjectPath, solutionPath, relativePath, code))]
            : [new CreateTestProjectOperation(Title, _testProjectDirectory, productProjectPath, solutionPath, relativePath, code)];
        return operations;
    }

    // Visual Studio の独自の操作（ディスクへの書き込み）はプレビューできないため、プレビューでは何も表示しない
    protected override async Task<IEnumerable<CodeActionOperation>> ComputePreviewOperationsAsync(CancellationToken cancellationToken)
    {
        return _useFileEdits ? await ComputeOperationsAsync(cancellationToken).ConfigureAwait(false) : [];
    }

    /// <summary>
    /// csproj とテストファイルを、プロダクトのプロジェクトの追加ファイルとして加えたソリューションを作る。
    /// VS Code はこれを「ファイルの作成」として反映する（プロダクトの csproj は変更されない）
    /// </summary>
    private Solution AddProjectFiles(string productProjectPath, string? solutionPath, string testFileRelativePath, string testCode)
    {
        var projectFilePath = Path.Combine(_testProjectDirectory, TestProjectName + ".csproj");
        var addCommand = solutionPath is null
            ? $"dotnet sln add \"{projectFilePath}\""
            : $"dotnet sln \"{solutionPath}\" add \"{projectFilePath}\"";
        var header = $"// ソリューションに追加するには: {addCommand}\r\n";

        var project = _document.Project;
        var solution = project.Solution
            .AddAdditionalDocument(
                DocumentId.CreateNewId(project.Id),
                TestProjectName + ".csproj",
                SourceText.From(TestProjectScaffolder.BuildProjectFile(_testProjectDirectory, productProjectPath)),
                filePath: projectFilePath)
            .AddAdditionalDocument(
                DocumentId.CreateNewId(project.Id),
                Path.GetFileName(testFileRelativePath),
                SourceText.From(header + testCode),
                filePath: Path.Combine(_testProjectDirectory, testFileRelativePath));
        return solution;
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

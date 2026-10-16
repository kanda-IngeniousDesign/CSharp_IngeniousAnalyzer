using CSharp_IngeniousAnalyzer.Style__Common;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace CSharp_IngeniousAnalyzer.Style_Test;

// テストコード（XxxTests）がないクラスを検知する。既存のプロジェクトでは大量に出るため、既定では無効にしている。
// アナライザーは自分のプロジェクトしか見られないため、テストプロジェクトのファイル一覧は
// パッケージ同梱の build/CSharp_IngeniousAnalyzer.targets が AdditionalFiles として渡す。
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public class MissingTest : CommonAnalyzer
{
    public const string DiagnosticId = "TEST001";
    private const string Category = "Test";
    private const string TestClassSuffix = "Tests";

    /// <summary>テストプロジェクトのファイルであることを示す AdditionalFiles のメタデータ（targets で付与）</summary>
    public const string TestFileMetadataKey = "build_metadata.AdditionalFiles.IngeniousTestFile";

    /// <summary>テストプロジェクトのフォルダ（targets で既定値を設定し、csproj で上書きできる）</summary>
    public const string TestProjectDirectoryKey = "build_property.IngeniousTestProjectDirectory";

    // テストフレームワークを参照しているプロジェクトはテストプロジェクトとみなし、検知の対象外にする
    private static readonly string[] TestFrameworkAssemblies = ["xunit.core", "xunit.v3.core", "nunit.framework", "Microsoft.VisualStudio.TestPlatform.TestFramework"];

    private static readonly LocalizableString Title = CreateLocalStr(nameof(ResourceEnum.TEST001_Title));
    private static readonly LocalizableString MessageFormat = CreateLocalStr(nameof(ResourceEnum.TEST001_Message));

    protected override DiagnosticDescriptor Rule { get; } = new(
        DiagnosticId, Title, MessageFormat, Category, DiagnosticSeverity.Warning, isEnabledByDefault: false);

    protected override SyntaxKind[] TargetKinds => [SyntaxKind.ClassDeclaration];

    protected override void AnalyzeNode(SyntaxNodeAnalysisContext context)
    {
        if (IsGeneratedFile(context)) return;
        var classDecl = (ClassDeclarationSyntax)context.Node;

        // 入れ子のクラスは外側のクラスのテストで扱い、抽象クラスは具象クラスのテストで扱う
        if (classDecl.Parent is TypeDeclarationSyntax) return;
        if (classDecl.Modifiers.Any(SyntaxKind.AbstractKeyword)) return;
        if (classDecl.HasIgnoreComment(DiagnosticId)) return;
        if (IsTestProject(context.Compilation)) return;

        if (context.SemanticModel.GetDeclaredSymbol(classDecl, context.CancellationToken) is not { } symbol) return;

        // partial クラスは最初の宣言にだけ報告する
        var first = symbol.DeclaringSyntaxReferences.FirstOrDefault();
        if (first is null || first.SyntaxTree != classDecl.SyntaxTree || first.Span != classDecl.Span) return;

        if (!HasTestableMembers(symbol)) return;
        if (HasTestFile(context.Options, symbol.Name + TestClassSuffix)) return;

        context.ReportDiagnostic(Diagnostic.Create(Rule, classDecl.Identifier.GetLocation(), symbol.Name));
    }

    /// <summary>
    /// テストフレームワークを参照している（＝テストプロジェクト自身の）コンパイルかを判定する
    /// </summary>
    private static bool IsTestProject(Compilation compilation)
    {
        return compilation.ReferencedAssemblyNames.Any(a => TestFrameworkAssemblies.Contains(a.Name, StringComparer.OrdinalIgnoreCase));
    }

    /// <summary>
    /// テストする対象（public なメソッド、または引数を持つ public コンストラクタ）があるかを判定する。
    /// プロパティだけのクラス（DTOなど）はテストを求めない
    /// </summary>
    private static bool HasTestableMembers(INamedTypeSymbol symbol)
    {
        return symbol.GetMembers().OfType<IMethodSymbol>().Any(m =>
            m.DeclaredAccessibility == Accessibility.Public
            && !m.IsImplicitlyDeclared
            && (m.MethodKind == MethodKind.Ordinary || (m.MethodKind == MethodKind.Constructor && 0 < m.Parameters.Length)));
    }

    /// <summary>
    /// テストプロジェクトに、指定の名前のテストファイル（XxxTests.cs）があるかを判定する
    /// </summary>
    private static bool HasTestFile(AnalyzerOptions options, string testClassName)
    {
        foreach (var file in options.AdditionalFiles)
        {
            var fileOptions = options.AnalyzerConfigOptionsProvider.GetOptions(file);
            if (!fileOptions.TryGetValue(TestFileMetadataKey, out var isTestFile)) continue;
            if (!string.Equals(isTestFile, "true", StringComparison.OrdinalIgnoreCase)) continue;

            if (string.Equals(Path.GetFileNameWithoutExtension(file.Path), testClassName, StringComparison.Ordinal)) return true;
        }

        return false;
    }
}

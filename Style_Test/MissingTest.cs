using System.Collections.Immutable;
using CSharp_IngeniousAnalyzer.Style__Common;
using IngeniousXUnitGenerator.Core;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace CSharp_IngeniousAnalyzer.Style_Test;

// テストコードの不足を検知する。既存のプロジェクトでは大量に出るため、既定では無効にしている。
// ・TEST001: 自動で生成できるテストが不足している。テストファイルがなければクラス名に、あればテストのない関数名に出し、Fix で生成する
// ・TEST002: テストがなく、自動では生成できない関数（理由を表示する）。別の方法で確認したうえで Ignore してもらう
// 判定は Core の TestCoverageInspector（実際の生成と同じ計画の処理）で行い、警告と Fix の結果が食い違わないようにする。
// アナライザーは自分のプロジェクトしか見られないため、テストプロジェクトのテストファイルと csproj は
// パッケージ同梱の build/CSharp_IngeniousAnalyzer.targets が AdditionalFiles として渡す。
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public class MissingTest : CommonAnalyzer
{
    public const string DiagnosticId = "TEST001";
    public const string NotGeneratableDiagnosticId = "TEST002";
    private const string Category = "Test";
    private const string TestClassSuffix = "Tests";

    /// <summary>テストプロジェクトのファイルであることを示す AdditionalFiles のメタデータ（targets で付与）</summary>
    public const string TestFileMetadataKey = "build_metadata.AdditionalFiles.IngeniousTestFile";

    /// <summary>テストプロジェクトの csproj であることを示す AdditionalFiles のメタデータ（targets で付与）</summary>
    public const string TestProjectFileMetadataKey = "build_metadata.AdditionalFiles.IngeniousTestProjectFile";

    /// <summary>テストプロジェクトのフォルダ（targets で既定値を設定し、csproj で上書きできる）</summary>
    public const string TestProjectDirectoryKey = "build_property.IngeniousTestProjectDirectory";

    // テストフレームワークを参照しているプロジェクトはテストプロジェクトとみなし、検知の対象外にする
    private static readonly string[] TestFrameworkAssemblies = ["xunit.core", "xunit.v3.core", "nunit.framework", "Microsoft.VisualStudio.TestPlatform.TestFramework"];

    private static readonly LocalizableString Title = CreateLocalStr(nameof(ResourceEnum.TEST001_Title));
    private static readonly LocalizableString MessageFormat = CreateLocalStr(nameof(ResourceEnum.TEST001_Message));
    private static readonly LocalizableString MemberMessageFormat = CreateLocalStr(nameof(ResourceEnum.TEST001_MemberMessage));
    private static readonly LocalizableString NotGeneratableTitle = CreateLocalStr(nameof(ResourceEnum.TEST002_Title));
    private static readonly LocalizableString NotGeneratableMessageFormat = CreateLocalStr(nameof(ResourceEnum.TEST002_Message));

    /// <summary>TEST001 のうち、テストファイルがないクラス</summary>
    public static DiagnosticDescriptor ClassRule { get; } = new(
        DiagnosticId, Title, MessageFormat, Category, DiagnosticSeverity.Warning, isEnabledByDefault: false);

    protected override DiagnosticDescriptor Rule => ClassRule;

    /// <summary>TEST001 のうち、テストファイルはあるが、関数のテストが不足しているもの</summary>
    public static DiagnosticDescriptor MemberRule { get; } = new(
        DiagnosticId, Title, MemberMessageFormat, Category, DiagnosticSeverity.Warning, isEnabledByDefault: false);

    /// <summary>TEST002: テストがなく、自動では生成できない関数</summary>
    public static DiagnosticDescriptor NotGeneratableRule { get; } = new(
        NotGeneratableDiagnosticId, NotGeneratableTitle, NotGeneratableMessageFormat, Category, DiagnosticSeverity.Warning, isEnabledByDefault: false);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => [Rule, MemberRule, NotGeneratableRule];

    protected override SyntaxKind[] TargetKinds => [SyntaxKind.ClassDeclaration];

    protected override void AnalyzeNode(SyntaxNodeAnalysisContext context)
    {
        if (IsGeneratedFile(context)) return;
        var classDecl = (ClassDeclarationSyntax)context.Node;

        // 入れ子のクラスは外側のクラスのテストで扱い、抽象クラスは具象クラスのテストで扱う
        if (classDecl.Parent is TypeDeclarationSyntax) return;
        if (classDecl.Modifiers.Any(SyntaxKind.AbstractKeyword)) return;
        if (IsTestProject(context.Compilation)) return;

        // 判定には生成と同じ計画の処理を使い重いため、どちらのルールも有効でなければ何もしない
        var tree = classDecl.SyntaxTree;
        var reportsMissing = IsEnabled(context.Compilation.Options, tree, DiagnosticId, context.CancellationToken) && !classDecl.HasIgnoreComment(DiagnosticId);
        var reportsNotGeneratable = IsEnabled(context.Compilation.Options, tree, NotGeneratableDiagnosticId, context.CancellationToken)
            && !classDecl.HasIgnoreComment(NotGeneratableDiagnosticId);
        if (!reportsMissing && !reportsNotGeneratable) return;

        if (context.SemanticModel.GetDeclaredSymbol(classDecl, context.CancellationToken) is not { } symbol) return;

        var testClassName = symbol.Name + TestClassSuffix;
        var testFile = FindAdditionalFile(context.Options, TestFileMetadataKey, f => Path.GetFileNameWithoutExtension(f.Path) == testClassName);
        var testFileText = testFile?.GetText(context.CancellationToken)?.ToString();
        var existingTestNames = testFileText is null ? [] : TestCoverageInspector.CollectTestNames(testFileText, testClassName);
        var coverages = TestCoverageInspector.Inspect(symbol, context.Compilation, GetGenerationOptions(context), existingTestNames);

        // テストファイルがなければクラス名に1つだけ出す（partial クラスは最初の宣言にだけ報告する）
        var first = symbol.DeclaringSyntaxReferences.FirstOrDefault();
        var isFirstDeclaration = first is not null && first.SyntaxTree == tree && first.Span == classDecl.Span;
        if (reportsMissing && testFileText is null && isFirstDeclaration && coverages.Any(c => c.Status == CoverageStatus.Generatable))
        {
            context.ReportDiagnostic(Diagnostic.Create(Rule, classDecl.Identifier.GetLocation(), symbol.Name));
        }

        foreach (var coverage in coverages)
        {
            ReportMember(context, classDecl, coverage, reportsMissing && testFileText is not null, reportsNotGeneratable);
        }
    }

    /// <summary>
    /// このクラス宣言の中にある関数について、テストの不足（TEST001）・自動生成できないこと（TEST002）を関数名に報告する
    /// </summary>
    private static void ReportMember(
        SyntaxNodeAnalysisContext context, ClassDeclarationSyntax classDecl, MemberCoverage coverage, bool reportsMissing, bool reportsNotGeneratable)
    {
        var declaration = FindMemberDeclaration(coverage.Member, classDecl);
        if (declaration is null) return;

        var (node, identifier) = declaration.Value;
        var name = coverage.Member.MethodKind == MethodKind.Constructor ? coverage.Member.ContainingType.Name : identifier.ValueText;
        if (coverage.Status == CoverageStatus.Generatable && reportsMissing && !node.HasIgnoreComment(DiagnosticId))
        {
            context.ReportDiagnostic(Diagnostic.Create(MemberRule, identifier.GetLocation(), name, coverage.Member.ContainingType.Name + TestClassSuffix));
        }
        else if (coverage.Status == CoverageStatus.NotGeneratable && reportsNotGeneratable && !node.HasIgnoreComment(NotGeneratableDiagnosticId))
        {
            context.ReportDiagnostic(Diagnostic.Create(NotGeneratableRule, identifier.GetLocation(), name, string.Join(" / ", coverage.Reasons)));
        }
    }

    /// <summary>
    /// 関数の宣言（メソッド・コンストラクタ、set アクセサーはプロパティ）と名前のトークンを、このクラス宣言の中から探す
    /// </summary>
    public static (MemberDeclarationSyntax Node, SyntaxToken Identifier)? FindMemberDeclaration(IMethodSymbol member, ClassDeclarationSyntax classDecl)
    {
        foreach (var reference in member.DeclaringSyntaxReferences)
        {
            if (reference.SyntaxTree != classDecl.SyntaxTree || !classDecl.Span.Contains(reference.Span)) continue;

            switch (reference.GetSyntax())
            {
                case MethodDeclarationSyntax method:
                    return (method, method.Identifier);
                case ConstructorDeclarationSyntax constructor:
                    return (constructor, constructor.Identifier);
                case AccessorDeclarationSyntax { Parent.Parent: PropertyDeclarationSyntax property }:
                    return (property, property.Identifier);
            }
        }

        return null;
    }

    /// <summary>
    /// 名前のトークンが指す関数（メソッド・コンストラクタ・プロパティの set アクセサー）のシンボルを返す（Fix で使う）
    /// </summary>
    public static IMethodSymbol? GetMemberSymbol(SemanticModel model, SyntaxNode declaration, CancellationToken cancellationToken)
    {
        return model.GetDeclaredSymbol(declaration, cancellationToken) switch
        {
            IMethodSymbol method => method,
            IPropertySymbol property => property.SetMethod,
            _ => null,
        };
    }

    /// <summary>
    /// ルールが有効か（.editorconfig・globalconfig・プロジェクトの設定で、重大度を none 以外にしているか）を判定する
    /// </summary>
    public static bool IsEnabled(CompilationOptions options, SyntaxTree tree, string diagnosticId, CancellationToken cancellationToken)
    {
        var provider = options.SyntaxTreeOptionsProvider;
        if (provider is not null)
        {
            if (provider.TryGetDiagnosticValue(tree, diagnosticId, cancellationToken, out var severity)) return IsOn(severity);
            if (provider.TryGetGlobalDiagnosticValue(diagnosticId, cancellationToken, out severity)) return IsOn(severity);
        }

        return options.SpecificDiagnosticOptions.TryGetValue(diagnosticId, out var specific) && IsOn(specific);
    }

    /// <summary>
    /// 重大度の設定が「報告する」かを判定する（Default は既定の無効のまま）
    /// </summary>
    private static bool IsOn(ReportDiagnostic severity)
    {
        return severity is not (ReportDiagnostic.Suppress or ReportDiagnostic.Default);
    }

    /// <summary>
    /// テストフレームワークを参照している（＝テストプロジェクト自身の）コンパイルかを判定する
    /// </summary>
    private static bool IsTestProject(Compilation compilation)
    {
        return compilation.ReferencedAssemblyNames.Any(a => TestFrameworkAssemblies.Contains(a.Name, StringComparer.OrdinalIgnoreCase));
    }

    /// <summary>
    /// テストプロジェクトで使えるパッケージ（Moq・Verify）を csproj から判定する。
    /// テストプロジェクトがまだなければ、Fix で作成するテストプロジェクトと同じく両方を使える前提にする
    /// </summary>
    private static GenerationOptions GetGenerationOptions(SyntaxNodeAnalysisContext context)
    {
        var projectFile = FindAdditionalFile(context.Options, TestProjectFileMetadataKey, _ => true);
        var text = projectFile?.GetText(context.CancellationToken)?.ToString();
        return text is null ? new GenerationOptions(useMoq: true, useVerify: true) : GenerationOptions.FromProjectFile(text);
    }

    /// <summary>
    /// targets が渡した AdditionalFiles のうち、指定のメタデータが付いていて条件に合うものを探す
    /// </summary>
    private static AdditionalText? FindAdditionalFile(AnalyzerOptions options, string metadataKey, Func<AdditionalText, bool> predicate)
    {
        foreach (var file in options.AdditionalFiles)
        {
            var fileOptions = options.AnalyzerConfigOptionsProvider.GetOptions(file);
            if (!fileOptions.TryGetValue(metadataKey, out var value)) continue;
            if (!string.Equals(value, "true", StringComparison.OrdinalIgnoreCase)) continue;

            if (predicate(file)) return file;
        }

        return null;
    }
}

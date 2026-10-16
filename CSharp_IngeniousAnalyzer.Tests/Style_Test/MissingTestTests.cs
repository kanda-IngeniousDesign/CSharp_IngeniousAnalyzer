using CSharp_IngeniousAnalyzer.Style_Test;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
using Microsoft.CodeAnalysis.Testing.Verifiers;

namespace CSharp_IngeniousAnalyzer.Tests.Style_Test;

using Verify = CSharpAnalyzerVerifier<MissingTest, XUnitVerifier>;
using CodeFixVerify = CSharpCodeFixVerifier<MissingTest, MissingTestFix, XUnitVerifier>;

/// <summary>
/// TEST001（MissingTest）の検知と、Ignore コメントを挿入する Fix を検証するテスト
/// </summary>
public class MissingTestTests
{
    private const string TestFilePath = "/0/Product.Tests/CalculatorTests.cs";

    // 既定では無効のため、テストでは globalconfig で有効にする。テストプロジェクトのファイルには targets と同じメタデータを付ける
    private const string EnableRule = """
        is_global = true
        dotnet_diagnostic.TEST001.severity = warning
        """;

    private const string EnableRuleWithTestFile = EnableRule + """


        [/0/Product.Tests/CalculatorTests.cs]
        build_metadata.AdditionalFiles.IngeniousTestFile = true
        """;

    /// <summary>
    /// public なメソッドを持つクラスにテストファイルがなければ、クラス名に診断が出ることを確認する
    /// </summary>
    [Fact]
    public async Task ClassWithoutTestFile_ReportsDiagnostic()
    {
        var test = CreateTest("""
            public class {|#0:Calculator|}
            {
                public int Add(int a, int b) => a + b;
            }
            """, EnableRule);
        test.ExpectedDiagnostics.Add(Verify.Diagnostic().WithLocation(0).WithArguments("Calculator"));

        await test.RunAsync();
    }

    /// <summary>
    /// テストプロジェクトに XxxTests.cs があれば、診断が出ないことを確認する
    /// </summary>
    [Fact]
    public async Task ClassWithTestFile_DoesNotReportDiagnostic()
    {
        var test = CreateTest("""
            public class Calculator
            {
                public int Add(int a, int b) => a + b;
            }
            """, EnableRuleWithTestFile);
        test.TestState.AdditionalFiles.Add((TestFilePath, string.Empty));

        await test.RunAsync();
    }

    /// <summary>
    /// 同名のファイルでも、テストプロジェクトのファイル（メタデータ付き）でなければテストとみなさないことを確認する
    /// </summary>
    [Fact]
    public async Task SameFileNameOutsideTestProject_ReportsDiagnostic()
    {
        var test = CreateTest("""
            public class {|#0:Calculator|}
            {
                public int Add(int a, int b) => a + b;
            }
            """, EnableRule);
        test.TestState.AdditionalFiles.Add((TestFilePath, string.Empty));
        test.ExpectedDiagnostics.Add(Verify.Diagnostic().WithLocation(0).WithArguments("Calculator"));

        await test.RunAsync();
    }

    /// <summary>
    /// 既存のプロジェクトで大量に出ないよう、既定では無効（.editorconfig で有効にする）であることを確認する
    /// （テストライブラリは既定で無効なルールも有効にして実行するため、ルールの設定値で確認する）
    /// </summary>
    [Fact]
    public void Rule_IsDisabledByDefault()
    {
        var descriptor = new MissingTest().SupportedDiagnostics.Single();

        Assert.False(descriptor.IsEnabledByDefault);
        Assert.Equal(DiagnosticSeverity.Warning, descriptor.DefaultSeverity);
    }

    /// <summary>
    /// テストを求めないクラス（プロパティだけ・抽象・入れ子・Ignore コメント付き・引数なしコンストラクタだけ）には診断が出ないことを確認する
    /// </summary>
    [Fact]
    public async Task ClassesWithoutTestableMembersOrIgnored_DoNotReportDiagnostic()
    {
        var test = CreateTest("""
            public class Dto
            {
                public Dto() { }

                public int Id { get; set; }
            }

            public abstract class Base
            {
                public abstract int Run();
            }

            public class Outer
            {
                internal Outer() { }

                public class Inner
                {
                    public void Run() { }
                }
            }

            // Ignore TEST001
            public class Legacy
            {
                public void Run() { }
            }
            """, EnableRule);

        await test.RunAsync();
    }

    /// <summary>
    /// static クラスや、引数を持つ public コンストラクタだけのクラスは対象になることを確認する
    /// </summary>
    [Fact]
    public async Task StaticClassAndConstructorWithParameters_ReportDiagnostics()
    {
        var test = CreateTest("""
            public static class {|#0:MathHelper|}
            {
                public static int Twice(int value) => value * 2;
            }

            public class {|#1:Greeter|}
            {
                private readonly string _prefix;

                public Greeter(string prefix) { _prefix = prefix; }
            }
            """, EnableRule);
        test.ExpectedDiagnostics.Add(Verify.Diagnostic().WithLocation(0).WithArguments("MathHelper"));
        test.ExpectedDiagnostics.Add(Verify.Diagnostic().WithLocation(1).WithArguments("Greeter"));

        await test.RunAsync();
    }

    /// <summary>
    /// partial クラスは、最初の宣言にだけ診断が出ることを確認する
    /// </summary>
    [Fact]
    public async Task PartialClass_ReportsOnlyOnFirstDeclaration()
    {
        var test = CreateTest("""
            public partial class {|#0:Calculator|}
            {
                public int Add(int a, int b) => a + b;
            }

            public partial class Calculator
            {
                public int Sub(int a, int b) => a - b;
            }
            """, EnableRule);
        test.ExpectedDiagnostics.Add(Verify.Diagnostic().WithLocation(0).WithArguments("Calculator"));

        await test.RunAsync();
    }

    /// <summary>
    /// テストフレームワークを参照しているプロジェクト（テストプロジェクト自身）では診断が出ないことを確認する
    /// </summary>
    [Fact]
    public async Task TestProject_DoesNotReportDiagnostic()
    {
        var test = CreateTest("""
            public class CalculatorTests
            {
                public void Add_Works() { }
            }
            """, EnableRule);
        test.TestState.AdditionalReferences.Add(MetadataReference.CreateFromFile(typeof(FactAttribute).Assembly.Location));

        await test.RunAsync();
    }

    /// <summary>
    /// Ignore の Fix で、クラスの直前（ドキュメントコメントの下）に「// Ignore TEST001」が挿入されることを確認する
    /// </summary>
    [Fact]
    public async Task IgnoreFix_InsertsCommentBeforeClass()
    {
        var test = new CSharpCodeFixTest<MissingTest, MissingTestFix, XUnitVerifier>
        {
            TestCode = """
                namespace Product;

                /// <summary>計算</summary>
                public class {|#0:Calculator|}
                {
                    public int Add(int a, int b) => a + b;
                }
                """,
            FixedCode = """
                namespace Product;

                /// <summary>計算</summary>
                // Ignore TEST001
                public class Calculator
                {
                    public int Add(int a, int b) => a + b;
                }
                """,
        };
        test.TestState.AnalyzerConfigFiles.Add(("/.globalconfig", EnableRule));
        test.ExpectedDiagnostics.Add(CodeFixVerify.Diagnostic().WithLocation(0).WithArguments("Calculator"));

        await test.RunAsync();
    }

    private static CSharpAnalyzerTest<MissingTest, XUnitVerifier> CreateTest(string source, string globalConfig)
    {
        var test = new CSharpAnalyzerTest<MissingTest, XUnitVerifier> { TestCode = source };
        test.TestState.AnalyzerConfigFiles.Add(("/.globalconfig", globalConfig));
        return test;
    }
}

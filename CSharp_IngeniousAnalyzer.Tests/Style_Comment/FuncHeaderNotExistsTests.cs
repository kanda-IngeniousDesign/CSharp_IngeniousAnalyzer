using CSharp_IngeniousAnalyzer.Style_Comment;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
using Microsoft.CodeAnalysis.Testing.Verifiers;

namespace CSharp_IngeniousAnalyzer.Tests.Style_Comment;

using Verify = CSharpAnalyzerVerifier<FuncHeaderNotExists, XUnitVerifier>;
using CodeFixVerify = CSharpCodeFixVerifier<FuncHeaderNotExists, FuncHeaderNotExistsFix, XUnitVerifier>;

/// <summary>
/// COMM001（FuncHeaderNotExists）の検知・Fix動作を検証するテスト
/// </summary>
public class FuncHeaderNotExistsTests
{
    /// <summary>
    /// 通常コメント（'///' ではない）は「コメントなし」として扱われ、診断が出ることを確認する
    /// </summary>
    [Fact]
    public async Task PlainCommentNotXmlDoc_ReportsDiagnostic()
    {
        var test = """
            public class C
            {
                // これは通常コメント
                void {|#0:M|}() { }
            }
            """;

        await Verify.VerifyAnalyzerAsync(test, Verify.Diagnostic().WithLocation(0));
    }

    /// <summary>
    /// <summary>タグの中身が空でも「存在」だけで許容され、診断が出ないことを確認する
    /// </summary>
    [Fact]
    public async Task EmptySummaryContent_DoesNotReportDiagnostic()
    {
        var test = """
            public class C
            {
                /// <summary>
                ///
                /// </summary>
                void M() { }
            }
            """;

        await Verify.VerifyAnalyzerAsync(test);
    }

    /// <summary>
    /// <summary>タグ自体が存在しない（<param>のみ）場合、診断が出ることを確認する
    /// </summary>
    [Fact]
    public async Task MissingSummaryTag_ReportsDiagnostic()
    {
        var test = """
            public class C
            {
                /// <param name="value"></param>
                void {|#0:M|}(int value) { }
            }
            """;

        await Verify.VerifyAnalyzerAsync(test, Verify.Diagnostic().WithLocation(0));
    }

    /// <summary>
    /// タグ名を大文字（&lt;SUMMARY&gt;）で書いた場合、"summary"と一致せず診断が出ることを確認する
    /// </summary>
    [Fact]
    public async Task UppercaseSummaryTag_ReportsDiagnostic()
    {
        var test = """
            public class C
            {
                /// <SUMMARY>
                /// text
                /// </SUMMARY>
                void {|#0:M|}() { }
            }
            """;

        await Verify.VerifyAnalyzerAsync(test, Verify.Diagnostic().WithLocation(0));
    }

    /// <summary>
    /// abstractメソッドは実装を持たないため、コメントがなくても対象外になることを確認する
    /// </summary>
    [Fact]
    public async Task AbstractMethod_DoesNotReportDiagnostic()
    {
        var test = """
            public abstract class C
            {
                public abstract void M();
            }
            """;

        await Verify.VerifyAnalyzerAsync(test);
    }

    /// <summary>
    /// Fixがコメントのないメソッドに対して、summaryタグとparamタグを含むヘッダーを新規作成することを確認する
    /// </summary>
    [Fact]
    public async Task Fix_CreatesHeaderWithSummaryAndParams()
    {
        var test = """
            public class C
            {
                void {|#0:M|}(int value)
                {
                }
            }
            """;

        var fixedSource = """
            public class C
            {
                /// <summary>
                /// 
                /// </summary>
                /// <param name="value"></param>
                void M(int value)
                {
                }
            }
            """;

        await CodeFixVerify.VerifyCodeFixAsync(test, CodeFixVerify.Diagnostic().WithLocation(0), fixedSource);
    }

    /// <summary>
    /// GenerateDocumentationFile が無効な状況を再現するため、"///" 等が構造化トリビアではなく
    /// 生テキストとして扱われるよう DocumentationMode.None を設定したテストを組み立てる
    /// （DocCommentScanner の生テキストフォールバック経路を検証するための共通ヘルパー）
    /// </summary>
    private static CSharpAnalyzerTest<FuncHeaderNotExists, XUnitVerifier> CreateRawFallbackTest(string source, params DiagnosticResult[] expected)
    {
        var test = new CSharpAnalyzerTest<FuncHeaderNotExists, XUnitVerifier> { TestCode = source };
        test.ExpectedDiagnostics.AddRange(expected);
        test.SolutionTransforms.Add((solution, projectId) =>
        {
            var project = solution.GetProject(projectId)!;
            var parseOptions = (CSharpParseOptions)project.ParseOptions!;
            return solution.WithProjectParseOptions(projectId, parseOptions.WithDocumentationMode(DocumentationMode.None));
        });
        return test;
    }

    /// <summary>
    /// GenerateDocumentationFile無効時（生テキストフォールバック）でも、単一の"///"ブロックに
    /// <summary>タグが含まれていれば診断が出ないことを確認する
    /// </summary>
    [Fact]
    public async Task RawFallback_SingleBlockWithSummary_DoesNotReportDiagnostic()
    {
        var test = """
            public class C
            {
                /// <summary>
                /// text
                /// </summary>
                void M() { }
            }
            """;

        await CreateRawFallbackTest(test).RunAsync();
    }

    /// <summary>
    /// GenerateDocumentationFile無効時（生テキストフォールバック）で、単一の"///"ブロックに
    /// <summary>タグが含まれていなければ診断が出ることを確認する
    /// </summary>
    [Fact]
    public async Task RawFallback_SingleBlockWithoutSummary_ReportsDiagnostic()
    {
        var test = """
            public class C
            {
                /// <param name="value"></param>
                void {|#0:M|}(int value) { }
            }
            """;

        await CreateRawFallbackTest(test, Verify.Diagnostic().WithLocation(0)).RunAsync();
    }

    /// <summary>
    /// 完結した"///"ブロックの直後（メソッドとの間）に通常コメントが割り込んでいても、
    /// そのブロック自身が持つ<summary>が誤って失われず診断が出ないことを確認する
    /// （「末尾の通常コメントによってブロックが分断される」false positiveの回帰テスト）
    /// </summary>
    [Fact]
    public async Task RawFallback_TrailingCommentAfterCompleteBlock_DoesNotReportDiagnostic()
    {
        var test = """
            public class C
            {
                /// <summary>
                /// text
                /// </summary>
                // 補足説明（ドキュメントコメント直後の通常コメント）
                void M() { }
            }
            """;

        await CreateRawFallbackTest(test).RunAsync();
    }

    /// <summary>
    /// 通常コメントで分断された2つの"///"ブロックのうち、メソッドに一番近い側が<summary>を
    /// 持たない断片（paramのみ）の場合、それより前のブロックと結合されて<summary>が
    /// 見つかり、診断が出ないことを確認する
    /// （「孤立したparam断片が新しいブロックと結合されない」false negativeの回帰テスト）
    /// </summary>
    [Fact]
    public async Task RawFallback_OrphanedFragmentMergesWithEarlierSummary_DoesNotReportDiagnostic()
    {
        var test = """
            public class C
            {
                /// <summary>
                /// old description
                /// </summary>
                // TODO: 古いparamタグの記述を整理する
                /// <param name="value"></param>
                void M(int value) { }
            }
            """;

        await CreateRawFallbackTest(test).RunAsync();
    }

    /// <summary>
    /// GenerateDocumentationFile無効時（生テキストフォールバック）で、"/** */"形式の
    /// ブロックコメントに<summary>タグが含まれていれば診断が出ないことを確認する
    /// </summary>
    [Fact]
    public async Task RawFallback_BlockStyleCommentWithSummary_DoesNotReportDiagnostic()
    {
        var test = """
            public class C
            {
                /**
                 * <summary>text</summary>
                 */
                void M() { }
            }
            """;

        await CreateRawFallbackTest(test).RunAsync();
    }

    /// <summary>
    /// <summary>タグが最上位ではなく別タグ（<remarks>）の内側にネストしていても、
    /// 再帰的に検出されて診断が出ないことを確認する
    /// </summary>
    [Fact]
    public async Task NestedSummaryInsideOtherElement_DoesNotReportDiagnostic()
    {
        var test = """
            public class C
            {
                /// <remarks>
                /// <summary>text</summary>
                /// </remarks>
                void M() { }
            }
            """;

        await Verify.VerifyAnalyzerAsync(test);
    }

    /// <summary>
    /// ネストしたタグの中に<summary>ではない要素（<para>）しかない場合、再帰探索を
    /// 行っても<summary>は見つからず診断が出ることを確認する
    /// </summary>
    [Fact]
    public async Task NestedNonSummaryElements_ReportsDiagnostic()
    {
        var test = """
            public class C
            {
                /// <remarks>
                /// <para>text</para>
                /// </remarks>
                void {|#0:M|}() { }
            }
            """;

        await Verify.VerifyAnalyzerAsync(test, Verify.Diagnostic().WithLocation(0));
    }
}

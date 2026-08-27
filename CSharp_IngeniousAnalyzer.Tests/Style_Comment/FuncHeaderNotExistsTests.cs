using CSharp_IngeniousAnalyzer.Style_Comment;
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
}

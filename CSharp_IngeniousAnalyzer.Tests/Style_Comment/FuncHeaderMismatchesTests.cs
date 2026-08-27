using CSharp_IngeniousAnalyzer.Style_Comment;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
using Microsoft.CodeAnalysis.Testing.Verifiers;

namespace CSharp_IngeniousAnalyzer.Tests.Style_Comment;

using Verify = CSharpAnalyzerVerifier<FuncHeaderMismatches, XUnitVerifier>;
using CodeFixVerify = CSharpCodeFixVerifier<FuncHeaderMismatches, FuncHeaderMismatchesFix, XUnitVerifier>;

/// <summary>
/// COMM002（FuncHeaderMismatches）の検知・Fix動作を検証するテスト
/// </summary>
public class FuncHeaderMismatchesTests
{
    /// <summary>
    /// 実引数名とドキュメントのparam名が不一致の場合、診断が出ることを確認する
    /// </summary>
    [Fact]
    public async Task MismatchedParamName_ReportsDiagnostic()
    {
        var test = """
            public class C
            {
                /// <summary>
                /// text
                /// </summary>
                /// <param name="wrongName"></param>
                void {|#0:M|}(int actualName) { }
            }
            """;

        await Verify.VerifyAnalyzerAsync(test, Verify.Diagnostic().WithLocation(0).WithArguments("actualName, wrongName"));
    }

    /// <summary>
    /// 実引数は2つあるが、ドキュメントのparamタグが1つ欠落している場合、診断が出ることを確認する
    /// </summary>
    [Fact]
    public async Task MissingParamTag_ReportsDiagnostic()
    {
        var test = """
            public class C
            {
                /// <summary>
                /// text
                /// </summary>
                /// <param name="first"></param>
                void {|#0:M|}(int first, int second) { }
            }
            """;

        await Verify.VerifyAnalyzerAsync(test, Verify.Diagnostic().WithLocation(0).WithArguments("second"));
    }

    /// <summary>
    /// ドキュメントに余分なparamタグ（extra）がある場合、診断が出ることを確認する
    /// </summary>
    [Fact]
    public async Task ExtraParamTag_ReportsDiagnostic()
    {
        var test = """
            public class C
            {
                /// <summary>
                /// text
                /// </summary>
                /// <param name="first"></param>
                /// <param name="extra"></param>
                void {|#0:M|}(int first) { }
            }
            """;

        await Verify.VerifyAnalyzerAsync(test, Verify.Diagnostic().WithLocation(0).WithArguments("extra"));
    }

    /// <summary>
    /// パラメータ名・順序ともに完全一致している場合、診断が出ないことを確認する
    /// </summary>
    [Fact]
    public async Task MatchedParams_DoesNotReportDiagnostic()
    {
        var test = """
            public class C
            {
                /// <summary>
                /// text
                /// </summary>
                /// <param name="first"></param>
                /// <param name="second"></param>
                void M(int first, int second) { }
            }
            """;

        await Verify.VerifyAnalyzerAsync(test);
    }

    /// <summary>
    /// 名前は両方とも存在するが、順序だけが入れ替わっている場合、診断が出ることを確認する
    /// </summary>
    [Fact]
    public async Task SwappedParamOrder_ReportsDiagnostic()
    {
        var test = """
            public class C
            {
                /// <summary>
                /// text
                /// </summary>
                /// <param name="second"></param>
                /// <param name="first"></param>
                void {|#0:M|}(int first, int second) { }
            }
            """;

        // 順序違反のみの場合、missing/extraどちらの集合にも入らないため、詳細は空文字列になる
        await Verify.VerifyAnalyzerAsync(test, Verify.Diagnostic().WithLocation(0).WithArguments(""));
    }

    /// <summary>
    /// Fixが既存のparamタグを削除し、メソッドの引数順に正しいparamタグを再生成することを確認する
    /// </summary>
    [Fact]
    public async Task Fix_SyncsParamsToMatchMethodSignature()
    {
        var test = """
            public class C
            {
                /// <summary>
                /// text
                /// </summary>
                /// <param name="first"></param>
                void {|#0:M|}(int first, int second)
                {
                }
            }
            """;

        var fixedSource = """
            public class C
            {
                /// <summary>
                /// text
                /// </summary>
                /// <param name="first"></param>
                /// <param name="second"></param>
                void M(int first, int second)
                {
                }
            }
            """;

        await CodeFixVerify.VerifyCodeFixAsync(test, CodeFixVerify.Diagnostic().WithLocation(0).WithArguments("second"), fixedSource);
    }
}

using CSharp_IngeniousAnalyzer.Style_Null;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
using Microsoft.CodeAnalysis.Testing.Verifiers;

namespace CSharp_IngeniousAnalyzer.Tests.Style_Null;

using Verify = CSharpAnalyzerVerifier<IsNull, XUnitVerifier>;
using CodeFixVerify = CSharpCodeFixVerifier<IsNull, IsNullFix, XUnitVerifier>;

/// <summary>
/// NULL001（IsNull）の検知・Fix動作を検証するテスト
/// </summary>
public class IsNullTests
{
    /// <summary>
    /// 基本パターン（== null）で診断が出ることを確認する
    /// </summary>
    [Fact]
    public async Task EqualsNull_ReportsDiagnostic()
    {
        var test = """
            public class C
            {
                void M(string s)
                {
                    if ({|#0:s == null|}) { }
                }
            }
            """;

        await Verify.VerifyAnalyzerAsync(test, Verify.Diagnostic().WithLocation(0).WithArguments("is null"));
    }

    /// <summary>
    /// 基本パターン（!= null）で診断が出ることを確認する
    /// </summary>
    [Fact]
    public async Task NotEqualsNull_ReportsDiagnostic()
    {
        var test = """
            public class C
            {
                void M(string s)
                {
                    if ({|#0:s != null|}) { }
                }
            }
            """;

        await Verify.VerifyAnalyzerAsync(test, Verify.Diagnostic().WithLocation(0).WithArguments("is null"));
    }

    /// <summary>
    /// ヨーダ記法（null == s）でも、null リテラルの位置に関わらず診断が出ることを確認する
    /// </summary>
    [Fact]
    public async Task YodaCondition_ReportsDiagnostic()
    {
        var test = """
            public class C
            {
                void M(string s)
                {
                    if ({|#0:null == s|}) { }
                }
            }
            """;

        await Verify.VerifyAnalyzerAsync(test, Verify.Diagnostic().WithLocation(0).WithArguments("is null"));
    }

    /// <summary>
    /// キャストを挟んだ比較（(object)s == null）でも診断が出ることを確認する
    /// </summary>
    [Fact]
    public async Task CastExpression_ReportsDiagnostic()
    {
        var test = """
            public class C
            {
                void M(string s)
                {
                    if ({|#0:(object)s == null|}) { }
                }
            }
            """;

        await Verify.VerifyAnalyzerAsync(test, Verify.Diagnostic().WithLocation(0).WithArguments("is null"));
    }

    /// <summary>
    /// プロパティチェーンの末尾がnull比較（s?.Length == null）でも診断が出ることを確認する
    /// </summary>
    [Fact]
    public async Task PropertyChainTail_ReportsDiagnostic()
    {
        var test = """
            public class C
            {
                void M(string s)
                {
                    if ({|#0:s?.Length == null|}) { }
                }
            }
            """;

        await Verify.VerifyAnalyzerAsync(test, Verify.Diagnostic().WithLocation(0).WithArguments("is null"));
    }

    /// <summary>
    /// .Equals(null) は == / != の構文ではないため、対象外（誤検知しない）ことを確認する
    /// </summary>
    [Fact]
    public async Task EqualsMethodCall_DoesNotReportDiagnostic()
    {
        var test = """
            public class C
            {
                void M(string s)
                {
                    if (s.Equals(null)) { }
                }
            }
            """;

        await Verify.VerifyAnalyzerAsync(test);
    }

    /// <summary>
    /// Fixが「== null」を「is null」パターンへ書き換えることを確認する
    /// </summary>
    [Fact]
    public async Task Fix_ReplacesEqualsNullWithIsNullPattern()
    {
        var test = """
            public class C
            {
                void M(string s)
                {
                    if ({|#0:s == null|}) { }
                }
            }
            """;

        var fixedSource = """
            public class C
            {
                void M(string s)
                {
                    if (s is null) { }
                }
            }
            """;

        await CodeFixVerify.VerifyCodeFixAsync(test, CodeFixVerify.Diagnostic().WithLocation(0).WithArguments("is null"), fixedSource);
    }

    /// <summary>
    /// Fixが「!= null」を「is not null」パターンへ書き換えることを確認する
    /// </summary>
    [Fact]
    public async Task Fix_ReplacesNotEqualsNullWithIsNotNullPattern()
    {
        var test = """
            public class C
            {
                void M(string s)
                {
                    if ({|#0:s != null|}) { }
                }
            }
            """;

        var fixedSource = """
            public class C
            {
                void M(string s)
                {
                    if (s is not null) { }
                }
            }
            """;

        await CodeFixVerify.VerifyCodeFixAsync(test, CodeFixVerify.Diagnostic().WithLocation(0).WithArguments("is null"), fixedSource);
    }

    /// <summary>
    /// Fix適用後も、式の直後にあるコメントが消えずに残ることを確認する（AnalyzerTestAppの意地悪ケースを移植）
    /// </summary>
    [Fact]
    public async Task Fix_PreservesTrailingComment()
    {
        var test = """
            public class C
            {
                void M(string s)
                {
                    if ({|#0:s == null|} /* keep me */ ) { }
                }
            }
            """;

        var fixedSource = """
            public class C
            {
                void M(string s)
                {
                    if (s is null /* keep me */ ) { }
                }
            }
            """;

        await CodeFixVerify.VerifyCodeFixAsync(test, CodeFixVerify.Diagnostic().WithLocation(0).WithArguments("is null"), fixedSource);
    }
}

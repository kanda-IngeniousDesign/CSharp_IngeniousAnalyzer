using CSharp_IngeniousAnalyzer.Style_Complexity;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
using Microsoft.CodeAnalysis.Testing.Verifiers;

namespace CSharp_IngeniousAnalyzer.Tests.Style_Complexity;

using Verify = CSharpAnalyzerVerifier<MethodComplexity, XUnitVerifier>;
using CodeFixVerify = CSharpCodeFixVerifier<MethodComplexity, MethodComplexityFix, XUnitVerifier>;

/// <summary>
/// CPX001（MethodComplexity）の検知・Fix動作を検証するテスト
/// </summary>
public class MethodComplexityTests
{
    /// <summary>
    /// 深くネストしたif文（複雑度31）により閾値(17)を超える場合、診断が出ることを確認する
    /// </summary>
    [Fact]
    public async Task DeeplyNestedIfStatements_ReportsDiagnostic()
    {
        var test = """
            public class C
            {
                void {|#0:M|}(bool a, bool b, bool c, bool d, bool e, bool f, bool g, bool h, bool i, bool j)
                {
                    if (a)
                    {
                        if (b)
                        {
                            if (c)
                            {
                                if (d)
                                {
                                    if (e)
                                    {
                                    }
                                }
                            }
                        }
                    }
                    if (f)
                    {
                        if (g)
                        {
                            if (h)
                            {
                                if (i)
                                {
                                    if (j)
                                    {
                                    }
                                }
                            }
                        }
                    }
                }
            }
            """;

        await Verify.VerifyAnalyzerAsync(test, Verify.Diagnostic().WithLocation(0).WithArguments("M", 31));
    }

    /// <summary>
    /// 同じ複雑な構造でも、メソッド本体の先頭に「// Ignore CPX001」コメントがあれば診断が出ないことを確認する
    /// </summary>
    [Fact]
    public async Task DeeplyNestedIfStatementsWithIgnoreComment_DoesNotReportDiagnostic()
    {
        var test = """
            public class C
            {
                void M(bool a, bool b, bool c, bool d, bool e, bool f, bool g, bool h, bool i, bool j)
                {
                    // Ignore CPX001
                    if (a)
                    {
                        if (b)
                        {
                            if (c)
                            {
                                if (d)
                                {
                                    if (e)
                                    {
                                    }
                                }
                            }
                        }
                    }
                    if (f)
                    {
                        if (g)
                        {
                            if (h)
                            {
                                if (i)
                                {
                                    if (j)
                                    {
                                    }
                                }
                            }
                        }
                    }
                }
            }
            """;

        await Verify.VerifyAnalyzerAsync(test);
    }

    /// <summary>
    /// Fixが複雑なメソッドの本体先頭に「// Ignore CPX001」コメントを挿入することを確認する
    /// </summary>
    [Fact]
    public async Task Fix_InsertsIgnoreCommentAsFirstLineInMethodBody()
    {
        var test = """
            public class C
            {
                void {|#0:M|}(bool a, bool b, bool c, bool d, bool e, bool f, bool g, bool h, bool i, bool j)
                {
                    if (a)
                    {
                        if (b)
                        {
                            if (c)
                            {
                                if (d)
                                {
                                    if (e)
                                    {
                                    }
                                }
                            }
                        }
                    }
                    if (f)
                    {
                        if (g)
                        {
                            if (h)
                            {
                                if (i)
                                {
                                    if (j)
                                    {
                                    }
                                }
                            }
                        }
                    }
                }
            }
            """;

        var fixedSource = """
            public class C
            {
                void M(bool a, bool b, bool c, bool d, bool e, bool f, bool g, bool h, bool i, bool j)
                {
                    // Ignore CPX001
                    if (a)
                    {
                        if (b)
                        {
                            if (c)
                            {
                                if (d)
                                {
                                    if (e)
                                    {
                                    }
                                }
                            }
                        }
                    }
                    if (f)
                    {
                        if (g)
                        {
                            if (h)
                            {
                                if (i)
                                {
                                    if (j)
                                    {
                                    }
                                }
                            }
                        }
                    }
                }
            }
            """;

        await CodeFixVerify.VerifyCodeFixAsync(test, CodeFixVerify.Diagnostic().WithLocation(0).WithArguments("M", 31), fixedSource);
    }
}

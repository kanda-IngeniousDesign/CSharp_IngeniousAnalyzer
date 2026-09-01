using CSharp_IngeniousAnalyzer.Style_Exception;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
using Microsoft.CodeAnalysis.Testing.Verifiers;

namespace CSharp_IngeniousAnalyzer.Tests.Style_Exception;

using Verify = CSharpAnalyzerVerifier<RethrowLosesStackTrace, XUnitVerifier>;
using CodeFixVerify = CSharpCodeFixVerifier<RethrowLosesStackTrace, RethrowLosesStackTraceFix, XUnitVerifier>;

/// <summary>
/// EXC002（RethrowLosesStackTrace）の検知・Fix動作を検証するテスト
/// </summary>
public class RethrowLosesStackTraceTests
{
    /// <summary>
    /// catch句の例外変数をそのまま throw する場合、診断が出ることを確認する
    /// </summary>
    [Fact]
    public async Task ThrowCaughtException_ReportsDiagnostic()
    {
        var test = """
            using System;

            public class C
            {
                void M()
                {
                    try
                    {
                        Console.WriteLine("A");
                    }
                    catch (Exception ex)
                    {
                        {|#0:throw ex;|}
                    }
                }
            }
            """;

        await Verify.VerifyAnalyzerAsync(test, Verify.Diagnostic().WithLocation(0).WithArguments("ex"));
    }

    /// <summary>
    /// 素の throw; （引数なし）は診断が出ないことを確認する
    /// </summary>
    [Fact]
    public async Task BareRethrow_DoesNotReportDiagnostic()
    {
        var test = """
            using System;

            public class C
            {
                void M()
                {
                    try
                    {
                        Console.WriteLine("B");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine(ex.Message);
                        throw;
                    }
                }
            }
            """;

        await Verify.VerifyAnalyzerAsync(test);
    }

    /// <summary>
    /// 捕捉した例外をラップして新しい例外を投げる場合は、識別子単体のthrowではないため診断が出ないことを確認する
    /// </summary>
    [Fact]
    public async Task ThrowNewWrappingException_DoesNotReportDiagnostic()
    {
        var test = """
            using System;

            public class C
            {
                void M()
                {
                    try
                    {
                        Console.WriteLine("C");
                    }
                    catch (Exception ex)
                    {
                        throw new InvalidOperationException("failed", ex);
                    }
                }
            }
            """;

        await Verify.VerifyAnalyzerAsync(test);
    }

    /// <summary>
    /// catch句の例外変数とは異なる名前の変数をthrowする場合は診断が出ないことを確認する
    /// </summary>
    [Fact]
    public async Task ThrowDifferentVariable_DoesNotReportDiagnostic()
    {
        var test = """
            using System;

            public class C
            {
                void M()
                {
                    Exception? stored = null;
                    try
                    {
                        Console.WriteLine("D");
                    }
                    catch (Exception ex)
                    {
                        stored = ex;
                    }

                    if (stored != null)
                    {
                        throw stored;
                    }
                }
            }
            """;

        await Verify.VerifyAnalyzerAsync(test);
    }

    /// <summary>
    /// 型指定なしの一般catch（変数宣言なし）の場合、catch自身の例外変数は存在しないため診断が出ないことを確認する
    /// </summary>
    [Fact]
    public async Task GeneralCatchWithoutDeclaration_DoesNotReportDiagnostic()
    {
        var test = """
            using System;

            public class C
            {
                private readonly Exception ex = new InvalidOperationException();

                void M()
                {
                    try
                    {
                        Console.WriteLine("E");
                    }
                    catch
                    {
                        throw ex;
                    }
                }
            }
            """;

        await Verify.VerifyAnalyzerAsync(test);
    }

    /// <summary>
    /// 内側のcatch句の中から、外側のcatch句で捕捉した例外を意図的にthrowしている場合は、
    /// bareなthrow;では書き換えられない（内側の例外が再スローされてしまう）ため診断が出ないことを確認する
    /// </summary>
    [Fact]
    public async Task ThrowOuterCaughtExceptionFromNestedCatch_DoesNotReportDiagnostic()
    {
        var test = """
            using System;
            using System.IO;

            public class C
            {
                void M()
                {
                    try
                    {
                        Console.WriteLine("F");
                    }
                    catch (Exception ex)
                    {
                        try
                        {
                            Console.WriteLine("G");
                        }
                        catch (IOException)
                        {
                            throw ex;
                        }
                    }
                }
            }
            """;

        await Verify.VerifyAnalyzerAsync(test);
    }

    /// <summary>
    /// 内側のcatch句自身が捕捉した例外をthrowする場合は、通常どおり診断が出ることを確認する
    /// （直近のcatch句を正しく特定できているかの確認）
    /// </summary>
    [Fact]
    public async Task ThrowInnerCatchOwnException_ReportsDiagnostic()
    {
        var test = """
            using System;
            using System.IO;

            public class C
            {
                void M()
                {
                    try
                    {
                        Console.WriteLine("H");
                    }
                    catch (Exception ex)
                    {
                        try
                        {
                            Console.WriteLine("I");
                        }
                        catch (IOException innerEx)
                        {
                            {|#0:throw innerEx;|}
                        }
                    }
                }
            }
            """;

        await Verify.VerifyAnalyzerAsync(test, Verify.Diagnostic().WithLocation(0).WithArguments("innerEx"));
    }

    /// <summary>
    /// catchブロック内のラムダ式を越えた先でthrowしている場合、その場所ではbareなthrow;が
    /// 使えない（コンパイルエラーになる）ため診断が出ないことを確認する
    /// </summary>
    [Fact]
    public async Task ThrowInsideLambdaWithinCatch_DoesNotReportDiagnostic()
    {
        var test = """
            using System;

            public class C
            {
                void M()
                {
                    try
                    {
                        Console.WriteLine("J");
                    }
                    catch (Exception ex)
                    {
                        Action rethrow = () => throw ex;
                        rethrow();
                    }
                }
            }
            """;

        await Verify.VerifyAnalyzerAsync(test);
    }

    /// <summary>
    /// catchブロック内のローカル関数を越えた先でthrowしている場合も、同様にbareなthrow;が
    /// 使えないため診断が出ないことを確認する
    /// </summary>
    [Fact]
    public async Task ThrowInsideLocalFunctionWithinCatch_DoesNotReportDiagnostic()
    {
        var test = """
            using System;

            public class C
            {
                void M()
                {
                    try
                    {
                        Console.WriteLine("K");
                    }
                    catch (Exception ex)
                    {
                        Rethrow();

                        void Rethrow()
                        {
                            throw ex;
                        }
                    }
                }
            }
            """;

        await Verify.VerifyAnalyzerAsync(test);
    }

    /// <summary>
    /// catch句にもラムダ式・ローカル関数・メソッド境界にも到達しないままprivateフィールドを
    /// throwする場合（プロパティのgetアクセサー等）、境界チェックに引っかからずcatchClauseが
    /// nullのままnull条件演算子の比較に到達するため、その分岐でも正しく診断が出ないことを確認する
    /// （AccessorDeclarationSyntaxはBaseMethodDeclarationSyntaxではないため境界判定を素通りする）
    /// </summary>
    [Fact]
    public async Task ThrowFieldInPropertyAccessorWithNoEnclosingCatch_DoesNotReportDiagnostic()
    {
        var test = """
            using System;

            public class C
            {
                private readonly Exception ex = new InvalidOperationException();

                public Exception Prop
                {
                    get
                    {
                        throw ex;
                    }
                }
            }
            """;

        await Verify.VerifyAnalyzerAsync(test);
    }

    /// <summary>
    /// Fixが "throw ex;" を "throw;" に置き換えることを確認する
    /// </summary>
    [Fact]
    public async Task Fix_ReplacesWithBareThrow()
    {
        var test = """
            using System;

            public class C
            {
                void M()
                {
                    try
                    {
                        Console.WriteLine("A");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine(ex.Message);
                        {|#0:throw ex;|}
                    }
                }
            }
            """;

        var fixedSource = """
            using System;

            public class C
            {
                void M()
                {
                    try
                    {
                        Console.WriteLine("A");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine(ex.Message);
                        throw;
                    }
                }
            }
            """;

        await CodeFixVerify.VerifyCodeFixAsync(test, CodeFixVerify.Diagnostic().WithLocation(0).WithArguments("ex"), fixedSource);
    }
}

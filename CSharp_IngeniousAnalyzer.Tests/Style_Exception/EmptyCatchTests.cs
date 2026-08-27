using CSharp_IngeniousAnalyzer.Style_Exception;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
using Microsoft.CodeAnalysis.Testing.Verifiers;

namespace CSharp_IngeniousAnalyzer.Tests.Style_Exception;

using Verify = CSharpAnalyzerVerifier<EmptyCatch, XUnitVerifier>;
using CodeFixVerify = CSharpCodeFixVerifier<EmptyCatch, EmptyCatchFix, XUnitVerifier>;

/// <summary>
/// EXC001（EmptyCatch）の検知・Fix動作を検証するテスト
/// </summary>
public class EmptyCatchTests
{
    /// <summary>
    /// 具体的な例外型を指定し、変数宣言もない完全に空なcatchブロックで、診断が出ることを確認する
    /// </summary>
    [Fact]
    public async Task EmptyCatchWithSpecificExceptionType_ReportsDiagnostic()
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
                    {|#0:catch|} (InvalidOperationException)
                    {
                    }
                }
            }
            """;

        await Verify.VerifyAnalyzerAsync(test, Verify.Diagnostic().WithLocation(0).WithArguments("InvalidOperationException"));
    }

    /// <summary>
    /// 例外変数を宣言しているが未使用のまま空である場合も、診断が出ることを確認する（CS0168の警告とは別に検知される）
    /// </summary>
    [Fact]
    public async Task EmptyCatchWithUnusedVariable_ReportsDiagnostic()
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
                    {|#0:catch|} (Exception ex)
                    {
                    }
                }
            }
            """;

        await Verify.VerifyAnalyzerAsync(test, Verify.Diagnostic().WithLocation(0).WithArguments("Exception"));
    }

    /// <summary>
    /// 型指定なしの一般catch（catch { }）であっても、空であれば診断が出ることを確認する
    /// </summary>
    [Fact]
    public async Task EmptyGeneralCatch_ReportsDiagnostic()
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
                    {|#0:catch|}
                    {
                    }
                }
            }
            """;

        await Verify.VerifyAnalyzerAsync(test, Verify.Diagnostic().WithLocation(0).WithArguments("Exception"));
    }

    /// <summary>
    /// 閉じ括弧の手前（自分の行）にコメントがある場合、「意図的に何もしない」とみなされ診断が出ないことを確認する
    /// </summary>
    [Fact]
    public async Task CommentBeforeClosingBrace_DoesNotReportDiagnostic()
    {
        var test = """
            using System;

            public class C
            {
                void M()
                {
                    try
                    {
                        Console.WriteLine("D");
                    }
                    catch
                    {
                        // 何もしない
                    }
                }
            }
            """;

        await Verify.VerifyAnalyzerAsync(test);
    }

    /// <summary>
    /// 開き括弧と同じ行にコメントがある場合も、「意図的に何もしない」とみなされ診断が出ないことを確認する
    /// </summary>
    [Fact]
    public async Task CommentOnSameLineAsOpenBrace_DoesNotReportDiagnostic()
    {
        var test = """
            using System;

            public class C
            {
                void M()
                {
                    try
                    {
                        Console.WriteLine("E");
                    }
                    catch (Exception ex)
                    { // 何もしない
                    }
                }
            }
            """;

        await Verify.VerifyAnalyzerAsync(test);
    }

    /// <summary>
    /// 実際に処理（ログ出力）が行われている場合は、そもそも「空」ではないため診断が出ないことを確認する
    /// </summary>
    [Fact]
    public async Task CatchWithHandlingCode_DoesNotReportDiagnostic()
    {
        var test = """
            using System;

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
                        LogError(ex);
                    }
                }

                void LogError(Exception ex) => Console.WriteLine(ex.Message);
            }
            """;

        await Verify.VerifyAnalyzerAsync(test);
    }

    /// <summary>
    /// rethrow（throw;）のみの場合も「空」ではないため診断が出ないことを確認する
    /// </summary>
    [Fact]
    public async Task CatchWithRethrowOnly_DoesNotReportDiagnostic()
    {
        var test = """
            using System;

            public class C
            {
                void M()
                {
                    try
                    {
                        Console.WriteLine("G");
                    }
                    catch (Exception)
                    {
                        throw;
                    }
                }
            }
            """;

        await Verify.VerifyAnalyzerAsync(test);
    }

    /// <summary>
    /// 同一try内に複数catchがある場合、空のcatchのみが独立して検知され、処理ありのcatchは検知されないことを確認する
    /// </summary>
    [Fact]
    public async Task MultipleCatchClauses_OnlyEmptyOneReportsDiagnostic()
    {
        var test = """
            using System;

            public class C
            {
                void M()
                {
                    try
                    {
                        Console.WriteLine("H");
                    }
                    {|#0:catch|} (ArgumentNullException)
                    {
                    }
                    catch (Exception ex)
                    {
                        LogError(ex);
                    }
                }

                void LogError(Exception ex) => Console.WriteLine(ex.Message);
            }
            """;

        await Verify.VerifyAnalyzerAsync(test, Verify.Diagnostic().WithLocation(0).WithArguments("ArgumentNullException"));
    }

    /// <summary>
    /// 例外フィルター（when句）が付与されていても、本体が空であれば診断が出ることを確認する
    /// </summary>
    [Fact]
    public async Task EmptyCatchWithExceptionFilter_ReportsDiagnostic()
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
                    {|#0:catch|} (Exception ex) when (ex.Message.Length > 0)
                    {
                    }
                }
            }
            """;

        await Verify.VerifyAnalyzerAsync(test, Verify.Diagnostic().WithLocation(0).WithArguments("Exception"));
    }

    /// <summary>
    /// Fixが空のcatchブロック内に、次段階のインデントでTODOコメントを挿入することを確認する
    /// </summary>
    [Fact]
    public async Task Fix_InsertsTodoComment()
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
                    {|#0:catch|} (InvalidOperationException)
                    {
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
                    catch (InvalidOperationException)
                    {
                        // TODO: 例外処理を検討してください
                    }
                }
            }
            """;

        await CodeFixVerify.VerifyCodeFixAsync(test, CodeFixVerify.Diagnostic().WithLocation(0).WithArguments("InvalidOperationException"), fixedSource);
    }
}

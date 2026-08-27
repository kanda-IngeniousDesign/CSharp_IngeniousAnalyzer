using CSharp_IngeniousAnalyzer.Style_Complexity;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
using Microsoft.CodeAnalysis.Testing.Verifiers;

namespace CSharp_IngeniousAnalyzer.Tests.Style_Complexity;

using Verify = CSharpAnalyzerVerifier<FatMethod, XUnitVerifier>;
using CodeFixVerify = CSharpCodeFixVerifier<FatMethod, FatMethodFix, XUnitVerifier>;

/// <summary>
/// CPX002（FatMethod）の検知・Fix動作を検証するテスト
/// </summary>
public class FatMethodTests
{
    /// <summary>
    /// メソッド本体が300行を超え、かつ関数呼び出しが行数に対して少ない（行数/100未満）場合に
    /// 診断が出ることを確認する
    /// </summary>
    [Fact]
    public async Task TooManyLinesWithFewInvocations_ReportsDiagnostic()
    {
        var body = string.Concat(Enumerable.Repeat("        hoge++;\n", 350));

        var test = $$"""
            public class C
            {
                void {|#0:BigMethod|}()
                {
                    int hoge = 0;
            {{body}}    }
            }
            """;

        await Verify.VerifyAnalyzerAsync(test, Verify.Diagnostic().WithLocation(0).WithArguments("BigMethod", 353, 0));
    }

    /// <summary>
    /// メソッド本体が300行を超えていても、関数呼び出しの比率が十分（行数/100以上）であれば
    /// 診断が出ないことを確認する
    /// </summary>
    [Fact]
    public async Task TooManyLinesWithEnoughInvocations_DoesNotReportDiagnostic()
    {
        var body = string.Concat(Enumerable.Repeat("        hoge++;\n", 350));
        var calls = string.Concat(Enumerable.Repeat("        System.Console.WriteLine(hoge);\n", 10));

        var test = $$"""
            public class C
            {
                void LongButOkMethod()
                {
                    int hoge = 0;
            {{body}}{{calls}}    }
            }
            """;

        await Verify.VerifyAnalyzerAsync(test);
    }

    /// <summary>
    /// Fixがメソッド本体の先頭ステートメントの直前に「// Ignore CPX002」コメントを挿入することを確認する
    /// </summary>
    [Fact]
    public async Task Fix_InsertsIgnoreComment()
    {
        var body = string.Concat(Enumerable.Repeat("        hoge++;\n", 350));

        var test = $$"""
            public class C
            {
                void {|#0:BigMethod|}()
                {
                    int hoge = 0;
            {{body}}    }
            }
            """;

        var crlf = "\r\n";
        var fixedSource = $$"""
            public class C
            {
                void BigMethod()
                {
                    // Ignore CPX002{{crlf}}        int hoge = 0;
            {{body}}    }
            }
            """;

        await CodeFixVerify.VerifyCodeFixAsync(test, CodeFixVerify.Diagnostic().WithLocation(0).WithArguments("BigMethod", 353, 0), fixedSource);
    }
}

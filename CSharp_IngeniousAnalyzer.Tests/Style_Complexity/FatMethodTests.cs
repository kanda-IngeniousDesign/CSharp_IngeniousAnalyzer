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

    /// <summary>
    /// メソッド本体を持たない（抽象メソッド等の）宣言に対しては、行数を計測しようがないため
    /// 診断が出ないことを確認する（method.Body == null の早期リターンを検証）
    /// </summary>
    [Fact]
    public async Task MethodWithoutBody_DoesNotReportDiagnostic()
    {
        var test = """
            public abstract class C
            {
                public abstract void BigMethod();
            }
            """;

        await Verify.VerifyAnalyzerAsync(test);
    }

    /// <summary>
    /// メソッド宣言そのものの直前（アクセス修飾子の前）に「// Ignore CPX002」コメントが
    /// 付与されている場合、300行超・呼び出し比率不足の条件を満たしていても
    /// 診断が抑制されることを確認する
    /// </summary>
    [Fact]
    public async Task MethodWithIgnoreComment_DoesNotReportDiagnostic()
    {
        var body = string.Concat(Enumerable.Repeat("        hoge++;\n", 350));

        var test = $$"""
            public class C
            {
                // Ignore CPX002
                void BigMethod()
                {
                    int hoge = 0;
            {{body}}    }
            }
            """;

        await Verify.VerifyAnalyzerAsync(test);
    }

    /// <summary>
    /// メソッド本体がちょうど300行（閾値と同値）の場合は「300行を超える」条件を満たさないため、
    /// 呼び出し数に関わらず診断が出ないことを確認する（&gt; の境界値）
    /// </summary>
    [Fact]
    public async Task ExactlyAtLineThreshold_DoesNotReportDiagnostic()
    {
        var body = string.Concat(Enumerable.Repeat("        hoge++;\n", 297));

        var test = $$"""
            public class C
            {
                void ExactlyAtThreshold()
                {
                    int hoge = 0;
            {{body}}    }
            }
            """;

        await Verify.VerifyAnalyzerAsync(test);
    }

    /// <summary>
    /// 行数が300を超え（304行）、呼び出し数がちょうど閾値（304/100=3、整数除算）と同値の場合は
    /// 「invocationCount &lt; threshold」を満たさないため診断が出ないことを確認する（&lt; の境界値）
    /// </summary>
    [Fact]
    public async Task InvocationCountAtThreshold_DoesNotReportDiagnostic()
    {
        var body = string.Concat(Enumerable.Repeat("        hoge++;\n", 298));
        var calls = string.Concat(Enumerable.Repeat("        System.Console.WriteLine(hoge);\n", 3));

        var test = $$"""
            public class C
            {
                void InvocationAtThreshold()
                {
                    int hoge = 0;
            {{body}}{{calls}}    }
            }
            """;

        await Verify.VerifyAnalyzerAsync(test);
    }

    /// <summary>
    /// 行数が300を超え（303行）、呼び出し数が閾値（303/100=3、整数除算）をちょうど1下回る場合は
    /// 診断が出ることを確認する（&lt; の境界値の反対側）
    /// </summary>
    [Fact]
    public async Task InvocationCountBelowThreshold_ReportsDiagnostic()
    {
        var body = string.Concat(Enumerable.Repeat("        hoge++;\n", 298));
        var calls = string.Concat(Enumerable.Repeat("        System.Console.WriteLine(hoge);\n", 2));

        var test = $$"""
            public class C
            {
                void {|#0:InvocationBelowThreshold|}()
                {
                    int hoge = 0;
            {{body}}{{calls}}    }
            }
            """;

        await Verify.VerifyAnalyzerAsync(test, Verify.Diagnostic().WithLocation(0).WithArguments("InvocationBelowThreshold", 303, 2));
    }
}

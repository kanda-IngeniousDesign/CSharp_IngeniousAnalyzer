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

    /// <summary>
    /// フラットな（ネストしない）if文16個で複雑度がちょうど閾値(17)になる場合、診断が出ないことを確認する
    /// </summary>
    [Fact]
    public async Task FlatIfsAtThreshold_DoesNotReportDiagnostic()
    {
        var test = """
            public class C
            {
                void M(bool a1, bool a2, bool a3, bool a4, bool a5, bool a6, bool a7, bool a8, bool a9, bool a10, bool a11, bool a12, bool a13, bool a14, bool a15, bool a16)
                {
                    if (a1) { }
                    if (a2) { }
                    if (a3) { }
                    if (a4) { }
                    if (a5) { }
                    if (a6) { }
                    if (a7) { }
                    if (a8) { }
                    if (a9) { }
                    if (a10) { }
                    if (a11) { }
                    if (a12) { }
                    if (a13) { }
                    if (a14) { }
                    if (a15) { }
                    if (a16) { }
                }
            }
            """;

        await Verify.VerifyAnalyzerAsync(test);
    }

    /// <summary>
    /// フラットな（ネストしない）if文17個で複雑度が閾値(17)を1つ超える場合、診断が出ることを確認する
    /// </summary>
    [Fact]
    public async Task FlatIfsOneOverThreshold_ReportsDiagnostic()
    {
        var test = """
            public class C
            {
                void {|#0:M|}(bool a1, bool a2, bool a3, bool a4, bool a5, bool a6, bool a7, bool a8, bool a9, bool a10, bool a11, bool a12, bool a13, bool a14, bool a15, bool a16, bool a17)
                {
                    if (a1) { }
                    if (a2) { }
                    if (a3) { }
                    if (a4) { }
                    if (a5) { }
                    if (a6) { }
                    if (a7) { }
                    if (a8) { }
                    if (a9) { }
                    if (a10) { }
                    if (a11) { }
                    if (a12) { }
                    if (a13) { }
                    if (a14) { }
                    if (a15) { }
                    if (a16) { }
                    if (a17) { }
                }
            }
            """;

        await Verify.VerifyAnalyzerAsync(test, Verify.Diagnostic().WithLocation(0).WithArguments("M", 18));
    }

    /// <summary>
    /// for文が複雑度加算の対象になっており、閾値超え（複雑度18）で診断が出ることを確認する
    /// </summary>
    [Fact]
    public async Task ForLoop_ReportsDiagnostic()
    {
        var test = """
            public class C
            {
                void {|#0:M|}(bool a1, bool a2, bool a3, bool a4, bool a5, bool a6, bool a7, bool a8, bool a9, bool a10, bool a11, bool a12, bool a13, bool a14, bool a15, bool a16, int n)
                {
                    if (a1) { }
                    if (a2) { }
                    if (a3) { }
                    if (a4) { }
                    if (a5) { }
                    if (a6) { }
                    if (a7) { }
                    if (a8) { }
                    if (a9) { }
                    if (a10) { }
                    if (a11) { }
                    if (a12) { }
                    if (a13) { }
                    if (a14) { }
                    if (a15) { }
                    if (a16) { }
                    for (int i = 0; i < n; i++) { }
                }
            }
            """;

        await Verify.VerifyAnalyzerAsync(test, Verify.Diagnostic().WithLocation(0).WithArguments("M", 18));
    }

    /// <summary>
    /// foreach文が複雑度加算の対象になっており、閾値超え（複雑度18）で診断が出ることを確認する
    /// </summary>
    [Fact]
    public async Task ForEachLoop_ReportsDiagnostic()
    {
        var test = """
            using System.Collections.Generic;

            public class C
            {
                void {|#0:M|}(bool a1, bool a2, bool a3, bool a4, bool a5, bool a6, bool a7, bool a8, bool a9, bool a10, bool a11, bool a12, bool a13, bool a14, bool a15, bool a16, List<int> items)
                {
                    if (a1) { }
                    if (a2) { }
                    if (a3) { }
                    if (a4) { }
                    if (a5) { }
                    if (a6) { }
                    if (a7) { }
                    if (a8) { }
                    if (a9) { }
                    if (a10) { }
                    if (a11) { }
                    if (a12) { }
                    if (a13) { }
                    if (a14) { }
                    if (a15) { }
                    if (a16) { }
                    foreach (var item in items) { }
                }
            }
            """;

        await Verify.VerifyAnalyzerAsync(test, Verify.Diagnostic().WithLocation(0).WithArguments("M", 18));
    }

    /// <summary>
    /// while文が複雑度加算の対象になっており、閾値超え（複雑度18）で診断が出ることを確認する
    /// </summary>
    [Fact]
    public async Task WhileLoop_ReportsDiagnostic()
    {
        var test = """
            public class C
            {
                void {|#0:M|}(bool a1, bool a2, bool a3, bool a4, bool a5, bool a6, bool a7, bool a8, bool a9, bool a10, bool a11, bool a12, bool a13, bool a14, bool a15, bool a16, int n)
                {
                    if (a1) { }
                    if (a2) { }
                    if (a3) { }
                    if (a4) { }
                    if (a5) { }
                    if (a6) { }
                    if (a7) { }
                    if (a8) { }
                    if (a9) { }
                    if (a10) { }
                    if (a11) { }
                    if (a12) { }
                    if (a13) { }
                    if (a14) { }
                    if (a15) { }
                    if (a16) { }
                    while (n > 0) { n--; }
                }
            }
            """;

        await Verify.VerifyAnalyzerAsync(test, Verify.Diagnostic().WithLocation(0).WithArguments("M", 18));
    }

    /// <summary>
    /// do-while文が複雑度加算の対象になっており、閾値超え（複雑度18）で診断が出ることを確認する
    /// </summary>
    [Fact]
    public async Task DoWhileLoop_ReportsDiagnostic()
    {
        var test = """
            public class C
            {
                void {|#0:M|}(bool a1, bool a2, bool a3, bool a4, bool a5, bool a6, bool a7, bool a8, bool a9, bool a10, bool a11, bool a12, bool a13, bool a14, bool a15, bool a16, int n)
                {
                    if (a1) { }
                    if (a2) { }
                    if (a3) { }
                    if (a4) { }
                    if (a5) { }
                    if (a6) { }
                    if (a7) { }
                    if (a8) { }
                    if (a9) { }
                    if (a10) { }
                    if (a11) { }
                    if (a12) { }
                    if (a13) { }
                    if (a14) { }
                    if (a15) { }
                    if (a16) { }
                    do { n--; } while (n > 0);
                }
            }
            """;

        await Verify.VerifyAnalyzerAsync(test, Verify.Diagnostic().WithLocation(0).WithArguments("M", 18));
    }

    /// <summary>
    /// switch文のcaseラベルが複数（case 1: case 2:）でも1つのSwitchSectionとして扱われ、
    /// 複雑度加算が1回分（ラベル数分の重複加算ではない）であることを確認する
    /// </summary>
    [Fact]
    public async Task SwitchWithGroupedCaseLabels_ReportsDiagnostic()
    {
        var test = """
            public class C
            {
                void {|#0:M|}(bool a1, bool a2, bool a3, bool a4, bool a5, bool a6, bool a7, bool a8, bool a9, bool a10, bool a11, bool a12, bool a13, bool a14, bool a15, bool a16, int n)
                {
                    if (a1) { }
                    if (a2) { }
                    if (a3) { }
                    if (a4) { }
                    if (a5) { }
                    if (a6) { }
                    if (a7) { }
                    if (a8) { }
                    if (a9) { }
                    if (a10) { }
                    if (a11) { }
                    if (a12) { }
                    if (a13) { }
                    if (a14) { }
                    if (a15) { }
                    if (a16) { }
                    switch (n)
                    {
                        case 1:
                        case 2:
                            break;
                    }
                }
            }
            """;

        await Verify.VerifyAnalyzerAsync(test, Verify.Diagnostic().WithLocation(0).WithArguments("M", 18));
    }

    /// <summary>
    /// catch節が複雑度加算の対象になっており、閾値超え（複雑度18）で診断が出ることを確認する
    /// </summary>
    [Fact]
    public async Task TryCatch_ReportsDiagnostic()
    {
        var test = """
            using System;

            public class C
            {
                void {|#0:M|}(bool a1, bool a2, bool a3, bool a4, bool a5, bool a6, bool a7, bool a8, bool a9, bool a10, bool a11, bool a12, bool a13, bool a14, bool a15, bool a16)
                {
                    if (a1) { }
                    if (a2) { }
                    if (a3) { }
                    if (a4) { }
                    if (a5) { }
                    if (a6) { }
                    if (a7) { }
                    if (a8) { }
                    if (a9) { }
                    if (a10) { }
                    if (a11) { }
                    if (a12) { }
                    if (a13) { }
                    if (a14) { }
                    if (a15) { }
                    if (a16) { }
                    try { }
                    catch (Exception) { }
                }
            }
            """;

        await Verify.VerifyAnalyzerAsync(test, Verify.Diagnostic().WithLocation(0).WithArguments("M", 18));
    }

    /// <summary>
    /// 三項演算子（条件式）が複雑度加算の対象になっており、閾値超え（複雑度18）で診断が出ることを確認する
    /// </summary>
    [Fact]
    public async Task TernaryConditionalExpression_ReportsDiagnostic()
    {
        var test = """
            public class C
            {
                int {|#0:M|}(bool a1, bool a2, bool a3, bool a4, bool a5, bool a6, bool a7, bool a8, bool a9, bool a10, bool a11, bool a12, bool a13, bool a14, bool a15, bool a16, int x)
                {
                    if (a1) { }
                    if (a2) { }
                    if (a3) { }
                    if (a4) { }
                    if (a5) { }
                    if (a6) { }
                    if (a7) { }
                    if (a8) { }
                    if (a9) { }
                    if (a10) { }
                    if (a11) { }
                    if (a12) { }
                    if (a13) { }
                    if (a14) { }
                    if (a15) { }
                    if (a16) { }
                    return x > 0 ? 1 : 0;
                }
            }
            """;

        await Verify.VerifyAnalyzerAsync(test, Verify.Diagnostic().WithLocation(0).WithArguments("M", 18));
    }

    /// <summary>
    /// System.Linqの拡張メソッド呼び出しが連鎖しているだけ（if等は無し）でも、
    /// その呼び出し数の分だけ複雑度が加算され、閾値超えで診断が出ることを確認する
    /// </summary>
    [Fact]
    public async Task LinqMethodChain_ReportsDiagnostic()
    {
        var test = """
            using System.Collections.Generic;
            using System.Linq;

            public class C
            {
                void {|#0:M|}(List<int> items)
                {
                    var result = items.Where(x => x > 0).Select(x => x * 2).OrderBy(x => x).ThenBy(x => x).Distinct().Skip(1).Take(5).Reverse()
                        .Where(x => x != 0).Select(x => x + 1).OrderByDescending(x => x).Where(x => x > 1).Select(x => x).Distinct().Skip(0).Take(10).Reverse();
                }
            }
            """;

        await Verify.VerifyAnalyzerAsync(test, Verify.Diagnostic().WithLocation(0).WithArguments("M", 18));
    }

    /// <summary>
    /// System.Linq名前空間に属さないメソッド呼び出し（Console.WriteLine）は、
    /// 複雑度に加算されないことを確認する（複雑度17のまま、診断は出ない）
    /// </summary>
    [Fact]
    public async Task NonLinqInvocation_DoesNotReportDiagnostic()
    {
        var test = """
            using System;

            public class C
            {
                void M(bool a1, bool a2, bool a3, bool a4, bool a5, bool a6, bool a7, bool a8, bool a9, bool a10, bool a11, bool a12, bool a13, bool a14, bool a15, bool a16)
                {
                    if (a1) { }
                    if (a2) { }
                    if (a3) { }
                    if (a4) { }
                    if (a5) { }
                    if (a6) { }
                    if (a7) { }
                    if (a8) { }
                    if (a9) { }
                    if (a10) { }
                    if (a11) { }
                    if (a12) { }
                    if (a13) { }
                    if (a14) { }
                    if (a15) { }
                    if (a16) { }
                    Console.WriteLine("hello");
                }
            }
            """;

        await Verify.VerifyAnalyzerAsync(test);
    }

    /// <summary>
    /// if → for → while と異なる種類の構文が入れ子になっている場合、
    /// ネスト深さが構文の種類をまたいで正しく積算されることを確認する（複雑度23）
    /// </summary>
    [Fact]
    public async Task CrossTypeNestedConstructs_ReportsDiagnostic()
    {
        var test = """
            public class C
            {
                void {|#0:M|}(bool a1, bool a2, bool a3, bool a4, bool a5, bool a6, bool a7, bool a8, bool a9, bool a10, bool a11, bool a12, bool a13, bool a14, bool a15, bool a16, bool cond, int n)
                {
                    if (a1) { }
                    if (a2) { }
                    if (a3) { }
                    if (a4) { }
                    if (a5) { }
                    if (a6) { }
                    if (a7) { }
                    if (a8) { }
                    if (a9) { }
                    if (a10) { }
                    if (a11) { }
                    if (a12) { }
                    if (a13) { }
                    if (a14) { }
                    if (a15) { }
                    if (a16) { }
                    if (cond)
                    {
                        for (int i = 0; i < n; i++)
                        {
                            while (n > 0)
                            {
                                n--;
                            }
                        }
                    }
                }
            }
            """;

        await Verify.VerifyAnalyzerAsync(test, Verify.Diagnostic().WithLocation(0).WithArguments("M", 23));
    }

    /// <summary>
    /// 式形式のメソッド（Body が null）は、内部が複雑な三項演算子の入れ子であっても
    /// 複雑度計算の対象外としてスキップされ、診断が出ないことを確認する
    /// </summary>
    [Fact]
    public async Task ExpressionBodiedMethod_DoesNotReportDiagnostic()
    {
        var test = """
            public class C
            {
                int M(int x) => x > 0 ? (x > 1 ? 1 : 2) : (x < -1 ? -1 : -2);
            }
            """;

        await Verify.VerifyAnalyzerAsync(test);
    }

    /// <summary>
    /// 「// Ignore CPX001」コメントがメソッド宣言自体の直前（ブロック本体の外側）にある場合も、
    /// 抑制コメントとして認識され診断が出ないことを確認する
    /// </summary>
    [Fact]
    public async Task IgnoreCommentBeforeMethodDeclaration_DoesNotReportDiagnostic()
    {
        var test = """
            public class C
            {
                // Ignore CPX001
                void M(bool a1, bool a2, bool a3, bool a4, bool a5, bool a6, bool a7, bool a8, bool a9, bool a10, bool a11, bool a12, bool a13, bool a14, bool a15, bool a16, bool a17)
                {
                    if (a1) { }
                    if (a2) { }
                    if (a3) { }
                    if (a4) { }
                    if (a5) { }
                    if (a6) { }
                    if (a7) { }
                    if (a8) { }
                    if (a9) { }
                    if (a10) { }
                    if (a11) { }
                    if (a12) { }
                    if (a13) { }
                    if (a14) { }
                    if (a15) { }
                    if (a16) { }
                    if (a17) { }
                }
            }
            """;

        await Verify.VerifyAnalyzerAsync(test);
    }

    /// <summary>
    /// 「// Ignore CPX001」コメントがメソッド本体の開き波かっこの直前（パラメータリストと { の間）にある場合も、
    /// 抑制コメントとして認識され診断が出ないことを確認する
    /// </summary>
    [Fact]
    public async Task IgnoreCommentBeforeOpeningBrace_DoesNotReportDiagnostic()
    {
        var test = """
            public class C
            {
                void M(bool a1, bool a2, bool a3, bool a4, bool a5, bool a6, bool a7, bool a8, bool a9, bool a10, bool a11, bool a12, bool a13, bool a14, bool a15, bool a16, bool a17)
                // Ignore CPX001
                {
                    if (a1) { }
                    if (a2) { }
                    if (a3) { }
                    if (a4) { }
                    if (a5) { }
                    if (a6) { }
                    if (a7) { }
                    if (a8) { }
                    if (a9) { }
                    if (a10) { }
                    if (a11) { }
                    if (a12) { }
                    if (a13) { }
                    if (a14) { }
                    if (a15) { }
                    if (a16) { }
                    if (a17) { }
                }
            }
            """;

        await Verify.VerifyAnalyzerAsync(test);
    }

    /// <summary>
    /// 抑制コメントの照合は大文字・小文字を区別しない（"// ignore cpx001" のような小文字表記でも
    /// 抑制コメントとして認識される）ことを確認する
    /// </summary>
    [Fact]
    public async Task IgnoreCommentCaseInsensitive_DoesNotReportDiagnostic()
    {
        var test = """
            public class C
            {
                void M(bool a1, bool a2, bool a3, bool a4, bool a5, bool a6, bool a7, bool a8, bool a9, bool a10, bool a11, bool a12, bool a13, bool a14, bool a15, bool a16, bool a17)
                {
                    // ignore cpx001
                    if (a1) { }
                    if (a2) { }
                    if (a3) { }
                    if (a4) { }
                    if (a5) { }
                    if (a6) { }
                    if (a7) { }
                    if (a8) { }
                    if (a9) { }
                    if (a10) { }
                    if (a11) { }
                    if (a12) { }
                    if (a13) { }
                    if (a14) { }
                    if (a15) { }
                    if (a16) { }
                    if (a17) { }
                }
            }
            """;

        await Verify.VerifyAnalyzerAsync(test);
    }

    /// <summary>
    /// 複数行コメント形式（/* Ignore CPX001 */）は、抑制コメントとして認識されないことを確認する。
    /// IsIgnoreCommentTrivia はトリビア種別としては MultiLineCommentTrivia も許容しているが、
    /// 比較対象の文字列は常に "// " 始まりで組み立てられているため、/* ... */ 形式は実際には
    /// 一致し得ない（現状の実装では単一行コメント以外は事実上抑制されない）。
    /// </summary>
    [Fact]
    public async Task IgnoreCommentMultiLineStyle_ReportsDiagnostic()
    {
        var test = """
            public class C
            {
                void {|#0:M|}(bool a1, bool a2, bool a3, bool a4, bool a5, bool a6, bool a7, bool a8, bool a9, bool a10, bool a11, bool a12, bool a13, bool a14, bool a15, bool a16, bool a17)
                {
                    /* Ignore CPX001 */
                    if (a1) { }
                    if (a2) { }
                    if (a3) { }
                    if (a4) { }
                    if (a5) { }
                    if (a6) { }
                    if (a7) { }
                    if (a8) { }
                    if (a9) { }
                    if (a10) { }
                    if (a11) { }
                    if (a12) { }
                    if (a13) { }
                    if (a14) { }
                    if (a15) { }
                    if (a16) { }
                    if (a17) { }
                }
            }
            """;

        await Verify.VerifyAnalyzerAsync(test, Verify.Diagnostic().WithLocation(0).WithArguments("M", 18));
    }
}

using CSharp_IngeniousAnalyzer.Style_Compare;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
using Microsoft.CodeAnalysis.Testing.Verifiers;

namespace CSharp_IngeniousAnalyzer.Tests.Style_Compare;

using Verify = CSharpAnalyzerVerifier<Inequality, XUnitVerifier>;
using CodeFixVerify = CSharpCodeFixVerifier<Inequality, InequalityFix, XUnitVerifier>;

/// <summary>
/// COMP001（Inequality）の検知・Fix動作を検証するテスト
/// </summary>
public class InequalityTests
{
    /// <summary>
    /// 変数同士の比較（b &lt; a）が、すでに安全な向きのため検知されないことを確認する
    /// </summary>
    [Fact]
    public async Task LessThan_DoesNotReportDiagnostic()
    {
        var test = """
            public class C
            {
                void M(int a, int b)
                {
                    if (b < a) { }
                }
            }
            """;

        await Verify.VerifyAnalyzerAsync(test);
    }

    /// <summary>
    /// 変数同士の比較（a &lt;= b）が、すでに安全な向きのため検知されないことを確認する
    /// </summary>
    [Fact]
    public async Task LessThanOrEqual_DoesNotReportDiagnostic()
    {
        var test = """
            public class C
            {
                void M(int a, int b)
                {
                    if (a <= b) { }
                }
            }
            """;

        await Verify.VerifyAnalyzerAsync(test);
    }

    /// <summary>
    /// リテラルが左辺にある比較（5 &lt; a）でも、向きが安全であれば検知されないことを確認する
    /// </summary>
    [Fact]
    public async Task LiteralOnLeftLessThan_DoesNotReportDiagnostic()
    {
        var test = """
            public class C
            {
                void M(int a)
                {
                    if (5 < a) { }
                }
            }
            """;

        await Verify.VerifyAnalyzerAsync(test);
    }

    /// <summary>
    /// リテラルが左辺にある比較（10 &lt;= b）でも、向きが安全であれば検知されないことを確認する
    /// </summary>
    [Fact]
    public async Task LiteralOnLeftLessThanOrEqual_DoesNotReportDiagnostic()
    {
        var test = """
            public class C
            {
                void M(int b)
                {
                    if (10 <= b) { }
                }
            }
            """;

        await Verify.VerifyAnalyzerAsync(test);
    }

    /// <summary>
    /// フィールド参照が左辺にある比較（FieldValue &lt; a）でも、向きが安全であれば検知されないことを確認する
    /// </summary>
    [Fact]
    public async Task FieldLessThanVariable_DoesNotReportDiagnostic()
    {
        var test = """
            public class C
            {
                private int FieldValue = 10;

                void M(int a)
                {
                    if (FieldValue < a) { }
                }
            }
            """;

        await Verify.VerifyAnalyzerAsync(test);
    }

    /// <summary>
    /// メソッド呼び出しが左辺にある比較（GetValue() &lt; a）でも、向きが安全であれば検知されないことを確認する
    /// </summary>
    [Fact]
    public async Task MethodCallLessThanVariable_DoesNotReportDiagnostic()
    {
        var test = """
            public class C
            {
                int GetValue() => 10;

                void M(int a)
                {
                    if (GetValue() < a) { }
                }
            }
            """;

        await Verify.VerifyAnalyzerAsync(test);
    }

    /// <summary>
    /// メソッド呼び出しが左辺にある比較（Calculate() &lt;= b）でも、向きが安全であれば検知されないことを確認する
    /// </summary>
    [Fact]
    public async Task MethodCallLessThanOrEqualVariable_DoesNotReportDiagnostic()
    {
        var test = """
            public class C
            {
                int Calculate() => 5;

                void M(int b)
                {
                    if (Calculate() <= b) { }
                }
            }
            """;

        await Verify.VerifyAnalyzerAsync(test);
    }

    /// <summary>
    /// 複合条件（&amp;&amp;）の両辺がいずれも安全な向き（&lt;）であれば検知されないことを確認する
    /// </summary>
    [Fact]
    public async Task CompoundSafeCondition_DoesNotReportDiagnostic()
    {
        var test = """
            public class C
            {
                void M(int a, int b, int c, int d)
                {
                    if (b < a && c < d) { }
                }
            }
            """;

        await Verify.VerifyAnalyzerAsync(test);
    }

    /// <summary>
    /// 基本パターン（a &gt; b）で診断が出ることを確認する
    /// </summary>
    [Fact]
    public async Task GreaterThan_ReportsDiagnostic()
    {
        var test = """
            public class C
            {
                void M(int a, int b)
                {
                    if ({|#0:a > b|}) { }
                }
            }
            """;

        await Verify.VerifyAnalyzerAsync(test, Verify.Diagnostic().WithLocation(0));
    }

    /// <summary>
    /// 基本パターン（a &gt;= b）で診断が出ることを確認する
    /// </summary>
    [Fact]
    public async Task GreaterThanOrEqual_ReportsDiagnostic()
    {
        var test = """
            public class C
            {
                void M(int a, int b)
                {
                    if ({|#0:a >= b|}) { }
                }
            }
            """;

        await Verify.VerifyAnalyzerAsync(test, Verify.Diagnostic().WithLocation(0));
    }

    /// <summary>
    /// メソッド呼び出しが左辺にある向き違反（GetValue() &gt; a）でも診断が出ることを確認する
    /// </summary>
    [Fact]
    public async Task MethodCallGreaterThan_ReportsDiagnostic()
    {
        var test = """
            public class C
            {
                int GetValue() => 10;

                void M(int a)
                {
                    if ({|#0:GetValue() > a|}) { }
                }
            }
            """;

        await Verify.VerifyAnalyzerAsync(test, Verify.Diagnostic().WithLocation(0));
    }

    /// <summary>
    /// 素人エイリアン：複合条件の両側にそれぞれ向き違反が紛れ込んでいるケースで、両方に診断が出ることを確認する
    /// </summary>
    [Fact]
    public async Task CompoundCondition_ReportsDiagnosticOnBothSides()
    {
        var test = """
            public class C
            {
                void M(int a, int b)
                {
                    if ({|#0:a > b|} && {|#1:b >= a|}) { }
                }
            }
            """;

        await Verify.VerifyAnalyzerAsync(test, Verify.Diagnostic().WithLocation(0), Verify.Diagnostic().WithLocation(1));
    }

    /// <summary>
    /// 素人エイリアン：三項演算子の条件式に紛れ込んでいる向き違反でも診断が出ることを確認する
    /// </summary>
    [Fact]
    public async Task TernaryExpression_ReportsDiagnostic()
    {
        var test = """
            public class C
            {
                string M(int a, int b)
                {
                    return {|#0:a > b|} ? "a" : "b";
                }
            }
            """;

        await Verify.VerifyAnalyzerAsync(test, Verify.Diagnostic().WithLocation(0));
    }

    /// <summary>
    /// Fixが「a &gt; b」を左右反転して「b &lt; a」へ書き換えることを確認する
    /// </summary>
    [Fact]
    public async Task Fix_ReversesGreaterThanToLessThan()
    {
        var test = """
            public class C
            {
                void M(int a, int b)
                {
                    if ({|#0:a > b|}) { }
                }
            }
            """;

        var fixedSource = """
            public class C
            {
                void M(int a, int b)
                {
                    if (b < a) { }
                }
            }
            """;

        await CodeFixVerify.VerifyCodeFixAsync(test, CodeFixVerify.Diagnostic().WithLocation(0), fixedSource);
    }

    /// <summary>
    /// Fixが「a &gt;= b」を左右反転して「b &lt;= a」へ書き換えることを確認する
    /// </summary>
    [Fact]
    public async Task Fix_ReversesGreaterThanOrEqualToLessThanOrEqual()
    {
        var test = """
            public class C
            {
                void M(int a, int b)
                {
                    if ({|#0:a >= b|}) { }
                }
            }
            """;

        var fixedSource = """
            public class C
            {
                void M(int a, int b)
                {
                    if (b <= a) { }
                }
            }
            """;

        await CodeFixVerify.VerifyCodeFixAsync(test, CodeFixVerify.Diagnostic().WithLocation(0), fixedSource);
    }

    /// <summary>
    /// リテラルが右辺にある比較（a &gt; 5）でも、演算子が'&gt;'であれば診断が出ることを確認する
    /// （本ルールは左右のオペランド種別を判定せず、演算子の種類のみで診断するため）
    /// </summary>
    [Fact]
    public async Task LiteralOnRightGreaterThan_ReportsDiagnostic()
    {
        var test = """
            public class C
            {
                void M(int a)
                {
                    if ({|#0:a > 5|}) { }
                }
            }
            """;

        await Verify.VerifyAnalyzerAsync(test, Verify.Diagnostic().WithLocation(0));
    }

    /// <summary>
    /// リテラルが右辺にある比較（b &gt;= 10）でも、演算子が'&gt;='であれば診断が出ることを確認する
    /// </summary>
    [Fact]
    public async Task LiteralOnRightGreaterThanOrEqual_ReportsDiagnostic()
    {
        var test = """
            public class C
            {
                void M(int b)
                {
                    if ({|#0:b >= 10|}) { }
                }
            }
            """;

        await Verify.VerifyAnalyzerAsync(test, Verify.Diagnostic().WithLocation(0));
    }

    /// <summary>
    /// フィールド参照が左辺にある向き違反（FieldValue &gt;= a）でも診断が出ることを確認する
    /// （FieldLessThanVariable_DoesNotReportDiagnosticと対になる、'&gt;='側の境界値テスト）
    /// </summary>
    [Fact]
    public async Task FieldGreaterThanOrEqualVariable_ReportsDiagnostic()
    {
        var test = """
            public class C
            {
                private int FieldValue = 10;

                void M(int a)
                {
                    if ({|#0:FieldValue >= a|}) { }
                }
            }
            """;

        await Verify.VerifyAnalyzerAsync(test, Verify.Diagnostic().WithLocation(0));
    }

    /// <summary>
    /// Fixが、リテラルが右辺にある「a &gt; 5」を左右反転して「5 &lt; a」へ書き換えることを確認する
    /// </summary>
    [Fact]
    public async Task Fix_ReversesLiteralOnRightGreaterThan()
    {
        var test = """
            public class C
            {
                void M(int a)
                {
                    if ({|#0:a > 5|}) { }
                }
            }
            """;

        var fixedSource = """
            public class C
            {
                void M(int a)
                {
                    if (5 < a) { }
                }
            }
            """;

        await CodeFixVerify.VerifyCodeFixAsync(test, CodeFixVerify.Diagnostic().WithLocation(0), fixedSource);
    }

    /// <summary>
    /// 自動生成ファイル（*.Designer.cs）内のコードは、IsGeneratedFileガードにより
    /// 「a &gt; b」のような向き違反があっても診断が出ないことを確認する
    /// </summary>
    [Fact]
    public async Task GeneratedFile_DoesNotReportDiagnostic()
    {
        var test = """
            public class C
            {
                void M(int a, int b)
                {
                    if (a > b) { }
                }
            }
            """;

        var verifier = new CSharpAnalyzerTest<Inequality, XUnitVerifier>
        {
            TestState = { Sources = { ("Test0.Designer.cs", test) } },
        };

        await verifier.RunAsync();
    }
}

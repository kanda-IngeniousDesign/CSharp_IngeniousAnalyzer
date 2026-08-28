using CSharp_IngeniousAnalyzer.Style_String;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
using Microsoft.CodeAnalysis.Testing.Verifiers;

namespace CSharp_IngeniousAnalyzer.Tests.Style_String;

using Verify = CSharpAnalyzerVerifier<StringEmpty, XUnitVerifier>;
using CodeFixVerify = CSharpCodeFixVerifier<StringEmpty, StringEmptyFix, XUnitVerifier>;

/// <summary>
/// STR001（StringEmpty）の検知・Fix動作を検証するテスト
/// </summary>
public class StringEmptyTests
{
    /// <summary>
    /// 基本パターン（== String.Empty）で診断が出ることを確認する
    /// </summary>
    [Fact]
    public async Task EqualsStringEmpty_ReportsDiagnostic()
    {
        var test = """
            using System;

            public class C
            {
                void M(string s)
                {
                    if (s == {|#0:String.Empty|}) { }
                }
            }
            """;

        await Verify.VerifyAnalyzerAsync(test, Verify.Diagnostic().WithLocation(0).WithArguments("String.Empty"));
    }

    /// <summary>
    /// 基本パターン（!= String.Empty）で診断が出ることを確認する
    /// </summary>
    [Fact]
    public async Task NotEqualsStringEmpty_ReportsDiagnostic()
    {
        var test = """
            using System;

            public class C
            {
                void M(string s)
                {
                    if (s != {|#0:String.Empty|}) { }
                }
            }
            """;

        await Verify.VerifyAnalyzerAsync(test, Verify.Diagnostic().WithLocation(0).WithArguments("String.Empty"));
    }

    /// <summary>
    /// ヨーダ記法（String.Empty == s）でも、大文字 String.Empty の位置に関わらず診断が出ることを確認する
    /// </summary>
    [Fact]
    public async Task YodaCondition_ReportsDiagnostic()
    {
        var test = """
            using System;

            public class C
            {
                void M(string s)
                {
                    if ({|#0:String.Empty|} == s) { }
                }
            }
            """;

        await Verify.VerifyAnalyzerAsync(test, Verify.Diagnostic().WithLocation(0).WithArguments("String.Empty"));
    }

    /// <summary>
    /// 同一行にNULL関連の式（!= null）が混在していても、STR001はそれに影響されず独立して検知されることを確認する
    /// </summary>
    [Fact]
    public async Task MixedWithNullCheckOnSameLine_ReportsDiagnostic()
    {
        var test = """
            using System;

            public class C
            {
                void M(string s)
                {
                    if ({|#0:String.Empty|} == s && s != null) { }
                }
            }
            """;

        await Verify.VerifyAnalyzerAsync(test, Verify.Diagnostic().WithLocation(0).WithArguments("String.Empty"));
    }

    /// <summary>
    /// すでに小文字 string.Empty が使われている場合は誤検知しないことを確認する
    /// </summary>
    [Fact]
    public async Task AlreadyLowercaseStringEmpty_DoesNotReportDiagnostic()
    {
        var test = """
            public class C
            {
                void M(string s)
                {
                    if (s == string.Empty) { }
                }
            }
            """;

        await Verify.VerifyAnalyzerAsync(test);
    }

    /// <summary>
    /// .Equals(String.Empty) は == / != の構文ではないため、対象外（誤検知しない）ことを確認する
    /// </summary>
    [Fact]
    public async Task EqualsMethodCall_DoesNotReportDiagnostic()
    {
        var test = """
            using System;

            public class C
            {
                void M(string s)
                {
                    if (s.Equals(String.Empty)) { }
                }
            }
            """;

        await Verify.VerifyAnalyzerAsync(test);
    }

    /// <summary>
    /// Fixが大文字「String.Empty」を小文字「string.Empty」へ書き換えることを確認する
    /// </summary>
    [Fact]
    public async Task Fix_ReplacesStringEmptyWithLowercase()
    {
        var test = """
            using System;

            public class C
            {
                void M(string s)
                {
                    if (s == {|#0:String.Empty|}) { }
                }
            }
            """;

        var fixedSource = """
            using System;

            public class C
            {
                void M(string s)
                {
                    if (s == string.Empty) { }
                }
            }
            """;

        await CodeFixVerify.VerifyCodeFixAsync(test, CodeFixVerify.Diagnostic().WithLocation(0).WithArguments("String.Empty"), fixedSource);
    }

    /// <summary>
    /// ヨーダ記法（左辺が String.Empty）でも、Fixが正しく小文字 string.Empty へ書き換えることを確認する
    /// </summary>
    [Fact]
    public async Task Fix_ReplacesYodaConditionWithLowercase()
    {
        var test = """
            using System;

            public class C
            {
                void M(string s)
                {
                    if ({|#0:String.Empty|} == s) { }
                }
            }
            """;

        var fixedSource = """
            using System;

            public class C
            {
                void M(string s)
                {
                    if (string.Empty == s) { }
                }
            }
            """;

        await CodeFixVerify.VerifyCodeFixAsync(test, CodeFixVerify.Diagnostic().WithLocation(0).WithArguments("String.Empty"), fixedSource);
    }

    /// <summary>
    /// Fix適用後も、式の直後にあるコメントが消えずに残ることを確認する（AnalyzerTestAppの意地悪ケースを移植）
    /// </summary>
    [Fact]
    public async Task Fix_PreservesTrailingComment()
    {
        var test = """
            using System;

            public class C
            {
                void M(string s)
                {
                    if (s == {|#0:String.Empty|} /* keep me */ ) { }
                }
            }
            """;

        var fixedSource = """
            using System;

            public class C
            {
                void M(string s)
                {
                    if (s == string.Empty /* keep me */ ) { }
                }
            }
            """;

        await CodeFixVerify.VerifyCodeFixAsync(test, CodeFixVerify.Diagnostic().WithLocation(0).WithArguments("String.Empty"), fixedSource);
    }

    /// <summary>
    /// 完全修飾名（System.String.Empty）は、Expressionが単純な識別子「String」ではないため
    /// 検知対象外（誤検知しない）ことを確認する
    /// </summary>
    [Fact]
    public async Task QualifiedSystemStringEmpty_DoesNotReportDiagnostic()
    {
        var test = """
            using System;

            public class C
            {
                void M(string s)
                {
                    if (s == System.String.Empty) { }
                }
            }
            """;

        await Verify.VerifyAnalyzerAsync(test);
    }

    /// <summary>
    /// 「String」以外の識別子が持つ「Empty」というメンバー（自作クラスのstaticフィールド等）は
    /// 検知対象外（誤検知しない）ことを確認する
    /// </summary>
    [Fact]
    public async Task CustomClassMemberNamedEmpty_DoesNotReportDiagnostic()
    {
        var test = """
            using System;

            public class Wrapper
            {
                public static string Empty = "";
            }

            public class C
            {
                void M(string s)
                {
                    if (s == Wrapper.Empty) { }
                }
            }
            """;

        await Verify.VerifyAnalyzerAsync(test);
    }

    /// <summary>
    /// メンバー名が「Empty」ではないメンバーアクセス（例：StringBuilder.Length）は
    /// 検知対象外（誤検知しない）ことを確認する
    /// </summary>
    [Fact]
    public async Task MemberAccessWithDifferentName_DoesNotReportDiagnostic()
    {
        var test = """
            using System.Text;

            public class C
            {
                void M(StringBuilder sb)
                {
                    if (sb.Length == 0) { }
                }
            }
            """;

        await Verify.VerifyAnalyzerAsync(test);
    }

    /// <summary>
    /// 括弧で囲まれた「(String.Empty)」は、MemberAccessExpressionSyntaxとして直接現れないため
    /// 検知対象外（誤検知しない）ことを確認する
    /// </summary>
    [Fact]
    public async Task ParenthesizedStringEmpty_DoesNotReportDiagnostic()
    {
        var test = """
            using System;

            public class C
            {
                void M(string s)
                {
                    if ((String.Empty) == s) { }
                }
            }
            """;

        await Verify.VerifyAnalyzerAsync(test);
    }

    /// <summary>
    /// 両辺とも大文字「String.Empty」の場合、左辺側のみが診断されることを確認する（境界値）
    /// </summary>
    [Fact]
    public async Task BothSidesStringEmpty_ReportsOnlyOnLeftSide()
    {
        var test = """
            using System;

            public class C
            {
                void M()
                {
                    if ({|#0:String.Empty|} == String.Empty) { }
                }
            }
            """;

        await Verify.VerifyAnalyzerAsync(test, Verify.Diagnostic().WithLocation(0).WithArguments("String.Empty"));
    }

    /// <summary>
    /// != 演算子のケースでも、Fixが正しく小文字 string.Empty へ書き換えることを確認する
    /// </summary>
    [Fact]
    public async Task Fix_ReplacesNotEqualsWithLowercase()
    {
        var test = """
            using System;

            public class C
            {
                void M(string s)
                {
                    if (s != {|#0:String.Empty|}) { }
                }
            }
            """;

        var fixedSource = """
            using System;

            public class C
            {
                void M(string s)
                {
                    if (s != string.Empty) { }
                }
            }
            """;

        await CodeFixVerify.VerifyCodeFixAsync(test, CodeFixVerify.Diagnostic().WithLocation(0).WithArguments("String.Empty"), fixedSource);
    }
}

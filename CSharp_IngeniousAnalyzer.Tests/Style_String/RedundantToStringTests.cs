using CSharp_IngeniousAnalyzer.Style_String;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
using Microsoft.CodeAnalysis.Testing.Verifiers;

namespace CSharp_IngeniousAnalyzer.Tests.Style_String;

using Verify = CSharpAnalyzerVerifier<RedundantToString, XUnitVerifier>;
using CodeFixVerify = CSharpCodeFixVerifier<RedundantToString, RedundantToStringFix, XUnitVerifier>;

/// <summary>
/// STR003（RedundantToString）の検知・Fix動作を検証するテスト
/// </summary>
public class RedundantToStringTests
{
    /// <summary>
    /// 非null許容のstring型に対する冗長なToString()呼び出しで診断が出ることを確認する
    /// </summary>
    [Fact]
    public async Task NonNullableString_ReportsDiagnostic()
    {
        var test = """
            #nullable enable

            public class C
            {
                void M()
                {
                    string s1 = "abc";
                    var r1 = {|#0:s1.ToString()|};
                }
            }
            """;

        await Verify.VerifyAnalyzerAsync(test, Verify.Diagnostic().WithLocation(0).WithArguments("s1"));
    }

    /// <summary>
    /// 早期return（if (s2 is null) return;）によるnull安全フローの絞り込みでnull非許容が確定した後は診断が出ることを確認する
    /// </summary>
    [Fact]
    public async Task NullableNarrowedByEarlyReturnGuard_ReportsDiagnostic()
    {
        var test = """
            #nullable enable

            public class C
            {
                string? GetNullableString() => null;

                void M()
                {
                    string? s2 = GetNullableString();
                    if (s2 is null) return;
                    var r2 = {|#0:s2.ToString()|};
                }
            }
            """;

        await Verify.VerifyAnalyzerAsync(test, Verify.Diagnostic().WithLocation(0).WithArguments("s2"));
    }

    /// <summary>
    /// nullになり得ることが確定できない（ガードのない）null許容string型への呼び出しは、誤検知回避のため診断が出ないことを確認する
    /// </summary>
    [Fact]
    public async Task UnnarrowedNullableString_DoesNotReportDiagnostic()
    {
        var test = """
            #nullable enable

            public class C
            {
                string? GetNullableString() => null;

                void M()
                {
                    string? s3 = GetNullableString();
                    var r3 = s3.ToString();
                }
            }
            """;

        await Verify.VerifyAnalyzerAsync(test);
    }

    /// <summary>
    /// null条件演算子（s4?.ToString()）はnull安全のため、フロー証明なしでも診断が出ることを確認する
    /// </summary>
    [Fact]
    public async Task ConditionalAccessToString_ReportsDiagnostic()
    {
        var test = """
            #nullable enable

            public class C
            {
                string? GetNullableString() => null;

                void M()
                {
                    string? s4 = GetNullableString();
                    var r4 = {|#0:s4?.ToString()|};
                }
            }
            """;

        await Verify.VerifyAnalyzerAsync(test, Verify.Diagnostic().WithLocation(0).WithArguments("s4"));
    }

    /// <summary>
    /// メソッドチェーンの戻り値（識別子でない式）に対する呼び出しでも診断が出ることを確認する
    /// </summary>
    [Fact]
    public async Task MethodChainResult_ReportsDiagnostic()
    {
        var test = """
            #nullable enable

            public class C
            {
                string GetName() => "Yuji";

                void M()
                {
                    var r5 = {|#0:GetName().ToString()|}.Length;
                }
            }
            """;

        await Verify.VerifyAnalyzerAsync(test, Verify.Diagnostic().WithLocation(0).WithArguments("GetName()"));
    }

    /// <summary>
    /// string型以外（int）への呼び出しは対象外で、診断が出ないことを確認する
    /// </summary>
    [Fact]
    public async Task NonStringType_DoesNotReportDiagnostic()
    {
        var test = """
            #nullable enable

            public class C
            {
                void M()
                {
                    int n = 5;
                    var r6 = n.ToString();
                }
            }
            """;

        await Verify.VerifyAnalyzerAsync(test);
    }

    /// <summary>
    /// 書式指定付き呼び出し（引数あり、例: ToString("D2")）は対象外で、診断が出ないことを確認する
    /// </summary>
    [Fact]
    public async Task FormattedToStringWithArgument_DoesNotReportDiagnostic()
    {
        var test = """
            #nullable enable

            public class C
            {
                void M()
                {
                    int n = 5;
                    var r7 = n.ToString("D2");
                }
            }
            """;

        await Verify.VerifyAnalyzerAsync(test);
    }

    /// <summary>
    /// objectへキャストすると静的型がstringではなくなるため、既知の仕様の境界線として診断が出ないことを確認する
    /// </summary>
    [Fact]
    public async Task CastToObject_DoesNotReportDiagnostic()
    {
        var test = """
            #nullable enable

            public class C
            {
                void M()
                {
                    string s1 = "abc";
                    var r8 = ((object)s1).ToString();
                }
            }
            """;

        await Verify.VerifyAnalyzerAsync(test);
    }

    /// <summary>
    /// 文字列結合の結果（常に非nullが保証される式）に対しても診断が出ることを確認する
    /// </summary>
    [Fact]
    public async Task StringConcatenationResult_ReportsDiagnostic()
    {
        var test = """
            #nullable enable

            public class C
            {
                string? GetNullableString() => null;

                void M()
                {
                    string? s9 = GetNullableString();
                    var r9 = {|#0:(s9 + "").ToString()|};
                }
            }
            """;

        await Verify.VerifyAnalyzerAsync(test, Verify.Diagnostic().WithLocation(0).WithArguments("(s9 + \"\")"));
    }

    /// <summary>
    /// Fixが通常のToString()呼び出し（s.ToString()）を削除し、受信側の式のみへ書き換えることを確認する
    /// </summary>
    [Fact]
    public async Task Fix_RemovesRedundantToString()
    {
        var test = """
            #nullable enable

            public class C
            {
                void M()
                {
                    string s1 = "abc";
                    var r1 = {|#0:s1.ToString()|};
                }
            }
            """;

        var fixedSource = """
            #nullable enable

            public class C
            {
                void M()
                {
                    string s1 = "abc";
                    var r1 = s1;
                }
            }
            """;

        await CodeFixVerify.VerifyCodeFixAsync(test, CodeFixVerify.Diagnostic().WithLocation(0).WithArguments("s1"), fixedSource);
    }

    /// <summary>
    /// Fixがnull条件演算子のToString()呼び出し（s?.ToString()）も削除できることを確認する
    /// </summary>
    [Fact]
    public async Task Fix_RemovesRedundantConditionalToString()
    {
        var test = """
            #nullable enable

            public class C
            {
                string? GetNullableString() => null;

                void M()
                {
                    string? s4 = GetNullableString();
                    var r4 = {|#0:s4?.ToString()|};
                }
            }
            """;

        var fixedSource = """
            #nullable enable

            public class C
            {
                string? GetNullableString() => null;

                void M()
                {
                    string? s4 = GetNullableString();
                    var r4 = s4;
                }
            }
            """;

        await CodeFixVerify.VerifyCodeFixAsync(test, CodeFixVerify.Diagnostic().WithLocation(0).WithArguments("s4"), fixedSource);
    }

    /// <summary>
    /// 冗長なToString()呼び出しがメソッド引数へ直接渡されている場合（例: Foo(s.ToString())）でも
    /// Fixが正しく登録・適用されることを確認する回帰テスト。
    /// この場合、呼び出し自体のスパンが引数(ArgumentSyntax)のスパンと完全に一致（タイ）するため、
    /// FindNodeにgetInnermostNodeForTie: trueを渡していないとFixが（例外を投げずに）
    /// 静かに登録されなくなる不具合があった。
    /// </summary>
    [Fact]
    public async Task Fix_RemovesRedundantToString_WhenPassedAsBareMethodArgument()
    {
        var test = """
            #nullable enable

            public class C
            {
                void Foo(string value) { }

                void M()
                {
                    string s1 = "abc";
                    Foo({|#0:s1.ToString()|});
                }
            }
            """;

        var fixedSource = """
            #nullable enable

            public class C
            {
                void Foo(string value) { }

                void M()
                {
                    string s1 = "abc";
                    Foo(s1);
                }
            }
            """;

        await CodeFixVerify.VerifyCodeFixAsync(test, CodeFixVerify.Diagnostic().WithLocation(0).WithArguments("s1"), fixedSource);
    }
}

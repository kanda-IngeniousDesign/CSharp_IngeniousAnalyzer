using CSharp_IngeniousAnalyzer.Style_String;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
using Microsoft.CodeAnalysis.Testing.Verifiers;

namespace CSharp_IngeniousAnalyzer.Tests.Style_String;

using Verify = CSharpAnalyzerVerifier<NameOf, XUnitVerifier>;
using CodeFixVerify = CSharpCodeFixVerifier<NameOf, NameOfFix, XUnitVerifier>;

/// <summary>
/// STR002（NameOf）の検知・Fix動作を検証するテスト
/// </summary>
public class NameOfTests
{
    /// <summary>
    /// 基本パターン：文字列連結中のハードコードされた変数名（"userId: " + userId）で診断が出ることを確認する
    /// </summary>
    [Fact]
    public async Task StringConcatenation_MatchesVariableName_ReportsDiagnostic()
    {
        var test = """
            public class C
            {
                void M(string userId)
                {
                    Log({|#0:"userId: "|} + userId);
                }
                void Log(string s) { }
            }
            """;

        await Verify.VerifyAnalyzerAsync(test, Verify.Diagnostic().WithLocation(0).WithArguments("userId"));
    }

    /// <summary>
    /// 基本パターン：補間文字列のテキスト部分が変数名と一致する（$"userId={userId}"）場合に診断が出ることを確認する
    /// </summary>
    [Fact]
    public async Task InterpolatedStringText_MatchesVariableName_ReportsDiagnostic()
    {
        var test = """
            public class C
            {
                void M(string userId)
                {
                    Log($"{|#0:userId=|}{userId}");
                }
                void Log(string s) { }
            }
            """;

        await Verify.VerifyAnalyzerAsync(test, Verify.Diagnostic().WithLocation(0).WithArguments("userId"));
    }

    /// <summary>
    /// 素人エイリアン：複数変数をまとめてログ出力する際、片方だけ生の変数名になっているケースでは、
    /// 生のまま（age=）の部分だけが検知され、すでに nameof() 済みの側（userId）は対象外になることを確認する
    /// </summary>
    [Fact]
    public async Task MultipleVariablesInInterpolatedString_OnlyRawVariableNameReportsDiagnostic()
    {
        var test = """
            public class C
            {
                void M(string userId, int age)
                {
                    Log($"{|#0:age=|}{age}, {nameof(userId)}={userId}");
                }
                void Log(string s) { }
            }
            """;

        await Verify.VerifyAnalyzerAsync(test, Verify.Diagnostic().WithLocation(0).WithArguments("age"));
    }

    /// <summary>
    /// すでに nameof() を使っている正しい書き方では、nameof呼び出し自体は文字列リテラルではないため対象外（誤検知しない）ことを確認する
    /// </summary>
    [Fact]
    public async Task AlreadyUsingNameof_DoesNotReportDiagnostic()
    {
        var test = """
            public class C
            {
                void M(string userId)
                {
                    Log($"{nameof(userId)}={userId}");
                }
                void Log(string s) { }
            }
            """;

        await Verify.VerifyAnalyzerAsync(test);
    }

    /// <summary>
    /// 対応する変数名が同じ文の中で実際には（識別子として）使われていない場合は対象外（誤検知しない）ことを確認する
    /// </summary>
    [Fact]
    public async Task VariableNotUsedElsewhereInStatement_DoesNotReportDiagnostic()
    {
        var test = """
            public class C
            {
                void M(string userId)
                {
                    Log("userId");
                }
                void Log(string s) { }
            }
            """;

        await Verify.VerifyAnalyzerAsync(test);
    }

    /// <summary>
    /// 変数宣言の初期化子である場合は対象外（除外ロジックの確認）であることを確認する
    /// </summary>
    [Fact]
    public async Task VariableDeclarationInitializer_DoesNotReportDiagnostic()
    {
        var test = """
            public class C
            {
                void M(string userId)
                {
                    var userIdCopy = "userId" + userId;
                    Log(userIdCopy);
                }
                void Log(string s) { }
            }
            """;

        await Verify.VerifyAnalyzerAsync(test);
    }

    /// <summary>
    /// 文全体が単純代入である場合は対象外（除外ロジックの確認）であることを確認する
    /// </summary>
    [Fact]
    public async Task PlainAssignmentStatement_DoesNotReportDiagnostic()
    {
        var test = """
            public class C
            {
                void M(string userId)
                {
                    string label;
                    label = "userId" + userId;
                    Log(label);
                }
                void Log(string s) { }
            }
            """;

        await Verify.VerifyAnalyzerAsync(test);
    }

    /// <summary>
    /// 制御文（if）の条件式部分に含まれている場合は対象外（除外ロジックの確認）であることを確認する
    /// </summary>
    [Fact]
    public async Task ControlStatementCondition_DoesNotReportDiagnostic()
    {
        var test = """
            public class C
            {
                void M(string userId)
                {
                    if ("userId" == userId) { }
                }
            }
            """;

        await Verify.VerifyAnalyzerAsync(test);
    }

    /// <summary>
    /// Fixが、文字列連結中のハードコードされた変数名を、nameof()を埋め込んだ補間文字列へ書き換えることを確認する
    /// （"userId: " + userId → $"{nameof(userId)}: " + userId）
    /// </summary>
    [Fact]
    public async Task Fix_ReplacesLiteralConcatenationWithInterpolatedNameof()
    {
        var test = """
            public class C
            {
                void M(string userId)
                {
                    Log({|#0:"userId: "|} + userId);
                }
                void Log(string s) { }
            }
            """;

        var fixedSource = """
            public class C
            {
                void M(string userId)
                {
                    Log($"{nameof(userId)}: " + userId);
                }
                void Log(string s) { }
            }
            """;

        await CodeFixVerify.VerifyCodeFixAsync(test, CodeFixVerify.Diagnostic().WithLocation(0).WithArguments("userId"), fixedSource);
    }

    /// <summary>
    /// Fixが、補間文字列内のテキスト部分（userId=）を nameof() の埋め込みへ書き換えることを確認する
    /// （$"userId={userId}" → $"{nameof(userId)}={userId}"）
    /// </summary>
    [Fact]
    public async Task Fix_ReplacesInterpolatedTextWithNameof()
    {
        var test = """
            public class C
            {
                void M(string userId)
                {
                    Log($"{|#0:userId=|}{userId}");
                }
                void Log(string s) { }
            }
            """;

        var fixedSource = """
            public class C
            {
                void M(string userId)
                {
                    Log($"{nameof(userId)}={userId}");
                }
                void Log(string s) { }
            }
            """;

        await CodeFixVerify.VerifyCodeFixAsync(test, CodeFixVerify.Diagnostic().WithLocation(0).WithArguments("userId"), fixedSource);
    }

    /// <summary>
    /// Fixが、変数名と完全に一致するリテラルを、補間文字列を経由せずそのまま nameof() 呼び出し式に置き換えることを確認する
    /// （"userId" → nameof(userId)）
    /// </summary>
    [Fact]
    public async Task Fix_ReplacesExactLiteralMatchWithNameofInvocation()
    {
        var test = """
            public class C
            {
                void M(string userId)
                {
                    Log({|#0:"userId"|}, userId);
                }
                void Log(string first, string second) { }
            }
            """;

        var fixedSource = """
            public class C
            {
                void M(string userId)
                {
                    Log(nameof(userId), userId);
                }
                void Log(string first, string second) { }
            }
            """;

        await CodeFixVerify.VerifyCodeFixAsync(test, CodeFixVerify.Diagnostic().WithLocation(0).WithArguments("userId"), fixedSource);
    }
}

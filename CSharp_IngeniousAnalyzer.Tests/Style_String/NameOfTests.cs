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

    /// <summary>
    /// 境界値：空文字列リテラル（""）は ExtractNodeText が空文字を返すため、トリム処理以前に対象外（誤検知しない）ことを確認する
    /// </summary>
    [Fact]
    public async Task EmptyStringLiteral_DoesNotReportDiagnostic()
    {
        var test = """
            public class C
            {
                void M(string userId)
                {
                    Log("" + userId);
                }
                void Log(string s) { }
            }
            """;

        await Verify.VerifyAnalyzerAsync(test);
    }

    /// <summary>
    /// 境界値：空白文字のみのリテラル（"   "）はトリムすると空文字になるため対象外（誤検知しない）ことを確認する
    /// </summary>
    [Fact]
    public async Task WhitespaceOnlyLiteral_DoesNotReportDiagnostic()
    {
        var test = """
            public class C
            {
                void M(string userId)
                {
                    Log("   " + userId);
                }
                void Log(string s) { }
            }
            """;

        await Verify.VerifyAnalyzerAsync(test);
    }

    /// <summary>
    /// トリム後の文字列が有効な識別子ではない（内部に空白を含む）場合は対象外（誤検知しない）ことを確認する
    /// </summary>
    [Fact]
    public async Task InvalidIdentifierText_DoesNotReportDiagnostic()
    {
        var test = """
            public class C
            {
                void M(string userId)
                {
                    Log("user Id" + userId);
                }
                void Log(string s) { }
            }
            """;

        await Verify.VerifyAnalyzerAsync(test);
    }

    /// <summary>
    /// メソッド内にパラメータもローカル変数も存在しない場合は、比較対象となる変数名が皆無のため対象外（誤検知しない）ことを確認する
    /// </summary>
    [Fact]
    public async Task NoLocalVariablesOrParameters_DoesNotReportDiagnostic()
    {
        var test = """
            public class C
            {
                void M()
                {
                    Log("userId");
                }
                void Log(string s) { }
            }
            """;

        await Verify.VerifyAnalyzerAsync(test);
    }

    /// <summary>
    /// トリム後の文字列がどのローカル変数・パラメータ名とも一致しない場合は対象外（誤検知しない）ことを確認する
    /// </summary>
    [Fact]
    public async Task LiteralDoesNotMatchAnyVariable_DoesNotReportDiagnostic()
    {
        var test = """
            public class C
            {
                void M(string userId)
                {
                    Log("otherName");
                }
                void Log(string s) { }
            }
            """;

        await Verify.VerifyAnalyzerAsync(test);
    }

    /// <summary>
    /// フィールド初期化子のように、そもそも文の中（StatementSyntax）に含まれない箇所の文字列リテラルは対象外（誤検知しない）ことを確認する
    /// </summary>
    [Fact]
    public async Task FieldInitializer_DoesNotReportDiagnostic()
    {
        var test = """
            public class C
            {
                private string userId = "userId";
                public string Get() => userId;
            }
            """;

        await Verify.VerifyAnalyzerAsync(test);
    }

    /// <summary>
    /// 文全体としては代入文ではないが、インデクサーへの代入式（dict["userId"] = ...）の左辺に含まれる場合は
    /// 代入先（IsAssignmentTarget）として対象外（誤検知しない）ことを確認する
    /// </summary>
    [Fact]
    public async Task NestedAssignmentExpressionTarget_DoesNotReportDiagnostic()
    {
        var test = """
            public class C
            {
                string this[string key] { set { } }

                void M(string userId)
                {
                    Log(this["userId"] = userId);
                }
                void Log(string s) { }
            }
            """;

        await Verify.VerifyAnalyzerAsync(test);
    }

    /// <summary>
    /// while文の条件式部分に含まれている場合は対象外（除外ロジックの確認）であることを確認する
    /// </summary>
    [Fact]
    public async Task WhileStatementCondition_DoesNotReportDiagnostic()
    {
        var test = """
            public class C
            {
                void M(string userId)
                {
                    while ("userId" == userId) { }
                }
            }
            """;

        await Verify.VerifyAnalyzerAsync(test);
    }

    /// <summary>
    /// do-while文の条件式部分に含まれている場合は対象外（除外ロジックの確認）であることを確認する
    /// </summary>
    [Fact]
    public async Task DoWhileStatementCondition_DoesNotReportDiagnostic()
    {
        var test = """
            public class C
            {
                void M(string userId)
                {
                    do { } while ("userId" == userId);
                }
            }
            """;

        await Verify.VerifyAnalyzerAsync(test);
    }

    /// <summary>
    /// for文の条件式部分に含まれている場合は対象外（除外ロジックの確認）であることを確認する
    /// </summary>
    [Fact]
    public async Task ForStatementCondition_DoesNotReportDiagnostic()
    {
        var test = """
            public class C
            {
                void M(string userId)
                {
                    for (int i = 0; "userId" == userId; i++) { }
                }
            }
            """;

        await Verify.VerifyAnalyzerAsync(test);
    }

    /// <summary>
    /// switch文の対象式（Expression）部分に含まれている場合は対象外（除外ロジックの確認）であることを確認する
    /// </summary>
    [Fact]
    public async Task SwitchStatementExpression_DoesNotReportDiagnostic()
    {
        var test = """
            public class C
            {
                void M(string userId)
                {
                    switch ("userId" + userId)
                    {
                        default:
                            break;
                    }
                }
            }
            """;

        await Verify.VerifyAnalyzerAsync(test);
    }

    /// <summary>
    /// 素人エイリアン：if文の「本体（条件式ではない）」の中にリテラルがあり、その if がさらに while の本体に
    /// ネストしている場合、条件式チェックはどちらの階層でも不一致となって素通りするため、通常どおり診断が出ることを確認する
    /// （IsInControlStatementCondition のループが複数階層の制御文を跨いで上に辿るケース）
    /// </summary>
    [Fact]
    public async Task IfBodyInsideWhileLoop_ReportsDiagnostic()
    {
        var test = """
            public class C
            {
                void M(string userId)
                {
                    while (true)
                    {
                        if (userId != null)
                        {
                            Log({|#0:"userId: "|} + userId);
                        }
                    }
                }
                void Log(string s) { }
            }
            """;

        await Verify.VerifyAnalyzerAsync(test, Verify.Diagnostic().WithLocation(0).WithArguments("userId"));
    }

    /// <summary>
    /// 素人エイリアン：for → do-while → switch と、条件式ではない本体の中に何重にもネストしたリテラルは、
    /// いずれの階層の条件式判定にも一致しないまま最上位まで辿り着くため、通常どおり診断が出ることを確認する
    /// </summary>
    [Fact]
    public async Task NestedForDoSwitchBodies_ReportsDiagnostic()
    {
        var test = """
            public class C
            {
                void M(string userId)
                {
                    for (int i = 0; i < 1; i++)
                    {
                        do
                        {
                            switch (i)
                            {
                                default:
                                    Log({|#0:"userId: "|} + userId);
                                    break;
                            }
                        } while (i < 1);
                    }
                }
                void Log(string s) { }
            }
            """;

        await Verify.VerifyAnalyzerAsync(test, Verify.Diagnostic().WithLocation(0).WithArguments("userId"));
    }

    /// <summary>
    /// パラメータではなく、メソッド内の別の文で宣言されたローカル変数の名前と一致する場合にも診断が出ることを確認する
    /// （GetLocalVariableNames がメソッド本体全体からローカル変数宣言を収集する経路の確認）
    /// </summary>
    [Fact]
    public async Task LocalVariableDeclaredElsewhereInMethod_ReportsDiagnostic()
    {
        var test = """
            public class C
            {
                void M()
                {
                    string userId = GetValue();
                    Log({|#0:"userId: "|} + userId);
                }
                string GetValue() => "x";
                void Log(string s) { }
            }
            """;

        await Verify.VerifyAnalyzerAsync(test, Verify.Diagnostic().WithLocation(0).WithArguments("userId"));
    }

    /// <summary>
    /// Fixが、パラメータではなくメソッド内の別の文で宣言されたローカル変数についても、
    /// nameof()を埋め込んだ補間文字列へ正しく書き換えることを確認する
    /// </summary>
    [Fact]
    public async Task Fix_ReplacesLocalVariableLiteralWithInterpolatedNameof()
    {
        var test = """
            public class C
            {
                void M()
                {
                    string userId = GetValue();
                    Log({|#0:"userId: "|} + userId);
                }
                string GetValue() => "x";
                void Log(string s) { }
            }
            """;

        var fixedSource = """
            public class C
            {
                void M()
                {
                    string userId = GetValue();
                    Log($"{nameof(userId)}: " + userId);
                }
                string GetValue() => "x";
                void Log(string s) { }
            }
            """;

        await CodeFixVerify.VerifyCodeFixAsync(test, CodeFixVerify.Diagnostic().WithLocation(0).WithArguments("userId"), fixedSource);
    }
}

using CSharp_IngeniousAnalyzer.Style_Unicode;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Testing;
using Microsoft.CodeAnalysis.Testing.Verifiers;

namespace CSharp_IngeniousAnalyzer.Tests.Style_Unicode;

using Verify = CSharpAnalyzerVerifier<InvisibleUnicode, XUnitVerifier>;
using CodeFixVerify = CSharpCodeFixVerifier<InvisibleUnicode, InvisibleUnicodeFix, XUnitVerifier>;

/// <summary>
/// UNI001（InvisibleUnicode）の検知・Ignore Fix動作を検証するテスト。
/// 不可視文字をテストコードに直接書くと目視で確認できないため、すべて定数経由で埋め込む。
/// </summary>
public class InvisibleUnicodeTests
{
    private const string Nbsp = "\u00A0";
    private const string Zwsp = "\u200B";
    private const string Zwj = "\u200D";
    private const string Rlo = "\u202E";
    private const string Bom = "\uFEFF";
    private const string Esc = "\u001B";
    private const string FormFeed = "\u000C";
    private const string C1Control = "\u0080";
    private const string NextLine = "\u0085";
    private const string LineSeparator = "\u2028";
    private const string CombiningDakuten = "\u3099";
    private const string IdeographicSpace = "\u3000";
    private const string TagLatinA = "\U000E0041";

    // ================================================================
    // 検知対象の文字（Fixは書き換えず、直近の文・メンバーの直前に「// Ignore UNI001」を挿入する）
    // ================================================================

    /// <summary>
    /// 文字列リテラル内のNBSPを検知し、Fixは文字列を書き換えず、文の直前にIgnoreコメントを挿入することを確認する（今回の実務事例）
    /// </summary>
    [Fact]
    public async Task NbspInStringLiteral_ReportsAndIgnoreInsertedBeforeStatement()
    {
        var test = $$"""
            public class C
            {
                void M()
                {
                    var s = "Hello{|#0:{{Nbsp}}|}World";
                }
            }
            """;
        var fixedCode = $$"""
            public class C
            {
                void M()
                {
                    // Ignore UNI001
                    var s = "Hello{{Nbsp}}World";
                }
            }
            """;

        await CodeFixVerify.VerifyCodeFixAsync(test, CodeFixVerify.Diagnostic().WithLocation(0).WithArguments("U+00A0", "NO-BREAK SPACE"), fixedCode);
    }

    /// <summary>
    /// 同じ文字が連続する場合は1件の診断にまとめることを確認する
    /// </summary>
    [Fact]
    public async Task ConsecutiveSameChars_ReportedOnce()
    {
        var test = $$"""
            public class C
            {
                string s = "A{|#0:{{Nbsp}}{{Nbsp}}{{Nbsp}}|}B";
            }
            """;

        await Verify.VerifyAnalyzerAsync(test, Verify.Diagnostic().WithLocation(0).WithArguments("U+00A0", "NO-BREAK SPACE"));
    }

    /// <summary>
    /// 異なる不可視文字が隣接する場合は文字ごとに別々の診断になり、
    /// 同じ文に対する一括修正（Fix All）ではIgnoreコメントが1つだけ挿入されることを確認する
    /// </summary>
    [Fact]
    public async Task AdjacentDifferentChars_ReportedSeparatelyAndSingleIgnoreInserted()
    {
        var test = $$"""
            public class C
            {
                string s = "A{|#0:{{Nbsp}}|}{|#1:{{Zwsp}}|}B";
            }
            """;
        var fixedCode = $$"""
            public class C
            {
                // Ignore UNI001
                string s = "A{{Nbsp}}{{Zwsp}}B";
            }
            """;

        await CodeFixVerify.VerifyCodeFixAsync(
            test,
            [
                CodeFixVerify.Diagnostic().WithLocation(0).WithArguments("U+00A0", "NO-BREAK SPACE"),
                CodeFixVerify.Diagnostic().WithLocation(1).WithArguments("U+200B", "ZERO WIDTH SPACE"),
            ],
            fixedCode);
    }

    /// <summary>
    /// 文字列・文字リテラルの各種形式（通常・文字・補間・逐語的・UTF-8）内の不可視文字を検知することを確認する
    /// </summary>
    [Fact]
    public async Task InvisibleCharsInVariousLiterals_Reported()
    {
        var test = $$"""
            using System;

            public class C
            {
                bool M(string id) => id == "ABC{|#0:{{Zwsp}}|}";
                char c = '{|#1:{{Nbsp}}|}';
                string N(int n) => $"{n}{|#2:{{Nbsp}}|}件";
                string p = @"C:\work{|#3:{{Nbsp}}|}dir";
                ReadOnlySpan<byte> U() => "A{|#4:{{Nbsp}}|}B"u8;
            }
            """;

        await new CSharpAnalyzerTest<InvisibleUnicode, XUnitVerifier>
        {
            TestCode = test,
            ReferenceAssemblies = ReferenceAssemblies.Net.Net80,
            ExpectedDiagnostics =
            {
                Verify.Diagnostic().WithLocation(0).WithArguments("U+200B", "ZERO WIDTH SPACE"),
                Verify.Diagnostic().WithLocation(1).WithArguments("U+00A0", "NO-BREAK SPACE"),
                Verify.Diagnostic().WithLocation(2).WithArguments("U+00A0", "NO-BREAK SPACE"),
                Verify.Diagnostic().WithLocation(3).WithArguments("U+00A0", "NO-BREAK SPACE"),
                Verify.Diagnostic().WithLocation(4).WithArguments("U+00A0", "NO-BREAK SPACE"),
            },
        }.RunAsync();
    }

    /// <summary>
    /// 複数行の生文字列リテラル（補間内のネストした文字列を含む）の途中にある文字でも、
    /// Ignoreコメントは文字列の中ではなく文の直前に挿入され、文字列の内容が変わらないことを確認する
    /// </summary>
    [Fact]
    public async Task MultiLineRawString_IgnoreInsertedOutsideString()
    {
        var test = $$$$""""
            public class C
            {
                string M(string x)
                {
                    return $"""
                        line1
                        {x + "a{|#0:{{{{Nbsp}}}}|}b"}
                        line{|#1:{{{{Zwsp}}}}|}3
                        """;
                }
            }
            """";
        var fixedCode = $$$$""""
            public class C
            {
                string M(string x)
                {
                    // Ignore UNI001
                    return $"""
                        line1
                        {x + "a{{{{Nbsp}}}}b"}
                        line{{{{Zwsp}}}}3
                        """;
                }
            }
            """";

        await new CSharpCodeFixTest<InvisibleUnicode, InvisibleUnicodeFix, XUnitVerifier>
        {
            TestCode = test,
            FixedCode = fixedCode,
            ReferenceAssemblies = ReferenceAssemblies.Net.Net80,
            ExpectedDiagnostics =
            {
                CodeFixVerify.Diagnostic().WithLocation(0).WithArguments("U+00A0", "NO-BREAK SPACE"),
                CodeFixVerify.Diagnostic().WithLocation(1).WithArguments("U+200B", "ZERO WIDTH SPACE"),
            },
        }.RunAsync();
    }

    /// <summary>
    /// インデントに混入したNBSPを検知することを確認する（C#ではNBSPも空白として扱われるためコンパイルが通り、気付きにくい）。
    /// 挿入するIgnoreコメント行のインデントには、NBSPをコピーせず半角スペースを使うことも確認する
    /// </summary>
    [Fact]
    public async Task NbspInIndentation_ReportsAndIgnoreUsesPlainSpaces()
    {
        var test = $$"""
            public class C
            {
                void M()
                {
                  {|#0:{{Nbsp}}{{Nbsp}}|}var x = 1;
                }
            }
            """;
        var fixedCode = $$"""
            public class C
            {
                void M()
                {
                    // Ignore UNI001
                  {{Nbsp}}{{Nbsp}}var x = 1;
                }
            }
            """;

        await CodeFixVerify.VerifyCodeFixAsync(test, CodeFixVerify.Diagnostic().WithLocation(0).WithArguments("U+00A0", "NO-BREAK SPACE"), fixedCode);
    }

    /// <summary>
    /// トークン間（演算子の前後など）に混入したNBSPを検知することを確認する
    /// </summary>
    [Fact]
    public async Task NbspBetweenTokens_Reported()
    {
        var test = $$"""
            public class C
            {
                int x ={|#0:{{Nbsp}}|}1;
            }
            """;

        await Verify.VerifyAnalyzerAsync(test, Verify.Diagnostic().WithLocation(0).WithArguments("U+00A0", "NO-BREAK SPACE"));
    }

    /// <summary>
    /// 識別子内のゼロ幅文字（C#の識別子として有効なためコンパイルが通る）を検知し、フィールドの直前にIgnoreコメントを挿入することを確認する
    /// </summary>
    [Fact]
    public async Task ZeroWidthJoinerInIdentifier_ReportsAndIgnoreInsertedBeforeField()
    {
        var test = $$"""
            public class C
            {
                int va{|#0:{{Zwj}}|}lue = 0;
            }
            """;
        var fixedCode = $$"""
            public class C
            {
                // Ignore UNI001
                int va{{Zwj}}lue = 0;
            }
            """;

        await CodeFixVerify.VerifyCodeFixAsync(test, CodeFixVerify.Diagnostic().WithLocation(0).WithArguments("U+200D", "ZERO WIDTH JOINER"), fixedCode);
    }

    /// <summary>
    /// コメント内の双方向制御文字（Trojan Source攻撃：表示上のコード順序を偽装できる）を検知することを確認する
    /// </summary>
    [Fact]
    public async Task BidiOverrideInComment_Reported()
    {
        var test = $$"""
            public class C
            {
                bool M(bool isAdmin)
                {
                    /*{|#0:{{Rlo}}|} } if (isAdmin) { */
                    return isAdmin;
                }
            }
            """;

        await Verify.VerifyAnalyzerAsync(test, Verify.Diagnostic().WithLocation(0).WithArguments("U+202E", "RIGHT-TO-LEFT OVERRIDE"));
    }

    /// <summary>
    /// 文の上の説明コメント内の文字は、Ignoreコメントが説明コメントより下（文の直前）に挿入されることを確認する
    /// </summary>
    [Fact]
    public async Task CharInCommentAboveStatement_IgnoreInsertedBelowComment()
    {
        var test = $$"""
            public class C
            {
                void M()
                {
                    // 説明{|#0:{{Zwsp}}|}
                    var x = 1;
                }
            }
            """;
        var fixedCode = $$"""
            public class C
            {
                void M()
                {
                    // 説明{{Zwsp}}
                    // Ignore UNI001
                    var x = 1;
                }
            }
            """;

        await CodeFixVerify.VerifyCodeFixAsync(test, CodeFixVerify.Diagnostic().WithLocation(0).WithArguments("U+200B", "ZERO WIDTH SPACE"), fixedCode);
    }

    /// <summary>
    /// ドキュメントコメント内の文字は、Ignoreコメントがドキュメントコメントとメソッドの間に挿入され、
    /// ドキュメントコメントがメソッドから切り離されない（CS1587が出ない）ことを確認する
    /// </summary>
    [Fact]
    public async Task CharInDocComment_IgnoreInsertedBetweenDocCommentAndMember()
    {
        var test = $$"""
            internal class C
            {
                /// <summary>
                /// 説明{|#0:{{Nbsp}}|}文
                /// </summary>
                public void M() { }
            }
            """;
        var fixedCode = $$"""
            internal class C
            {
                /// <summary>
                /// 説明{{Nbsp}}文
                /// </summary>
                // Ignore UNI001
                public void M() { }
            }
            """;

        await new CSharpCodeFixTest<InvisibleUnicode, InvisibleUnicodeFix, XUnitVerifier>
        {
            TestCode = test,
            FixedCode = fixedCode,
            CompilerDiagnostics = CompilerDiagnostics.Warnings,
            ExpectedDiagnostics = { CodeFixVerify.Diagnostic().WithLocation(0).WithArguments("U+00A0", "NO-BREAK SPACE") },
            SolutionTransforms =
            {
                // ドキュメントコメントを構造化トリビアとして解析させ、CS1587等の警告も検証対象にする
                (solution, projectId) =>
                {
                    var options = solution.GetProject(projectId)!.ParseOptions!.WithDocumentationMode(DocumentationMode.Diagnose);
                    return solution.WithProjectParseOptions(projectId, options);
                },
            },
        }.RunAsync();
    }

    /// <summary>
    /// #if で無効化されたコード内の不可視文字も検知することを確認する
    /// </summary>
    [Fact]
    public async Task NbspInDisabledText_Reported()
    {
        var test = $$"""
            public class C
            {
            #if NEVER_DEFINED
                int x ={|#0:{{Nbsp}}|}1;
            #endif
            }
            """;

        await Verify.VerifyAnalyzerAsync(test, Verify.Diagnostic().WithLocation(0).WithArguments("U+00A0", "NO-BREAK SPACE"));
    }

    /// <summary>
    /// 制御文字（C0・C1、改行扱いのNEXT LINEを含む）を検知することを確認する
    /// </summary>
    [Fact]
    public async Task ControlCharacters_Reported()
    {
        var test = $$"""
            public class C
            {
                string a = "{|#0:{{Esc}}|}[0m";
                string b = "x{|#1:{{C1Control}}|}y";
                string c = @"x{|#2:{{NextLine}}|}y";
            {|#3:{{FormFeed}}|}
            }
            """;

        await Verify.VerifyAnalyzerAsync(
            test,
            Verify.Diagnostic().WithLocation(0).WithArguments("U+001B", "ESCAPE"),
            Verify.Diagnostic().WithLocation(1).WithArguments("U+0080", "C1 CONTROL CHARACTER"),
            Verify.Diagnostic().WithLocation(2).WithArguments("U+0085", "NEXT LINE"),
            Verify.Diagnostic().WithLocation(3).WithArguments("U+000C", "FORM FEED"));
    }

    /// <summary>
    /// 結合用の濁点（「か」＋U+3099 で「が」と同じ見た目になる）を検知することを確認する
    /// </summary>
    [Fact]
    public async Task CombiningDakuten_Reported()
    {
        var test = $$"""
            public class C
            {
                bool M(string name) => name == "か{|#0:{{CombiningDakuten}}|}いぎ資料.xlsx";
            }
            """;

        await Verify.VerifyAnalyzerAsync(test, Verify.Diagnostic().WithLocation(0).WithArguments("U+3099", "COMBINING KATAKANA-HIRAGANA VOICED SOUND MARK"));
    }

    /// <summary>
    /// BMP外の不可視文字（タグ文字。生成AIへの不可視の指示埋め込みに悪用されうる）を、サロゲートペアを1文字として検知することを確認する
    /// </summary>
    [Fact]
    public async Task TagCharacter_ReportedAsSingleCodePoint()
    {
        var test = $$"""
            public class C
            {
                string s = "OK{|#0:{{TagLatinA}}|}";
            }
            """;

        await Verify.VerifyAnalyzerAsync(test, Verify.Diagnostic().WithLocation(0).WithArguments("U+E0041", "TAG CHARACTER"));
    }

    /// <summary>
    /// ファイルの途中に混入したBOM（U+FEFF）を検知することを確認する
    /// </summary>
    [Fact]
    public async Task BomInMiddleOfFile_Reported()
    {
        var test = $$"""
            public class C
            {
                string s = "A{|#0:{{Bom}}|}B";
            }
            """;

        await Verify.VerifyAnalyzerAsync(test, Verify.Diagnostic().WithLocation(0).WithArguments("U+FEFF", "ZERO WIDTH NO-BREAK SPACE"));
    }

    // ================================================================
    // Ignore Fix の挿入位置
    // ================================================================

    /// <summary>
    /// 直前のトークンと同じ行にある文に対しては、改行してからIgnoreコメントを挿入し、
    /// コメントが直前の文の後続トリビアにならない（＝挿入後に診断が消える）ことを確認する
    /// </summary>
    [Fact]
    public async Task StatementOnSameLineAsPrevious_IgnoreInsertedOnNewLine()
    {
        var test = $$"""
            public class C
            {
                void M()
                {
                    var a = 1; var b = "x{|#0:{{Nbsp}}|}";
                }
            }
            """;
        var fixedCode = $$"""
            public class C
            {
                void M()
                {
                    var a = 1;
                    // Ignore UNI001
                    var b = "x{{Nbsp}}";
                }
            }
            """;

        await CodeFixVerify.VerifyCodeFixAsync(test, CodeFixVerify.Diagnostic().WithLocation(0).WithArguments("U+00A0", "NO-BREAK SPACE"), fixedCode);
    }

    /// <summary>
    /// メソッド本体の末尾（閉じ括弧の直前）のコメント内の文字は、本体の { の手前ではなく、メソッドの直前にIgnoreコメントを挿入することを確認する
    /// </summary>
    [Fact]
    public async Task CharBeforeClosingBraceOfMethodBody_IgnoreInsertedBeforeMethod()
    {
        var test = $$"""
            public class C
            {
                void M()
                {
                    var x = 1;
                    // 末尾{|#0:{{Zwsp}}|}
                }
            }
            """;
        var fixedCode = $$"""
            public class C
            {
                // Ignore UNI001
                void M()
                {
                    var x = 1;
                    // 末尾{{Zwsp}}
                }
            }
            """;

        await CodeFixVerify.VerifyCodeFixAsync(test, CodeFixVerify.Diagnostic().WithLocation(0).WithArguments("U+200B", "ZERO WIDTH SPACE"), fixedCode);
    }

    /// <summary>
    /// 行頭にインデントのない文の上に、インデントされた説明コメントがある場合でも、
    /// Ignoreコメントが説明コメントより下（文の直前）に挿入されることを確認する
    /// </summary>
    [Fact]
    public async Task UnindentedStatementBelowIndentedComment_IgnoreInsertedBelowComment()
    {
        var test = $$"""
            public class C
            {
                void M()
                {
                    // 説明
            var x = "a{|#0:{{Nbsp}}|}";
                }
            }
            """;
        var fixedCode = $$"""
            public class C
            {
                void M()
                {
                    // 説明
            // Ignore UNI001
            var x = "a{{Nbsp}}";
                }
            }
            """;

        await CodeFixVerify.VerifyCodeFixAsync(test, CodeFixVerify.Diagnostic().WithLocation(0).WithArguments("U+00A0", "NO-BREAK SPACE"), fixedCode);
    }

    /// <summary>
    /// 挿入先の行の改行が LINE SEPARATOR（U+2028。C#では改行扱い）の場合でも、
    /// 挿入するIgnoreコメントの改行には不可視文字を使わず、ファイル内の通常の改行（LF）を使うことを確認する
    /// </summary>
    [Fact]
    public async Task LineEndingWithLineSeparator_IgnoreUsesOrdinaryNewLine()
    {
        var test = $"public class C\n{{\n    string s = \"A{{|#0:{Nbsp}|}}B\";{{|#1:{LineSeparator}|}}}}\n";
        var fixedCode = $"public class C\n{{\n    // Ignore UNI001\n    string s = \"A{Nbsp}B\";{LineSeparator}}}\n";

        await CodeFixVerify.VerifyCodeFixAsync(
            test,
            [
                CodeFixVerify.Diagnostic().WithLocation(0).WithArguments("U+00A0", "NO-BREAK SPACE"),
                CodeFixVerify.Diagnostic().WithLocation(1).WithArguments("U+2028", "LINE SEPARATOR"),
            ],
            fixedCode);
    }

    /// <summary>
    /// 一括修正（Fix All）では、文ごとにIgnoreコメントが挿入されることを確認する
    /// </summary>
    [Fact]
    public async Task FixAll_InsertsIgnorePerStatement()
    {
        var test = $$"""
            public class C
            {
                void M()
                {
                    var a = "1{|#0:{{Nbsp}}|}2";
                    var b ={|#1:{{Nbsp}}|}"3{|#2:{{Nbsp}}|}4";
                }
            }
            """;
        var fixedCode = $$"""
            public class C
            {
                void M()
                {
                    // Ignore UNI001
                    var a = "1{{Nbsp}}2";
                    // Ignore UNI001
                    var b ={{Nbsp}}"3{{Nbsp}}4";
                }
            }
            """;

        await CodeFixVerify.VerifyCodeFixAsync(
            test,
            [
                CodeFixVerify.Diagnostic().WithLocation(0).WithArguments("U+00A0", "NO-BREAK SPACE"),
                CodeFixVerify.Diagnostic().WithLocation(1).WithArguments("U+00A0", "NO-BREAK SPACE"),
                CodeFixVerify.Diagnostic().WithLocation(2).WithArguments("U+00A0", "NO-BREAK SPACE"),
            ],
            fixedCode);
    }

    /// <summary>
    /// CRLFのファイルでは、挿入するIgnoreコメントの改行もCRLFになることを確認する
    /// </summary>
    [Fact]
    public async Task CrlfFile_IgnoreUsesCrlf()
    {
        var test = $"public class C\r\n{{\r\n    string s = \"A{{|#0:{Nbsp}|}}B\";\r\n}}\r\n";
        var fixedCode = $"public class C\r\n{{\r\n    // Ignore UNI001\r\n    string s = \"A{Nbsp}B\";\r\n}}\r\n";

        await CodeFixVerify.VerifyCodeFixAsync(test, CodeFixVerify.Diagnostic().WithLocation(0).WithArguments("U+00A0", "NO-BREAK SPACE"), fixedCode);
    }

    /// <summary>
    /// ファイル先頭のコメント内の文字は、最初の using ディレクティブの直前にIgnoreコメントを挿入することを確認する
    /// </summary>
    [Fact]
    public async Task CharInFileHeaderComment_IgnoreInsertedBeforeUsing()
    {
        var test = $$"""
            // Copyright{|#0:{{Nbsp}}|}2026
            using System;

            public class C { }
            """;
        var fixedCode = $$"""
            // Copyright{{Nbsp}}2026
            // Ignore UNI001
            using System;

            public class C { }
            """;

        await CodeFixVerify.VerifyCodeFixAsync(test, CodeFixVerify.Diagnostic().WithLocation(0).WithArguments("U+00A0", "NO-BREAK SPACE"), fixedCode);
    }

    /// <summary>
    /// ファイル末尾のコメント内の文字は、挿入先の文・メンバーがないため、Fixが提供されないことを確認する
    /// </summary>
    [Fact]
    public async Task CharInTrailingCommentOfFile_NoFixOffered()
    {
        var test = $$"""
            public class C { }
            // 末尾{|#0:{{Zwsp}}|}
            """;

        await CodeFixVerify.VerifyCodeFixAsync(test, CodeFixVerify.Diagnostic().WithLocation(0).WithArguments("U+200B", "ZERO WIDTH SPACE"), test);
    }

    // ================================================================
    // Ignore コメントによる抑制
    // ================================================================

    /// <summary>
    /// 文の直前の「// Ignore UNI001」はその文だけを抑制し、隣の文の診断は残ることを確認する
    /// </summary>
    [Fact]
    public async Task IgnoreOnStatement_SuppressesOnlyThatStatement()
    {
        var test = $$"""
            public class C
            {
                void M()
                {
                    // Ignore UNI001
                    var a = "1{{Nbsp}}2";
                    var b = "3{|#0:{{Nbsp}}|}4";
                }
            }
            """;

        await Verify.VerifyAnalyzerAsync(test, Verify.Diagnostic().WithLocation(0).WithArguments("U+00A0", "NO-BREAK SPACE"));
    }

    /// <summary>
    /// メソッドの直前の「// Ignore UNI001」は、そのメソッド内（ネストしたブロック・ラムダ式を含む）をすべて抑制し、
    /// 他のメソッドの診断は残ることを確認する
    /// </summary>
    [Fact]
    public async Task IgnoreOnMethod_SuppressesWholeMethodOnly()
    {
        var test = $$"""
            using System;

            public class C
            {
                // Ignore UNI001
                void M(bool flag)
                {
                    if (flag)
                    {
                        Func<string> f = () => "1{{Nbsp}}2";
                    }
                }

                void N()
                {
                    var b = "3{|#0:{{Nbsp}}|}4";
                }
            }
            """;

        await Verify.VerifyAnalyzerAsync(test, Verify.Diagnostic().WithLocation(0).WithArguments("U+00A0", "NO-BREAK SPACE"));
    }

    /// <summary>
    /// クラスの直前の「// Ignore UNI001」は、クラス内のすべてのメンバーを抑制することを確認する
    /// </summary>
    [Fact]
    public async Task IgnoreOnClass_SuppressesAllMembers()
    {
        var test = $$"""
            // Ignore UNI001
            public class C
            {
                string a = "1{{Nbsp}}2";
                void M() { var b = "3{{Zwsp}}4"; }
            }
            """;

        await Verify.VerifyAnalyzerAsync(test);
    }

    // ================================================================
    // 検知対象の網羅性・境界値
    // ================================================================

    /// <summary>
    /// 仕様として定めた検知対象のコードポイント一覧（実装とは独立に記述し、実装との完全一致を検証する）
    /// </summary>
    private static readonly HashSet<int> SpecifiedTargets = BuildSpecifiedTargets();

    private static HashSet<int> BuildSpecifiedTargets()
    {
        var set = new HashSet<int>();
        void AddRange(int from, int to) { for (var c = from; c <= to; c++) set.Add(c); }

        AddRange(0x0000, 0x0008);       // C0（タブ U+0009・LF U+000A を除く）
        AddRange(0x000B, 0x000C);       // C0（CR U+000D を除く）
        AddRange(0x000E, 0x001F);       // C0
        set.Add(0x007F);                // DELETE
        AddRange(0x0080, 0x009F);       // C1
        set.Add(0x00A0);                // NO-BREAK SPACE
        set.Add(0x00AD);                // SOFT HYPHEN
        set.Add(0x034F);                // COMBINING GRAPHEME JOINER
        set.Add(0x061C);                // ARABIC LETTER MARK
        AddRange(0x115F, 0x1160);       // HANGUL CHOSEONG / JUNGSEONG FILLER
        AddRange(0x17B4, 0x17B5);       // KHMER VOWEL INHERENT AQ / AA
        set.Add(0x180E);                // MONGOLIAN VOWEL SEPARATOR
        AddRange(0x2000, 0x200F);       // 各種スペース・ゼロ幅文字・LRM/RLM
        AddRange(0x2028, 0x202F);       // 行・段落区切り、双方向埋め込み/上書き、NARROW NO-BREAK SPACE
        AddRange(0x205F, 0x2064);       // MEDIUM MATHEMATICAL SPACE、WORD JOINER、不可視演算子
        AddRange(0x2066, 0x206F);       // 双方向分離、非推奨の書式文字
        AddRange(0x3099, 0x309A);       // 結合用の濁点・半濁点
        set.Add(0x3164);                // HANGUL FILLER
        set.Add(0xFEFF);                // ZERO WIDTH NO-BREAK SPACE（ファイル先頭以外）
        set.Add(0xFFA0);                // HALFWIDTH HANGUL FILLER
        AddRange(0xFFF9, 0xFFFB);       // INTERLINEAR ANNOTATION
        set.Add(0xE0001);               // LANGUAGE TAG
        AddRange(0xE0020, 0xE007F);     // TAG CHARACTER
        return set;
    }

    /// <summary>
    /// BMP全域（U+0000〜U+FFFF。サロゲート領域と " を除く）とタグ文字ブロック（U+E0000〜U+E01FF）の全文字を1文字ずつ並べ、
    /// 検知されたコードポイントの集合が仕様の一覧と過不足なく一致することを確認する（検知漏れ・誤検知の両方を検証）
    /// </summary>
    [Fact]
    public async Task AllCodePoints_DetectedExactlyAsSpecified()
    {
        var builder = new System.Text.StringBuilder("class C { string s = @\"");
        for (var c = 0; c <= 0xFFFF; c++)
        {
            if (c is >= 0xD800 and <= 0xDFFF || c == '"') continue;
            builder.Append('x').Append((char)c);
        }
        for (var c = 0xE0000; c <= 0xE01FF; c++)
        {
            builder.Append('x').Append(char.ConvertFromUtf32(c));
        }
        builder.Append("\"; }");
        var source = builder.ToString();

        var diagnostics = await GetAnalyzerDiagnosticsAsync(source);

        var detected = new HashSet<int>();
        foreach (var diagnostic in diagnostics)
        {
            var span = diagnostic.Location.SourceSpan;
            var codePoint = char.ConvertToUtf32(source, span.Start);
            Assert.Equal(char.ConvertFromUtf32(codePoint).Length, span.Length);
            Assert.True(detected.Add(codePoint), $"U+{codePoint:X4} が重複して検知された");
        }

        Assert.Empty(SpecifiedTargets.Except(detected).Select(c => $"U+{c:X4}"));
        Assert.Empty(detected.Except(SpecifiedTargets).Select(c => $"U+{c:X4}"));
    }

    /// <summary>
    /// 各範囲の境界と、その外側の隣接文字（見た目のある文字・除外した文字）の検知結果と名称を確認する
    /// </summary>
    [Theory]
    [InlineData(0x0008, "BACKSPACE")]
    [InlineData(0x000B, "LINE TABULATION")]
    [InlineData(0x000E, "SHIFT OUT")]
    [InlineData(0x001F, "UNIT SEPARATOR")]
    [InlineData(0x007F, "DELETE")]
    [InlineData(0x0080, "C1 CONTROL CHARACTER")]
    [InlineData(0x009F, "C1 CONTROL CHARACTER")]
    [InlineData(0x00A0, "NO-BREAK SPACE")]
    [InlineData(0x2000, "EN QUAD")]
    [InlineData(0x200F, "RIGHT-TO-LEFT MARK")]
    [InlineData(0x2028, "LINE SEPARATOR")]
    [InlineData(0x202F, "NARROW NO-BREAK SPACE")]
    [InlineData(0x205F, "MEDIUM MATHEMATICAL SPACE")]
    [InlineData(0x2064, "INVISIBLE PLUS")]
    [InlineData(0x2066, "LEFT-TO-RIGHT ISOLATE")]
    [InlineData(0x206F, "NOMINAL DIGIT SHAPES")]
    [InlineData(0x3099, "COMBINING KATAKANA-HIRAGANA VOICED SOUND MARK")]
    [InlineData(0x309A, "COMBINING KATAKANA-HIRAGANA SEMI-VOICED SOUND MARK")]
    [InlineData(0xFFF9, "INTERLINEAR ANNOTATION ANCHOR")]
    [InlineData(0xFFFB, "INTERLINEAR ANNOTATION TERMINATOR")]
    [InlineData(0xE0001, "LANGUAGE TAG")]
    [InlineData(0xE0020, "TAG CHARACTER")]
    [InlineData(0xE007F, "TAG CHARACTER")]
    public async Task BoundaryCodePoint_Reported(int codePoint, string expectedName)
    {
        var test = $"class C {{ string s = @\"x{{|#0:{char.ConvertFromUtf32(codePoint)}|}}x\"; }}";

        await new CSharpAnalyzerTest<InvisibleUnicode, XUnitVerifier>
        {
            TestCode = test,
            CompilerDiagnostics = CompilerDiagnostics.None,
            ExpectedDiagnostics = { Verify.Diagnostic().WithLocation(0).WithArguments($"U+{codePoint:X4}", expectedName) },
        }.RunAsync();
    }

    /// <summary>
    /// 範囲の外側に隣接する文字（タブ・LF・CR・半角スペース、見た目のあるハイフンや「゛」、全角スペース、
    /// 未割り当て・対象外のタグ領域など）は、診断が出ないことを確認する
    /// </summary>
    [Theory]
    [InlineData(0x0009)]  // タブ
    [InlineData(0x000A)]  // LF
    [InlineData(0x000D)]  // CR
    [InlineData(0x0020)]  // 半角スペース
    [InlineData(0x007E)]  // ~
    [InlineData(0x00A1)]  // ¡
    [InlineData(0x1680)]  // OGHAM SPACE MARK（ダッシュ状に描画されるため対象外）
    [InlineData(0x1FFF)]
    [InlineData(0x2010)]  // HYPHEN（目視できる）
    [InlineData(0x2027)]  // HYPHENATION POINT（目視できる）
    [InlineData(0x2030)]  // ‰
    [InlineData(0x205E)]
    [InlineData(0x2065)]  // 未割り当て
    [InlineData(0x2070)]  // ⁰
    [InlineData(0x3000)]  // 全角スペース
    [InlineData(0x3098)]
    [InlineData(0x309B)]  // 単独の濁点「゛」（目視できる）
    [InlineData(0xFFF8)]
    [InlineData(0xFFFC)]  // OBJECT REPLACEMENT CHARACTER（目視できる）
    [InlineData(0xE0000)]
    [InlineData(0xE0002)]
    [InlineData(0xE001F)]
    [InlineData(0xE0080)]
    public async Task OutOfRangeNeighbor_DoesNotReportDiagnostic(int codePoint)
    {
        var test = $"class C {{ string s = @\"x{char.ConvertFromUtf32(codePoint)}x\"; }}";

        await new CSharpAnalyzerTest<InvisibleUnicode, XUnitVerifier>
        {
            TestCode = test,
            CompilerDiagnostics = CompilerDiagnostics.None,
        }.RunAsync();
    }

    // ================================================================
    // 異常系・端のケース
    // ================================================================

    /// <summary>
    /// 空のファイルでは、例外が発生せず診断も出ないことを確認する
    /// </summary>
    [Fact]
    public async Task EmptyFile_DoesNotReportDiagnostic()
    {
        await Verify.VerifyAnalyzerAsync(string.Empty);
    }

    /// <summary>
    /// ファイルの先頭（BOM以外）と末尾（最後の1文字）にある不可視文字も検知することを確認する
    /// </summary>
    [Fact]
    public async Task CharsAtStartAndEndOfFile_Reported()
    {
        var test = $"{{|#0:{Nbsp}|}}class C {{ }}{{|#1:{FormFeed}|}}";

        await Verify.VerifyAnalyzerAsync(
            test,
            Verify.Diagnostic().WithLocation(0).WithArguments("U+00A0", "NO-BREAK SPACE"),
            Verify.Diagnostic().WithLocation(1).WithArguments("U+000C", "FORM FEED"));
    }

    /// <summary>
    /// ファイル先頭にBOMが2つ続く場合、先頭の1つだけを除外し、2つ目は検知することを確認する
    /// </summary>
    [Fact]
    public async Task DoubleBomAtStartOfFile_SecondReported()
    {
        var test = $"{Bom}{{|#0:{Bom}|}}class C {{ }}";

        await Verify.VerifyAnalyzerAsync(test, Verify.Diagnostic().WithLocation(0).WithArguments("U+FEFF", "ZERO WIDTH NO-BREAK SPACE"));
    }

    /// <summary>
    /// ペアになっていないサロゲート（文字列の途中・ファイル末尾）があっても、例外が発生せず誤検知もしないことを確認する
    /// </summary>
    [Fact]
    public async Task UnpairedSurrogates_DoNotThrowOrReport()
    {
        var test = "class C { string s = @\"a\uD800b\uDC00c\"; }\uD800";

        await new CSharpAnalyzerTest<InvisibleUnicode, XUnitVerifier>
        {
            TestCode = test,
            CompilerDiagnostics = CompilerDiagnostics.None,
        }.RunAsync();
    }

    /// <summary>
    /// 入力途中の閉じていない文字列（構文エラーのあるコード）でも、例外が発生せず検知とIgnore Fixが動作することを確認する
    /// </summary>
    [Fact]
    public async Task UnterminatedString_ReportsAndIgnoreInserted()
    {
        var test = $$"""
            class C
            {
                void M()
                {
                    var s = "abc{|#0:{{Nbsp}}|}
                }
            }
            """;
        var fixedCode = $$"""
            class C
            {
                void M()
                {
                    // Ignore UNI001
                    var s = "abc{{Nbsp}}
                }
            }
            """;

        await new CSharpCodeFixTest<InvisibleUnicode, InvisibleUnicodeFix, XUnitVerifier>
        {
            TestCode = test,
            FixedCode = fixedCode,
            CompilerDiagnostics = CompilerDiagnostics.None,
            ExpectedDiagnostics = { CodeFixVerify.Diagnostic().WithLocation(0).WithArguments("U+00A0", "NO-BREAK SPACE") },
        }.RunAsync();
    }

    /// <summary>
    /// 連続する LINE SEPARATOR はそれぞれが別の改行トリビアであるため、トリビアの境界を越えてまとめず、別々の診断になることを確認する
    /// </summary>
    [Fact]
    public async Task ConsecutiveLineSeparators_ReportedPerTrivia()
    {
        var test = $"class C {{ int a;{{|#0:{LineSeparator}|}}{{|#1:{LineSeparator}|}}}}";

        await Verify.VerifyAnalyzerAsync(
            test,
            Verify.Diagnostic().WithLocation(0).WithArguments("U+2028", "LINE SEPARATOR"),
            Verify.Diagnostic().WithLocation(1).WithArguments("U+2028", "LINE SEPARATOR"));
    }

    // ================================================================
    // Ignore コメントの書式
    // ================================================================

    /// <summary>
    /// Ignoreコメントは大文字小文字を区別せず、前後の空白も許容することを確認する（既存ルールと同じ判定）
    /// </summary>
    [Theory]
    [InlineData("// ignore uni001")]
    [InlineData("// IGNORE UNI001")]
    [InlineData("// Ignore UNI001   ")]
    public async Task IgnoreCommentVariants_Suppress(string comment)
    {
        var test = $$"""
            class C
            {
                {{comment}}
                string s = "a{{Nbsp}}b";
            }
            """;

        await Verify.VerifyAnalyzerAsync(test);
    }

    /// <summary>
    /// 書式の異なるコメント（ID違い・空白なし・ブロックコメント・説明付き）や、
    /// 直前の文の行末に書かれたコメントでは抑制されないことを確認する
    /// </summary>
    [Theory]
    [InlineData("// Ignore UNI002")]
    [InlineData("// Ignore CPX001")]
    [InlineData("//Ignore UNI001")]
    [InlineData("/* Ignore UNI001 */")]
    [InlineData("// Ignore UNI001 意図的")]
    [InlineData("int dummy; // Ignore UNI001")]
    public async Task NonMatchingIgnoreComments_DoNotSuppress(string comment)
    {
        var test = $$"""
            class C
            {
                {{comment}}
                string s = "a{|#0:{{Nbsp}}|}b";
            }
            """;

        await Verify.VerifyAnalyzerAsync(test, Verify.Diagnostic().WithLocation(0).WithArguments("U+00A0", "NO-BREAK SPACE"));
    }

    // ================================================================
    // Ignore Fix の挿入先（文・メンバーの種類ごと）
    // ================================================================

    /// <summary>
    /// 文字がある構文の種類ごとに、Ignoreコメントが意図した文・メンバーの直前に挿入され、挿入後に診断が消えることを確認する。
    /// {MARK} は検知位置、{CHAR} はFix後に残る文字を表す
    /// </summary>
    [Theory]
    [InlineData( // 属性の引数：属性を含めたメソッドの直前
        "class C\n{\n    [System.Obsolete(\"a{MARK}\")]\n    void M() { }\n}",
        "class C\n{\n    // Ignore UNI001\n    [System.Obsolete(\"a{CHAR}\")]\n    void M() { }\n}")]
    [InlineData( // プロパティのアクセサー：プロパティの直前
        "class C\n{\n    string P\n    {\n        get => \"a{MARK}\";\n    }\n}",
        "class C\n{\n    // Ignore UNI001\n    string P\n    {\n        get => \"a{CHAR}\";\n    }\n}")]
    [InlineData( // enum のメンバー：そのメンバーの直前
        "enum E\n{\n    A,\n    B ={MARK}1,\n}",
        "enum E\n{\n    A,\n    // Ignore UNI001\n    B ={CHAR}1,\n}")]
    [InlineData( // ローカル関数内の文：その文の直前
        "class C\n{\n    void M()\n    {\n        string F()\n        {\n            return \"a{MARK}\";\n        }\n    }\n}",
        "class C\n{\n    void M()\n    {\n        string F()\n        {\n            // Ignore UNI001\n            return \"a{CHAR}\";\n        }\n    }\n}")]
    [InlineData( // switch の case ラベル：switch 文の直前
        "class C\n{\n    void M(string s)\n    {\n        switch (s)\n        {\n            case \"a{MARK}\":\n                break;\n        }\n    }\n}",
        "class C\n{\n    void M(string s)\n    {\n        // Ignore UNI001\n        switch (s)\n        {\n            case \"a{CHAR}\":\n                break;\n        }\n    }\n}")]
    [InlineData( // 引数の既定値：メソッドの直前
        "class C\n{\n    void M(string s = \"a{MARK}\") { }\n}",
        "class C\n{\n    // Ignore UNI001\n    void M(string s = \"a{CHAR}\") { }\n}")]
    [InlineData( // #region 名：直後のメンバーの直前（ディレクティブより下）
        "class C\n{\n    #region A{MARK}B\n    int x;\n    #endregion\n}",
        "class C\n{\n    #region A{CHAR}B\n    // Ignore UNI001\n    int x;\n    #endregion\n}")]
    [InlineData( // ファイルスコープの名前空間内の型：型の直前
        "namespace N;\n\nclass C{MARK}{ }",
        "namespace N;\n\n// Ignore UNI001\nclass C{CHAR}{ }")]
    [InlineData( // タブインデント：挿入行のインデントもタブ
        "class C\n{\n\tstring s = \"a{MARK}\";\n}",
        "class C\n{\n\t// Ignore UNI001\n\tstring s = \"a{CHAR}\";\n}")]
    [InlineData( // 最終行に改行がない：ファイル内の他の行の改行（LF）を使う
        "class C\n{\n    int a; string s = \"a{MARK}\"; }",
        "class C\n{\n    int a;\n    // Ignore UNI001\n    string s = \"a{CHAR}\"; }")]
    [InlineData( // 改行が1つもないファイル：CRLFにフォールバックする
        "class C { string s = \"a{MARK}\"; }",
        "class C {\r\n// Ignore UNI001\r\nstring s = \"a{CHAR}\"; }")]
    public async Task IgnoreAnchorKinds_IgnoreInsertedBeforeExpectedNode(string testTemplate, string fixedTemplate)
    {
        var test = testTemplate.Replace("{MARK}", "{|#0:" + Nbsp + "|}");
        var fixedCode = fixedTemplate.Replace("{CHAR}", Nbsp);

        await CodeFixVerify.VerifyCodeFixAsync(test, CodeFixVerify.Diagnostic().WithLocation(0).WithArguments("U+00A0", "NO-BREAK SPACE"), fixedCode);
    }

    /// <summary>
    /// トップレベルステートメント内の文字は、その文の直前にIgnoreコメントを挿入することを確認する
    /// </summary>
    [Fact]
    public async Task TopLevelStatement_IgnoreInsertedBeforeStatement()
    {
        var test = $$"""
            var x = 1;
            System.Console.WriteLine("a{|#0:{{Nbsp}}|}");
            """;
        var fixedCode = $$"""
            var x = 1;
            // Ignore UNI001
            System.Console.WriteLine("a{{Nbsp}}");
            """;

        await new CSharpCodeFixTest<InvisibleUnicode, InvisibleUnicodeFix, XUnitVerifier>
        {
            TestCode = test,
            FixedCode = fixedCode,
            TestState = { OutputKind = OutputKind.ConsoleApplication },
            ExpectedDiagnostics = { CodeFixVerify.Diagnostic().WithLocation(0).WithArguments("U+00A0", "NO-BREAK SPACE") },
        }.RunAsync();
    }

    // ================================================================
    // 診断が出ないケース
    // ================================================================

    /// <summary>
    /// 全角スペース（U+3000）は日本語の文字列・コメントで意図的に使われることが多いため、診断が出ないことを確認する
    /// </summary>
    [Fact]
    public async Task IdeographicSpace_DoesNotReportDiagnostic()
    {
        var test = $$"""
            public class C
            {
                // 全角{{IdeographicSpace}}スペース
                string s = "山田{{IdeographicSpace}}太郎";
            }
            """;

        await Verify.VerifyAnalyzerAsync(test);
    }

    /// <summary>
    /// エスケープシーケンスで記述された不可視文字（ソース上は可視のASCII）は、診断が出ないことを確認する
    /// </summary>
    [Fact]
    public async Task EscapedInvisibleChars_DoNotReportDiagnostic()
    {
        var test = """
            public class C
            {
                string a = "Hello\u00A0World";
                string b = "\u200B\U000E0041\x1B";
                char c = '\u202E';
            }
            """;

        await Verify.VerifyAnalyzerAsync(test);
    }

    /// <summary>
    /// 通常の空白・タブ・日本語・絵文字（ZWJを含まないもの）・合成済みの濁音のみのコードでは、診断が出ないことを確認する
    /// </summary>
    [Fact]
    public async Task OrdinaryText_DoesNotReportDiagnostic()
    {
        var test = """
            public class C
            {
            	// タブ インデント
                string s = "こんにちは 世界 😀 がぎぐげご パピプペポ";
            }
            """;

        await Verify.VerifyAnalyzerAsync(test);
    }

    /// <summary>
    /// ファイル先頭のBOMはエンコーディングの指定であり混入ではないため、診断が出ないことを確認する
    /// </summary>
    [Fact]
    public async Task BomAtStartOfFile_DoesNotReportDiagnostic()
    {
        var test = Bom + """
            public class C
            {
            }
            """;

        await Verify.VerifyAnalyzerAsync(test);
    }

    /// <summary>
    /// 自動生成ファイル（*.Designer.cs）内のコードは、診断が出ないことを確認する
    /// </summary>
    [Fact]
    public async Task GeneratedFile_DoesNotReportDiagnostic()
    {
        var test = $$"""
            public class C
            {
                string s = "A{{Nbsp}}B";
            }
            """;

        await new CSharpAnalyzerTest<InvisibleUnicode, XUnitVerifier>
        {
            TestState = { Sources = { ("Test0.Designer.cs", test) } },
        }.RunAsync();
    }

    // ================================================================
    // ヘルパー
    // ================================================================

    /// <summary>
    /// コンパイルエラーの有無に関係なく、UNI001の診断のみを取得する（大量の文字を一度に検証するため、テストフレームワークを介さず直接実行する）
    /// </summary>
    private static async Task<IReadOnlyList<Diagnostic>> GetAnalyzerDiagnosticsAsync(string source)
    {
        var tree = CSharpSyntaxTree.ParseText(source, path: "Test0.cs");
        var compilation = CSharpCompilation.Create(
            "Test",
            [tree],
            [MetadataReference.CreateFromFile(typeof(object).Assembly.Location)]);

        var diagnostics = await compilation.WithAnalyzers([new InvisibleUnicode()]).GetAnalyzerDiagnosticsAsync();
        return diagnostics.Where(d => d.Id == InvisibleUnicode.DiagnosticId).ToList();
    }
}

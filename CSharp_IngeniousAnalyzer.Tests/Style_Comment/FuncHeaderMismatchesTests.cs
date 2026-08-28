using CSharp_IngeniousAnalyzer.Style_Comment;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
using Microsoft.CodeAnalysis.Testing.Verifiers;

namespace CSharp_IngeniousAnalyzer.Tests.Style_Comment;

using Verify = CSharpAnalyzerVerifier<FuncHeaderMismatches, XUnitVerifier>;
using CodeFixVerify = CSharpCodeFixVerifier<FuncHeaderMismatches, FuncHeaderMismatchesFix, XUnitVerifier>;

/// <summary>
/// COMM002（FuncHeaderMismatches）の検知・Fix動作を検証するテスト
/// </summary>
public class FuncHeaderMismatchesTests
{
    /// <summary>
    /// 実引数名とドキュメントのparam名が不一致の場合、診断が出ることを確認する
    /// </summary>
    [Fact]
    public async Task MismatchedParamName_ReportsDiagnostic()
    {
        var test = """
            public class C
            {
                /// <summary>
                /// text
                /// </summary>
                /// <param name="wrongName"></param>
                void {|#0:M|}(int actualName) { }
            }
            """;

        await Verify.VerifyAnalyzerAsync(test, Verify.Diagnostic().WithLocation(0).WithArguments("actualName, wrongName"));
    }

    /// <summary>
    /// 実引数は2つあるが、ドキュメントのparamタグが1つ欠落している場合、診断が出ることを確認する
    /// </summary>
    [Fact]
    public async Task MissingParamTag_ReportsDiagnostic()
    {
        var test = """
            public class C
            {
                /// <summary>
                /// text
                /// </summary>
                /// <param name="first"></param>
                void {|#0:M|}(int first, int second) { }
            }
            """;

        await Verify.VerifyAnalyzerAsync(test, Verify.Diagnostic().WithLocation(0).WithArguments("second"));
    }

    /// <summary>
    /// ドキュメントに余分なparamタグ（extra）がある場合、診断が出ることを確認する
    /// </summary>
    [Fact]
    public async Task ExtraParamTag_ReportsDiagnostic()
    {
        var test = """
            public class C
            {
                /// <summary>
                /// text
                /// </summary>
                /// <param name="first"></param>
                /// <param name="extra"></param>
                void {|#0:M|}(int first) { }
            }
            """;

        await Verify.VerifyAnalyzerAsync(test, Verify.Diagnostic().WithLocation(0).WithArguments("extra"));
    }

    /// <summary>
    /// パラメータ名・順序ともに完全一致している場合、診断が出ないことを確認する
    /// </summary>
    [Fact]
    public async Task MatchedParams_DoesNotReportDiagnostic()
    {
        var test = """
            public class C
            {
                /// <summary>
                /// text
                /// </summary>
                /// <param name="first"></param>
                /// <param name="second"></param>
                void M(int first, int second) { }
            }
            """;

        await Verify.VerifyAnalyzerAsync(test);
    }

    /// <summary>
    /// 名前は両方とも存在するが、順序だけが入れ替わっている場合、診断が出ることを確認する
    /// </summary>
    [Fact]
    public async Task SwappedParamOrder_ReportsDiagnostic()
    {
        var test = """
            public class C
            {
                /// <summary>
                /// text
                /// </summary>
                /// <param name="second"></param>
                /// <param name="first"></param>
                void {|#0:M|}(int first, int second) { }
            }
            """;

        // 順序違反のみの場合、missing/extraどちらの集合にも入らないため、詳細は空文字列になる
        await Verify.VerifyAnalyzerAsync(test, Verify.Diagnostic().WithLocation(0).WithArguments(""));
    }

    /// <summary>
    /// Fixが既存のparamタグを削除し、メソッドの引数順に正しいparamタグを再生成することを確認する
    /// </summary>
    [Fact]
    public async Task Fix_SyncsParamsToMatchMethodSignature()
    {
        var test = """
            public class C
            {
                /// <summary>
                /// text
                /// </summary>
                /// <param name="first"></param>
                void {|#0:M|}(int first, int second)
                {
                }
            }
            """;

        var fixedSource = """
            public class C
            {
                /// <summary>
                /// text
                /// </summary>
                /// <param name="first"></param>
                /// <param name="second"></param>
                void M(int first, int second)
                {
                }
            }
            """;

        await CodeFixVerify.VerifyCodeFixAsync(test, CodeFixVerify.Diagnostic().WithLocation(0).WithArguments("second"), fixedSource);
    }

    // ==========================================================================
    // 以下、GenerateDocumentationFile 無効時（生テキストフォールバック）の検証
    // DocumentationMode.None を明示することで、/// コメントが構造化されず
    // 単純な SingleLineCommentTrivia として扱われる状況を再現する
    // ==========================================================================

    /// <summary>
    /// GenerateDocumentationFile 無効時でも、生テキストからparam名を抽出し、
    /// 一致していれば診断が出ないことを確認する
    /// </summary>
    [Fact]
    public async Task RawTextFallback_MatchedParams_DoesNotReportDiagnostic()
    {
        var test = """
            public class C
            {
                /// <summary>
                /// text
                /// </summary>
                /// <param name="first"></param>
                void M(int first) { }
            }
            """;

        var analyzerTest = new CSharpAnalyzerTest<FuncHeaderMismatches, XUnitVerifier>
        {
            TestCode = test,
            TestState = { DocumentationMode = DocumentationMode.None },
        };

        await analyzerTest.RunAsync();
    }

    /// <summary>
    /// GenerateDocumentationFile 無効時、生テキストの正規表現抽出でparam名の不一致を検知できることを確認する
    /// </summary>
    [Fact]
    public async Task RawTextFallback_MismatchedParamName_ReportsDiagnostic()
    {
        var test = """
            public class C
            {
                /// <summary>
                /// text
                /// </summary>
                /// <param name="wrongName"></param>
                void {|#0:M|}(int actualName) { }
            }
            """;

        var analyzerTest = new CSharpAnalyzerTest<FuncHeaderMismatches, XUnitVerifier>
        {
            TestCode = test,
            TestState = { DocumentationMode = DocumentationMode.None },
        };
        analyzerTest.ExpectedDiagnostics.Add(Verify.Diagnostic().WithLocation(0).WithArguments("actualName, wrongName"));

        await analyzerTest.RunAsync();
    }

    /// <summary>
    /// GenerateDocumentationFile 無効時、生テキストに &lt;summary が含まれない場合は
    /// （COMM001の責務のため）診断が出ないことを確認する
    /// </summary>
    [Fact]
    public async Task RawTextFallback_NoSummaryTag_DoesNotReportDiagnostic()
    {
        var test = """
            public class C
            {
                /// text only, no summary tag
                /// <param name="wrongName"></param>
                void M(int actualName) { }
            }
            """;

        var analyzerTest = new CSharpAnalyzerTest<FuncHeaderMismatches, XUnitVerifier>
        {
            TestCode = test,
            TestState = { DocumentationMode = DocumentationMode.None },
        };

        await analyzerTest.RunAsync();
    }

    /// <summary>
    /// GenerateDocumentationFile 無効時、summaryブロックの後に通常コメント（///以外）が割り込んでいても、
    /// メソッドに一番近いブロックがsummaryを持たない場合は前のブロックと結合され、
    /// param不一致が正しく検知されることを確認する（分断されたドキュメントブロックの誤結合防止に関する回帰テスト）
    /// </summary>
    [Fact]
    public async Task RawTextFallback_SplitBySeparateComment_MergesSegments_ReportsDiagnosticOnMismatch()
    {
        var test = """
            public class C
            {
                /// <summary>
                /// text
                /// </summary>
                // a plain comment interrupting the doc block
                /// <param name="wrongName"></param>
                void {|#0:M|}(int actualName) { }
            }
            """;

        var analyzerTest = new CSharpAnalyzerTest<FuncHeaderMismatches, XUnitVerifier>
        {
            TestCode = test,
            TestState = { DocumentationMode = DocumentationMode.None },
        };
        analyzerTest.ExpectedDiagnostics.Add(Verify.Diagnostic().WithLocation(0).WithArguments("actualName, wrongName"));

        await analyzerTest.RunAsync();
    }

    /// <summary>
    /// GenerateDocumentationFile 無効時、メソッドに一番近いブロックが自身のsummaryを持つ独立したブロックである場合、
    /// その手前にある無関係な（孤立した）paramタグの残骸は無視され、誤って不一致と判定されないことを確認する
    /// （孤立したparamフラグメントとの誤結合防止に関する回帰テスト）
    /// </summary>
    [Fact]
    public async Task RawTextFallback_OrphanedFragmentBeforeSelfContainedBlock_DoesNotReportDiagnostic()
    {
        var test = """
            public class C
            {
                /// <param name="oldParam"></param>
                // separator
                /// <summary>
                /// text
                /// </summary>
                /// <param name="first"></param>
                void M(int first) { }
            }
            """;

        var analyzerTest = new CSharpAnalyzerTest<FuncHeaderMismatches, XUnitVerifier>
        {
            TestCode = test,
            TestState = { DocumentationMode = DocumentationMode.None },
        };

        await analyzerTest.RunAsync();
    }

    /// <summary>
    /// メソッドにドキュメントコメントが一切存在しない場合、診断が出ないことを確認する（欠落自体はCOMM001の責務）
    /// </summary>
    [Fact]
    public async Task NoDocComment_DoesNotReportDiagnostic()
    {
        var test = """
            public class C
            {
                void M(int first) { }
            }
            """;

        await Verify.VerifyAnalyzerAsync(test);
    }

    /// <summary>
    /// summaryタグがXMLドキュメントコメント内の先頭要素でない場合（例: remarksが先行する場合）や、
    /// summary内部にネストしたXML要素（paraタグ等）が含まれる場合でも、
    /// summaryの存在を正しく検出し、param不一致を検知できることを確認する
    /// </summary>
    [Fact]
    public async Task SummaryNotFirstElement_WithNestedXml_ReportsDiagnosticOnMismatch()
    {
        var test = """
            public class C
            {
                /// <remarks>Note here.</remarks>
                /// <summary>
                /// <para>First paragraph.</para>
                /// </summary>
                /// <param name="wrongName"></param>
                void {|#0:M|}(int actualName) { }
            }
            """;

        await Verify.VerifyAnalyzerAsync(test, Verify.Diagnostic().WithLocation(0).WithArguments("actualName, wrongName"));
    }

    /// <summary>
    /// 引数が0個のメソッドでドキュメントのparamタグも0個の場合（完全一致の境界値）、診断が出ないことを確認する
    /// </summary>
    [Fact]
    public async Task ZeroParamMethod_NoParamTags_DoesNotReportDiagnostic()
    {
        var test = """
            public class C
            {
                /// <summary>
                /// text
                /// </summary>
                void M() { }
            }
            """;

        await Verify.VerifyAnalyzerAsync(test);
    }

    /// <summary>
    /// 引数が0個のメソッドにもかかわらずドキュメントに余分なparamタグが存在する場合、診断が出ることを確認する
    /// </summary>
    [Fact]
    public async Task ZeroParamMethod_WithExtraParamTag_ReportsDiagnostic()
    {
        var test = """
            public class C
            {
                /// <summary>
                /// text
                /// </summary>
                /// <param name="extra"></param>
                void {|#0:M|}() { }
            }
            """;

        await Verify.VerifyAnalyzerAsync(test, Verify.Diagnostic().WithLocation(0).WithArguments("extra"));
    }

    /// <summary>
    /// name属性を持たない不正な &lt;param&gt;&lt;/param&gt; タグが存在しても、
    /// 例外を起こさず無視され、他の正常なparamタグの一致判定に影響しないことを確認する
    /// </summary>
    [Fact]
    public async Task MalformedParamTagWithoutNameAttribute_IsIgnored_DoesNotReportDiagnostic()
    {
        var test = """
            public class C
            {
                /// <summary>
                /// text
                /// </summary>
                /// <param></param>
                /// <param name="first"></param>
                void M(int first) { }
            }
            """;

        await Verify.VerifyAnalyzerAsync(test);
    }

    /// <summary>
    /// GenerateDocumentationFile 無効時でも、Fixが生テキストのparamタグを正しく同期できることを確認する
    /// </summary>
    [Fact]
    public async Task Fix_RawTextFallback_SyncsParamsToMatchMethodSignature()
    {
        var test = """
            public class C
            {
                /// <summary>
                /// text
                /// </summary>
                /// <param name="first"></param>
                void {|#0:M|}(int first, int second)
                {
                }
            }
            """;

        var fixedSource = """
            public class C
            {
                /// <summary>
                /// text
                /// </summary>
                /// <param name="first"></param>
                /// <param name="second"></param>
                void M(int first, int second)
                {
                }
            }
            """;

        var codeFixTest = new CSharpCodeFixTest<FuncHeaderMismatches, FuncHeaderMismatchesFix, XUnitVerifier>
        {
            TestCode = test,
            FixedCode = fixedSource,
            TestState = { DocumentationMode = DocumentationMode.None },
        };
        codeFixTest.ExpectedDiagnostics.Add(CodeFixVerify.Diagnostic().WithLocation(0).WithArguments("second"));

        await codeFixTest.RunAsync();
    }

    /// <summary>
    /// GenerateDocumentationFile 無効時、summaryタグが自己終端（&lt;summary/&gt;）で "&lt;/summary&gt;" という
    /// 文字列を含まない場合、Fixの挿入位置決定ロジックが「装飾行(---)以降」でもなく「&lt;/summary&gt;以降」でもない、
    /// 最初の意味のある行の直後にフォールバックすることを確認する（境界値: DetermineInsertIndexの最終フォールバック）
    /// </summary>
    [Fact]
    public async Task Fix_RawTextFallback_SelfClosingSummaryTag_InsertsAfterFirstMeaningfulLine()
    {
        var test = """
            public class C
            {
                /// <summary/>
                /// <param name="wrongName"></param>
                void {|#0:M|}(int actualName)
                {
                }
            }
            """;

        var fixedSource = """
            public class C
            {
                /// <summary/>
                /// <param name="actualName"></param>
                void M(int actualName)
                {
                }
            }
            """;

        var codeFixTest = new CSharpCodeFixTest<FuncHeaderMismatches, FuncHeaderMismatchesFix, XUnitVerifier>
        {
            TestCode = test,
            FixedCode = fixedSource,
            TestState = { DocumentationMode = DocumentationMode.None },
        };
        codeFixTest.ExpectedDiagnostics.Add(CodeFixVerify.Diagnostic().WithLocation(0).WithArguments("actualName, wrongName"));

        await codeFixTest.RunAsync();
    }

    /// <summary>
    /// GenerateDocumentationFile 無効時、summaryタグが自己終端で "&lt;/summary&gt;" を含まず、
    /// かつ装飾行（---）が存在する場合、Fixの挿入位置決定ロジックが装飾行の直後に
    /// paramタグを挿入することを確認する（境界値: DetermineInsertIndexの"---"フォールバック分岐）
    /// </summary>
    [Fact]
    public async Task Fix_RawTextFallback_SelfClosingSummaryTagWithDecoratorLine_InsertsAfterDecoratorLine()
    {
        var test = """
            public class C
            {
                /// ---
                /// <summary/>
                /// <param name="wrongName"></param>
                void {|#0:M|}(int actualName)
                {
                }
            }
            """;

        var fixedSource = """
            public class C
            {
                /// ---
                /// <param name="actualName"></param>
                /// <summary/>
                void M(int actualName)
                {
                }
            }
            """;

        var codeFixTest = new CSharpCodeFixTest<FuncHeaderMismatches, FuncHeaderMismatchesFix, XUnitVerifier>
        {
            TestCode = test,
            FixedCode = fixedSource,
            TestState = { DocumentationMode = DocumentationMode.None },
        };
        codeFixTest.ExpectedDiagnostics.Add(CodeFixVerify.Diagnostic().WithLocation(0).WithArguments("actualName, wrongName"));

        await codeFixTest.RunAsync();
    }
}

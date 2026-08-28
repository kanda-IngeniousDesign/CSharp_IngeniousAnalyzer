using CSharp_IngeniousAnalyzer.Style_Linq;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
using Microsoft.CodeAnalysis.Testing.Verifiers;

namespace CSharp_IngeniousAnalyzer.Tests.Style_Linq;

using Verify = CSharpAnalyzerVerifier<LinqOptimize, XUnitVerifier>;
using CodeFixVerify = CSharpCodeFixVerifier<LinqOptimize, LinqOptimizeFix, XUnitVerifier>;

/// <summary>
/// LINQ001（LinqOptimize）の検知・Fix動作を検証するテスト
/// </summary>
public class LinqOptimizeTests
{
    /// <summary>
    /// 基本パターン：Where(pred).FirstOrDefault() のチェーンで診断が出ることを確認する
    /// </summary>
    [Fact]
    public async Task WhereFirstOrDefault_ReportsDiagnostic()
    {
        var test = """
            using System.Collections.Generic;
            using System.Linq;

            public class C
            {
                void M()
                {
                    var numberList = new List<int> { 1, 2, 3, 4, 5 };
                    if ({|#0:numberList.Where(x => 3 < x).FirstOrDefault()|} != 0) { }
                }
            }
            """;

        await Verify.VerifyAnalyzerAsync(test, Verify.Diagnostic().WithLocation(0).WithArguments("FirstOrDefault"));
    }

    /// <summary>
    /// 基本パターン：Where(pred).Any() のチェーンで診断が出ることを確認する
    /// </summary>
    [Fact]
    public async Task WhereAny_ReportsDiagnostic()
    {
        var test = """
            using System.Collections.Generic;
            using System.Linq;

            public class C
            {
                void M()
                {
                    var numberList = new List<int> { 1, 2, 3, 4, 5 };
                    if ({|#0:numberList.Where(x => 3 < x).Any()|}) { }
                }
            }
            """;

        await Verify.VerifyAnalyzerAsync(test, Verify.Diagnostic().WithLocation(0).WithArguments("Any"));
    }

    /// <summary>
    /// 基本パターン：Where(pred).Last() のチェーンで診断が出ることを確認する
    /// </summary>
    [Fact]
    public async Task WhereLast_ReportsDiagnostic()
    {
        var test = """
            using System.Collections.Generic;
            using System.Linq;

            public class C
            {
                void M()
                {
                    var numberList = new List<int> { 1, 2, 3, 4, 5 };
                    if ({|#0:numberList.Where(x => 3 < x).Last()|} != 0) { }
                }
            }
            """;

        await Verify.VerifyAnalyzerAsync(test, Verify.Diagnostic().WithLocation(0).WithArguments("Last"));
    }

    /// <summary>
    /// 素人エイリアン：Whereを2回チェーンしても、直前がWhereであれば診断が出ることを確認する
    /// </summary>
    [Fact]
    public async Task ChainedWhereWhereFirstOrDefault_ReportsDiagnostic()
    {
        var test = """
            using System.Collections.Generic;
            using System.Linq;

            public class C
            {
                void M()
                {
                    var numberList = new List<int> { 1, 2, 3, 4, 5 };
                    if ({|#0:numberList.Where(x => 1 < x).Where(y => y < 10).FirstOrDefault()|} != 0) { }
                }
            }
            """;

        await Verify.VerifyAnalyzerAsync(test, Verify.Diagnostic().WithLocation(0).WithArguments("FirstOrDefault"));
    }

    /// <summary>
    /// Where(pred).FirstOrDefault() の後ろにさらに .ToString() が続いていても、
    /// 診断の位置は内側のWhere().FirstOrDefault()部分のみに限定されることを確認する
    /// </summary>
    [Fact]
    public async Task TrailingMemberAccessAfterChain_ReportsDiagnosticOnInnerInvocationOnly()
    {
        var test = """
            using System.Collections.Generic;
            using System.Linq;

            public class C
            {
                void M()
                {
                    var numberList = new List<int> { 1, 2, 3, 4, 5 };
                    var pattern1 = {|#0:numberList.Where(x => 3 < x).FirstOrDefault()|}.ToString();
                }
            }
            """;

        await Verify.VerifyAnalyzerAsync(test, Verify.Diagnostic().WithLocation(0).WithArguments("FirstOrDefault"));
    }

    /// <summary>
    /// すでに最適化済み（Whereを介さず直接述語をFirstOrDefaultへ渡している）場合は診断が出ないことを確認する
    /// </summary>
    [Fact]
    public async Task AlreadyOptimizedDirectPredicate_DoesNotReportDiagnostic()
    {
        var test = """
            using System.Collections.Generic;
            using System.Linq;

            public class C
            {
                void M()
                {
                    var numberList = new List<int> { 1, 2, 3, 4, 5 };
                    if (numberList.FirstOrDefault(x => 3 < x) != 0) { }
                }
            }
            """;

        await Verify.VerifyAnalyzerAsync(test);
    }

    /// <summary>
    /// ローカル関数を述語として直接FirstOrDefaultへ渡している場合も、診断が出ないことを確認する
    /// </summary>
    [Fact]
    public async Task LocalFunctionPredicate_DoesNotReportDiagnostic()
    {
        var test = """
            using System.Collections.Generic;
            using System.Linq;

            public class C
            {
                void M()
                {
                    var numberList = new List<int> { 1, 2, 3, 4, 5 };
                    static bool predicate(int x) => 3 < x;
                    var pattern3 = numberList.FirstOrDefault(predicate);
                }
            }
            """;

        await Verify.VerifyAnalyzerAsync(test);
    }

    /// <summary>
    /// 独自コレクションが持つ疑似Where/Anyメソッドは、System.Linqの本物ではないため
    /// アナライザーが標準LINQと誤認して検知しないことを確認する
    /// </summary>
    [Fact]
    public async Task CustomCollectionLookalike_DoesNotReportDiagnostic()
    {
        var test = """
            using System;

            public class C
            {
                public class MyCustomCollection
                {
                    public MyCustomCollection Where(Func<int, bool> predicate) => this;
                    public bool Any() => true;
                }

                void M()
                {
                    var myClass = new MyCustomCollection();
                    if (myClass.Where(x => 3 < x).Any()) { }
                }
            }
            """;

        await Verify.VerifyAnalyzerAsync(test);
    }

    /// <summary>
    /// Fixが「Where(pred).FirstOrDefault()」を「FirstOrDefault(pred)」へ統合することを確認する
    /// </summary>
    [Fact]
    public async Task Fix_ReplacesWhereFirstOrDefaultWithDirectPredicate()
    {
        var test = """
            using System.Collections.Generic;
            using System.Linq;

            public class C
            {
                void M()
                {
                    var numberList = new List<int> { 1, 2, 3, 4, 5 };
                    if ({|#0:numberList.Where(x => 3 < x).FirstOrDefault()|} != 0) { }
                }
            }
            """;

        var fixedSource = """
            using System.Collections.Generic;
            using System.Linq;

            public class C
            {
                void M()
                {
                    var numberList = new List<int> { 1, 2, 3, 4, 5 };
                    if (numberList.FirstOrDefault(x => 3 < x) != 0) { }
                }
            }
            """;

        await CodeFixVerify.VerifyCodeFixAsync(test, CodeFixVerify.Diagnostic().WithLocation(0).WithArguments("FirstOrDefault"), fixedSource);
    }

    /// <summary>
    /// 基本パターン：Where(pred).First() のチェーンで診断が出ることを確認する
    /// </summary>
    [Fact]
    public async Task WhereFirst_ReportsDiagnostic()
    {
        var test = """
            using System.Collections.Generic;
            using System.Linq;

            public class C
            {
                void M()
                {
                    var numberList = new List<int> { 1, 2, 3, 4, 5 };
                    if ({|#0:numberList.Where(x => 3 < x).First()|} != 0) { }
                }
            }
            """;

        await Verify.VerifyAnalyzerAsync(test, Verify.Diagnostic().WithLocation(0).WithArguments("First"));
    }

    /// <summary>
    /// 基本パターン：Where(pred).LastOrDefault() のチェーンで診断が出ることを確認する
    /// </summary>
    [Fact]
    public async Task WhereLastOrDefault_ReportsDiagnostic()
    {
        var test = """
            using System.Collections.Generic;
            using System.Linq;

            public class C
            {
                void M()
                {
                    var numberList = new List<int> { 1, 2, 3, 4, 5 };
                    if ({|#0:numberList.Where(x => 3 < x).LastOrDefault()|} != 0) { }
                }
            }
            """;

        await Verify.VerifyAnalyzerAsync(test, Verify.Diagnostic().WithLocation(0).WithArguments("LastOrDefault"));
    }

    /// <summary>
    /// レシーバーを持たない（メンバーアクセスではない）単純なメソッド呼び出しは、
    /// 対象メソッド名チェックに進む前の時点で除外され、診断が出ないことを確認する
    /// </summary>
    [Fact]
    public async Task BareMethodInvocation_DoesNotReportDiagnostic()
    {
        var test = """
            public class C
            {
                void M()
                {
                    Helper();
                }

                void Helper() { }
            }
            """;

        await Verify.VerifyAnalyzerAsync(test);
    }

    /// <summary>
    /// FirstOrDefault()の直前の呼び出しがWhereではない（例：Select）場合は、
    /// 診断が出ないことを確認する
    /// </summary>
    [Fact]
    public async Task PriorCallIsSelectNotWhere_DoesNotReportDiagnostic()
    {
        var test = """
            using System.Collections.Generic;
            using System.Linq;

            public class C
            {
                void M()
                {
                    var numberList = new List<int> { 1, 2, 3, 4, 5 };
                    if (numberList.Select(x => x).FirstOrDefault() != 0) { }
                }
            }
            """;

        await Verify.VerifyAnalyzerAsync(test);
    }

    /// <summary>
    /// FirstOrDefault()の直前の呼び出しが、メンバーアクセスを介さない単純なメソッド呼び出し
    /// （Whereチェーンではない）の場合は、診断が出ないことを確認する
    /// </summary>
    [Fact]
    public async Task ReceiverInvocationIsBareMethodCall_DoesNotReportDiagnostic()
    {
        var test = """
            using System.Collections.Generic;
            using System.Linq;

            public class C
            {
                IEnumerable<int> GetNumbers() => new List<int> { 1, 2, 3 };

                void M()
                {
                    if (GetNumbers().FirstOrDefault() != 0) { }
                }
            }
            """;

        await Verify.VerifyAnalyzerAsync(test);
    }

    /// <summary>
    /// Fixが「Where(pred).Any()」を「Any(pred)」へ統合することを確認する
    /// </summary>
    [Fact]
    public async Task Fix_ReplacesWhereAnyWithDirectPredicate()
    {
        var test = """
            using System.Collections.Generic;
            using System.Linq;

            public class C
            {
                void M()
                {
                    var numberList = new List<int> { 1, 2, 3, 4, 5 };
                    if ({|#0:numberList.Where(x => 3 < x).Any()|}) { }
                }
            }
            """;

        var fixedSource = """
            using System.Collections.Generic;
            using System.Linq;

            public class C
            {
                void M()
                {
                    var numberList = new List<int> { 1, 2, 3, 4, 5 };
                    if (numberList.Any(x => 3 < x)) { }
                }
            }
            """;

        await CodeFixVerify.VerifyCodeFixAsync(test, CodeFixVerify.Diagnostic().WithLocation(0).WithArguments("Any"), fixedSource);
    }

    /// <summary>
    /// Fixが「Where(pred).Last()」を「Last(pred)」へ統合することを確認する
    /// </summary>
    [Fact]
    public async Task Fix_ReplacesWhereLastWithDirectPredicate()
    {
        var test = """
            using System.Collections.Generic;
            using System.Linq;

            public class C
            {
                void M()
                {
                    var numberList = new List<int> { 1, 2, 3, 4, 5 };
                    if ({|#0:numberList.Where(x => 3 < x).Last()|} != 0) { }
                }
            }
            """;

        var fixedSource = """
            using System.Collections.Generic;
            using System.Linq;

            public class C
            {
                void M()
                {
                    var numberList = new List<int> { 1, 2, 3, 4, 5 };
                    if (numberList.Last(x => 3 < x) != 0) { }
                }
            }
            """;

        await CodeFixVerify.VerifyCodeFixAsync(test, CodeFixVerify.Diagnostic().WithLocation(0).WithArguments("Last"), fixedSource);
    }

    /// <summary>
    /// Fixが「Where(pred).First()」を「First(pred)」へ統合することを確認する
    /// </summary>
    [Fact]
    public async Task Fix_ReplacesWhereFirstWithDirectPredicate()
    {
        var test = """
            using System.Collections.Generic;
            using System.Linq;

            public class C
            {
                void M()
                {
                    var numberList = new List<int> { 1, 2, 3, 4, 5 };
                    if ({|#0:numberList.Where(x => 3 < x).First()|} != 0) { }
                }
            }
            """;

        var fixedSource = """
            using System.Collections.Generic;
            using System.Linq;

            public class C
            {
                void M()
                {
                    var numberList = new List<int> { 1, 2, 3, 4, 5 };
                    if (numberList.First(x => 3 < x) != 0) { }
                }
            }
            """;

        await CodeFixVerify.VerifyCodeFixAsync(test, CodeFixVerify.Diagnostic().WithLocation(0).WithArguments("First"), fixedSource);
    }

    /// <summary>
    /// Fixが「Where(pred).LastOrDefault()」を「LastOrDefault(pred)」へ統合することを確認する
    /// </summary>
    [Fact]
    public async Task Fix_ReplacesWhereLastOrDefaultWithDirectPredicate()
    {
        var test = """
            using System.Collections.Generic;
            using System.Linq;

            public class C
            {
                void M()
                {
                    var numberList = new List<int> { 1, 2, 3, 4, 5 };
                    if ({|#0:numberList.Where(x => 3 < x).LastOrDefault()|} != 0) { }
                }
            }
            """;

        var fixedSource = """
            using System.Collections.Generic;
            using System.Linq;

            public class C
            {
                void M()
                {
                    var numberList = new List<int> { 1, 2, 3, 4, 5 };
                    if (numberList.LastOrDefault(x => 3 < x) != 0) { }
                }
            }
            """;

        await CodeFixVerify.VerifyCodeFixAsync(test, CodeFixVerify.Diagnostic().WithLocation(0).WithArguments("LastOrDefault"), fixedSource);
    }

    /// <summary>
    /// Where().Where().FirstOrDefault() の場合、Fixは直前（FirstOrDefaultに隣接する）のWhereのみを
    /// 統合し、その前段のWhereは変更せず残すことを確認する
    /// </summary>
    [Fact]
    public async Task Fix_ChainedWhereWhereFirstOrDefault_ReplacesNearestWhereOnly()
    {
        var test = """
            using System.Collections.Generic;
            using System.Linq;

            public class C
            {
                void M()
                {
                    var numberList = new List<int> { 1, 2, 3, 4, 5 };
                    if ({|#0:numberList.Where(x => 1 < x).Where(y => y < 10).FirstOrDefault()|} != 0) { }
                }
            }
            """;

        var fixedSource = """
            using System.Collections.Generic;
            using System.Linq;

            public class C
            {
                void M()
                {
                    var numberList = new List<int> { 1, 2, 3, 4, 5 };
                    if (numberList.Where(x => 1 < x).FirstOrDefault(y => y < 10) != 0) { }
                }
            }
            """;

        await CodeFixVerify.VerifyCodeFixAsync(test, CodeFixVerify.Diagnostic().WithLocation(0).WithArguments("FirstOrDefault"), fixedSource);
    }

    /// <summary>
    /// Where().FirstOrDefault() の後ろに .ToString() が続いていても、
    /// Fixが内側のWhere().FirstOrDefault()部分のみを正しく統合することを確認する
    /// </summary>
    [Fact]
    public async Task Fix_TrailingMemberAccessAfterChain_ReplacesInnerChainOnly()
    {
        var test = """
            using System.Collections.Generic;
            using System.Linq;

            public class C
            {
                void M()
                {
                    var numberList = new List<int> { 1, 2, 3, 4, 5 };
                    var pattern1 = {|#0:numberList.Where(x => 3 < x).FirstOrDefault()|}.ToString();
                }
            }
            """;

        var fixedSource = """
            using System.Collections.Generic;
            using System.Linq;

            public class C
            {
                void M()
                {
                    var numberList = new List<int> { 1, 2, 3, 4, 5 };
                    var pattern1 = numberList.FirstOrDefault(x => 3 < x).ToString();
                }
            }
            """;

        await CodeFixVerify.VerifyCodeFixAsync(test, CodeFixVerify.Diagnostic().WithLocation(0).WithArguments("FirstOrDefault"), fixedSource);
    }

    /// <summary>
    /// Where().FirstOrDefault() の呼び出しが他メソッドの引数としてそのまま渡されている場合
    /// （呼び出し自体のスパンが、それを包むArgumentSyntaxとタイになるケース）でも、
    /// Fixが正しく内側のチェーンを検出して統合できることを確認する
    /// （getInnermostNodeForTie: true 化前は、この形だとFixが無反応になっていた回帰テスト）
    /// </summary>
    [Fact]
    public async Task Fix_InvocationPassedAsBareMethodArgument_ReplacesInnerChainOnly()
    {
        var test = """
            using System.Collections.Generic;
            using System.Linq;

            public class C
            {
                void M()
                {
                    var numberList = new List<int> { 1, 2, 3, 4, 5 };
                    Process({|#0:numberList.Where(x => 3 < x).FirstOrDefault()|});
                }

                void Process(int value) { }
            }
            """;

        var fixedSource = """
            using System.Collections.Generic;
            using System.Linq;

            public class C
            {
                void M()
                {
                    var numberList = new List<int> { 1, 2, 3, 4, 5 };
                    Process(numberList.FirstOrDefault(x => 3 < x));
                }

                void Process(int value) { }
            }
            """;

        await CodeFixVerify.VerifyCodeFixAsync(test, CodeFixVerify.Diagnostic().WithLocation(0).WithArguments("FirstOrDefault"), fixedSource);
    }
}

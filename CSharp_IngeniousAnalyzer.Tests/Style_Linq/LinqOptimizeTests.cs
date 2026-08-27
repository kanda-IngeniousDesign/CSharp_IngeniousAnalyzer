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
}

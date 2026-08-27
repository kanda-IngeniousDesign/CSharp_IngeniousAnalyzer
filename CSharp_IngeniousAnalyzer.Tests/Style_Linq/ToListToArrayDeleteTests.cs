using CSharp_IngeniousAnalyzer.Style_Linq;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
using Microsoft.CodeAnalysis.Testing.Verifiers;

namespace CSharp_IngeniousAnalyzer.Tests.Style_Linq;

using Verify = CSharpAnalyzerVerifier<ToListToArrayDelete, XUnitVerifier>;
using CodeFixVerify = CSharpCodeFixVerifier<ToListToArrayDelete, ToListToArrayDeleteFix, XUnitVerifier>;

/// <summary>
/// LINQ002（ToListToArrayDelete）の検知・Fix動作を検証するテスト
/// </summary>
public class ToListToArrayDeleteTests
{
    /// <summary>
    /// メソッドチェーンが長く複雑でも、結果を foreach で1回しか使っていない場合は不要な ToArray() として警告することを確認する
    /// </summary>
    [Fact]
    public async Task LongChainEndingInToArray_ReportsDiagnostic()
    {
        var test = """
            using System.Collections.Generic;
            using System.Linq;

            public class C
            {
                void M(List<int> srcList)
                {
                    var chaosDel1 = {|#0:srcList.Where(n => 1 < n).Select(n => n * 2).OrderBy(n => n).ToArray()|};
                    foreach (var item in chaosDel1) { }
                }
            }
            """;

        await Verify.VerifyAnalyzerAsync(test, Verify.Diagnostic().WithLocation(0).WithArguments("srcList.Where(n => 1 < n).Select(n => n * 2).OrderBy(n => n)"));
    }

    /// <summary>
    /// Where の結果を ToList() で確定させても、変数が foreach で1回しか使われていない場合は不要な ToList() として警告することを確認する
    /// </summary>
    [Fact]
    public async Task ToListViaVariable_ReportsDiagnostic()
    {
        var test = """
            using System.Collections.Generic;
            using System.Linq;

            public class C
            {
                void M(List<int> srcList)
                {
                    var chaosDel2 = {|#0:srcList.Where(n => 0 < n).ToList()|};
                    foreach (var item in chaosDel2) { }
                }
            }
            """;

        await Verify.VerifyAnalyzerAsync(test, Verify.Diagnostic().WithLocation(0).WithArguments("srcList.Where(n => 0 < n)"));
    }

    /// <summary>
    /// 変数を経由せず foreach の式に直接 .ToList() を書いている場合でも警告することを確認する
    /// </summary>
    [Fact]
    public async Task DirectForEachUsage_ReportsDiagnostic()
    {
        var test = """
            using System.Collections.Generic;
            using System.Linq;

            public class C
            {
                void M(List<int> srcList)
                {
                    foreach (var item in {|#0:srcList.Where(n => 0 < n).ToList()|}) { }
                }
            }
            """;

        await Verify.VerifyAnalyzerAsync(test, Verify.Diagnostic().WithLocation(0).WithArguments("srcList.Where(n => 0 < n)"));
    }

    /// <summary>
    /// 確定した変数が foreach 以外でも再利用される（2回以上参照される）場合は警告しないことを確認する
    /// </summary>
    [Fact]
    public async Task VariableUsedMultipleTimes_DoesNotReportDiagnostic()
    {
        var test = """
            using System.Collections.Generic;
            using System.Linq;

            public class C
            {
                void M(List<int> srcList)
                {
                    var listMultiple = srcList.Where(n => 0 < n).ToList();
                    foreach (var item in listMultiple) { }
                    var cnt = listMultiple.Count;
                }
            }
            """;

        await Verify.VerifyAnalyzerAsync(test);
    }

    /// <summary>
    /// 確定した変数が foreach の前に別の値へ再代入される場合は警告しないことを確認する
    /// </summary>
    [Fact]
    public async Task VariableReassignedBeforeForEach_DoesNotReportDiagnostic()
    {
        var test = """
            using System.Collections.Generic;
            using System.Linq;

            public class C
            {
                void M(List<int> srcList, List<int> anotherList)
                {
                    var items = srcList.Where(n => 0 < n).ToList();
                    if (true)
                    {
                        items = anotherList;
                    }
                    foreach (var item in items) { }
                }
            }
            """;

        await Verify.VerifyAnalyzerAsync(test);
    }

    /// <summary>
    /// Fixが不要な ToList() を削除し、直前の LINQ チェーンをそのまま残すことを確認する
    /// </summary>
    [Fact]
    public async Task Fix_RemovesToListKeepingChain()
    {
        var test = """
            using System.Collections.Generic;
            using System.Linq;

            public class C
            {
                void M(List<int> srcList)
                {
                    var chaosDel2 = {|#0:srcList.Where(n => 0 < n).ToList()|};
                    foreach (var item in chaosDel2) { }
                }
            }
            """;

        var fixedSource = """
            using System.Collections.Generic;
            using System.Linq;

            public class C
            {
                void M(List<int> srcList)
                {
                    var chaosDel2 = srcList.Where(n => 0 < n);
                    foreach (var item in chaosDel2) { }
                }
            }
            """;

        await CodeFixVerify.VerifyCodeFixAsync(test, CodeFixVerify.Diagnostic().WithLocation(0).WithArguments("srcList.Where(n => 0 < n)"), fixedSource);
    }

    /// <summary>
    /// Fixが長いメソッドチェーンの末尾にある不要な ToArray() だけを削除し、チェーンの他の部分を保持することを確認する
    /// </summary>
    [Fact]
    public async Task Fix_RemovesToArrayKeepingLongChain()
    {
        var test = """
            using System.Collections.Generic;
            using System.Linq;

            public class C
            {
                void M(List<int> srcList)
                {
                    var chaosDel1 = {|#0:srcList.Where(n => 1 < n).Select(n => n * 2).OrderBy(n => n).ToArray()|};
                    foreach (var item in chaosDel1) { }
                }
            }
            """;

        var fixedSource = """
            using System.Collections.Generic;
            using System.Linq;

            public class C
            {
                void M(List<int> srcList)
                {
                    var chaosDel1 = srcList.Where(n => 1 < n).Select(n => n * 2).OrderBy(n => n);
                    foreach (var item in chaosDel1) { }
                }
            }
            """;

        await CodeFixVerify.VerifyCodeFixAsync(test, CodeFixVerify.Diagnostic().WithLocation(0).WithArguments("srcList.Where(n => 1 < n).Select(n => n * 2).OrderBy(n => n)"), fixedSource);
    }
}

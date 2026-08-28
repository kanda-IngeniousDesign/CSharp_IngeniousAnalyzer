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

    /// <summary>
    /// System.Linq.Enumerable 以外の型が独自に定義した ToList() メソッドは対象外であり警告しないことを確認する
    /// </summary>
    [Fact]
    public async Task CustomTypeToListMethod_DoesNotReportDiagnostic()
    {
        var test = """
            using System.Collections.Generic;

            public class MyCollection
            {
                public List<int> ToList() => new List<int>();
            }

            public class C
            {
                void M(MyCollection custom)
                {
                    var items = custom.ToList();
                    foreach (var item in items) { }
                }
            }
            """;

        await Verify.VerifyAnalyzerAsync(test);
    }

    /// <summary>
    /// ToHashSet() など ToList/ToArray 以外の Enumerable マテリアライズ メソッドは対象外であり警告しないことを確認する
    /// </summary>
    [Fact]
    public async Task ToHashSetMaterialization_DoesNotReportDiagnostic()
    {
        var test = """
            using System.Collections.Generic;
            using System.Linq;

            public class C
            {
                void M(List<int> srcList)
                {
                    var items = srcList.Where(n => 0 < n).ToHashSet();
                    foreach (var item in items) { }
                }
            }
            """;

        await Verify.VerifyAnalyzerAsync(test);
    }

    /// <summary>
    /// 変数に代入せず式ステートメントとして結果を破棄している場合は変数宣言子が見つからず警告しないことを確認する
    /// </summary>
    [Fact]
    public async Task StandaloneStatementResultDiscarded_DoesNotReportDiagnostic()
    {
        var test = """
            using System.Collections.Generic;
            using System.Linq;

            public class C
            {
                void M(List<int> srcList)
                {
                    srcList.Where(n => 0 < n).ToList();
                }
            }
            """;

        await Verify.VerifyAnalyzerAsync(test);
    }

    /// <summary>
    /// 明示的な型（List&lt;int&gt;）で宣言され ToList() を除去すると暗黙的に変換できなくなる場合は警告しないことを確認する
    /// </summary>
    [Fact]
    public async Task ExplicitListType_DoesNotReportDiagnostic()
    {
        var test = """
            using System.Collections.Generic;
            using System.Linq;

            public class C
            {
                void M(List<int> srcList)
                {
                    List<int> items = srcList.Where(n => 0 < n).ToList();
                    foreach (var item in items) { }
                }
            }
            """;

        await Verify.VerifyAnalyzerAsync(test);
    }

    /// <summary>
    /// 明示的な型（IEnumerable&lt;int&gt;）で宣言されていても ToList() を除去して暗黙的に代入できる場合は警告することを確認する
    /// </summary>
    [Fact]
    public async Task ExplicitInterfaceType_ReportsDiagnostic()
    {
        var test = """
            using System.Collections.Generic;
            using System.Linq;

            public class C
            {
                void M(List<int> srcList)
                {
                    IEnumerable<int> items = {|#0:srcList.Where(n => 0 < n).ToList()|};
                    foreach (var item in items) { }
                }
            }
            """;

        await Verify.VerifyAnalyzerAsync(test, Verify.Diagnostic().WithLocation(0).WithArguments("srcList.Where(n => 0 < n)"));
    }

    /// <summary>
    /// プロパティの get アクセサー内で1回だけ foreach 参照される場合も警告することを確認する
    /// </summary>
    [Fact]
    public async Task PropertyGetterAccessor_ReportsDiagnostic()
    {
        var test = """
            using System.Collections.Generic;
            using System.Linq;

            public class C
            {
                List<int> srcList = new List<int>();

                int Count
                {
                    get
                    {
                        var filtered = {|#0:srcList.Where(n => 0 < n).ToList()|};
                        int total = 0;
                        foreach (var item in filtered) { total++; }
                        return total;
                    }
                }
            }
            """;

        await Verify.VerifyAnalyzerAsync(test, Verify.Diagnostic().WithLocation(0).WithArguments("srcList.Where(n => 0 < n)"));
    }

    /// <summary>
    /// ローカル関数内で1回だけ foreach 参照される場合も警告することを確認する
    /// </summary>
    [Fact]
    public async Task LocalFunctionBody_ReportsDiagnostic()
    {
        var test = """
            using System.Collections.Generic;
            using System.Linq;

            public class C
            {
                void M(List<int> srcList)
                {
                    void Local()
                    {
                        var filtered = {|#0:srcList.Where(n => 0 < n).ToList()|};
                        foreach (var item in filtered) { }
                    }
                    Local();
                }
            }
            """;

        await Verify.VerifyAnalyzerAsync(test, Verify.Diagnostic().WithLocation(0).WithArguments("srcList.Where(n => 0 < n)"));
    }

    /// <summary>
    /// フィールド初期化子で ToList() を使用している場合はメソッド本体が特定できず警告しないことを確認する
    /// </summary>
    [Fact]
    public async Task FieldInitializer_DoesNotReportDiagnostic()
    {
        var test = """
            using System.Collections.Generic;
            using System.Linq;

            public class C
            {
                private static List<int> Source = new List<int> { 1, 2, 3 };
                private static IEnumerable<int> Filtered = Source.Where(n => 0 < n).ToList();
            }
            """;

        await Verify.VerifyAnalyzerAsync(test);
    }

    /// <summary>
    /// コンストラクター内で通常のメソッドと同じパターンを使っている場合も、メソッド本体と同様に警告することを確認する
    /// （HasSingleSafeForEachReference が ConstructorDeclarationSyntax も本体スコープとして認識する）
    /// </summary>
    [Fact]
    public async Task ConstructorBody_ReportsDiagnostic()
    {
        var test = """
            using System.Collections.Generic;
            using System.Linq;

            public class C
            {
                List<int> srcList;

                public C(List<int> input)
                {
                    srcList = input;
                    var filtered = {|#0:srcList.Where(n => 0 < n).ToList()|};
                    foreach (var item in filtered) { }
                }
            }
            """;

        await Verify.VerifyAnalyzerAsync(test, Verify.Diagnostic().WithLocation(0).WithArguments("srcList.Where(n => 0 < n)"));
    }

    /// <summary>
    /// 確定した変数が foreach 以外（return文など）で1回だけ参照される場合は警告しないことを確認する
    /// </summary>
    [Fact]
    public async Task VariableReturnedNotForEach_DoesNotReportDiagnostic()
    {
        var test = """
            using System.Collections.Generic;
            using System.Linq;

            public class C
            {
                List<int> M(List<int> srcList)
                {
                    var filtered = srcList.Where(n => 0 < n).ToList();
                    return filtered;
                }
            }
            """;

        await Verify.VerifyAnalyzerAsync(test);
    }

    /// <summary>
    /// 確定した変数が宣言後に一度も参照されない場合は警告しないことを確認する
    /// </summary>
    [Fact]
    public async Task VariableDeclaredButNeverUsed_DoesNotReportDiagnostic()
    {
        var test = """
            using System.Collections.Generic;
            using System.Linq;

            public class C
            {
                void M(List<int> srcList)
                {
                    var filtered = srcList.Where(n => 0 < n).ToList();
                }
            }
            """;

        await Verify.VerifyAnalyzerAsync(test);
    }

    /// <summary>
    /// null条件演算子（?.）経由で ToList() を呼び出す場合、invocation.Expression が MemberAccessExpressionSyntax
    /// ではなく MemberBindingExpressionSyntax となるため、レシーバー名が取得できず引数が "expression" に
    /// フォールバックすることを確認する
    /// </summary>
    [Fact]
    public async Task NullConditionalToList_ReportsDiagnosticWithFallbackName()
    {
        var test = """
            using System.Collections.Generic;
            using System.Linq;

            public class C
            {
                void M(List<int>? srcList)
                {
                    var items = srcList?{|#0:.ToList()|};
                    foreach (var item in items) { }
                }
            }
            """;

        await Verify.VerifyAnalyzerAsync(test, Verify.Diagnostic().WithLocation(0).WithArguments("expression"));
    }
}

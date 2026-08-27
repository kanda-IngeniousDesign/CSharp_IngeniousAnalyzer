using CSharp_IngeniousAnalyzer.Style_Collection;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
using Microsoft.CodeAnalysis.Testing.Verifiers;

namespace CSharp_IngeniousAnalyzer.Tests.Style_Collection;

using Verify = CSharpAnalyzerVerifier<ListCapacity, XUnitVerifier>;
using CodeFixVerify = CSharpCodeFixVerifier<ListCapacity, ListCapacityFix, XUnitVerifier>;

/// <summary>
/// COLL001（ListCapacity）の検知・Fix動作を検証するテスト
/// </summary>
public class ListCapacityTests
{
    /// <summary>
    /// ローカル変数の上限によるループでList生成（容量未指定）を行っている場合、診断が出ることを確認する
    /// </summary>
    [Fact]
    public async Task LocalVariableBound_ReportsDiagnostic()
    {
        var test = """
            using System.Collections.Generic;

            public class C
            {
                void M()
                {
                    int intMaxCount = 100;
                    var list1 = {|#0:new List<string>()|};
                    for (int i = 0; i < intMaxCount; i++)
                    {
                        list1.Add(i.ToString());
                    }
                }
            }
            """;

        await Verify.VerifyAnalyzerAsync(test, Verify.Diagnostic().WithLocation(0));
    }

    /// <summary>
    /// ループ上限が別コレクションのCountプロパティである場合、意図的に検知対象外となることを確認する
    /// </summary>
    [Fact]
    public async Task PropertyCountBound_DoesNotReportDiagnostic()
    {
        var test = """
            using System.Collections.Generic;

            public class C
            {
                void M()
                {
                    var sourceList = new List<int> { 1, 2, 3 };
                    var list2 = new List<int>();
                    for (int i = 0; i < sourceList.Count; i++)
                    {
                        list2.Add(sourceList[i]);
                    }
                }
            }
            """;

        await Verify.VerifyAnalyzerAsync(test);
    }

    /// <summary>
    /// 上限となるローカル変数がリスト生成よりも「後」で宣言されている場合、診断が出ないことを確認する
    /// </summary>
    [Fact]
    public async Task LimitDeclaredAfterList_DoesNotReportDiagnostic()
    {
        var test = """
            using System.Collections.Generic;

            public class C
            {
                void M()
                {
                    var list3 = new List<int>();
                    int lateLimit = 50;
                    for (int i = 0; i < lateLimit; i++)
                    {
                        list3.Add(i);
                    }
                }
            }
            """;

        await Verify.VerifyAnalyzerAsync(test);
    }

    /// <summary>
    /// 上限のCountプロパティを持つ元オブジェクト自体がリスト生成よりも「後」で宣言されている場合も、診断が出ないことを確認する
    /// </summary>
    [Fact]
    public async Task SourceObjectDeclaredAfterList_DoesNotReportDiagnostic()
    {
        var test = """
            using System.Collections.Generic;

            public class C
            {
                void M()
                {
                    var list4 = new List<int>();
                    var lateSource = new List<int> { 1, 2 };
                    for (int i = 0; i < lateSource.Count; i++)
                    {
                        list4.Add(i);
                    }
                }
            }
            """;

        await Verify.VerifyAnalyzerAsync(test);
    }

    /// <summary>
    /// ループ上限が単なる変数やプロパティではなく、メソッド呼び出しなど複雑な式になっている場合、診断が出ないことを確認する
    /// </summary>
    [Fact]
    public async Task ComplexExpressionOrMethodCall_DoesNotReportDiagnostic()
    {
        var test = """
            using System.Collections.Generic;

            public class C
            {
                void M()
                {
                    var list5 = new List<int>();
                    for (int i = 0; i < GetLimitCount(); i++)
                    {
                        list5.Add(i);
                    }
                }

                int GetLimitCount() => 10;
            }
            """;

        await Verify.VerifyAnalyzerAsync(test);
    }

    /// <summary>
    /// Fixが「new List&lt;T&gt;()」に、ループ上限のローカル変数を初期キャパシティとして追加することを確認する
    /// </summary>
    [Fact]
    public async Task Fix_AddsCapacityArgument()
    {
        var test = """
            using System.Collections.Generic;

            public class C
            {
                void M()
                {
                    int intMaxCount = 100;
                    var list1 = {|#0:new List<string>()|};
                    for (int i = 0; i < intMaxCount; i++)
                    {
                        list1.Add(i.ToString());
                    }
                }
            }
            """;

        var fixedSource = """
            using System.Collections.Generic;

            public class C
            {
                void M()
                {
                    int intMaxCount = 100;
                    var list1 = new List<string>(intMaxCount);
                    for (int i = 0; i < intMaxCount; i++)
                    {
                        list1.Add(i.ToString());
                    }
                }
            }
            """;

        await CodeFixVerify.VerifyCodeFixAsync(test, CodeFixVerify.Diagnostic().WithLocation(0), fixedSource);
    }
}

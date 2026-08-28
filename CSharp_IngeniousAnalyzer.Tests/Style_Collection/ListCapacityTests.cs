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

    /// <summary>
    /// List&lt;T&gt;以外の型（Dictionary等）を生成している場合、診断が出ないことを確認する
    /// </summary>
    [Fact]
    public async Task NonListType_DoesNotReportDiagnostic()
    {
        var test = """
            using System.Collections.Generic;

            public class C
            {
                void M()
                {
                    int max = 100;
                    var dict = new Dictionary<string, int>();
                    for (int i = 0; i < max; i++)
                    {
                        dict.Add(i.ToString(), i);
                    }
                }
            }
            """;

        await Verify.VerifyAnalyzerAsync(test);
    }

    /// <summary>
    /// コンストラクタで既に初期キャパシティが指定されている場合、診断が出ないことを確認する
    /// </summary>
    [Fact]
    public async Task CapacityAlreadySpecified_DoesNotReportDiagnostic()
    {
        var test = """
            using System.Collections.Generic;

            public class C
            {
                void M()
                {
                    int max = 100;
                    var list1 = new List<int>(max);
                    for (int i = 0; i < max; i++)
                    {
                        list1.Add(i);
                    }
                }
            }
            """;

        await Verify.VerifyAnalyzerAsync(test);
    }

    /// <summary>
    /// コレクション初期化子を使用している場合、診断が出ないことを確認する
    /// </summary>
    [Fact]
    public async Task CollectionInitializer_DoesNotReportDiagnostic()
    {
        var test = """
            using System.Collections.Generic;

            public class C
            {
                void M()
                {
                    int max = 3;
                    var list1 = new List<int> { 1, 2, 3 };
                    for (int i = 0; i < max; i++)
                    {
                        list1.Add(i);
                    }
                }
            }
            """;

        await Verify.VerifyAnalyzerAsync(test);
    }

    /// <summary>
    /// リスト生成がローカル変数に代入されていない場合（引数への直接渡し等）、診断が出ないことを確認する
    /// </summary>
    [Fact]
    public async Task NotAssignedToVariable_DoesNotReportDiagnostic()
    {
        var test = """
            using System.Collections.Generic;

            public class C
            {
                void M()
                {
                    Process(new List<int>());
                }

                void Process(List<int> list) { }
            }
            """;

        await Verify.VerifyAnalyzerAsync(test);
    }

    /// <summary>
    /// forループが存在せず、Add呼び出しのみでリストへ要素を追加している場合、診断が出ないことを確認する
    /// </summary>
    [Fact]
    public async Task NoForLoopPresent_DoesNotReportDiagnostic()
    {
        var test = """
            using System.Collections.Generic;

            public class C
            {
                void M()
                {
                    var list1 = new List<int>();
                    list1.Add(1);
                    list1.Add(2);
                }
            }
            """;

        await Verify.VerifyAnalyzerAsync(test);
    }

    /// <summary>
    /// 対象リストを使用しない無関係なforループが先行していても、正しいforループを発見して診断が出ることを確認する
    /// </summary>
    [Fact]
    public async Task IrrelevantForLoopPresent_StillFindsCorrectLoop_ReportsDiagnostic()
    {
        var test = """
            using System.Collections.Generic;

            public class C
            {
                void M()
                {
                    for (int j = 0; j < 5; j++)
                    {
                        DoSomething(j);
                    }

                    int intMaxCount = 100;
                    var list1 = {|#0:new List<string>()|};
                    for (int i = 0; i < intMaxCount; i++)
                    {
                        list1.Add(i.ToString());
                    }
                }

                void DoSomething(int x) { }
            }
            """;

        await Verify.VerifyAnalyzerAsync(test, Verify.Diagnostic().WithLocation(0));
    }

    /// <summary>
    /// ループ条件が「以下（&lt;=）」の場合は現状未対応であり、診断が出ないことを確認する（境界値）
    /// </summary>
    [Fact]
    public async Task LessOrEqualCondition_DoesNotReportDiagnostic()
    {
        var test = """
            using System.Collections.Generic;

            public class C
            {
                void M()
                {
                    int max = 100;
                    var list1 = new List<int>();
                    for (int i = 0; i <= max; i++)
                    {
                        list1.Add(i);
                    }
                }
            }
            """;

        await Verify.VerifyAnalyzerAsync(test);
    }

    /// <summary>
    /// ループ上限がドット付きのメンバーアクセス（フィールド）の場合、診断が出ないことを確認する
    /// </summary>
    [Fact]
    public async Task QualifiedFieldAccessBound_DoesNotReportDiagnostic()
    {
        var test = """
            using System.Collections.Generic;

            public class Config
            {
                public int MaxCount = 100;
            }

            public class C
            {
                void M(Config config)
                {
                    var list1 = new List<int>();
                    for (int i = 0; i < config.MaxCount; i++)
                    {
                        list1.Add(i);
                    }
                }
            }
            """;

        await Verify.VerifyAnalyzerAsync(test);
    }

    /// <summary>
    /// ループ上限が整数リテラルの場合でも、診断が出ることを確認する
    /// </summary>
    [Fact]
    public async Task LiteralIntegerBound_ReportsDiagnostic()
    {
        var test = """
            using System.Collections.Generic;

            public class C
            {
                void M()
                {
                    var list1 = {|#0:new List<int>()|};
                    for (int i = 0; i < 100; i++)
                    {
                        list1.Add(i);
                    }
                }
            }
            """;

        await Verify.VerifyAnalyzerAsync(test, Verify.Diagnostic().WithLocation(0));
    }

    /// <summary>
    /// ループ上限がドット無しのフィールド（定数）の場合でも、診断が出ることを確認する
    /// </summary>
    [Fact]
    public async Task FieldConstantBound_ReportsDiagnostic()
    {
        var test = """
            using System.Collections.Generic;

            public class C
            {
                private const int MaxCount = 50;

                void M()
                {
                    var list1 = {|#0:new List<int>()|};
                    for (int i = 0; i < MaxCount; i++)
                    {
                        list1.Add(i);
                    }
                }
            }
            """;

        await Verify.VerifyAnalyzerAsync(test, Verify.Diagnostic().WithLocation(0));
    }

    /// <summary>
    /// ループ上限が整数リテラルの場合、Fixがそのリテラル値をキャパシティとして追加することを確認する
    /// </summary>
    [Fact]
    public async Task Fix_AddsCapacityArgument_WithLiteralBound()
    {
        var test = """
            using System.Collections.Generic;

            public class C
            {
                void M()
                {
                    var list1 = {|#0:new List<int>()|};
                    for (int i = 0; i < 100; i++)
                    {
                        list1.Add(i);
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
                    var list1 = new List<int>(100);
                    for (int i = 0; i < 100; i++)
                    {
                        list1.Add(i);
                    }
                }
            }
            """;

        await CodeFixVerify.VerifyCodeFixAsync(test, CodeFixVerify.Diagnostic().WithLocation(0), fixedSource);
    }

    /// <summary>
    /// ループ上限がフィールド（定数）の場合、Fixがそのフィールド名をキャパシティとして追加することを確認する
    /// </summary>
    [Fact]
    public async Task Fix_AddsCapacityArgument_WithFieldBound()
    {
        var test = """
            using System.Collections.Generic;

            public class C
            {
                private const int MaxCount = 50;

                void M()
                {
                    var list1 = {|#0:new List<int>()|};
                    for (int i = 0; i < MaxCount; i++)
                    {
                        list1.Add(i);
                    }
                }
            }
            """;

        var fixedSource = """
            using System.Collections.Generic;

            public class C
            {
                private const int MaxCount = 50;

                void M()
                {
                    var list1 = new List<int>(MaxCount);
                    for (int i = 0; i < MaxCount; i++)
                    {
                        list1.Add(i);
                    }
                }
            }
            """;

        await CodeFixVerify.VerifyCodeFixAsync(test, CodeFixVerify.Diagnostic().WithLocation(0), fixedSource);
    }

    /// <summary>
    /// 対象リストを使用しない無関係なforループが先行していても、Fixが正しいforループの上限値を採用することを確認する
    /// </summary>
    [Fact]
    public async Task Fix_MultipleForLoops_PicksCorrectLoop()
    {
        var test = """
            using System.Collections.Generic;

            public class C
            {
                void M()
                {
                    for (int j = 0; j < 5; j++)
                    {
                        DoSomething(j);
                    }

                    int intMaxCount = 100;
                    var list1 = {|#0:new List<string>()|};
                    for (int i = 0; i < intMaxCount; i++)
                    {
                        list1.Add(i.ToString());
                    }
                }

                void DoSomething(int x) { }
            }
            """;

        var fixedSource = """
            using System.Collections.Generic;

            public class C
            {
                void M()
                {
                    for (int j = 0; j < 5; j++)
                    {
                        DoSomething(j);
                    }

                    int intMaxCount = 100;
                    var list1 = new List<string>(intMaxCount);
                    for (int i = 0; i < intMaxCount; i++)
                    {
                        list1.Add(i.ToString());
                    }
                }

                void DoSomething(int x) { }
            }
            """;

        await CodeFixVerify.VerifyCodeFixAsync(test, CodeFixVerify.Diagnostic().WithLocation(0), fixedSource);
    }
}

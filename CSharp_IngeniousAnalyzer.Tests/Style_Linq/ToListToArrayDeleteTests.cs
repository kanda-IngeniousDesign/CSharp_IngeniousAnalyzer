using CSharp_IngeniousAnalyzer.Style_Linq;
using Microsoft.CodeAnalysis.CSharp;
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
    /// null条件演算子（?.）経由で ToList() を呼び出す場合は警告しないことを確認する
    /// （invocation.Expression が MemberBindingExpressionSyntax となり、Fixで書き換えられず列挙元も解析できないため）
    /// </summary>
    [Fact]
    public async Task NullConditionalToList_DoesNotReportDiagnostic()
    {
        var test = """
            using System.Collections.Generic;
            using System.Linq;

            public class C
            {
                void M(List<int>? srcList)
                {
                    var items = srcList?.ToList();
                    foreach (var item in items) { }
                }
            }
            """;

        await Verify.VerifyAnalyzerAsync(test);
    }

    /// <summary>
    /// 変数経由で ToList() を確定させ、foreach ループ本体の中で列挙元のコレクション自体から
    /// Remove している場合は警告しないことを確認する。
    /// ToList() を除去すると、列挙中に列挙元を変更したことになり実行時に
    /// InvalidOperationException（コレクションが変更されました）が発生してしまうため、
    /// この ToList() は不要な確定ではなく意図的なスナップショットである
    /// </summary>
    [Fact]
    public async Task LoopRemovesFromSourceViaVariable_DoesNotReportDiagnostic()
    {
        var test = """
            using System.Collections.Generic;
            using System.Linq;

            public class C
            {
                void M(List<int> srcList)
                {
                    var filtered = srcList.Where(n => 0 < n).ToList();
                    foreach (var item in filtered)
                    {
                        srcList.Remove(item);
                    }
                }
            }
            """;

        await Verify.VerifyAnalyzerAsync(test);
    }

    /// <summary>
    /// 変数を経由せず foreach の式に直接 ToList() を書いている場合でも、
    /// ループ本体で列挙元を変更していれば警告しないことを確認する
    /// </summary>
    [Fact]
    public async Task LoopRemovesFromSourceDirectForEach_DoesNotReportDiagnostic()
    {
        var test = """
            using System.Collections.Generic;
            using System.Linq;

            public class C
            {
                void M(List<int> srcList)
                {
                    foreach (var item in srcList.Where(n => 0 < n).ToList())
                    {
                        srcList.Remove(item);
                    }
                }
            }
            """;

        await Verify.VerifyAnalyzerAsync(test);
    }

    /// <summary>
    /// foreach ループ本体で、列挙元とは無関係に見えるコレクションのメソッドを呼んでいる場合も警告しないことを確認する
    /// （unrelated が呼び出し元で srcList と同じインスタンスである可能性を排除できないため）
    /// </summary>
    [Fact]
    public async Task LoopCallsMethodOnUnrelatedCollection_DoesNotReportDiagnostic()
    {
        var test = """
            using System.Collections.Generic;
            using System.Linq;

            public class C
            {
                void M(List<int> srcList, List<int> unrelated)
                {
                    var filtered = srcList.Where(n => 0 < n).ToList();
                    foreach (var item in filtered)
                    {
                        unrelated.Add(item);
                    }
                }
            }
            """;

        await Verify.VerifyAnalyzerAsync(test);
    }

    /// <summary>
    /// foreach ループ本体で、同一クラスのメソッドを暗黙の this で呼び出している場合も警告しないことを確認する
    /// （呼び出し先で列挙元を変更している可能性を排除できないため）
    /// </summary>
    [Fact]
    public async Task LoopCallsMethodWithoutMemberAccess_DoesNotReportDiagnostic()
    {
        var test = """
            using System.Collections.Generic;
            using System.Linq;

            public class C
            {
                void M(List<int> srcList)
                {
                    var filtered = srcList.Where(n => 0 < n).ToList();
                    foreach (var item in filtered)
                    {
                        Process(item);
                    }
                }

                void Process(int item) { }
            }
            """;

        await Verify.VerifyAnalyzerAsync(test);
    }

    /// <summary>
    /// ToList() で確定させた後、foreach に入る前に列挙元を Clear() している場合は警告しないことを確認する
    /// （ToList() を削除すると遅延評価になり、ループが Clear() 後の空の列挙元を回して何も処理しなくなる）
    /// </summary>
    [Fact]
    public async Task SourceClearedBeforeForEach_DoesNotReportDiagnostic()
    {
        var test = """
            using System.Collections.Generic;
            using System.Linq;

            public class C
            {
                void M(List<int> srcList)
                {
                    var snapshot = srcList.Where(n => 0 < n).ToList();
                    srcList.Clear();
                    foreach (var item in snapshot) { }
                }
            }
            """;

        await Verify.VerifyAnalyzerAsync(test);
    }

    /// <summary>
    /// ToList() の直接呼び出し（Where なし）でも、foreach の前に列挙元へ要素を追加している場合は警告しないことを確認する
    /// （ToList() を削除すると変数が列挙元と同じインスタンスを指し、追加した要素までループで処理されてしまう）
    /// </summary>
    [Fact]
    public async Task SourceAddedBeforeForEachWithoutWhere_DoesNotReportDiagnostic()
    {
        var test = """
            using System.Collections.Generic;
            using System.Linq;

            public class C
            {
                void M(List<int> srcList)
                {
                    var snapshot = srcList.ToList();
                    srcList.Add(99);
                    foreach (var item in snapshot) { }
                }
            }
            """;

        await Verify.VerifyAnalyzerAsync(test);
    }

    /// <summary>
    /// foreach の前で列挙元の要素をインデクサーで書き換えている場合は警告しないことを確認する
    /// （メソッド呼び出しを伴わない変更も、確定後の書き換えとして検知できていることの確認）
    /// </summary>
    [Fact]
    public async Task SourceIndexerAssignedBeforeForEach_DoesNotReportDiagnostic()
    {
        var test = """
            using System.Collections.Generic;
            using System.Linq;

            public class C
            {
                void M(List<int> srcList)
                {
                    var snapshot = srcList.ToList();
                    srcList[0] = 99;
                    foreach (var item in snapshot) { }
                }
            }
            """;

        await Verify.VerifyAnalyzerAsync(test);
    }

    /// <summary>
    /// 外側のループにより、テキスト上は foreach の後にある列挙元の変更が次の周回の foreach より先に実行される場合も
    /// 警告しないことを確認する
    /// </summary>
    [Fact]
    public async Task SourceClearedAfterForEachInsideOuterLoop_DoesNotReportDiagnostic()
    {
        var test = """
            using System.Collections.Generic;
            using System.Linq;

            public class C
            {
                void M(List<int> srcList)
                {
                    var snapshot = srcList.Where(n => 0 < n).ToList();
                    for (var i = 0; i < 2; i++)
                    {
                        foreach (var item in snapshot) { }
                        srcList.Clear();
                    }
                }
            }
            """;

        await Verify.VerifyAnalyzerAsync(test);
    }

    /// <summary>
    /// foreach の前で、列挙元とは無関係に見えるコレクションのメソッドを呼んでいる場合も警告しないことを確認する
    /// （unrelated が呼び出し元で srcList と同じインスタンスである可能性を排除できないため）
    /// </summary>
    [Fact]
    public async Task UnrelatedCollectionCallBeforeForEach_DoesNotReportDiagnostic()
    {
        var test = """
            using System.Collections.Generic;
            using System.Linq;

            public class C
            {
                void M(List<int> srcList, List<int> unrelated)
                {
                    var filtered = srcList.Where(n => 0 < n).ToList();
                    unrelated.Clear();
                    foreach (var item in filtered) { }
                }
            }
            """;

        await Verify.VerifyAnalyzerAsync(test);
    }

    /// <summary>
    /// foreach の前に、中で列挙元のフィールドを Clear() するメソッドを呼んでいる場合は警告しないことを確認する
    /// （呼び出し先の処理は解析できないため、メソッド呼び出しがあれば内容を問わず変更の可能性ありとみなす）
    /// </summary>
    [Fact]
    public async Task MethodCallBeforeForEach_DoesNotReportDiagnostic()
    {
        var test = """
            using System.Collections.Generic;
            using System.Linq;

            public class C
            {
                List<int> _items = new List<int>();

                void M()
                {
                    var snapshot = _items.Where(n => 0 < n).ToList();
                    ResetItems();
                    foreach (var item in snapshot) { }
                }

                void ResetItems() => _items.Clear();
            }
            """;

        await Verify.VerifyAnalyzerAsync(test);
    }

    /// <summary>
    /// 列挙元が引数で、呼び出すメソッドに列挙元を渡していない場合でも警告しないことを確認する
    /// （呼び出し元が同じインスタンスをフィールドとして保持している可能性を排除できないため）
    /// </summary>
    [Fact]
    public async Task MethodCallWithoutSourceReferenceBeforeForEach_DoesNotReportDiagnostic()
    {
        var test = """
            using System.Collections.Generic;
            using System.Linq;

            public class C
            {
                void M(List<int> srcList)
                {
                    var snapshot = srcList.Where(n => 0 < n).ToList();
                    Reset();
                    foreach (var item in snapshot) { }
                }

                void Reset() { }
            }
            """;

        await Verify.VerifyAnalyzerAsync(test);
    }

    /// <summary>
    /// foreach の前にデリゲートを呼び出している場合は警告しないことを確認する
    /// </summary>
    [Fact]
    public async Task DelegateInvocationBeforeForEach_DoesNotReportDiagnostic()
    {
        var test = """
            using System;
            using System.Collections.Generic;
            using System.Linq;

            public class C
            {
                void M(List<int> srcList, Action beforeLoop)
                {
                    var snapshot = srcList.Where(n => 0 < n).ToList();
                    beforeLoop();
                    foreach (var item in snapshot) { }
                }
            }
            """;

        await Verify.VerifyAnalyzerAsync(test);
    }

    /// <summary>
    /// foreach の前にローカル関数を呼び出している場合は警告しないことを確認する
    /// </summary>
    [Fact]
    public async Task LocalFunctionCallBeforeForEach_DoesNotReportDiagnostic()
    {
        var test = """
            using System.Collections.Generic;
            using System.Linq;

            public class C
            {
                void M(List<int> srcList)
                {
                    var snapshot = srcList.Where(n => 0 < n).ToList();
                    Clear();
                    foreach (var item in snapshot) { }

                    void Clear() => srcList.Clear();
                }
            }
            """;

        await Verify.VerifyAnalyzerAsync(test);
    }

    /// <summary>
    /// foreach の前でオブジェクトを生成している場合は警告しないことを確認する
    /// （コンストラクターの中で列挙元を変更している可能性を排除できないため）
    /// </summary>
    [Fact]
    public async Task ObjectCreationBeforeForEach_DoesNotReportDiagnostic()
    {
        var test = """
            using System.Collections.Generic;
            using System.Linq;

            public class C
            {
                void M(List<int> srcList)
                {
                    var snapshot = srcList.Where(n => 0 < n).ToList();
                    var resetter = new Resetter();
                    foreach (var item in snapshot) { }
                }
            }

            public class Resetter { }
            """;

        await Verify.VerifyAnalyzerAsync(test);
    }

    /// <summary>
    /// foreach の前で await している場合は警告しないことを確認する
    /// （待機中に別の処理が列挙元を変更する可能性があるため）
    /// </summary>
    [Fact]
    public async Task AwaitBeforeForEach_DoesNotReportDiagnostic()
    {
        var test = """
            using System.Collections.Generic;
            using System.Linq;
            using System.Threading.Tasks;

            public class C
            {
                async Task M(List<int> srcList, Task pending)
                {
                    var snapshot = srcList.Where(n => 0 < n).ToList();
                    await pending;
                    foreach (var item in snapshot) { }
                }
            }
            """;

        await Verify.VerifyAnalyzerAsync(test);
    }

    /// <summary>
    /// foreach の前でフィールドに代入している場合は警告しないことを確認する
    /// （ラムダが読んでいる値を、メソッドやプロパティを経由して変えている可能性があるため）
    /// </summary>
    [Fact]
    public async Task FieldWriteBeforeForEach_DoesNotReportDiagnostic()
    {
        var test = """
            using System.Collections.Generic;
            using System.Linq;

            public class C
            {
                int _threshold;

                void M(List<int> srcList)
                {
                    var snapshot = srcList.Where(n => 0 < n).ToList();
                    _threshold = 10;
                    foreach (var item in snapshot) { }
                }
            }
            """;

        await Verify.VerifyAnalyzerAsync(test);
    }

    /// <summary>
    /// foreach の前でプロパティに代入している場合は警告しないことを確認する
    /// （setter の中で列挙元を変更している可能性を排除できないため）
    /// </summary>
    [Fact]
    public async Task PropertySetterBeforeForEach_DoesNotReportDiagnostic()
    {
        var test = """
            using System.Collections.Generic;
            using System.Linq;

            public class C
            {
                int Count { get; set; }

                void M(List<int> srcList)
                {
                    var snapshot = srcList.Where(n => 0 < n).ToList();
                    Count = 0;
                    foreach (var item in snapshot) { }
                }
            }
            """;

        await Verify.VerifyAnalyzerAsync(test);
    }

    /// <summary>
    /// ループ本体でフィールドに複合代入している場合は警告しないことを確認する
    /// </summary>
    [Fact]
    public async Task CompoundAssignmentToFieldInLoop_DoesNotReportDiagnostic()
    {
        var test = """
            using System.Collections.Generic;
            using System.Linq;

            public class C
            {
                int _total;

                void M(List<int> srcList)
                {
                    var snapshot = srcList.Where(n => 0 < n).ToList();
                    foreach (var item in snapshot)
                    {
                        _total += item;
                    }
                }
            }
            """;

        await Verify.VerifyAnalyzerAsync(test);
    }

    /// <summary>
    /// ループ本体でフィールドをインクリメントしている場合は警告しないことを確認する
    /// </summary>
    [Fact]
    public async Task IncrementOfFieldInLoop_DoesNotReportDiagnostic()
    {
        var test = """
            using System.Collections.Generic;
            using System.Linq;

            public class C
            {
                int _count;

                void M(List<int> srcList)
                {
                    foreach (var item in srcList.Where(n => 0 < n).ToList())
                    {
                        _count++;
                    }
                }
            }
            """;

        await Verify.VerifyAnalyzerAsync(test);
    }

    /// <summary>
    /// foreach の前でイベントを購読している場合は警告しないことを確認する
    /// （イベントの add アクセサーはメソッド呼び出しと同じく任意の処理を実行できるため）
    /// </summary>
    [Fact]
    public async Task EventSubscriptionBeforeForEach_DoesNotReportDiagnostic()
    {
        var test = """
            using System;
            using System.Collections.Generic;
            using System.Linq;

            public class C
            {
                event Action? Changed;

                void M(List<int> srcList, Action handler)
                {
                    var snapshot = srcList.Where(n => 0 < n).ToList();
                    Changed += handler;
                    foreach (var item in snapshot) { }
                }
            }
            """;

        await Verify.VerifyAnalyzerAsync(test);
    }

    /// <summary>
    /// 列挙元とは別の名前の変数（同じインスタンスを指す別名）を通して要素を書き換えている場合は警告しないことを確認する
    /// （配列の要素への書き込みは、ローカル変数への書き込みではないため変更の可能性ありとみなす）
    /// </summary>
    [Fact]
    public async Task ElementWriteViaAliasBeforeForEach_DoesNotReportDiagnostic()
    {
        var test = """
            using System.Collections.Generic;
            using System.Linq;

            public class C
            {
                void M(int[] srcArray)
                {
                    var alias = srcArray;
                    var snapshot = srcArray.Where(n => 0 < n).ToList();
                    alias[0] = -1;
                    foreach (var item in snapshot) { }
                }
            }
            """;

        await Verify.VerifyAnalyzerAsync(test);
    }

    /// <summary>
    /// ループ変数のメンバーに書き込んでいる場合は警告しないことを確認する
    /// （要素の状態を変えると、まだ評価していない要素に対するラムダの判定結果が変わりうるため）
    /// </summary>
    [Fact]
    public async Task MemberWriteOnLoopVariable_DoesNotReportDiagnostic()
    {
        var test = """
            using System.Collections.Generic;
            using System.Linq;

            public class Item
            {
                public bool Done;
            }

            public class C
            {
                void M(List<Item> srcList)
                {
                    foreach (var item in srcList.Where(i => !i.Done).ToList())
                    {
                        item.Done = true;
                    }
                }
            }
            """;

        await Verify.VerifyAnalyzerAsync(test);
    }

    /// <summary>
    /// イテレーターのループ本体で yield return している場合は警告しないことを確認する
    /// （yield return のたびに呼び出し元へ制御が戻り、その間に列挙元が変更されうるため）
    /// </summary>
    [Fact]
    public async Task YieldReturnInLoop_DoesNotReportDiagnostic()
    {
        var test = """
            using System.Collections.Generic;
            using System.Linq;

            public class C
            {
                IEnumerable<int> M(List<int> srcList)
                {
                    foreach (var item in srcList.Where(n => 0 < n).ToList())
                    {
                        yield return item;
                    }
                }
            }
            """;

        await Verify.VerifyAnalyzerAsync(test);
    }

    /// <summary>
    /// ループ本体に using がある場合は警告しないことを確認する
    /// （Dispose() の呼び出しで任意の処理が実行されるため）
    /// </summary>
    [Fact]
    public async Task UsingStatementInLoop_DoesNotReportDiagnostic()
    {
        var test = """
            using System;
            using System.Collections.Generic;
            using System.Linq;

            public class C
            {
                void M(List<int> srcList, IDisposable resource)
                {
                    foreach (var item in srcList.Where(n => 0 < n).ToList())
                    {
                        using (resource) { }
                    }
                }
            }
            """;

        await Verify.VerifyAnalyzerAsync(test);
    }

    /// <summary>
    /// ループ本体で別のコレクションを foreach している場合は警告しないことを確認する
    /// （GetEnumerator()/MoveNext() の呼び出しで任意の処理が実行されうるため）
    /// </summary>
    [Fact]
    public async Task NestedForEachInLoop_DoesNotReportDiagnostic()
    {
        var test = """
            using System.Collections.Generic;
            using System.Linq;

            public class C
            {
                void M(List<int> srcList, List<int> other)
                {
                    var total = 0;
                    foreach (var item in srcList.Where(n => 0 < n).ToList())
                    {
                        foreach (var o in other)
                        {
                            total += o;
                        }
                    }
                }
            }
            """;

        await Verify.VerifyAnalyzerAsync(test);
    }

    /// <summary>
    /// 外側のループにより、テキスト上は foreach の後ろにあるメソッド呼び出しが次の周回の foreach より先に実行される場合は
    /// 警告しないことを確認する
    /// </summary>
    [Fact]
    public async Task MethodCallAfterForEachInsideOuterLoop_DoesNotReportDiagnostic()
    {
        var test = """
            using System.Collections.Generic;
            using System.Linq;

            public class C
            {
                void M(List<int> srcList)
                {
                    var snapshot = srcList.Where(n => 0 < n).ToList();
                    for (var i = 0; i < 2; i++)
                    {
                        foreach (var item in snapshot) { }
                        Reset();
                    }
                }

                void Reset() { }
            }
            """;

        await Verify.VerifyAnalyzerAsync(test);
    }

    /// <summary>
    /// 外側のループの条件式でメソッドを呼んでいる場合は警告しないことを確認する
    /// （条件式も、宣言の後、各周回の foreach より先に評価されるため）
    /// </summary>
    [Fact]
    public async Task MethodCallInOuterLoopConditionBeforeForEach_DoesNotReportDiagnostic()
    {
        var test = """
            using System.Collections.Generic;
            using System.Linq;

            public class C
            {
                void M(List<int> srcList)
                {
                    var snapshot = srcList.Where(n => 0 < n).ToList();
                    while (TryReset())
                    {
                        foreach (var item in snapshot) { }
                    }
                }

                bool TryReset() => false;
            }
            """;

        await Verify.VerifyAnalyzerAsync(test);
    }

    /// <summary>
    /// foreach が宣言とは別のラムダの中にある場合は警告しないことを確認する
    /// （ラムダがいつ呼ばれるか分からず、それまでの間に列挙元が変更されうるため）
    /// </summary>
    [Fact]
    public async Task ForEachInsideLambda_DoesNotReportDiagnostic()
    {
        var test = """
            using System;
            using System.Collections.Generic;
            using System.Linq;

            public class C
            {
                Action M(List<int> srcList)
                {
                    var snapshot = srcList.Where(n => 0 < n).ToList();
                    return () =>
                    {
                        foreach (var item in snapshot) { }
                    };
                }
            }
            """;

        await Verify.VerifyAnalyzerAsync(test);
    }

    /// <summary>
    /// foreach が宣言とは別のローカル関数の中にあり、呼び出す前に列挙元を変更している場合は警告しないことを確認する
    /// （テキスト上の区間にはローカル関数の宣言しかなく、呼び出しの前にある Clear() を見落とすため）
    /// </summary>
    [Fact]
    public async Task ForEachInsideLocalFunction_DoesNotReportDiagnostic()
    {
        var test = """
            using System.Collections.Generic;
            using System.Linq;

            public class C
            {
                void M(List<int> srcList)
                {
                    var snapshot = srcList.Where(n => 0 < n).ToList();

                    void Run()
                    {
                        foreach (var item in snapshot) { }
                    }

                    srcList.Clear();
                    Run();
                }
            }
            """;

        await Verify.VerifyAnalyzerAsync(test);
    }

    /// <summary>
    /// メソッド内に goto がある場合は警告しないことを確認する
    /// （テキスト上の順序と実行順序が一致せず、確定からループ終了までの区間を特定できないため）
    /// </summary>
    [Fact]
    public async Task GotoInMethod_DoesNotReportDiagnostic()
    {
        var test = """
            using System.Collections.Generic;
            using System.Linq;

            public class C
            {
                void M(List<int> srcList, bool retry)
                {
                    var snapshot = srcList.Where(n => 0 < n).ToList();
                again:
                    foreach (var item in snapshot) { }
                    if (retry)
                    {
                        retry = false;
                        srcList.Clear();
                        goto again;
                    }
                }
            }
            """;

        await Verify.VerifyAnalyzerAsync(test);
    }

    /// <summary>
    /// 同じ宣言文の後続の宣言子でメソッドを呼んでいる場合は警告しないことを確認する
    /// （後続の宣言子の初期化子は、ToList() の後に実行されるため）
    /// </summary>
    [Fact]
    public async Task MethodCallInFollowingDeclarator_DoesNotReportDiagnostic()
    {
        var test = """
            using System.Collections.Generic;
            using System.Linq;

            public class C
            {
                List<int> _items = new List<int>();

                void M()
                {
                    IEnumerable<int> snapshot = _items.Where(n => 0 < n).ToList(), replaced = Reset();
                    foreach (var item in snapshot) { }
                }

                List<int> Reset()
                {
                    _items.Clear();
                    return new List<int>();
                }
            }
            """;

        await Verify.VerifyAnalyzerAsync(test);
    }

    /// <summary>
    /// チェーンのラムダが参照している変数を、foreach の前で変更している場合は警告しないことを確認する
    /// （ToList() を削除するとラムダの実行が foreach の時点にずれ、変更後の値でフィルターされてしまうため）
    /// </summary>
    [Fact]
    public async Task CapturedVariableChangedBeforeForEach_DoesNotReportDiagnostic()
    {
        var test = """
            using System.Collections.Generic;
            using System.Linq;

            public class C
            {
                void M(List<int> srcList)
                {
                    var limit = 3;
                    var filtered = srcList.Where(n => n > limit).ToList();
                    limit = 30;
                    foreach (var item in filtered) { }
                }
            }
            """;

        await Verify.VerifyAnalyzerAsync(test);
    }

    /// <summary>
    /// チェーンのラムダが参照している変数を、ループ本体で変更している場合（変数経由）は警告しないことを確認する
    /// （ToList() を削除するとラムダとループ本体が交互に実行され、途中から変更後の値でフィルターされてしまうため）
    /// </summary>
    [Fact]
    public async Task CapturedVariableChangedInLoopViaVariable_DoesNotReportDiagnostic()
    {
        var test = """
            using System.Collections.Generic;
            using System.Linq;

            public class C
            {
                void M(List<int> srcList)
                {
                    var limit = 3;
                    var filtered = srcList.Where(n => n > limit).ToList();
                    foreach (var item in filtered)
                    {
                        limit++;
                    }
                }
            }
            """;

        await Verify.VerifyAnalyzerAsync(test);
    }

    /// <summary>
    /// チェーンのラムダが参照している変数を、ループ本体で変更している場合（foreach に直接記述）も警告しないことを確認する
    /// </summary>
    [Fact]
    public async Task CapturedVariableChangedInLoopDirectForEach_DoesNotReportDiagnostic()
    {
        var test = """
            using System.Collections.Generic;
            using System.Linq;

            public class C
            {
                void M(List<int> srcList)
                {
                    var limit = 3;
                    foreach (var item in srcList.Where(n => n > limit).ToList())
                    {
                        limit = item;
                    }
                }
            }
            """;

        await Verify.VerifyAnalyzerAsync(test);
    }

    /// <summary>
    /// チェーンのラムダが参照しているメソッドの引数を、foreach の前で変更している場合も警告しないことを確認する
    /// </summary>
    [Fact]
    public async Task CapturedParameterChangedBeforeForEach_DoesNotReportDiagnostic()
    {
        var test = """
            using System.Collections.Generic;
            using System.Linq;

            public class C
            {
                void M(List<int> srcList, int limit)
                {
                    var filtered = srcList.Where(n => n > limit).ToList();
                    limit = 30;
                    foreach (var item in filtered) { }
                }
            }
            """;

        await Verify.VerifyAnalyzerAsync(test);
    }

    /// <summary>
    /// チェーンのラムダが参照している変数を、ループ本体で読むだけの場合も警告しないことを確認する
    /// （読み取りだけなら結果は変わらないが、チェーンで使う変数に後ろで触れていれば一律に警告しない、安全側の仕様であることの確認）
    /// </summary>
    [Fact]
    public async Task CapturedVariableReadInLoop_DoesNotReportDiagnostic()
    {
        var test = """
            using System.Collections.Generic;
            using System.Linq;

            public class C
            {
                void M(List<int> srcList)
                {
                    var limit = 3;
                    var count = 0;
                    foreach (var item in srcList.Where(n => n > limit).ToList())
                    {
                        if (item > limit * 2) count++;
                    }
                }
            }
            """;

        await Verify.VerifyAnalyzerAsync(test);
    }

    /// <summary>
    /// チェーンのラムダが参照している変数を、分解代入で変更している場合は警告しないことを確認する
    /// </summary>
    [Fact]
    public async Task CapturedVariableChangedByDeconstruction_DoesNotReportDiagnostic()
    {
        var test = """
            using System.Collections.Generic;
            using System.Linq;

            public class C
            {
                void M(List<int> srcList)
                {
                    var limit = 3;
                    var other = 0;
                    var filtered = srcList.Where(n => n > limit).ToList();
                    (limit, other) = (30, 1);
                    foreach (var item in filtered) { }
                }
            }
            """;

        await Verify.VerifyAnalyzerAsync(test);
    }

    /// <summary>
    /// チェーンのラムダが参照している変数を、ref ローカル（別名）経由で変更している場合は警告しないことを確認する
    /// （ref ローカルへの代入は参照先の変数を書き換えるため、ローカル変数への書き込みとはみなさない）
    /// </summary>
    [Fact]
    public async Task CapturedVariableChangedViaRefLocal_DoesNotReportDiagnostic()
    {
        var test = """
            using System.Collections.Generic;
            using System.Linq;

            public class C
            {
                void M(List<int> srcList)
                {
                    var limit = 3;
                    ref int alias = ref limit;
                    var filtered = srcList.Where(n => n > limit).ToList();
                    alias = 30;
                    foreach (var item in filtered) { }
                }
            }
            """;

        await Verify.VerifyAnalyzerAsync(test);
    }

    /// <summary>
    /// Take(n) の n のように、チェーンでラムダの外から参照している変数を後ろで変更している場合も警告しないことを確認する
    /// （Take(n) は呼び出し時に値を受け取るため実際には結果が変わらないが、チェーンで使う変数に触れていれば一律に警告しない安全側の仕様）
    /// </summary>
    [Fact]
    public async Task EagerArgumentVariableChangedAfter_DoesNotReportDiagnostic()
    {
        var test = """
            using System.Collections.Generic;
            using System.Linq;

            public class C
            {
                void M(List<int> srcList)
                {
                    var n = 2;
                    var firstItems = srcList.Take(n).ToList();
                    n = 5;
                    foreach (var item in firstItems) { }
                }
            }
            """;

        await Verify.VerifyAnalyzerAsync(test);
    }

    /// <summary>
    /// チェーンのラムダの中でメソッドを呼んでいる場合は警告しないことを確認する
    /// （呼び出しの実行タイミングがループ本体と交互にずれ、副作用の順序が変わりうるため）
    /// </summary>
    [Fact]
    public async Task MethodCallInsideChainLambda_DoesNotReportDiagnostic()
    {
        var test = """
            using System.Collections.Generic;
            using System.Linq;

            public class C
            {
                void M(List<int> srcList)
                {
                    var filtered = srcList.Where(n => IsValid(n)).ToList();
                    foreach (var item in filtered) { }
                }

                bool IsValid(int n) => 0 < n;
            }
            """;

        await Verify.VerifyAnalyzerAsync(test);
    }

    /// <summary>
    /// チェーンにメソッドグループを渡している場合は警告しないことを確認する
    /// （列挙時に実行されるメソッドの中身を確認できないため）
    /// </summary>
    [Fact]
    public async Task MethodGroupInChain_DoesNotReportDiagnostic()
    {
        var test = """
            using System.Collections.Generic;
            using System.Linq;

            public class C
            {
                void M(List<int> srcList)
                {
                    var filtered = srcList.Where(IsValid).ToList();
                    foreach (var item in filtered) { }
                }

                bool IsValid(int n) => 0 < n;
            }
            """;

        await Verify.VerifyAnalyzerAsync(test);
    }

    /// <summary>
    /// チェーンにデリゲート変数を渡している場合は警告しないことを確認する
    /// （デリゲートの中で参照している変数が後から変更される可能性を確認できないため）
    /// </summary>
    [Fact]
    public async Task DelegateVariableInChain_DoesNotReportDiagnostic()
    {
        var test = """
            using System;
            using System.Collections.Generic;
            using System.Linq;

            public class C
            {
                void M(List<int> srcList)
                {
                    var limit = 3;
                    Func<int, bool> predicate = n => n > limit;
                    var filtered = srcList.Where(predicate).ToList();
                    limit = 30;
                    foreach (var item in filtered) { }
                }
            }
            """;

        await Verify.VerifyAnalyzerAsync(test);
    }

    /// <summary>
    /// チェーンのラムダの中でオブジェクトを生成している場合は警告しないことを確認する
    /// （コンストラクターの実行タイミングがずれ、副作用の順序が変わりうるため）
    /// </summary>
    [Fact]
    public async Task ObjectCreationInsideChainLambda_DoesNotReportDiagnostic()
    {
        var test = """
            using System.Collections.Generic;
            using System.Linq;

            public class Box
            {
                public Box(int value) { }
            }

            public class C
            {
                void M(List<int> srcList)
                {
                    var boxes = srcList.Select(n => new Box(n)).ToList();
                    foreach (var box in boxes) { }
                }
            }
            """;

        await Verify.VerifyAnalyzerAsync(test);
    }

    /// <summary>
    /// チェーンのラムダの中で変数をインクリメントしている場合は警告しないことを確認する
    /// （ToList() があるとループ前に全要素ぶん加算済みだが、削除するとループ本体と交互に加算されるため）
    /// </summary>
    [Fact]
    public async Task IncrementInsideChainLambda_DoesNotReportDiagnostic()
    {
        var test = """
            using System.Collections.Generic;
            using System.Linq;

            public class C
            {
                void M(List<int> srcList)
                {
                    var counter = 0;
                    var numbered = srcList.Select(n => counter++).ToList();
                    foreach (var item in numbered) { }
                }
            }
            """;

        await Verify.VerifyAnalyzerAsync(test);
    }

    /// <summary>
    /// チェーンのステートメント形式のラムダの中で代入している場合は警告しないことを確認する
    /// </summary>
    [Fact]
    public async Task AssignmentInsideChainLambda_DoesNotReportDiagnostic()
    {
        var test = """
            using System.Collections.Generic;
            using System.Linq;

            public class C
            {
                int _last;

                void M(List<int> srcList)
                {
                    var filtered = srcList.Where(n => { _last = n; return 0 < n; }).ToList();
                    foreach (var item in filtered) { }
                }
            }
            """;

        await Verify.VerifyAnalyzerAsync(test);
    }

    /// <summary>
    /// チェーンのラムダの中で DateTime.Now を参照している場合は警告しないことを確認する
    /// （評価のタイミングがずれると、比較に使う現在時刻が変わるため）
    /// </summary>
    [Fact]
    public async Task DateTimeNowInsideChainLambda_DoesNotReportDiagnostic()
    {
        var test = """
            using System;
            using System.Collections.Generic;
            using System.Linq;

            public class C
            {
                void M(List<DateTime> srcList)
                {
                    var past = srcList.Where(d => d < DateTime.Now).ToList();
                    foreach (var item in past) { }
                }
            }
            """;

        await Verify.VerifyAnalyzerAsync(test);
    }

    /// <summary>
    /// チェーンがクエリ構文の場合は警告しないことを確認する
    /// （クエリ構文は暗黙のラムダを含み、ラムダの中身を同じ方法で解析できないため）
    /// </summary>
    [Fact]
    public async Task QuerySyntaxChain_DoesNotReportDiagnostic()
    {
        var test = """
            using System.Collections.Generic;
            using System.Linq;

            public class C
            {
                void M(List<int> srcList)
                {
                    var filtered = (from n in srcList where 0 < n select n).ToList();
                    foreach (var item in filtered) { }
                }
            }
            """;

        await Verify.VerifyAnalyzerAsync(test);
    }

    /// <summary>
    /// チェーンが IQueryable の場合は警告しないことを確認する
    /// （DBクエリ等は列挙時に外部へ問い合わせるため、評価を遅らせると取得結果や接続の状態が変わりうるため）
    /// </summary>
    [Fact]
    public async Task QueryableChain_DoesNotReportDiagnostic()
    {
        var test = """
            using System.Collections.Generic;
            using System.Linq;

            public class C
            {
                void M(List<int> srcList)
                {
                    var filtered = srcList.AsQueryable().Where(n => 0 < n).ToList();
                    foreach (var item in filtered) { }
                }
            }
            """;

        await Verify.VerifyAnalyzerAsync(test);
    }

    /// <summary>
    /// IQueryable を AsEnumerable() で IEnumerable に変換した後でも、チェーンの途中に IQueryable があれば警告しないことを確認する
    /// </summary>
    [Fact]
    public async Task QueryableChainConvertedToEnumerable_DoesNotReportDiagnostic()
    {
        var test = """
            using System.Collections.Generic;
            using System.Linq;

            public class C
            {
                void M(List<int> srcList)
                {
                    var filtered = srcList.AsQueryable().Where(n => 0 < n).AsEnumerable().ToList();
                    foreach (var item in filtered) { }
                }
            }
            """;

        await Verify.VerifyAnalyzerAsync(test);
    }

    /// <summary>
    /// チェーンのラムダの中の LINQ にメソッドグループを渡している場合は警告しないことを確認する
    /// </summary>
    [Fact]
    public async Task MethodGroupInsideNestedLinqInLambda_DoesNotReportDiagnostic()
    {
        var test = """
            using System.Collections.Generic;
            using System.Linq;

            public class Group
            {
                public List<int> Items = new List<int>();
            }

            public class C
            {
                void M(List<Group> srcList)
                {
                    var groups = srcList.Where(g => g.Items.Any(IsValid)).ToList();
                    foreach (var group in groups) { }
                }

                bool IsValid(int n) => 0 < n;
            }
            """;

        await Verify.VerifyAnalyzerAsync(test);
    }

    /// <summary>
    /// 確定後の処理が、チェーンと無関係なローカル変数への書き込みだけの場合は警告することを確認する
    /// （ローカル変数はチェーンから見えないため、遅延評価にしても結果が変わらない）
    /// </summary>
    [Fact]
    public async Task LocalVariableWritesOnly_ReportsDiagnostic()
    {
        var test = """
            using System.Collections.Generic;
            using System.Linq;

            public class C
            {
                int M(List<int> srcList)
                {
                    var total = 0;
                    var count = 0;
                    var filtered = {|#0:srcList.Where(n => 0 < n).ToList()|};
                    count = 1;
                    foreach (var item in filtered)
                    {
                        total += item;
                        count++;
                        --count;
                        var (a, b) = (item, item);
                        (total, _) = (total + a - b, 0);
                    }
                    return total + count;
                }
            }
            """;

        await Verify.VerifyAnalyzerAsync(test, Verify.Diagnostic().WithLocation(0).WithArguments("srcList.Where(n => 0 < n)"));
    }

    /// <summary>
    /// チェーンと無関係な値渡しの引数への書き込みだけの場合は警告することを確認する
    /// </summary>
    [Fact]
    public async Task ValueParameterWriteInLoop_ReportsDiagnostic()
    {
        var test = """
            using System.Collections.Generic;
            using System.Linq;

            public class C
            {
                int M(List<int> srcList, int total)
                {
                    foreach (var item in {|#0:srcList.Where(n => 0 < n).ToList()|})
                    {
                        total += item;
                    }
                    return total;
                }
            }
            """;

        await Verify.VerifyAnalyzerAsync(test, Verify.Diagnostic().WithLocation(0).WithArguments("srcList.Where(n => 0 < n)"));
    }

    /// <summary>
    /// メソッド呼び出しがループより後ろにあるだけの場合は警告することを確認する
    /// （ループが終わった後の処理は、列挙結果に影響しない）
    /// </summary>
    [Fact]
    public async Task MethodCallAfterLoop_ReportsDiagnostic()
    {
        var test = """
            using System.Collections.Generic;
            using System.Linq;

            public class C
            {
                void M(List<int> srcList)
                {
                    var total = 0;
                    var filtered = {|#0:srcList.Where(n => 0 < n).ToList()|};
                    foreach (var item in filtered)
                    {
                        total += item;
                    }
                    srcList.Clear();
                    Report(total);
                }

                void Report(int total) { }
            }
            """;

        await Verify.VerifyAnalyzerAsync(test, Verify.Diagnostic().WithLocation(0).WithArguments("srcList.Where(n => 0 < n)"));
    }

    /// <summary>
    /// メソッド呼び出しが ToList() より前にあるだけの場合は警告することを確認する
    /// </summary>
    [Fact]
    public async Task MethodCallBeforeDeclaration_ReportsDiagnostic()
    {
        var test = """
            using System.Collections.Generic;
            using System.Linq;

            public class C
            {
                void M(List<int> srcList)
                {
                    srcList.Add(1);
                    var filtered = {|#0:srcList.Where(n => 0 < n).ToList()|};
                    foreach (var item in filtered) { }
                }
            }
            """;

        await Verify.VerifyAnalyzerAsync(test, Verify.Diagnostic().WithLocation(0).WithArguments("srcList.Where(n => 0 < n)"));
    }

    /// <summary>
    /// 外側のループが宣言と foreach の両方を含み、メソッド呼び出しが宣言より前にある場合は警告することを確認する
    /// （周回ごとに宣言からやり直されるため、宣言より前の処理は ToList() の有無に関係しない）
    /// </summary>
    [Fact]
    public async Task MethodCallInOuterLoopBeforeDeclaration_ReportsDiagnostic()
    {
        var test = """
            using System.Collections.Generic;
            using System.Linq;

            public class C
            {
                void M(List<int> srcList)
                {
                    for (var i = 0; i < 2; i++)
                    {
                        srcList.Add(i);
                        var filtered = {|#0:srcList.Where(n => 0 < n).ToList()|};
                        foreach (var item in filtered) { }
                    }
                }
            }
            """;

        await Verify.VerifyAnalyzerAsync(test, Verify.Diagnostic().WithLocation(0).WithArguments("srcList.Where(n => 0 < n)"));
    }

    /// <summary>
    /// チェーンのラムダの中で、ラムダを渡す LINQ を使っているだけの場合は警告することを確認する
    /// （LINQ のメソッド自体は副作用を持たないため）
    /// </summary>
    [Fact]
    public async Task NestedLinqInsideChainLambda_ReportsDiagnostic()
    {
        var test = """
            using System.Collections.Generic;
            using System.Linq;

            public class Group
            {
                public List<int> Items = new List<int>();
            }

            public class C
            {
                void M(List<Group> srcList)
                {
                    var groups = {|#0:srcList.Where(g => g.Items.Any(n => 0 < n)).ToList()|};
                    foreach (var group in groups) { }
                }
            }
            """;

        await Verify.VerifyAnalyzerAsync(test, Verify.Diagnostic().WithLocation(0).WithArguments("srcList.Where(g => g.Items.Any(n => 0 < n))"));
    }

    /// <summary>
    /// チェーンのラムダの外（Take の引数など）でメソッドを呼んでいるだけの場合は警告することを確認する
    /// （ラムダの外の式は ToList() の有無に関係なく、確定させる行で1回だけ評価されるため）
    /// </summary>
    [Fact]
    public async Task MethodCallOutsideChainLambda_ReportsDiagnostic()
    {
        var test = """
            using System.Collections.Generic;
            using System.Linq;

            public class C
            {
                void M(List<int> srcList)
                {
                    var firstItems = {|#0:GetSource().Where(n => 0 < n).Take(GetCount()).ToList()|};
                    foreach (var item in firstItems) { }
                }

                List<int> GetSource() => new List<int>();
                int GetCount() => 2;
            }
            """;

        await Verify.VerifyAnalyzerAsync(test, Verify.Diagnostic().WithLocation(0).WithArguments("GetSource().Where(n => 0 < n).Take(GetCount())"));
    }

    /// <summary>
    /// ループ本体で nameof を使っているだけの場合は警告することを確認する
    /// （nameof はメソッド呼び出しの形をしているが、コンパイル時に定数へ置き換わるため）
    /// </summary>
    [Fact]
    public async Task NameOfInLoop_ReportsDiagnostic()
    {
        var test = """
            using System.Collections.Generic;
            using System.Linq;

            public class C
            {
                void M(List<int> srcList)
                {
                    var name = "";
                    foreach (var item in {|#0:srcList.Where(n => 0 < n).ToList()|})
                    {
                        name = nameof(item);
                    }
                }
            }
            """;

        await Verify.VerifyAnalyzerAsync(test, Verify.Diagnostic().WithLocation(0).WithArguments("srcList.Where(n => 0 < n)"));
    }

    /// <summary>
    /// ループ本体でプロパティを読むだけの場合は警告することを確認する
    /// （getter は副作用を持たないという前提で扱う）
    /// </summary>
    [Fact]
    public async Task PropertyReadInLoop_ReportsDiagnostic()
    {
        var test = """
            using System.Collections.Generic;
            using System.Linq;

            public class C
            {
                int M(List<string> srcList)
                {
                    var total = 0;
                    foreach (var s in {|#0:srcList.Where(s => s.Length > 0).ToList()|})
                    {
                        total += s.Length;
                    }
                    return total;
                }
            }
            """;

        await Verify.VerifyAnalyzerAsync(test, Verify.Diagnostic().WithLocation(0).WithArguments("srcList.Where(s => s.Length > 0)"));
    }

    /// <summary>
    /// ループ本体に break があるだけの場合は警告することを確認する
    /// （途中で抜けると、まだ評価していない要素に対するラムダは実行されなくなるが、ラムダに副作用がないため結果は変わらない）
    /// </summary>
    [Fact]
    public async Task BreakInLoop_ReportsDiagnostic()
    {
        var test = """
            using System.Collections.Generic;
            using System.Linq;

            public class C
            {
                int M(List<int> srcList)
                {
                    var first = 0;
                    foreach (var item in {|#0:srcList.Where(n => 0 < n).ToList()|})
                    {
                        first = item;
                        break;
                    }
                    return first;
                }
            }
            """;

        await Verify.VerifyAnalyzerAsync(test, Verify.Diagnostic().WithLocation(0).WithArguments("srcList.Where(n => 0 < n)"));
    }

    /// <summary>
    /// ループ本体で、チェーンのラムダの引数と同じ名前の別の変数に書き込んでいるだけの場合は警告することを確認する
    /// （名前ではなくシンボルで比較しているため、別の変数を取り違えないことの確認）
    /// </summary>
    [Fact]
    public async Task SameNameDifferentVariableInLoop_ReportsDiagnostic()
    {
        var test = """
            using System.Collections.Generic;
            using System.Linq;

            public class C
            {
                void M(List<int> srcList)
                {
                    foreach (var item in {|#0:srcList.Where(n => 0 < n).ToList()|})
                    {
                        var n = item;
                        n++;
                    }
                }
            }
            """;

        await Verify.VerifyAnalyzerAsync(test, Verify.Diagnostic().WithLocation(0).WithArguments("srcList.Where(n => 0 < n)"));
    }

    /// <summary>
    /// チェーンのラムダの中で配列や匿名型を作っているだけの場合は警告することを確認する
    /// （配列や匿名型の生成は、ユーザー定義のコンストラクターを実行しないため）
    /// </summary>
    [Fact]
    public async Task ArrayCreationAndAnonymousObjectInChain_ReportsDiagnostic()
    {
        var test = """
            using System.Collections.Generic;
            using System.Linq;

            public class C
            {
                void M(List<int> srcList)
                {
                    var pairs = {|#0:srcList.Select(n => new { Value = n, Twice = new[] { n, n } }).ToArray()|};
                    foreach (var pair in pairs) { }
                }
            }
            """;

        await Verify.VerifyAnalyzerAsync(test, Verify.Diagnostic().WithLocation(0).WithArguments("srcList.Select(n => new { Value = n, Twice = new[] { n, n } })"));
    }

    /// <summary>
    /// ループ本体で、別のラムダに捕まえられているローカル変数へ書き込んでいる場合は警告しないことを確認する
    /// （そのラムダが列挙元を作る遅延クエリかどうかを区別せず、ほかの場所から見える変数への書き込みとして安全側に倒す）
    /// </summary>
    [Fact]
    public async Task CapturedLocalWriteInLoop_DoesNotReportDiagnostic()
    {
        var test = """
            using System;
            using System.Collections.Generic;
            using System.Linq;

            public class C
            {
                Func<int> M(List<int> srcList)
                {
                    var total = 0;
                    Func<int> getTotal = () => total;
                    foreach (var item in srcList.Where(n => 0 < n).ToList())
                    {
                        total += item;
                    }
                    return getTotal;
                }
            }
            """;

        await Verify.VerifyAnalyzerAsync(test);
    }

    /// <summary>
    /// ループ本体で with 式を使っている場合は警告しないことを確認する
    /// （record のコピーコンストラクターは任意の処理を実行できるため。初期化子で代入しない with { } でも警告しないことの確認）
    /// </summary>
    [Fact]
    public async Task WithExpressionInLoop_DoesNotReportDiagnostic()
    {
        var test = """
            using System.Collections.Generic;
            using System.Linq;

            public record Point(int X);

            public class C
            {
                void M(List<int> srcList, Point origin)
                {
                    foreach (var item in srcList.Where(n => 0 < n).ToList())
                    {
                        var copied = origin with { };
                    }
                }
            }
            """;

        await VerifyNet80Async(test);
    }

    /// <summary>
    /// ループ本体でコレクション式を使っている場合は警告しないことを確認する
    /// （作成先の型によっては、ユーザー定義の Add やビルダーメソッドが実行されるため）
    /// </summary>
    [Fact]
    public async Task CollectionExpressionInLoop_DoesNotReportDiagnostic()
    {
        var test = """
            using System.Collections.Generic;
            using System.Linq;

            public class C
            {
                void M(List<int> srcList)
                {
                    foreach (var item in srcList.Where(n => 0 < n).ToList())
                    {
                        int[] pair = [item, item];
                    }
                }
            }
            """;

        await VerifyNet80Async(test);
    }

    /// <summary>
    /// ループ本体で位置パターンを使っている場合は警告しないことを確認する
    /// （ユーザー定義の Deconstruct が実行されるため）
    /// </summary>
    [Fact]
    public async Task PositionalPatternInLoop_DoesNotReportDiagnostic()
    {
        var test = """
            using System.Collections.Generic;
            using System.Linq;

            public class Pair
            {
                public void Deconstruct(out int a, out int b) { a = 0; b = 0; }
            }

            public class C
            {
                void M(List<int> srcList, Pair pair)
                {
                    var hit = false;
                    foreach (var item in srcList.Where(n => 0 < n).ToList())
                    {
                        hit = pair is (1, _);
                    }
                }
            }
            """;

        await Verify.VerifyAnalyzerAsync(test);
    }

    /// <summary>
    /// ループ本体で await foreach している場合は警告しないことを確認する
    /// </summary>
    [Fact]
    public async Task AwaitForEachInLoop_DoesNotReportDiagnostic()
    {
        var test = """
            using System.Collections.Generic;
            using System.Linq;
            using System.Threading.Tasks;

            public class C
            {
                async Task M(List<int> srcList, IAsyncEnumerable<int> stream)
                {
                    var total = 0;
                    foreach (var item in srcList.Where(n => 0 < n).ToList())
                    {
                        await foreach (var s in stream)
                        {
                            total += s;
                        }
                    }
                }
            }
            """;

        await VerifyNet80Async(test);
    }

    /// <summary>
    /// 確定後に using 宣言がある場合は警告しないことを確認する
    /// （スコープの終わりで Dispose() が呼ばれるため）
    /// </summary>
    [Fact]
    public async Task UsingDeclarationBeforeForEach_DoesNotReportDiagnostic()
    {
        var test = """
            using System;
            using System.Collections.Generic;
            using System.Linq;

            public class C
            {
                void M(List<int> srcList, IDisposable resource)
                {
                    var snapshot = srcList.Where(n => 0 < n).ToList();
                    using var scope = resource;
                    foreach (var item in snapshot) { }
                }
            }
            """;

        await Verify.VerifyAnalyzerAsync(test);
    }

    /// <summary>
    /// チェーンのラムダの中で await している場合は警告しないことを確認する
    /// </summary>
    [Fact]
    public async Task AwaitInsideChainLambda_DoesNotReportDiagnostic()
    {
        var test = """
            using System.Collections.Generic;
            using System.Linq;
            using System.Threading.Tasks;

            public class C
            {
                void M(List<int> srcList, Task pending)
                {
                    var tasks = srcList.Select(async n => { await pending; return n; }).ToList();
                    foreach (var task in tasks) { }
                }
            }
            """;

        await Verify.VerifyAnalyzerAsync(test);
    }

    /// <summary>
    /// チェーンのステートメント形式のラムダの中で foreach している場合は警告しないことを確認する
    /// （GetEnumerator()/MoveNext() の呼び出しで任意の処理が実行されうるため）
    /// </summary>
    [Fact]
    public async Task ForEachInsideChainLambda_DoesNotReportDiagnostic()
    {
        var test = """
            using System.Collections.Generic;
            using System.Linq;

            public class C
            {
                void M(List<List<int>> srcList)
                {
                    var allPositive = srcList.Where(list =>
                    {
                        foreach (var n in list)
                        {
                            if (n <= 0) return false;
                        }
                        return true;
                    }).ToList();
                    foreach (var list in allPositive) { }
                }
            }
            """;

        await Verify.VerifyAnalyzerAsync(test);
    }

    /// <summary>
    /// チェーンのステートメント形式のラムダの中に using がある場合は警告しないことを確認する
    /// </summary>
    [Fact]
    public async Task UsingInsideChainLambda_DoesNotReportDiagnostic()
    {
        var test = """
            using System;
            using System.Collections.Generic;
            using System.Linq;

            public class C
            {
                void M(List<IDisposable> srcList)
                {
                    var disposed = srcList.Where(d =>
                    {
                        using (d) { }
                        return true;
                    }).ToList();
                    foreach (var item in disposed) { }
                }
            }
            """;

        await Verify.VerifyAnalyzerAsync(test);
    }

    /// <summary>
    /// チェーンのラムダの中で with 式を使っている場合は警告しないことを確認する
    /// （初期化子で代入しない with { } でも、コピーコンストラクターが実行されるため警告しない）
    /// </summary>
    [Fact]
    public async Task WithExpressionInsideChainLambda_DoesNotReportDiagnostic()
    {
        var test = """
            using System.Collections.Generic;
            using System.Linq;

            public record Point(int X);

            public class C
            {
                void M(List<Point> srcList)
                {
                    var copies = srcList.Select(p => p with { }).ToList();
                    foreach (var item in copies) { }
                }
            }
            """;

        await VerifyNet80Async(test);
    }

    /// <summary>
    /// チェーンのラムダの中でコレクション式を使っている場合は警告しないことを確認する
    /// </summary>
    [Fact]
    public async Task CollectionExpressionInsideChainLambda_DoesNotReportDiagnostic()
    {
        var test = """
            using System.Collections.Generic;
            using System.Linq;

            public class C
            {
                void M(List<int> srcList)
                {
                    var pairs = srcList.Select(n => (int[])[n, n]).ToList();
                    foreach (var pair in pairs) { }
                }
            }
            """;

        await VerifyNet80Async(test);
    }

    /// <summary>
    /// チェーンのラムダの中で位置パターンを使っている場合は警告しないことを確認する
    /// </summary>
    [Fact]
    public async Task PositionalPatternInsideChainLambda_DoesNotReportDiagnostic()
    {
        var test = """
            using System.Collections.Generic;
            using System.Linq;

            public class Pair
            {
                public void Deconstruct(out int a, out int b) { a = 0; b = 0; }
            }

            public class C
            {
                void M(List<Pair> srcList)
                {
                    var hits = srcList.Where(p => p is (1, _)).ToList();
                    foreach (var item in hits) { }
                }
            }
            """;

        await Verify.VerifyAnalyzerAsync(test);
    }

    /// <summary>
    /// チェーンのラムダの中で前置デクリメントしている場合は警告しないことを確認する
    /// </summary>
    [Fact]
    public async Task PrefixDecrementInsideChainLambda_DoesNotReportDiagnostic()
    {
        var test = """
            using System.Collections.Generic;
            using System.Linq;

            public class C
            {
                void M(List<int> srcList)
                {
                    var remaining = 10;
                    var numbered = srcList.Select(n => --remaining).ToList();
                    foreach (var item in numbered) { }
                }
            }
            """;

        await Verify.VerifyAnalyzerAsync(test);
    }

    /// <summary>
    /// チェーンのラムダの中で DateTimeOffset.UtcNow を参照している場合は警告しないことを確認する
    /// </summary>
    [Fact]
    public async Task DateTimeOffsetUtcNowInsideChainLambda_DoesNotReportDiagnostic()
    {
        var test = """
            using System;
            using System.Collections.Generic;
            using System.Linq;

            public class C
            {
                void M(List<DateTimeOffset> srcList)
                {
                    var past = srcList.Where(d => d < DateTimeOffset.UtcNow).ToList();
                    foreach (var item in past) { }
                }
            }
            """;

        await Verify.VerifyAnalyzerAsync(test);
    }

    /// <summary>
    /// チェーンのラムダの中で Environment.TickCount を参照している場合は警告しないことを確認する
    /// </summary>
    [Fact]
    public async Task EnvironmentTickCountInsideChainLambda_DoesNotReportDiagnostic()
    {
        var test = """
            using System;
            using System.Collections.Generic;
            using System.Linq;

            public class C
            {
                void M(List<int> srcList)
                {
                    var expired = srcList.Where(t => t < Environment.TickCount).ToList();
                    foreach (var item in expired) { }
                }
            }
            """;

        await Verify.VerifyAnalyzerAsync(test);
    }

    /// <summary>
    /// チェーンのラムダの中で、時刻に依存しない静的プロパティ（DateTime.MaxValue 等）を参照しているだけの場合は警告することを確認する
    /// （非決定的なプロパティの判定が、同じ型の別のメンバーまで巻き込んでいないことの確認）
    /// </summary>
    [Fact]
    public async Task OtherStaticPropertyInsideChainLambda_ReportsDiagnostic()
    {
        var test = """
            using System;
            using System.Collections.Generic;
            using System.Linq;

            public class C
            {
                void M(List<DateTime> srcList)
                {
                    var valid = {|#0:srcList.Where(d => d < DateTime.MaxValue && d.Year > 2000).ToList()|};
                    foreach (var item in valid) { }
                }
            }
            """;

        await Verify.VerifyAnalyzerAsync(test, Verify.Diagnostic().WithLocation(0).WithArguments("srcList.Where(d => d < DateTime.MaxValue && d.Year > 2000)"));
    }

    /// <summary>
    /// チェーンに delegate キーワードの匿名メソッドを渡している場合も、ラムダと同様に中身を確認して警告することを確認する
    /// </summary>
    [Fact]
    public async Task AnonymousMethodInChain_ReportsDiagnostic()
    {
        var test = """
            using System.Collections.Generic;
            using System.Linq;

            public class C
            {
                void M(List<int> srcList)
                {
                    var filtered = {|#0:srcList.Where(delegate (int n) { return 0 < n; }).ToList()|};
                    foreach (var item in filtered) { }
                }
            }
            """;

        await Verify.VerifyAnalyzerAsync(test, Verify.Diagnostic().WithLocation(0).WithArguments("srcList.Where(delegate (int n) { return 0 < n; })"));
    }

    /// <summary>
    /// ループ本体に lock があるだけの場合は警告することを確認する
    /// （lock はユーザー定義の処理を実行しないため）
    /// </summary>
    [Fact]
    public async Task LockInLoop_ReportsDiagnostic()
    {
        var test = """
            using System.Collections.Generic;
            using System.Linq;

            public class C
            {
                readonly object _gate = new object();

                int M(List<int> srcList)
                {
                    var total = 0;
                    foreach (var item in {|#0:srcList.Where(n => 0 < n).ToList()|})
                    {
                        lock (_gate)
                        {
                            total += item;
                        }
                    }
                    return total;
                }
            }
            """;

        await Verify.VerifyAnalyzerAsync(test, Verify.Diagnostic().WithLocation(0).WithArguments("srcList.Where(n => 0 < n)"));
    }

    /// <summary>
    /// イテレーターのループ本体に yield break があるだけの場合は警告することを確認する
    /// （yield break は呼び出し元へ制御を戻したまま再開しないため、yield return とは扱いが異なる）
    /// </summary>
    [Fact]
    public async Task YieldBreakInLoop_ReportsDiagnostic()
    {
        var test = """
            using System.Collections.Generic;
            using System.Linq;

            public class C
            {
                IEnumerable<int> M(List<int> srcList)
                {
                    var count = 0;
                    foreach (var item in {|#0:srcList.Where(n => 0 < n).ToList()|})
                    {
                        count += item;
                        if (count > 10) yield break;
                    }
                    yield return count;
                }
            }
            """;

        await Verify.VerifyAnalyzerAsync(test, Verify.Diagnostic().WithLocation(0).WithArguments("srcList.Where(n => 0 < n)"));
    }

    /// <summary>
    /// ループ本体でクエリ構文を使っている場合は警告しないことを確認する
    /// （クエリ構文は Where/Select 等のメソッド呼び出しに変換され、任意の処理が実行されうるため）
    /// </summary>
    [Fact]
    public async Task QuerySyntaxInLoop_DoesNotReportDiagnostic()
    {
        var test = """
            using System.Collections.Generic;
            using System.Linq;

            public class C
            {
                void M(List<int> srcList, List<int> other)
                {
                    foreach (var item in srcList.Where(n => 0 < n).ToList())
                    {
                        var matches = from o in other where o == item select o;
                    }
                }
            }
            """;

        await Verify.VerifyAnalyzerAsync(test);
    }

    /// <summary>
    /// 列挙元がフィールドで、確定後にそのフィールドを読むだけの場合も警告しないことを確認する
    /// （列挙元に後ろで触れていれば一律に警告しない、安全側の仕様であることの確認）
    /// </summary>
    [Fact]
    public async Task SourceFieldReadBeforeForEach_DoesNotReportDiagnostic()
    {
        var test = """
            using System.Collections.Generic;
            using System.Linq;

            public class C
            {
                List<int> _items = new List<int>();

                void M()
                {
                    var snapshot = _items.Where(n => 0 < n).ToList();
                    var count = _items.Count;
                    foreach (var item in snapshot) { }
                }
            }
            """;

        await Verify.VerifyAnalyzerAsync(test);
    }

    /// <summary>
    /// ループ本体でフィールドを前置インクリメントしている場合は警告しないことを確認する
    /// </summary>
    [Fact]
    public async Task PrefixIncrementOfFieldInLoop_DoesNotReportDiagnostic()
    {
        var test = """
            using System.Collections.Generic;
            using System.Linq;

            public class C
            {
                int _count;

                void M(List<int> srcList)
                {
                    foreach (var item in srcList.Where(n => 0 < n).ToList())
                    {
                        ++_count;
                    }
                }
            }
            """;

        await Verify.VerifyAnalyzerAsync(test);
    }

    /// <summary>
    /// ループ本体で ref 引数に書き込んでいる場合は警告しないことを確認する
    /// （ref 引数は呼び出し元の変数やフィールドの別名であり、ローカル変数への書き込みとはみなさない）
    /// </summary>
    [Fact]
    public async Task RefParameterWriteInLoop_DoesNotReportDiagnostic()
    {
        var test = """
            using System.Collections.Generic;
            using System.Linq;

            public class C
            {
                void M(List<int> srcList, ref int total)
                {
                    foreach (var item in srcList.Where(n => 0 < n).ToList())
                    {
                        total += item;
                    }
                }
            }
            """;

        await Verify.VerifyAnalyzerAsync(test);
    }

    /// <summary>
    /// チェーンのラムダの中で nameof を使っているだけの場合は警告することを確認する
    /// （nameof はコンパイル時に定数へ置き換わるため、メソッド呼び出しとはみなさない）
    /// </summary>
    [Fact]
    public async Task NameOfInsideChainLambda_ReportsDiagnostic()
    {
        var test = """
            using System.Collections.Generic;
            using System.Linq;

            public class C
            {
                void M(List<string> srcList)
                {
                    var names = {|#0:srcList.Where(s => s != nameof(M)).ToList()|};
                    foreach (var name in names) { }
                }
            }
            """;

        await Verify.VerifyAnalyzerAsync(test, Verify.Diagnostic().WithLocation(0).WithArguments("srcList.Where(s => s != nameof(M))"));
    }

    /// <summary>
    /// 分解代入の書き込み先にフィールドが含まれている場合は警告しないことを確認する
    /// （タプルの要素ごとに書き込み先を確認し、ローカル変数以外への書き込みを見落とさないことの確認）
    /// </summary>
    [Fact]
    public async Task DeconstructionIntoFieldInLoop_DoesNotReportDiagnostic()
    {
        var test = """
            using System.Collections.Generic;
            using System.Linq;

            public class C
            {
                int _last;

                void M(List<int> srcList)
                {
                    var count = 0;
                    foreach (var item in srcList.Where(n => 0 < n).ToList())
                    {
                        (count, _last) = (count + 1, item);
                    }
                }
            }
            """;

        await Verify.VerifyAnalyzerAsync(test);
    }

    /// <summary>
    /// 列挙元が先に作った遅延クエリで、そのラムダが捕まえているローカル変数をループ本体で変更している場合は警告しないことを確認する
    /// （ToList() を削除すると、ループ本体での変更がまだ評価していない要素のフィルター条件に影響するため）
    /// </summary>
    [Fact]
    public async Task LazySourceCapturingLocalChangedInLoop_DoesNotReportDiagnostic()
    {
        var test = """
            using System.Collections.Generic;
            using System.Linq;

            public class C
            {
                int M(List<int> srcList)
                {
                    var limit = 1;
                    IEnumerable<int> big = srcList.Where(x => x > limit);
                    var total = 0;
                    foreach (var x in big.ToList())
                    {
                        total += x;
                        limit = 10;
                    }
                    return total;
                }
            }
            """;

        await Verify.VerifyAnalyzerAsync(test);
    }

    /// <summary>
    /// 列挙元が先に作った遅延クエリで、そのラムダが捕まえているローカル変数をループ本体で変更している場合（変数経由）も警告しないことを確認する
    /// </summary>
    [Fact]
    public async Task LazySourceCapturingLocalChangedInLoopViaVariable_DoesNotReportDiagnostic()
    {
        var test = """
            using System.Collections.Generic;
            using System.Linq;

            public class C
            {
                int M(List<int> srcList)
                {
                    var offset = 0;
                    var shifted = srcList.Select(x => x + offset);
                    var snapshot = shifted.ToArray();
                    var total = 0;
                    foreach (var x in snapshot)
                    {
                        total += x;
                        offset += 100;
                    }
                    return total;
                }
            }
            """;

        await Verify.VerifyAnalyzerAsync(test);
    }

    /// <summary>
    /// チェーンに渡した別の遅延クエリ（Concat の引数）が捕まえているローカル変数を、ループ本体で変更している場合は警告しないことを確認する
    /// </summary>
    [Fact]
    public async Task LazyArgumentCapturingLocalChangedInLoop_DoesNotReportDiagnostic()
    {
        var test = """
            using System.Collections.Generic;
            using System.Linq;

            public class C
            {
                int M(List<int> first, List<int> second)
                {
                    var lim = 0;
                    var tail = second.Where(v => v > lim);
                    var total = 0;
                    foreach (var x in first.Concat(tail).ToList())
                    {
                        total += x;
                        lim = 100;
                    }
                    return total;
                }
            }
            """;

        await Verify.VerifyAnalyzerAsync(test);
    }

    /// <summary>
    /// アドレスを取られているローカル変数をループ本体で変更している場合は警告しないことを確認する
    /// （ポインター経由でチェーンから参照されている可能性があるため）
    /// </summary>
    [Fact]
    public async Task LocalAddressTakenChangedInLoop_DoesNotReportDiagnostic()
    {
        var test = """
            using System.Collections.Generic;
            using System.Linq;

            public class C
            {
                unsafe int M(List<int> srcList)
                {
                    var limit = 1;
                    int* p = &limit;
                    var total = 0;
                    foreach (var x in srcList.Where(v => v > 0).ToList())
                    {
                        total += x + *p;
                        limit = 10;
                    }
                    return total;
                }
            }
            """;

        await VerifyUnsafeAsync(test);
    }

    /// <summary>
    /// プライマリコンストラクターの引数をループ本体で変更している場合は警告しないことを確認する
    /// （実体はフィールドであり、チェーンのラムダからプロパティ経由で参照されうるため）
    /// </summary>
    [Fact]
    public async Task PrimaryConstructorParameterChangedInLoop_DoesNotReportDiagnostic()
    {
        var test = """
            using System.Collections.Generic;
            using System.Linq;

            public class C(int limit)
            {
                int Limit => limit;

                int M(List<int> srcList)
                {
                    var total = 0;
                    foreach (var x in srcList.Where(v => v > Limit).ToList())
                    {
                        total += x;
                        limit = 10;
                    }
                    return total;
                }
            }
            """;

        await VerifyNet80Async(test);
    }

    /// <summary>
    /// for の初期化子で確定させ、ループ本体で foreach している場合は警告しないことを確認する
    /// （初期化子は1回しか実行されないため、foreach が同じ確定結果を周回ごとに何度も列挙する）
    /// </summary>
    [Fact]
    public async Task DeclaredInForInitializer_DoesNotReportDiagnostic()
    {
        var test = """
            using System.Collections.Generic;
            using System.Linq;

            public class C
            {
                void M(List<int> srcList)
                {
                    var total = 0;
                    for (var q = srcList.Where(x => x > 0).ToList(); total < 100; total++)
                    {
                        foreach (var x in q) { total += x; }
                        srcList.Add(10);
                    }
                }
            }
            """;

        await Verify.VerifyAnalyzerAsync(test);
    }

    /// <summary>
    /// 外側のループで同じ確定結果を何度も列挙する場合は、ループ内に変更がなくても警告しないことを確認する
    /// （ToList() を削除すると周回ごとにラムダが実行し直され、作った配列が周回ごとに別のインスタンスになるため）
    /// </summary>
    [Fact]
    public async Task EnumeratedMultipleTimesByOuterLoop_DoesNotReportDiagnostic()
    {
        var test = """
            using System.Collections.Generic;
            using System.Linq;

            public class C
            {
                int M(List<int> srcList)
                {
                    var arrays = srcList.Select(x => new int[] { x }).ToList();
                    var count = 0;
                    for (var i = 0; i < 2; i++)
                    {
                        foreach (var a in arrays) { count += a.Length; }
                    }
                    return count;
                }
            }
            """;

        await Verify.VerifyAnalyzerAsync(test);
    }

    /// <summary>
    /// Enumerable.ToList(src) のような静的メソッド形式の呼び出しは警告しないことを確認する
    /// （Fixが受け手として型名 Enumerable を取り出してしまい、コンパイルエラーになるため）
    /// </summary>
    [Fact]
    public async Task StaticCallForm_DoesNotReportDiagnostic()
    {
        var test = """
            using System.Collections.Generic;
            using System.Linq;

            public class C
            {
                void M(List<int> srcList)
                {
                    foreach (var x in Enumerable.ToList(srcList)) { }
                }
            }
            """;

        await Verify.VerifyAnalyzerAsync(test);
    }

    /// <summary>
    /// ToList&lt;object&gt;() のように列挙元より広い型引数を明示している場合は警告しないことを確認する
    /// （削除するとループ変数の型が object から string に変わり、== が参照比較から値比較に変わるため）
    /// </summary>
    [Fact]
    public async Task ExplicitWiderTypeArgument_DoesNotReportDiagnostic()
    {
        var test = """
            using System.Collections.Generic;
            using System.Linq;

            public class C
            {
                int M(List<string> strs)
                {
                    var hits = 0;
                    foreach (var x in strs.ToList<object>())
                    {
                        if (x == "aa") hits++;
                    }
                    return hits;
                }
            }
            """;

        await Verify.VerifyAnalyzerAsync(test);
    }

    /// <summary>
    /// 列挙元の公開 GetEnumerator() が、IEnumerable&lt;T&gt; の明示的な実装と異なる場合は警告しないことを確認する
    /// （削除すると foreach が公開 GetEnumerator() を使うようになり、ループ変数の型や列挙内容が変わるため）
    /// </summary>
    [Fact]
    public async Task PatternGetEnumeratorDiffersFromInterface_DoesNotReportDiagnostic()
    {
        var test = """
            using System.Collections;
            using System.Collections.Generic;
            using System.Linq;

            public class Coll : IEnumerable<string>
            {
                readonly List<string> _items = new List<string> { "aa", "b" };
                public IEnumerator GetEnumerator() => _items.GetEnumerator();
                IEnumerator<string> IEnumerable<string>.GetEnumerator() => _items.GetEnumerator();
            }

            public class C
            {
                int M(Coll coll)
                {
                    var hits = 0;
                    var items = coll.ToList();
                    foreach (var x in items)
                    {
                        if (x == "aa") hits++;
                    }
                    return hits;
                }
            }
            """;

        await Verify.VerifyAnalyzerAsync(test);
    }

    /// <summary>
    /// MatchCollection.ToList() は警告しないことを確認する
    /// （MatchCollection の公開 GetEnumerator() は非ジェネリックで、削除するとループ変数が object になりコンパイルできなくなるため）
    /// </summary>
    [Fact]
    public async Task MatchCollectionToList_DoesNotReportDiagnostic()
    {
        var test = """
            using System.Collections.Generic;
            using System.Linq;
            using System.Text.RegularExpressions;

            public class C
            {
                int M(string text)
                {
                    var length = 0;
                    foreach (var m in Regex.Matches(text, "[0-9]").ToList())
                    {
                        length += m.Length;
                    }
                    return length;
                }
            }
            """;

        await Verify.VerifyAnalyzerAsync(test);
    }

    /// <summary>
    /// using static System.DateTime; で Now を単独の識別子として参照している場合も警告しないことを確認する
    /// </summary>
    [Fact]
    public async Task UsingStaticNowInsideChainLambda_DoesNotReportDiagnostic()
    {
        var test = """
            using System;
            using System.Collections.Generic;
            using System.Linq;
            using static System.DateTime;

            public class C
            {
                void M(List<DateTime> srcList)
                {
                    var past = srcList.Where(d => d < Now).ToList();
                    foreach (var item in past) { }
                }
            }
            """;

        await Verify.VerifyAnalyzerAsync(test);
    }

    /// <summary>
    /// チェーンのラムダの中で Stopwatch の経過時間を参照している場合は警告しないことを確認する
    /// </summary>
    [Fact]
    public async Task StopwatchElapsedInsideChainLambda_DoesNotReportDiagnostic()
    {
        var test = """
            using System.Collections.Generic;
            using System.Diagnostics;
            using System.Linq;

            public class C
            {
                void M(List<long> srcList, Stopwatch watch)
                {
                    var expired = srcList.Where(t => t < watch.ElapsedMilliseconds).ToList();
                    foreach (var item in expired) { }
                }
            }
            """;

        await Verify.VerifyAnalyzerAsync(test);
    }

    /// <summary>
    /// 配列を ToList() しているだけの場合は警告することを確認する
    /// （配列の foreach は、ToList() と同じ要素を同じ順序で列挙するため）
    /// </summary>
    [Fact]
    public async Task ArrayReceiverToList_ReportsDiagnostic()
    {
        var test = """
            using System.Collections.Generic;
            using System.Linq;

            public class C
            {
                int M(int[] values)
                {
                    var total = 0;
                    foreach (var x in {|#0:values.ToList()|}) { total += x; }
                    return total;
                }
            }
            """;

        await Verify.VerifyAnalyzerAsync(test, Verify.Diagnostic().WithLocation(0).WithArguments("values"));
    }

    /// <summary>
    /// 文字列を ToList() しているだけの場合は警告することを確認する
    /// （string の公開 GetEnumerator() は .NET 標準の実装で、同じ文字を同じ順序で列挙するため）
    /// </summary>
    [Fact]
    public async Task StringReceiverToList_ReportsDiagnostic()
    {
        var test = """
            using System.Collections.Generic;
            using System.Linq;

            public class C
            {
                int M(string text)
                {
                    var count = 0;
                    foreach (var c in {|#0:text.ToList()|}) { if (c == 'a') count++; }
                    return count;
                }
            }
            """;

        await Verify.VerifyAnalyzerAsync(test, Verify.Diagnostic().WithLocation(0).WithArguments("text"));
    }

    /// <summary>
    /// 自作コレクションの公開 GetEnumerator() が IEnumerable&lt;T&gt; の実装そのものである場合は警告することを確認する
    /// </summary>
    [Fact]
    public async Task CustomCollectionImplementingInterface_ReportsDiagnostic()
    {
        var test = """
            using System.Collections;
            using System.Collections.Generic;
            using System.Linq;

            public class Coll : IEnumerable<int>
            {
                readonly List<int> _items = new List<int> { 1, 2 };
                public IEnumerator<int> GetEnumerator() => _items.GetEnumerator();
                IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
            }

            public class C
            {
                int M(Coll coll)
                {
                    var total = 0;
                    foreach (var x in {|#0:coll.ToList()|}) { total += x; }
                    return total;
                }
            }
            """;

        await Verify.VerifyAnalyzerAsync(test, Verify.Diagnostic().WithLocation(0).WithArguments("coll"));
    }

    /// <summary>
    /// ToList&lt;string&gt;() のように列挙元と同じ型引数を明示しているだけの場合は警告することを確認する
    /// </summary>
    [Fact]
    public async Task SameExplicitTypeArgument_ReportsDiagnostic()
    {
        var test = """
            using System.Collections.Generic;
            using System.Linq;

            public class C
            {
                void M(List<string> strs)
                {
                    foreach (var x in {|#0:strs.Where(s => s.Length > 0).ToList<string>()|}) { }
                }
            }
            """;

        await Verify.VerifyAnalyzerAsync(test, Verify.Diagnostic().WithLocation(0).WithArguments("strs.Where(s => s.Length > 0)"));
    }

    /// <summary>
    /// ラムダの中で宣言したローカル変数に、同じラムダの中で書き込んでいるだけの場合は警告することを確認する
    /// （宣言と同じラムダの中での参照は、ほかの場所から見える変数とはみなさない）
    /// </summary>
    [Fact]
    public async Task LocalDeclaredAndWrittenInSameLambda_ReportsDiagnostic()
    {
        var test = """
            using System;
            using System.Collections.Generic;
            using System.Linq;

            public class C
            {
                Func<int> M(List<int> srcList)
                {
                    return () =>
                    {
                        var total = 0;
                        foreach (var x in {|#0:srcList.Where(n => 0 < n).ToList()|}) { total += x; }
                        return total;
                    };
                }
            }
            """;

        await Verify.VerifyAnalyzerAsync(test, Verify.Diagnostic().WithLocation(0).WithArguments("srcList.Where(n => 0 < n)"));
    }

    /// <summary>
    /// 宣言と foreach がどちらも同じラムダの中にあり、そのラムダが外側の foreach の式の中に書かれている場合は警告することを確認する
    /// （宣言と foreach の共通の祖先より外側は両方をまとめて繰り返すため、1回の確定に対して列挙は1回のまま）
    /// </summary>
    [Fact]
    public async Task DeclarationAndForEachInLambdaInsideOuterForEachExpression_ReportsDiagnostic()
    {
        var test = """
            using System;
            using System.Collections.Generic;
            using System.Linq;

            public class C
            {
                void M(List<int> srcList)
                {
                    foreach (var f in new Func<int>[] { () => { var total = 0; var q = {|#0:srcList.Where(n => 0 < n).ToList()|}; foreach (var x in q) { total += x; } return total; } })
                    {
                    }
                }
            }
            """;

        await Verify.VerifyAnalyzerAsync(test, Verify.Diagnostic().WithLocation(0).WithArguments("srcList.Where(n => 0 < n)"));
    }

    /// <summary>
    /// 自作コレクションの公開 GetEnumerator() が、要素の型は同じでも IEnumerable&lt;T&gt; の明示的な実装とは別の内容を返す場合は警告しないことを確認する
    /// （削除すると foreach が公開 GetEnumerator() を使うようになり、列挙される内容が変わるため）
    /// </summary>
    [Fact]
    public async Task PatternGetEnumeratorWithSameElementTypeDiffers_DoesNotReportDiagnostic()
    {
        var test = """
            using System.Collections;
            using System.Collections.Generic;
            using System.Linq;

            public class Coll : IEnumerable<string>
            {
                readonly List<string> _items = new List<string> { "a", "b" };
                public IEnumerator<string> GetEnumerator() => _items.AsEnumerable().Reverse().GetEnumerator();
                IEnumerator<string> IEnumerable<string>.GetEnumerator() => _items.GetEnumerator();
                IEnumerator IEnumerable.GetEnumerator() => _items.GetEnumerator();
            }

            public class C
            {
                string M(Coll coll)
                {
                    var first = "";
                    foreach (var x in coll.ToList())
                    {
                        first = x;
                        break;
                    }
                    return first;
                }
            }
            """;

        await Verify.VerifyAnalyzerAsync(test);
    }

    /// <summary>
    /// 列挙元が先に作った遅延クエリで、そのラムダが捕まえているメソッドの引数をループ本体で変更している場合は警告しないことを確認する
    /// </summary>
    [Fact]
    public async Task LazySourceCapturingParameterChangedInLoop_DoesNotReportDiagnostic()
    {
        var test = """
            using System.Collections.Generic;
            using System.Linq;

            public class C
            {
                int M(List<int> srcList, int limit)
                {
                    var big = srcList.Where(x => x > limit);
                    var total = 0;
                    foreach (var x in big.ToList())
                    {
                        total += x;
                        limit = 10;
                    }
                    return total;
                }
            }
            """;

        await Verify.VerifyAnalyzerAsync(test);
    }

    /// <summary>
    /// ループ本体でローカル変数に集計しているだけの安全なケースで、Fixが ToList() だけを削除し本体をそのまま残すことを確認する
    /// </summary>
    [Fact]
    public async Task Fix_RemovesToListKeepingLoopBody()
    {
        var test = """
            using System.Collections.Generic;
            using System.Linq;

            public class C
            {
                int M(List<int> srcList)
                {
                    var total = 0;
                    var filtered = {|#0:srcList.Where(n => 0 < n).ToList()|};
                    foreach (var item in filtered)
                    {
                        total += item;
                    }
                    return total;
                }
            }
            """;

        var fixedSource = """
            using System.Collections.Generic;
            using System.Linq;

            public class C
            {
                int M(List<int> srcList)
                {
                    var total = 0;
                    var filtered = srcList.Where(n => 0 < n);
                    foreach (var item in filtered)
                    {
                        total += item;
                    }
                    return total;
                }
            }
            """;

        await CodeFixVerify.VerifyCodeFixAsync(test, CodeFixVerify.Diagnostic().WithLocation(0).WithArguments("srcList.Where(n => 0 < n)"), fixedSource);
    }

    /// <summary>
    /// record / IAsyncEnumerable / コレクション式など、既定より新しい参照アセンブリが必要なコードを .NET 8 の参照で検証する
    /// </summary>
    private static Task VerifyNet80Async(string test, params DiagnosticResult[] expected)
    {
        var analyzerTest = new CSharpAnalyzerTest<ToListToArrayDelete, XUnitVerifier>
        {
            TestCode = test,
            ReferenceAssemblies = ReferenceAssemblies.Net.Net80,
        };
        analyzerTest.ExpectedDiagnostics.AddRange(expected);
        return analyzerTest.RunAsync();
    }

    /// <summary>
    /// ポインターを使うコードを、unsafe を許可したコンパイルオプションで検証する
    /// </summary>
    private static Task VerifyUnsafeAsync(string test, params DiagnosticResult[] expected)
    {
        var analyzerTest = new CSharpAnalyzerTest<ToListToArrayDelete, XUnitVerifier> { TestCode = test };
        analyzerTest.SolutionTransforms.Add((solution, projectId) =>
        {
            var options = (CSharpCompilationOptions)solution.GetProject(projectId)!.CompilationOptions!;
            return solution.WithProjectCompilationOptions(projectId, options.WithAllowUnsafe(true));
        });
        analyzerTest.ExpectedDiagnostics.AddRange(expected);
        return analyzerTest.RunAsync();
    }
}

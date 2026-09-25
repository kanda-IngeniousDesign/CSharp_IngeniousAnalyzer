using CSharp_IngeniousAnalyzer.Style_Async;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
using Microsoft.CodeAnalysis.Testing.Verifiers;

namespace CSharp_IngeniousAnalyzer.Tests.Style_Async;

using Verify = CSharpAnalyzerVerifier<MissingConfigureAwait, XUnitVerifier>;
using CodeFixVerify = CSharpCodeFixVerifier<MissingConfigureAwait, MissingConfigureAwaitFix, XUnitVerifier>;

/// <summary>
/// ASYNC001（MissingConfigureAwait）の検知・Fix動作を検証するテスト
/// </summary>
public class MissingConfigureAwaitTests
{
    // ================================================================
    // 診断が出るべきケース
    // ================================================================

    /// <summary>
    /// Taskを返すメソッド呼び出しを ConfigureAwait なしで await する場合、診断が出ることを確認する
    /// </summary>
    [Fact]
    public async Task AwaitTask_ReportsDiagnostic()
    {
        var test = """
            using System.Threading.Tasks;

            public class C
            {
                async Task M()
                {
                    {|#0:await SomeAsync()|};
                }

                Task SomeAsync() => Task.CompletedTask;
            }
            """;

        await Verify.VerifyAnalyzerAsync(test, Verify.Diagnostic().WithLocation(0).WithArguments("SomeAsync()"));
    }

    /// <summary>
    /// Task&lt;int&gt; を返すメソッド呼び出しを await する場合、診断が出ることを確認する
    /// </summary>
    [Fact]
    public async Task AwaitGenericTask_ReportsDiagnostic()
    {
        var test = """
            using System.Threading.Tasks;

            public class C
            {
                async Task<int> M()
                {
                    var value = {|#0:await GetValueAsync()|};
                    return value;
                }

                Task<int> GetValueAsync() => Task.FromResult(1);
            }
            """;

        await Verify.VerifyAnalyzerAsync(test, Verify.Diagnostic().WithLocation(0).WithArguments("GetValueAsync()"));
    }

    /// <summary>
    /// ValueTask / ValueTask&lt;T&gt; を返すメソッド呼び出しを await する場合、診断が出ることを確認する
    /// </summary>
    [Fact]
    public async Task AwaitValueTask_ReportsDiagnostic()
    {
        var test = """
            using System.Threading.Tasks;

            public class C
            {
                async Task M()
                {
                    {|#0:await RunAsync()|};
                    var value = {|#1:await GetValueAsync()|};
                }

                ValueTask RunAsync() => default;
                ValueTask<int> GetValueAsync() => new(1);
            }
            """;

        await Verify.VerifyAnalyzerAsync(test,
            Verify.Diagnostic().WithLocation(0).WithArguments("RunAsync()"),
            Verify.Diagnostic().WithLocation(1).WithArguments("GetValueAsync()"));
    }

    /// <summary>
    /// メソッド呼び出し以外（引数・フィールド・プロパティ・インデクサー）で得た Task を await する場合も、
    /// 型で判定しているため診断が出ることを確認する
    /// </summary>
    [Fact]
    public async Task AwaitTaskFromVariableFieldPropertyIndexer_ReportsDiagnostic()
    {
        var test = """
            using System.Threading.Tasks;

            public class C
            {
                private readonly Task _field = Task.CompletedTask;
                private Task<int> Prop => Task.FromResult(1);

                async Task M(Task task, Task[] tasks)
                {
                    {|#0:await task|};
                    {|#1:await _field|};
                    var value = {|#2:await Prop|};
                    {|#3:await tasks[0]|};
                }
            }
            """;

        await Verify.VerifyAnalyzerAsync(test,
            Verify.Diagnostic().WithLocation(0).WithArguments("task"),
            Verify.Diagnostic().WithLocation(1).WithArguments("_field"),
            Verify.Diagnostic().WithLocation(2).WithArguments("Prop"),
            Verify.Diagnostic().WithLocation(3).WithArguments("tasks[0]"));
    }

    /// <summary>
    /// 三項演算子・キャスト・null条件演算子を含む複雑な式でも、型が Task であれば診断が出ることを確認する
    /// </summary>
    [Fact]
    public async Task AwaitComplexExpressions_ReportsDiagnostic()
    {
        var test = """
            using System.Threading.Tasks;

            public class Worker
            {
                public Task RunAsync() => Task.CompletedTask;
            }

            public class C
            {
                async Task M(bool flag, Task taskA, Task taskB, object obj, Worker worker)
                {
                    {|#0:await (flag ? taskA : taskB)|};
                    {|#1:await (Task)obj|};
                    {|#2:await worker?.RunAsync()|};
                }
            }
            """;

        await Verify.VerifyAnalyzerAsync(test,
            Verify.Diagnostic().WithLocation(0).WithArguments("(flag ? taskA : taskB)"),
            Verify.Diagnostic().WithLocation(1).WithArguments("(Task)obj"),
            Verify.Diagnostic().WithLocation(2).WithArguments("worker?.RunAsync()"));
    }

    /// <summary>
    /// 式形式（=&gt;）の async メソッド、try/catch/finally 内の await も診断が出ることを確認する
    /// </summary>
    [Fact]
    public async Task AwaitInExpressionBodyAndTryCatchFinally_ReportsDiagnostic()
    {
        var test = """
            using System.Threading.Tasks;

            public class C
            {
                async Task M1() => {|#0:await Task.Delay(1)|};

                async Task M2()
                {
                    try
                    {
                        {|#1:await Task.Delay(2)|};
                    }
                    catch
                    {
                        {|#2:await Task.Delay(3)|};
                    }
                    finally
                    {
                        {|#3:await Task.Delay(4)|};
                    }
                }
            }
            """;

        await Verify.VerifyAnalyzerAsync(test,
            Verify.Diagnostic().WithLocation(0).WithArguments("Task.Delay(1)"),
            Verify.Diagnostic().WithLocation(1).WithArguments("Task.Delay(2)"),
            Verify.Diagnostic().WithLocation(2).WithArguments("Task.Delay(3)"),
            Verify.Diagnostic().WithLocation(3).WithArguments("Task.Delay(4)"));
    }

    /// <summary>
    /// Func&lt;Task&gt; に変換される async ラムダ、Task を返す async ローカル関数内の await は診断が出ることを確認する
    /// </summary>
    [Fact]
    public async Task AwaitInTaskReturningLambdaAndLocalFunction_ReportsDiagnostic()
    {
        var test = """
            using System;
            using System.Threading.Tasks;

            public class C
            {
                void M()
                {
                    Func<Task> func = async () => {|#0:await Task.Delay(1)|};
                    _ = Task.Run(async () => {|#1:await Task.Delay(2)|});

                    async Task LocalAsync()
                    {
                        {|#2:await Task.Delay(3)|};
                    }

                    _ = LocalAsync();
                }
            }
            """;

        await Verify.VerifyAnalyzerAsync(test,
            Verify.Diagnostic().WithLocation(0).WithArguments("Task.Delay(1)"),
            Verify.Diagnostic().WithLocation(1).WithArguments("Task.Delay(2)"),
            Verify.Diagnostic().WithLocation(2).WithArguments("Task.Delay(3)"));
    }

    /// <summary>
    /// async void メソッドの中にある Func&lt;Task&gt; の async ラムダ内の await は、
    /// await を直接含む関数（ラムダ）が void ではないため診断が出ることを確認する
    /// （外側の async void 判定が内側に波及しないことの確認）
    /// </summary>
    [Fact]
    public async Task TaskReturningLambdaInsideAsyncVoidMethod_ReportsDiagnostic()
    {
        var test = """
            using System;
            using System.Threading.Tasks;

            public class C
            {
                async void OnClick(object sender, EventArgs e)
                {
                    await Task.Delay(1);
                    Func<Task> func = async () => {|#0:await Task.Delay(2)|};
                    await func();
                }
            }
            """;

        await Verify.VerifyAnalyzerAsync(test, Verify.Diagnostic().WithLocation(0).WithArguments("Task.Delay(2)"));
    }

    /// <summary>
    /// static ではない Main メソッドはエントリポイントになり得ないため、除外されず診断が出ることを確認する
    /// </summary>
    [Fact]
    public async Task NonStaticMainMethod_ReportsDiagnostic()
    {
        var test = """
            using System.Threading.Tasks;

            public class C
            {
                async Task Main()
                {
                    {|#0:await Task.Delay(1)|};
                }
            }
            """;

        await Verify.VerifyAnalyzerAsync(test, Verify.Diagnostic().WithLocation(0).WithArguments("Task.Delay(1)"));
    }

    /// <summary>
    /// 別のルールIDの Ignore コメントでは ASYNC001 が抑制されないことを確認する
    /// </summary>
    [Fact]
    public async Task IgnoreCommentForOtherRule_ReportsDiagnostic()
    {
        var test = """
            using System.Threading.Tasks;

            public class C
            {
                // Ignore CPX001
                async Task M()
                {
                    {|#0:await Task.Delay(1)|};
                }
            }
            """;

        await Verify.VerifyAnalyzerAsync(test, Verify.Diagnostic().WithLocation(0).WithArguments("Task.Delay(1)"));
    }

    // ================================================================
    // 診断が出ないべきケース
    // ================================================================

    /// <summary>
    /// ConfigureAwait(true) が明示されている場合は、意図表明済みとして診断が出ないことを確認する
    /// </summary>
    [Fact]
    public async Task ConfigureAwaitTrue_DoesNotReportDiagnostic()
    {
        var test = """
            using System.Threading.Tasks;

            public class C
            {
                async Task M()
                {
                    await Task.Delay(1).ConfigureAwait(true);
                }
            }
            """;

        await Verify.VerifyAnalyzerAsync(test);
    }

    /// <summary>
    /// ConfigureAwait(false) が既に付いている場合（括弧で包まれている場合、ValueTask の場合を含む）は
    /// 診断が出ないことを確認する
    /// </summary>
    [Fact]
    public async Task ConfigureAwaitFalse_DoesNotReportDiagnostic()
    {
        var test = """
            using System.Threading.Tasks;

            public class C
            {
                async Task M(bool flag)
                {
                    await Task.Delay(1).ConfigureAwait(false);
                    await (Task.Delay(1).ConfigureAwait(false));
                    var value = await Task.FromResult(1).ConfigureAwait(false);
                    await RunAsync().ConfigureAwait(false);
                    await (flag ? Task.Delay(1) : Task.Delay(2)).ConfigureAwait(false);
                }

                ValueTask RunAsync() => default;
            }
            """;

        await Verify.VerifyAnalyzerAsync(test);
    }

    /// <summary>
    /// 戻り値が Task のままとなる独自の ConfigureAwait 拡張メソッドであっても、ConfigureAwait という名前で
    /// 呼び出されていれば意図表明済みとして診断が出ないことを確認する（括弧で包まれている場合を含む）。
    /// 標準の ConfigureAwait は ConfiguredTaskAwaitable を返すため型チェックでも除外されるが、
    /// 名前による判定そのものを検証するためにこのケースを用いる
    /// </summary>
    [Fact]
    public async Task CustomConfigureAwaitReturningTask_DoesNotReportDiagnostic()
    {
        var test = """
            using System.Threading.Tasks;

            public static class TaskExtensions
            {
                public static Task ConfigureAwait(this Task task, string mode) => task;
            }

            public class C
            {
                async Task M(Task task)
                {
                    await task.ConfigureAwait("custom");
                    await (task.ConfigureAwait("custom"));
                }
            }
            """;

        await Verify.VerifyAnalyzerAsync(test);
    }

    /// <summary>
    /// async void メソッド（イベントハンドラー）内の await は診断が出ないことを確認する
    /// </summary>
    [Fact]
    public async Task AsyncVoidMethod_DoesNotReportDiagnostic()
    {
        var test = """
            using System;
            using System.Threading.Tasks;

            public class C
            {
                async void OnClick(object sender, EventArgs e)
                {
                    await Task.Delay(1);
                }
            }
            """;

        await Verify.VerifyAnalyzerAsync(test);
    }

    /// <summary>
    /// Action 系デリゲート・イベントハンドラーに変換される async ラムダ／匿名メソッド、async void ローカル関数内の
    /// await は診断が出ないことを確認する
    /// </summary>
    [Fact]
    public async Task AsyncVoidLambdaAnonymousMethodAndLocalFunction_DoesNotReportDiagnostic()
    {
        var test = """
            using System;
            using System.Collections.Generic;
            using System.Threading.Tasks;

            public class C
            {
                public event EventHandler Clicked;

                void M(List<int> list)
                {
                    Action action = async () => await Task.Delay(1);
                    Action<int> actionWithArg = async delegate (int x) { await Task.Delay(x); };
                    Clicked += async (sender, e) => await Task.Delay(2);
                    list.ForEach(async x => await Task.Delay(x));

                    async void LocalHandler()
                    {
                        await Task.Delay(3);
                    }

                    LocalHandler();
                }
            }
            """;

        await Verify.VerifyAnalyzerAsync(test);
    }

    /// <summary>
    /// static async Task Main（エントリポイント）内の await は、ラムダ式・ローカル関数内を含め診断が出ないことを確認する
    /// </summary>
    [Fact]
    public async Task StaticMainMethod_DoesNotReportDiagnostic()
    {
        var test = """
            using System;
            using System.Threading.Tasks;

            public class Program
            {
                static async Task<int> Main(string[] args)
                {
                    await Task.Delay(1);
                    Func<Task> func = async () => await Task.Delay(2);
                    await func();

                    async Task LocalAsync()
                    {
                        await Task.Delay(3);
                    }

                    await LocalAsync();
                    return 0;
                }
            }
            """;

        await Verify.VerifyAnalyzerAsync(test);
    }

    /// <summary>
    /// トップレベルステートメント内の await は、ローカル関数内を含め診断が出ないことを確認する
    /// </summary>
    [Fact]
    public async Task TopLevelStatements_DoesNotReportDiagnostic()
    {
        var test = new CSharpAnalyzerTest<MissingConfigureAwait, XUnitVerifier>
        {
            TestCode = """
                using System.Threading.Tasks;

                await Task.Delay(1);
                await LocalAsync();

                async Task LocalAsync()
                {
                    await Task.Delay(2);
                }
                """,
            TestState = { OutputKind = OutputKind.ConsoleApplication },
        };

        await test.RunAsync();
    }

    /// <summary>
    /// Task / ValueTask 系ではないカスタムawaitable型、Task.Yield()（YieldAwaitable）、dynamic を await する場合は
    /// 診断が出ないことを確認する（これらには ConfigureAwait が存在しない、または型を静的に確定できない）
    /// </summary>
    [Fact]
    public async Task NonTaskAwaitables_DoesNotReportDiagnostic()
    {
        var test = """
            using System.Runtime.CompilerServices;
            using System.Threading.Tasks;

            public class MyAwaitable
            {
                public TaskAwaiter GetAwaiter() => Task.CompletedTask.GetAwaiter();
            }

            public class C
            {
                async Task M(dynamic d)
                {
                    await new MyAwaitable();
                    await Task.Yield();
                    await d;
                }
            }
            """;

        await Verify.VerifyAnalyzerAsync(test);
    }

    /// <summary>
    /// await foreach / await using は第1弾のスコープ外であり、AwaitExpression でもないため診断が出ないことを確認する
    /// </summary>
    [Fact]
    public async Task AwaitForeachAndAwaitUsing_DoesNotReportDiagnostic()
    {
        var test = """
            using System;
            using System.Collections.Generic;
            using System.Threading.Tasks;

            public class C
            {
                async Task M(IAsyncEnumerable<int> source, IAsyncDisposable resource)
                {
                    await foreach (var item in source)
                    {
                    }

                    await using (resource)
                    {
                    }
                }
            }
            """;

        await Verify.VerifyAnalyzerAsync(test);
    }

    /// <summary>
    /// メソッド宣言の直前、またはメソッド本体の先頭に「// Ignore ASYNC001」コメントがある場合、
    /// メソッド内のラムダ式・ローカル関数を含むすべての await で診断が抑制されることを確認する
    /// </summary>
    [Fact]
    public async Task MethodWithIgnoreComment_DoesNotReportDiagnostic()
    {
        var test = """
            using System;
            using System.Threading.Tasks;

            public class C
            {
                // Ignore ASYNC001
                async Task M1()
                {
                    await Task.Delay(1);
                    Func<Task> func = async () => await Task.Delay(2);

                    async Task LocalAsync()
                    {
                        await Task.Delay(3);
                    }

                    await LocalAsync();
                }

                async Task M2()
                {
                    // Ignore ASYNC001
                    await Task.Delay(4);
                }
            }
            """;

        await Verify.VerifyAnalyzerAsync(test);
    }

    /// <summary>
    /// 自動生成ファイル（*.Designer.cs）内のコードは、IsGeneratedFileガードにより診断が出ないことを確認する
    /// </summary>
    [Fact]
    public async Task GeneratedFile_DoesNotReportDiagnostic()
    {
        var test = """
            using System.Threading.Tasks;

            public class C
            {
                async Task M()
                {
                    await Task.Delay(1);
                }
            }
            """;

        await new CSharpAnalyzerTest<MissingConfigureAwait, XUnitVerifier>
        {
            TestState = { Sources = { ("Test0.Designer.cs", test) } },
        }.RunAsync();
    }

    // ================================================================
    // Fix（Ignoreコメント挿入のみ。".ConfigureAwait(false)" の追記は動作を変えうるため提供しない）
    // ================================================================

    /// <summary>
    /// Fixがコードを書き換えず、メソッド本体の先頭に「// Ignore ASYNC001」コメントを挿入することを確認する。
    /// 同一メソッド内に複数の診断があっても、コメントは1つだけ挿入されることを確認する
    /// </summary>
    [Fact]
    public async Task Fix_InsertsIgnoreCommentOnce()
    {
        var test = """
            using System.Threading.Tasks;

            public class C
            {
                async Task M()
                {
                    {|#0:await Task.Delay(1)|};
                    {|#1:await Task.Delay(2)|};
                }
            }
            """;

        var fixedSource = $$"""
            using System.Threading.Tasks;

            public class C
            {
                async Task M()
                {
                    // Ignore ASYNC001
                    await Task.Delay(1);
                    await Task.Delay(2);
                }
            }
            """;

        await CodeFixVerify.VerifyCodeFixAsync(test,
            [
                CodeFixVerify.Diagnostic().WithLocation(0).WithArguments("Task.Delay(1)"),
                CodeFixVerify.Diagnostic().WithLocation(1).WithArguments("Task.Delay(2)"),
            ],
            fixedSource);
    }

    /// <summary>
    /// ラムダ式内の await に対するFixは、それを含むメソッドの本体先頭にコメントを挿入することを確認する
    /// </summary>
    [Fact]
    public async Task Fix_AwaitInsideLambda_InsertsIgnoreCommentInEnclosingMethod()
    {
        var test = """
            using System;
            using System.Threading.Tasks;

            public class C
            {
                void M()
                {
                    Func<Task> func = async () => {|#0:await Task.Delay(1)|};
                }
            }
            """;

        var fixedSource = $$"""
            using System;
            using System.Threading.Tasks;

            public class C
            {
                void M()
                {
                    // Ignore ASYNC001
                    Func<Task> func = async () => await Task.Delay(1);
                }
            }
            """;

        await CodeFixVerify.VerifyCodeFixAsync(test, CodeFixVerify.Diagnostic().WithLocation(0).WithArguments("Task.Delay(1)"), fixedSource);
    }

    /// <summary>
    /// ブロック本体を持たない式形式のメソッドや、メソッドに属さないフィールド初期化子のラムダ式では
    /// コメントの挿入先がないため、Fixが提供されないことを確認する
    /// </summary>
    [Fact]
    public async Task Fix_NoBlockBodyOrNoEnclosingMethod_NotOffered()
    {
        var test = """
            using System;
            using System.Threading.Tasks;

            public class C
            {
                private readonly Func<Task> _func = async () => {|#0:await Task.Delay(1)|};

                async Task M() => {|#1:await Task.Delay(2)|};
            }
            """;

        await CodeFixVerify.VerifyCodeFixAsync(test,
            [
                CodeFixVerify.Diagnostic().WithLocation(0).WithArguments("Task.Delay(1)"),
                CodeFixVerify.Diagnostic().WithLocation(1).WithArguments("Task.Delay(2)"),
            ],
            test);
    }
}

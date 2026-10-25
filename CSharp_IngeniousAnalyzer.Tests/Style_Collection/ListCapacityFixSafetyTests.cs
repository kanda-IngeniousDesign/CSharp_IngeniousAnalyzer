using CSharp_IngeniousAnalyzer.Style_Collection;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace CSharp_IngeniousAnalyzer.Tests.Style_Collection;

/// <summary>
/// COLL001 の Fix（ListCapacityFix）が持つ安全弁を、アナライザーを通さずに直接確かめるテスト。
/// アナライザー側でも同じケースは診断しないため、通常の Fix のテストではこの安全弁に到達しない。
/// 書き換えるとビルドエラーや意味の変化を起こすケースで、Fix が出ないことを確認する
/// </summary>
public class ListCapacityFixSafetyTests
{
    /// <summary>
    /// 安全なケース（上限がリスト生成より前に宣言され、条件が「未満」）では Fix が出ることを確認する（下の2件の前提）
    /// </summary>
    [Fact]
    public async Task SafeLoop_OffersFix()
    {
        var actions = await ComputeFixesAsync("""
            int limit = 10;
            var list = new List<int>();
            for (int i = 0; i < limit; i++) list.Add(i);
            """);

        Assert.Equal("Fix : 初期キャパシティを指定する", Assert.Single(actions).Title);
    }

    /// <summary>
    /// 上限の変数がリスト生成より後で宣言されている場合は、書き換えるとビルドエラーになるため Fix を出さないことを確認する
    /// </summary>
    [Fact]
    public async Task LimitDeclaredAfterList_OffersNoFix()
    {
        var actions = await ComputeFixesAsync("""
            var list = new List<int>();
            int limit = 10;
            for (int i = 0; i < limit; i++) list.Add(i);
            """);

        Assert.Empty(actions);
    }

    /// <summary>
    /// 条件が「未満」以外（以下など）の場合は、要素数と上限が一致しないため Fix を出さないことを確認する
    /// </summary>
    [Fact]
    public async Task LessOrEqualCondition_OffersNoFix()
    {
        var actions = await ComputeFixesAsync("""
            int limit = 10;
            var list = new List<int>();
            for (int i = 0; i <= limit; i++) list.Add(i);
            """);

        Assert.Empty(actions);
    }

    /// <summary>
    /// メソッド本体に文を入れたコードに対し、new List&lt;int&gt;() の位置に COLL001 があるものとして Fix を求める
    /// </summary>
    /// <param name="body">メソッド本体の文</param>
    /// <returns>登録された Fix</returns>
    private static async Task<List<CodeAction>> ComputeFixesAsync(string body)
    {
        var source = "using System.Collections.Generic;\npublic class C\n{\n    void M()\n    {\n" + body + "\n    }\n}\n";
        using var workspace = new AdhocWorkspace();
        var project = workspace.AddProject(ProjectInfo.Create(
            ProjectId.CreateNewId(), VersionStamp.Create(), "P", "P", LanguageNames.CSharp,
            metadataReferences: [MetadataReference.CreateFromFile(typeof(object).Assembly.Location)]));
        var document = workspace.AddDocument(project.Id, "C.cs", SourceText.From(source));

        var root = (await document.GetSyntaxRootAsync())!;
        var creation = root.DescendantNodes().OfType<ObjectCreationExpressionSyntax>().Single();
        var diagnostic = Diagnostic.Create(new ListCapacity().SupportedDiagnostics[0], creation.GetLocation());

        var actions = new List<CodeAction>();
        await new ListCapacityFix().RegisterCodeFixesAsync(new CodeFixContext(document, diagnostic, (a, _) => actions.Add(a), CancellationToken.None));
        return actions;
    }
}

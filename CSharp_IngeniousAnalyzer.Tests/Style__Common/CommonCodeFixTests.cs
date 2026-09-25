using CSharp_IngeniousAnalyzer.Style__Common;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace CSharp_IngeniousAnalyzer.Tests.Style__Common;

/// <summary>
/// CommonCodeFix.InsertIgnoreCommentInMethodAsync（CPX001/CPX002/ASYNC001 の Ignore Fix で共用）の
/// 挿入位置・改行コードの判定を、ルールを介さず直接検証するテスト
/// </summary>
public class CommonCodeFixTests
{
    private const string LF = "\n";
    private const string CRLF = "\r\n";

    /// <summary>
    /// 指定ソースの最初のメソッドに Ignore コメントを挿入した結果の全文を返す
    /// </summary>
    private static async Task<string> InsertAsync(string source)
    {
        using var workspace = new AdhocWorkspace();
        var project = workspace.AddProject("Test", LanguageNames.CSharp);
        var document = workspace.AddDocument(project.Id, "Test.cs", SourceText.From(source));

        var root = await document.GetSyntaxRootAsync();
        var method = root!.DescendantNodes().OfType<BaseMethodDeclarationSyntax>().First();

        var newDocument = await document.InsertIgnoreCommentInMethodAsync(method, "TEST001", CancellationToken.None);
        return (await newDocument.GetTextAsync()).ToString();
    }

    /// <summary>
    /// 先頭ステートメントの直前に挿入する場合、ファイルの改行コード（LF / CRLF）に合わせて改行されることを確認する。
    /// 先頭ステートメントの先行トリビアはインデントのみのため、以前は常に CRLF にフォールバックしていた
    /// </summary>
    [Theory]
    [InlineData(LF)]
    [InlineData(CRLF)]
    public async Task BeforeFirstStatement_UsesFileNewLine(string nl)
    {
        var source = string.Join(nl,
            "class C",
            "{",
            "    void M()",
            "    {",
            "        int x = 0;",
            "    }",
            "}");

        var expected = string.Join(nl,
            "class C",
            "{",
            "    void M()",
            "    {",
            "        // Ignore TEST001",
            "        int x = 0;",
            "    }",
            "}");

        Assert.Equal(expected, await InsertAsync(source));
    }

    /// <summary>
    /// 先頭ステートメントの前に説明コメント・空行がある場合、それらを維持したまま実コードの直前に挿入され、
    /// ファイルの改行コードに合わせて改行されることを確認する
    /// </summary>
    [Theory]
    [InlineData(LF)]
    [InlineData(CRLF)]
    public async Task BeforeFirstStatementWithLeadingComment_KeepsCommentAndUsesFileNewLine(string nl)
    {
        var source = string.Join(nl,
            "class C",
            "{",
            "    void M()",
            "    {",
            "        // explanation",
            "",
            "        int x = 0;",
            "    }",
            "}");

        var expected = string.Join(nl,
            "class C",
            "{",
            "    void M()",
            "    {",
            "        // explanation",
            "",
            "        // Ignore TEST001",
            "        int x = 0;",
            "    }",
            "}");

        Assert.Equal(expected, await InsertAsync(source));
    }

    /// <summary>
    /// メソッド本体が空の場合、{ の直後に挿入され、ファイルの改行コードに合わせて改行されることを確認する
    /// </summary>
    [Theory]
    [InlineData(LF)]
    [InlineData(CRLF)]
    public async Task EmptyBody_InsertsAfterOpenBraceWithFileNewLine(string nl)
    {
        var source = string.Join(nl,
            "class C",
            "{",
            "    void M()",
            "    {",
            "    }",
            "}");

        var expected = string.Join(nl,
            "class C",
            "{",
            "    void M()",
            "    {",
            "    // Ignore TEST001",
            "    }",
            "}");

        Assert.Equal(expected, await InsertAsync(source));
    }

    /// <summary>
    /// { と } が同じ行にある空のメソッド本体（{ の後続トリビアに改行がない）でも、
    /// ファイル内の他の改行から改行コードを判定することを確認する
    /// </summary>
    [Theory]
    [InlineData(LF)]
    [InlineData(CRLF)]
    public async Task SingleLineEmptyBody_UsesNewLineFromElsewhereInFile(string nl)
    {
        var source = string.Join(nl,
            "class C",
            "{",
            "    void M() { }",
            "}");

        var expected = string.Join(nl,
            "class C",
            "{",
            "    void M() {     // Ignore TEST001",
            "}",
            "}");

        Assert.Equal(expected, await InsertAsync(source));
    }

    /// <summary>
    /// 改行コードが混在するファイルでは、ファイル全体ではなく挿入位置付近の改行コードが優先されることを確認する
    /// </summary>
    [Fact]
    public async Task MixedNewLines_PrefersNewLineNearInsertionPoint()
    {
        var source = "class C" + CRLF + "{" + CRLF + "    void M()" + CRLF + "    {" + LF + "        int x = 0;" + LF + "    }" + CRLF + "}";

        var expected = "class C" + CRLF + "{" + CRLF + "    void M()" + CRLF + "    {" + LF + "        // Ignore TEST001" + LF + "        int x = 0;" + LF + "    }" + CRLF + "}";

        Assert.Equal(expected, await InsertAsync(source));
    }

    /// <summary>
    /// ファイル内に改行が1つも存在しない場合は、従来どおり CRLF にフォールバックすることを確認する
    /// </summary>
    [Fact]
    public async Task NoNewLineInFile_FallsBackToCrLf()
    {
        var source = "class C { void M() { int x = 0; } }";

        var expected = "class C { void M() { // Ignore TEST001" + CRLF + "int x = 0; } }";

        Assert.Equal(expected, await InsertAsync(source));
    }

    /// <summary>
    /// ブロック本体を持たない（抽象メソッド等の）宣言の場合、ドキュメントが変更されないことを確認する
    /// </summary>
    [Fact]
    public async Task MethodWithoutBody_ReturnsUnchangedDocument()
    {
        var source = string.Join(LF,
            "abstract class C",
            "{",
            "    public abstract void M();",
            "}");

        Assert.Equal(source, await InsertAsync(source));
    }
}

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace CSharp_IngeniousAnalyzer.Style__Common;

// CodeFixProvider（Microsoft.CodeAnalysis.Document を扱う）専用のヘルパー。
// DiagnosticAnalyzer側はDocumentに触れてはならない（RS1022）ため、CommonRoslynとは別クラスに分離している。
public static class CommonCodeFix
{
    /// <summary>
    /// "// Ignore &lt;DiagnosticId&gt;" コメントを、メソッド系宣言のブロック本体の先頭
    /// （先頭ステートメントの直前、または空ボディの場合は { の直後）に挿入する。
    /// </summary>
    public static async Task<Document> InsertIgnoreCommentInMethodAsync(this Document document, BaseMethodDeclarationSyntax method, string diagnosticId, CancellationToken cancellationToken)
    {
        var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
        if (root is null || method.Body is null) return document;

        BaseMethodDeclarationSyntax newMethod;

        var firstStmt = method.Body.Statements.FirstOrDefault();
        if (firstStmt != null)
        {
            // 先頭ステートメントの先行トリビアは通常インデントのみで、直前の改行は { の後続トリビアに属するため、
            // 直前トークンの後続トリビアも改行コードの判定材料に含める
            var newLine = DetectNewLine(root, firstStmt.GetLeadingTrivia(), firstStmt.GetFirstToken().GetPreviousToken().TrailingTrivia);
            var newFirstStmt = firstStmt.WithLeadingTrivia(BuildIgnoreTriviaList(firstStmt.GetLeadingTrivia(), diagnosticId, newLine));
            var newBody = method.Body.ReplaceNode(firstStmt, newFirstStmt);
            newMethod = method.WithBody(newBody);
        }
        else
        {
            var openBraceToken = method.Body.OpenBraceToken;
            var newLine = DetectNewLine(root, openBraceToken.TrailingTrivia);
            var newOpenBraceToken = openBraceToken.WithTrailingTrivia(
                openBraceToken.TrailingTrivia.AddRange(SyntaxFactory.TriviaList(
                    SyntaxFactory.Whitespace("    "),
                    SyntaxFactory.Comment($"// Ignore {diagnosticId}"),
                    SyntaxFactory.EndOfLine(newLine))));

            var newBody = method.Body.WithOpenBraceToken(newOpenBraceToken);
            newMethod = method.WithBody(newBody);
        }

        var newRoot = root.ReplaceNode(method, newMethod);
        return document.WithSyntaxRoot(newRoot);
    }

    /// <summary>
    /// 既存の先行トリビア（先頭ステートメントより前にある説明コメントや空行を含みうる）のうち、
    /// 実コードの直前にある最後のインデント（空白トリビア）の手前にIgnoreコメントを挿入する。
    /// 単純にリストの先頭に追加すると、既存の説明コメントより上に挿入されてしまうため。
    /// </summary>
    private static SyntaxTriviaList BuildIgnoreTriviaList(SyntaxTriviaList existingLeadingTrivia, string diagnosticId, string newLine)
    {
        var triviaArray = existingLeadingTrivia.ToArray();
        var insertIndex = triviaArray.Length;
        for (var i = triviaArray.Length - 1; i >= 0; i--)
        {
            if (triviaArray[i].IsKind(SyntaxKind.WhitespaceTrivia))
            {
                insertIndex = i;
                break;
            }
        }

        var indent = insertIndex < triviaArray.Length ? triviaArray[insertIndex].ToString() : string.Empty;

        return SyntaxFactory.TriviaList(triviaArray.Take(insertIndex))
            .AddRange(SyntaxFactory.TriviaList(
                SyntaxFactory.Whitespace(indent),
                SyntaxFactory.Comment($"// Ignore {diagnosticId}"),
                SyntaxFactory.EndOfLine(newLine)))
            .AddRange(triviaArray.Skip(insertIndex));
    }

    /// <summary>
    /// 挿入位置付近のトリビア（指定順）→ ファイル全体の最初の改行、の順に改行コードを検出する。
    /// ファイル内に改行が1つも存在しない場合のみ "\r\n" にフォールバックする。
    /// </summary>
    private static string DetectNewLine(SyntaxNode root, params SyntaxTriviaList[] nearbyTriviaLists)
    {
        foreach (var triviaList in nearbyTriviaLists)
        {
            foreach (var trivia in triviaList)
            {
                if (trivia.IsKind(SyntaxKind.EndOfLineTrivia)) return trivia.ToString();
            }
        }

        foreach (var trivia in root.DescendantTrivia())
        {
            if (trivia.IsKind(SyntaxKind.EndOfLineTrivia)) return trivia.ToString();
        }
        return "\r\n";
    }
}

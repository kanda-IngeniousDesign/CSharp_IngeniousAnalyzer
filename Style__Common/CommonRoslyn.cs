using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace CSharp_IngeniousAnalyzer.Style__Common;

public static class CommonRoslyn
{
    /// <summary>
    /// 警告の出ているスパンから、特定の構文ノードを安全に逆引きする共通処理
    /// </summary>
    public static T? FindNodeAtSpan<T>(this SyntaxNode root, TextSpan span) where T : SyntaxNode
    {
        return root.FindToken(span.Start).Parent?.AncestorsAndSelf().OfType<T>().FirstOrDefault();
    }

    /// <summary>
    /// 指定ノードに "// Ignore &lt;DiagnosticId&gt;" 抑制コメントが付与されているかを判定する。
    /// ・ノード自身の直前行
    /// ・メソッド系宣言でブロック本体を持つ場合は、ブロック先頭（{ の直後）
    /// を許容する。
    /// </summary>
    public static bool HasIgnoreComment(this SyntaxNode node, string diagnosticId)
    {
        if (HasIgnoreCommentInLeadingTrivia(node, diagnosticId)) return true;

        // ブロック本体を持つメソッド系宣言は、ブロック先頭（{ の直後）に書かれたコメントも許容する
        if (node is BaseMethodDeclarationSyntax { Body: { } body })
        {
            if (HasIgnoreCommentInLeadingTrivia(body, diagnosticId)) return true;

            var firstStmt = body.Statements.FirstOrDefault();
            if (firstStmt != null && HasIgnoreCommentInLeadingTrivia(firstStmt, diagnosticId)) return true;
        }

        return false;
    }

    private static bool HasIgnoreCommentInLeadingTrivia(SyntaxNode node, string diagnosticId)
    {
        foreach (var trivia in node.GetLeadingTrivia())
        {
            if (IsIgnoreCommentTrivia(trivia, diagnosticId)) return true;
        }
        return false;
    }

    private static bool IsIgnoreCommentTrivia(SyntaxTrivia trivia, string diagnosticId)
    {
        if (!trivia.IsKind(SyntaxKind.SingleLineCommentTrivia))
        {
            return false;
        }

        var text = trivia.ToString().Trim();
        return text.Equals($"// Ignore {diagnosticId}", StringComparison.OrdinalIgnoreCase);
    }
}
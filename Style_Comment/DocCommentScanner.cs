using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace CSharp_IngeniousAnalyzer.Style_Comment;

/// <summary>
/// COMM001/COMM002 で共通利用する、メソッド直前のドキュメントコメント取得ロジック
/// </summary>
public static class DocCommentScanner
{
    /// <summary>
    /// メソッドの先行トリビアから、構造化されたドキュメントコメント（GenerateDocumentationFile有効時）
    /// もしくは生テキストの /// 行（無効時のフォールバック）を取得する。
    /// 生テキスト側は、メソッドに一番近い連続した /// ブロックが単独で &lt;summary&gt; を持つ場合のみ
    /// それを独立したブロックとみなして採用し、そうでなければ間の割り込み（通常コメント等）を無視して
    /// 全体を1つの意味的ブロックとして結合する（「ドキュメント→説明コメント→メソッド」という
    /// 単なる末尾の割り込みを、意図せず2ブロックとして分断しないため）。
    /// </summary>
    public static (DocumentationCommentTriviaSyntax? Structured, List<SyntaxTrivia>? RawLines) TryGetDocComment(MethodDeclarationSyntax methodDeclaration)
    {
        var segments = new List<List<SyntaxTrivia>>();
        List<SyntaxTrivia>? currentSegment = null;

        foreach (var trivia in methodDeclaration.GetLeadingTrivia())
        {
            if (trivia.GetStructure() is DocumentationCommentTriviaSyntax doc)
            {
                return (doc, null);
            }

            var text = trivia.ToString();
            bool isDocLine = (trivia.IsKind(SyntaxKind.SingleLineCommentTrivia) && text.StartsWith("///")) ||
                              (trivia.IsKind(SyntaxKind.MultiLineCommentTrivia) && text.StartsWith("/**"));

            if (isDocLine)
            {
                (currentSegment ??= []).Add(trivia);
            }
            else if (!trivia.IsKind(SyntaxKind.WhitespaceTrivia) && !trivia.IsKind(SyntaxKind.EndOfLineTrivia))
            {
                if (currentSegment != null)
                {
                    segments.Add(currentSegment);
                    currentSegment = null;
                }
            }
        }

        if (currentSegment != null)
        {
            segments.Add(currentSegment);
        }

        if (segments.Count == 0) return (null, null);

        var lastSegment = segments[segments.Count - 1];
        if (segments.Count == 1 || string.Concat(lastSegment.Select(t => t.ToString())).Contains("<summary"))
        {
            // 一番近いブロックが単独で完結している（自身のsummaryを持つ、または他にブロックがない）場合は、それだけを採用する
            return (null, lastSegment);
        }

        // 一番近いブロックが summary を持たない場合、間の割り込みは単なる説明コメント等とみなし、
        // 全ブロックを1つの意味的ブロックとして結合する
        var merged = new List<SyntaxTrivia>();
        foreach (var segment in segments)
        {
            merged.AddRange(segment);
        }
        return (null, merged);
    }
}

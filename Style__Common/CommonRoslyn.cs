using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

namespace CSharp_IngeniousAnalyzer.Core;

public static class CommonRoslyn
{
    /// <summary>
    /// 警告の出ているスパンから、特定の構文ノードを安全に逆引きする共通処理
    /// </summary>
    public static T? FindNodeAtSpan<T>(this SyntaxNode root, TextSpan span) where T : SyntaxNode
    {
        return root.FindToken(span.Start).Parent?.AncestorsAndSelf().OfType<T>().FirstOrDefault();
    }
}
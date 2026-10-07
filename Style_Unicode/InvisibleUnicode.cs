using CSharp_IngeniousAnalyzer.Style__Common;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;

namespace CSharp_IngeniousAnalyzer.Style_Unicode;

// 不可視文字を書き換えるFixは提供しない。
// 例えば文字列内のNBSPは、半角スペースにすべきか・削除すべきか・意図的なのかをアナライザーが判断できず、
// エスケープシーケンス（ ）への置き換えは値を保つ代わりに不具合を残したまま警告だけを消してしまう。
// そのため修正は人が行い、ここでは意図的な箇所を抑制する「// Ignore UNI001」の挿入Fixのみを提供する。
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public class InvisibleUnicode : CommonAnalyzer
{
    public const string DiagnosticId = "UNI001";
    private const string Category = "Unicode";
    private static readonly LocalizableString Title = CreateLocalStr(nameof(ResourceEnum.UNI001_Title));
    private static readonly LocalizableString MessageFormat = CreateLocalStr(nameof(ResourceEnum.UNI001_Message));

    protected override DiagnosticDescriptor Rule { get; } = new(
        DiagnosticId, Title, MessageFormat, Category, DiagnosticSeverity.Warning, isEnabledByDefault: true);

    // ファイル単位で1回だけテキスト全体を走査するため、ルートノードを対象とする
    protected override SyntaxKind[] TargetKinds => [SyntaxKind.CompilationUnit];

    // 目視で判別できないUnicode文字とその名称。
    // 全角スペース（U+3000）は日本語の文字列・コメントで意図的に使われることが多いため対象外とする。
    private static readonly Dictionary<int, string> TargetChars = new()
    {
        // 制御文字（C0）：タブ・LF・CRを除く
        [0x0000] = "NULL",
        [0x0001] = "START OF HEADING",
        [0x0002] = "START OF TEXT",
        [0x0003] = "END OF TEXT",
        [0x0004] = "END OF TRANSMISSION",
        [0x0005] = "ENQUIRY",
        [0x0006] = "ACKNOWLEDGE",
        [0x0007] = "BELL",
        [0x0008] = "BACKSPACE",
        [0x000B] = "LINE TABULATION",
        [0x000C] = "FORM FEED",
        [0x000E] = "SHIFT OUT",
        [0x000F] = "SHIFT IN",
        [0x0010] = "DATA LINK ESCAPE",
        [0x0011] = "DEVICE CONTROL ONE",
        [0x0012] = "DEVICE CONTROL TWO",
        [0x0013] = "DEVICE CONTROL THREE",
        [0x0014] = "DEVICE CONTROL FOUR",
        [0x0015] = "NEGATIVE ACKNOWLEDGE",
        [0x0016] = "SYNCHRONOUS IDLE",
        [0x0017] = "END OF TRANSMISSION BLOCK",
        [0x0018] = "CANCEL",
        [0x0019] = "END OF MEDIUM",
        [0x001A] = "SUBSTITUTE",
        [0x001B] = "ESCAPE",
        [0x001C] = "FILE SEPARATOR",
        [0x001D] = "GROUP SEPARATOR",
        [0x001E] = "RECORD SEPARATOR",
        [0x001F] = "UNIT SEPARATOR",
        [0x007F] = "DELETE",

        // 特殊なスペース
        [0x00A0] = "NO-BREAK SPACE",
        [0x2000] = "EN QUAD",
        [0x2001] = "EM QUAD",
        [0x2002] = "EN SPACE",
        [0x2003] = "EM SPACE",
        [0x2004] = "THREE-PER-EM SPACE",
        [0x2005] = "FOUR-PER-EM SPACE",
        [0x2006] = "SIX-PER-EM SPACE",
        [0x2007] = "FIGURE SPACE",
        [0x2008] = "PUNCTUATION SPACE",
        [0x2009] = "THIN SPACE",
        [0x200A] = "HAIR SPACE",
        [0x202F] = "NARROW NO-BREAK SPACE",
        [0x205F] = "MEDIUM MATHEMATICAL SPACE",

        // ゼロ幅文字・不可視の書式文字
        [0x00AD] = "SOFT HYPHEN",
        [0x034F] = "COMBINING GRAPHEME JOINER",
        [0x180E] = "MONGOLIAN VOWEL SEPARATOR",
        [0x200B] = "ZERO WIDTH SPACE",
        [0x200C] = "ZERO WIDTH NON-JOINER",
        [0x200D] = "ZERO WIDTH JOINER",
        [0x2060] = "WORD JOINER",
        [0x2061] = "FUNCTION APPLICATION",
        [0x2062] = "INVISIBLE TIMES",
        [0x2063] = "INVISIBLE SEPARATOR",
        [0x2064] = "INVISIBLE PLUS",
        [0xFEFF] = "ZERO WIDTH NO-BREAK SPACE",
        [0xFFF9] = "INTERLINEAR ANNOTATION ANCHOR",
        [0xFFFA] = "INTERLINEAR ANNOTATION SEPARATOR",
        [0xFFFB] = "INTERLINEAR ANNOTATION TERMINATOR",

        // 双方向制御文字（Trojan Source攻撃で表示上のコード順序の偽装に使われる）
        [0x061C] = "ARABIC LETTER MARK",
        [0x200E] = "LEFT-TO-RIGHT MARK",
        [0x200F] = "RIGHT-TO-LEFT MARK",
        [0x202A] = "LEFT-TO-RIGHT EMBEDDING",
        [0x202B] = "RIGHT-TO-LEFT EMBEDDING",
        [0x202C] = "POP DIRECTIONAL FORMATTING",
        [0x202D] = "LEFT-TO-RIGHT OVERRIDE",
        [0x202E] = "RIGHT-TO-LEFT OVERRIDE",
        [0x2066] = "LEFT-TO-RIGHT ISOLATE",
        [0x2067] = "RIGHT-TO-LEFT ISOLATE",
        [0x2068] = "FIRST STRONG ISOLATE",
        [0x2069] = "POP DIRECTIONAL ISOLATE",
        [0x206A] = "INHIBIT SYMMETRIC SWAPPING",
        [0x206B] = "ACTIVATE SYMMETRIC SWAPPING",
        [0x206C] = "INHIBIT ARABIC FORM SHAPING",
        [0x206D] = "ACTIVATE ARABIC FORM SHAPING",
        [0x206E] = "NATIONAL DIGIT SHAPES",
        [0x206F] = "NOMINAL DIGIT SHAPES",

        // 改行として扱われるが、エディタ上は改行に見えない文字
        [0x0085] = "NEXT LINE",
        [0x2028] = "LINE SEPARATOR",
        [0x2029] = "PARAGRAPH SEPARATOR",

        // 空白として描画される文字（識別子に使えるため、見えない変数名を作れる）
        [0x115F] = "HANGUL CHOSEONG FILLER",
        [0x1160] = "HANGUL JUNGSEONG FILLER",
        [0x17B4] = "KHMER VOWEL INHERENT AQ",
        [0x17B5] = "KHMER VOWEL INHERENT AA",
        [0x3164] = "HANGUL FILLER",
        [0xFFA0] = "HALFWIDTH HANGUL FILLER",

        // 結合用の濁点・半濁点（「か」＋U+3099 が「が」と同じ見た目になる。Macのファイル名のコピー等で混入する）
        [0x3099] = "COMBINING KATAKANA-HIRAGANA VOICED SOUND MARK",
        [0x309A] = "COMBINING KATAKANA-HIRAGANA SEMI-VOICED SOUND MARK",
    };

    protected override void AnalyzeNode(SyntaxNodeAnalysisContext context)
    {
        if (IsGeneratedFile(context)) return;

        var root = context.Node;
        var content = root.SyntaxTree.GetText(context.CancellationToken).ToString();

        // ファイル先頭のBOMはエンコーディングの指定であり、混入ではないため対象外とする
        var pos = content.Length > 0 && content[0] == '﻿' ? 1 : 0;
        while (pos < content.Length)
        {
            var codePoint = ReadCodePoint(content, pos, out var length);
            var name = GetTargetName(codePoint);
            if (name is null)
            {
                pos += length;
                continue;
            }

            // 同じ文字が連続する場合は1件にまとめる（ただし、トークン・トリビアの境界は越えない）
            var containingSpan = GetContainingSpan(root, pos);
            var end = pos + length;
            while (end < containingSpan.End && ReadCodePoint(content, end, out var nextLength) == codePoint)
            {
                end += nextLength;
            }

            if (!IsIgnored(root, pos))
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    Rule,
                    Location.Create(root.SyntaxTree, TextSpan.FromBounds(pos, end)),
                    $"U+{codePoint:X4}",
                    name));
            }

            pos = end;
        }
    }

    /// <summary>
    /// 指定位置の文字を含む、Ignoreコメントの挿入先となる直近の文またはメンバー宣言（フィールド・メソッド・クラス等）を返す。
    /// 該当するものがなければ null を返す。
    /// </summary>
    public static SyntaxNode? FindIgnoreAnchor(SyntaxNode root, int position)
    {
        for (var node = GetContainingToken(root, position).Parent; node != null; node = node.Parent)
        {
            // ドキュメントコメント・プリプロセッサディレクティブ内の文字は、それらが付いているトークンの位置で判断する
            if (node is StructuredTriviaSyntax structured)
            {
                node = structured.ParentTrivia.Token.Parent;
                if (node is null) return null;
            }

            if (IsAnchorKind(node)) return node;
        }
        return null;
    }

    /// <summary>
    /// 直近の文・メンバー宣言、またはそれを囲む文・メンバー宣言のいずれかに「// Ignore UNI001」があるかを判定する
    /// </summary>
    private static bool IsIgnored(SyntaxNode root, int position)
    {
        for (var anchor = FindIgnoreAnchor(root, position); anchor != null; anchor = anchor.Parent)
        {
            if (IsAnchorKind(anchor) && anchor.HasIgnoreCommentInLeadingTrivia(DiagnosticId)) return true;
        }
        return false;
    }

    /// <summary>
    /// Ignoreコメントの挿入先となる種類のノードかを判定する。
    /// ブロック（{ }）は、メソッド本体の { の手前にコメントが入り不自然になるため対象外とし、外側の文・メンバーを使う。
    /// </summary>
    private static bool IsAnchorKind(SyntaxNode node)
    {
        return node is (StatementSyntax and not BlockSyntax) or MemberDeclarationSyntax or UsingDirectiveSyntax;
    }

    /// <summary>
    /// 指定位置の文字を含むトークン、またはトリビアが付いているトークンを返す
    /// </summary>
    private static SyntaxToken GetContainingToken(SyntaxNode root, int position)
    {
        var token = root.FindToken(position, findInsideTrivia: true);
        return token.Span.Contains(position) ? token : root.FindTrivia(position, findInsideTrivia: true).Token;
    }

    /// <summary>
    /// 指定位置の文字を含むトークンまたはトリビアの範囲を返す
    /// </summary>
    private static TextSpan GetContainingSpan(SyntaxNode root, int position)
    {
        var token = root.FindToken(position, findInsideTrivia: true);
        if (token.Span.Contains(position)) return token.Span;

        var trivia = root.FindTrivia(position, findInsideTrivia: true);
        return trivia.Span.Contains(position) ? trivia.Span : new TextSpan(position, 1);
    }

    /// <summary>
    /// 指定位置のコードポイントを読み取る（サロゲートペアの場合は2文字分を1つとして扱う）
    /// </summary>
    private static int ReadCodePoint(string content, int index, out int length)
    {
        if (char.IsHighSurrogate(content[index]) && index + 1 < content.Length && char.IsLowSurrogate(content[index + 1]))
        {
            length = 2;
            return char.ConvertToUtf32(content[index], content[index + 1]);
        }

        length = 1;
        return content[index];
    }

    /// <summary>
    /// 検知対象の文字であればその名称を、対象外であれば null を返す
    /// </summary>
    private static string? GetTargetName(int codePoint)
    {
        if (TargetChars.TryGetValue(codePoint, out var name)) return name;

        // 制御文字（C1）：文字コードの取り違え（Windows-1252等）で混入しやすい
        if (codePoint is >= 0x0080 and <= 0x009F) return "C1 CONTROL CHARACTER";

        // タグ文字（U+E0000ブロック）：不可視のまま任意のASCII文字列を埋め込めるため、生成AIの出力経由で混入しうる
        if (codePoint == 0xE0001) return "LANGUAGE TAG";
        if (codePoint is >= 0xE0020 and <= 0xE007F) return "TAG CHARACTER";

        return null;
    }
}

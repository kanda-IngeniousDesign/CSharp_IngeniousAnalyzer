using CSharp_IngeniousAnalyzer.Style__Common;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text.RegularExpressions;

namespace CSharp_IngeniousAnalyzer.Style_Comment;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public class FuncHeaderMismatches : CommonAnalyzer
{
    public const string DiagnosticId = "COMM002";
    private const string Category = "Comment";
    private static readonly LocalizableString Title = CreateLocalStr(nameof(ResourceEnum.COMM002_Title));
    private static readonly LocalizableString MessageFormat = CreateLocalStr(nameof(ResourceEnum.COMM002_Message));

    protected override DiagnosticDescriptor Rule { get; } = new(
        DiagnosticId, Title, MessageFormat, Category, DiagnosticSeverity.Warning, isEnabledByDefault: true);

    protected override SyntaxKind[] TargetKinds => [SyntaxKind.MethodDeclaration];

    // 生テキストフォールバック時に <param name="..."> の名称部分を抽出する
    private static readonly Regex ParamNamePattern = new(@"<param\s+name\s*=\s*""([^""]*)""", RegexOptions.Compiled);

    protected override void AnalyzeNode(SyntaxNodeAnalysisContext context)
    {
        if (IsGeneratedFile(context)) return;
        var methodDeclaration = (MethodDeclarationSyntax)context.Node;

        // 1. 先行トリビアから DocumentationCommentTriviaSyntax を効率よく取得
        DocumentationCommentTriviaSyntax? xmlTrivia = null;
        // GenerateDocumentationFile が無効なプロジェクトをビルドすると、
        // コンパイラは /// コメントを構造化せず単純な SingleLineCommentTrivia として解釈する
        // （IDE上の解析では常に構造化されるため、ビルド時のみ検知漏れが発生するのを防ぐためのフォールバック）
        List<string>? rawDocCommentLines = null;
        foreach (var trivia in methodDeclaration.GetLeadingTrivia())
        {
            if (trivia.GetStructure() is DocumentationCommentTriviaSyntax doc)
            {
                xmlTrivia = doc;
                break;
            }

            var text = trivia.ToString();
            if ((trivia.IsKind(SyntaxKind.SingleLineCommentTrivia) && text.StartsWith("///")) ||
                (trivia.IsKind(SyntaxKind.MultiLineCommentTrivia) && text.StartsWith("/**")))
            {
                (rawDocCommentLines ??= []).Add(text);
            }
        }

        List<string> paramTags;

        if (xmlTrivia != null)
        {
            // 2. 文字列化（ToString()）を避け、構文木ベースで <summary> が含まれているかを高速に判定する
            bool hasSummary = false;
            foreach (var node in GetElementsRecursive(xmlTrivia.Content))
            {
                if (node.StartTag.Name.ToString() == "summary")
                {
                    hasSummary = true;
                    break;
                }
            }

            if (!hasSummary) return;

            // 3. paramタグの名称をアロケーションを抑えて収集
            // 重複や順序を考慮し、HashSet / List を効率的に構築
            paramTags = [];
            foreach (var element in GetElementsRecursive(xmlTrivia.Content))
            {
                if (element.StartTag.Name.ToString() == "param")
                {
                    foreach (var attr in element.StartTag.Attributes)
                    {
                        if (attr is XmlNameAttributeSyntax nameAttr)
                        {
                            var name = nameAttr.Identifier.Identifier.ValueText;
                            if (!string.IsNullOrEmpty(name))
                            {
                                paramTags.Add(name);
                            }
                            break;
                        }
                    }
                }
            }
        }
        else if (rawDocCommentLines != null)
        {
            // 構造化されていない場合は、生テキストから <summary>/<param name="..."> を判定する
            var rawText = string.Join("", rawDocCommentLines);
            if (!rawText.Contains("<summary")) return;

            paramTags = [];
            foreach (Match match in ParamNamePattern.Matches(rawText))
            {
                paramTags.Add(match.Groups[1].Value);
            }
        }
        else
        {
            // コメント自体が存在しない場合はスキップ（欠落自体はCOMM001の責務）
            return;
        }

        // 4. メソッドの引数名を効率的に抽出
        var parameters = methodDeclaration.ParameterList.Parameters;
        int paramCount = parameters.Count;

        // パラメータ数が0かつXML側のparamも0件なら完全に一致しているため即リターン
        if (paramCount == 0 && paramTags.Count == 0) return;

        // 配列やリストを使って高速に比較
        var parameterNames = new List<string>(paramCount);
        foreach (var p in parameters)
        {
            parameterNames.Add(p.Identifier.ValueText);
        }

        // 5. 不一致の判定（要素数または順序・内容の比較）
        bool isMismatched = paramTags.Count != parameterNames.Count;
        if (!isMismatched)
        {
            for (int i = 0; i < paramCount; i++)
            {
                if (paramTags[i] != parameterNames[i])
                {
                    isMismatched = true;
                    break;
                }
            }
        }

        if (isMismatched)
        {
            // 不一致箇所（過不足のあるパラメータ名）の特定
            // 元の Except ロジック（missing と extra の結合）を維持
            var missingParams = parameterNames.Except(paramTags);
            var extraParams = paramTags.Except(parameterNames);
            var details = string.Join(", ", missingParams.Concat(extraParams));

            context.ReportDiagnostic(Diagnostic.Create(Rule, methodDeclaration.Identifier.GetLocation(), details));
        }
    }

    /// <summary>
    /// SyntaxList や XmlNodeSyntax の中から XmlElementSyntax を安全に再帰抽出するヘルパー
    /// </summary>
    private static IEnumerable<XmlElementSyntax> GetElementsRecursive(SyntaxList<XmlNodeSyntax> nodes)
    {
        foreach (var node in nodes)
        {
            if (node is XmlElementSyntax element)
            {
                yield return element;
                foreach (var child in GetElementsRecursive(element.Content))
                {
                    yield return child;
                }
            }
        }
    }
}
using CSharp_IngeniousAnalyzer.Style__Common;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using System.Collections.Generic;
using System.Collections.Immutable;

namespace CSharp_IngeniousAnalyzer.Style_Comment;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public class FuncHeaderNotExists : CommonAnalyzer
{
    public const string DiagnosticId = "COMM001";
    private const string Category = "Comment";
    private static readonly LocalizableString Title = CreateLocalStr(nameof(ResourceEnum.COMM001_Title));
    private static readonly LocalizableString MessageFormat = CreateLocalStr(nameof(ResourceEnum.COMM001_Message));

    protected override DiagnosticDescriptor Rule { get; } = new(
        DiagnosticId, Title, MessageFormat, Category, DiagnosticSeverity.Warning, isEnabledByDefault: true);

    protected override SyntaxKind[] TargetKinds => [SyntaxKind.MethodDeclaration];

    protected override void AnalyzeNode(SyntaxNodeAnalysisContext context)
    {
        if (IsGeneratedFile(context)) return;
        var methodDeclaration = (MethodDeclarationSyntax)context.Node;

        // 1. extern メソッドを除外
        // 2. abstract メソッドも除外（実装がないため）
        // 4. override メソッドも除外（コメントが重複するため）
        foreach (var modifier in methodDeclaration.Modifiers)
        {
            if (modifier.IsKind(SyntaxKind.ExternKeyword) ||
                modifier.IsKind(SyntaxKind.AbstractKeyword) ||
                modifier.IsKind(SyntaxKind.OverrideKeyword))
            {
                return;
            }
        }

        // 3. インターフェース内のメソッド判定（念のため）
        if (methodDeclaration.Parent is InterfaceDeclarationSyntax) return;

        // 既存のドキュメントコメントを効率よく取得
        DocumentationCommentTriviaSyntax? xmlTrivia = null;
        foreach (var trivia in methodDeclaration.GetLeadingTrivia())
        {
            if (trivia.GetStructure() is DocumentationCommentTriviaSyntax doc)
            {
                xmlTrivia = doc;
                break;
            }
        }

        // 1. コメント自体が存在しない場合は無効とみなす
        bool isInvalidComment = xmlTrivia == null;

        // 2. コメントはあるが、中に <summary> タグが含まれていないかを検証
        if (xmlTrivia != null)
        {
            bool hasSummary = false;
            foreach (var element in GetElementsRecursive(xmlTrivia.Content))
            {
                if (element.StartTag.Name.ToString() == "summary")
                {
                    hasSummary = true;
                    break;
                }
            }

            if (!hasSummary)
            {
                isInvalidComment = true;
            }
        }

        if (isInvalidComment)
        {
            context.ReportDiagnostic(Diagnostic.Create(Rule, methodDeclaration.Identifier.GetLocation()));
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
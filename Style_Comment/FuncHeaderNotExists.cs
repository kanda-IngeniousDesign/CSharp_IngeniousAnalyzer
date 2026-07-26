using CSharp_IngeniousAnalyzer.Style__Common;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using System.Collections.Immutable;
using System.Linq;

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
        if (methodDeclaration.Modifiers.Any(m => m.IsKind(SyntaxKind.ExternKeyword))) return;

        // 2. abstract メソッドも除外（実装がないため）
        if (methodDeclaration.Modifiers.Any(m => m.IsKind(SyntaxKind.AbstractKeyword))) return;

        // 3. インターフェース内のメソッド判定（念のため）
        if (methodDeclaration.Parent is InterfaceDeclarationSyntax) return;

        //　4. override メソッドも除外（コメントが重複するため）
        if (methodDeclaration.Modifiers.Any(m => m.IsKind(SyntaxKind.OverrideKeyword))) return;

        // 既存のドキュメントコメントチェック
        var xmlTrivia = methodDeclaration.GetLeadingTrivia()
            .Select(t => t.GetStructure())
            .OfType<DocumentationCommentTriviaSyntax>()
            .FirstOrDefault();

        // 1. コメント自体が存在しない
        // 2. コメントはあるが、中に <summary> タグが含まれていない
        bool isInvalidComment = xmlTrivia == null;

        if (xmlTrivia != null)
        {
            var summaryElement = GetElementsRecursive(xmlTrivia.Content)
                .FirstOrDefault(e => e.StartTag.Name.ToString() == "summary");

            if (summaryElement == null)
            {
                isInvalidComment = true;
            }
        }

        if (isInvalidComment)
        {
            context.ReportDiagnostic(Diagnostic.Create(Rule, methodDeclaration.Identifier.GetLocation()));
        }
    }

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
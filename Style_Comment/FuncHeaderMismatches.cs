using CSharp_IngeniousAnalyzer.Style__Common;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using System.Collections.Immutable;
using System.Linq;

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

    protected override void AnalyzeNode(SyntaxNodeAnalysisContext context)
    {
        if (IsGeneratedFile(context)) return;
        var methodDeclaration = (MethodDeclarationSyntax)context.Node;

        var xmlTrivia = methodDeclaration.GetLeadingTrivia()
            .Select(i => i.GetStructure())
            .OfType<DocumentationCommentTriviaSyntax>()
            .FirstOrDefault();

        // コメントがない場合は別のアナライザー(COMM001)の担当とする
        if (xmlTrivia == null) return;

        // コメントの文字列を取得
        var commentText = xmlTrivia.ToString();

        // <summary> が含まれていない変則的なコメント（エイリアンコード）の場合は、
        // COMM002 の監視対象外としてスキップする（COMM001 や別の仕組みに委ねる）
        if (!commentText.Contains("<summary>")) return;

        // paramタグの抽出
        var paramTags = xmlTrivia.Content
            .OfType<XmlElementSyntax>()
            .Where(e => e.StartTag.Name.ToString() == "param")
            .Select(e => e.StartTag.Attributes.OfType<XmlNameAttributeSyntax>().FirstOrDefault()?.Identifier.Identifier.ValueText)
            .Where(name => name != null)
            .ToList();

        // メソッドの引数名抽出
        var parameterNames = methodDeclaration.ParameterList.Parameters
            .Select(p => p.Identifier.ValueText)
            .ToList();

        // 不一致の判定
        if (paramTags.Count != parameterNames.Count || !paramTags.SequenceEqual(parameterNames))
        {
            // 不一致箇所を特定し、メッセージに埋め込むための文字列生成
            var missingParams = parameterNames.Except(paramTags).ToList();
            var extraParams = paramTags.Except(parameterNames).ToList();
            var details = string.Join(", ", missingParams.Concat(extraParams));

            context.ReportDiagnostic(Diagnostic.Create(Rule, methodDeclaration.Identifier.GetLocation(), details));
        }
    }
}
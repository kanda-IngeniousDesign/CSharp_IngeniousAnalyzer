using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using System.Collections.Generic;
using System.Linq;
using CSharp_IngeniousAnalyzer.Style__Common;

namespace CSharp_IngeniousAnalyzer.Style_String;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public class Nameof : CommonAnalyzer
{
    public const string DiagnosticId = "STR002";
    private const string Category = "Style";
    private static readonly LocalizableString Title = CreateLocalStr(nameof(ResourceEnum.STR002_Title));
    private static readonly LocalizableString MessageFormat = CreateLocalStr(nameof(ResourceEnum.STR002_Message));

    protected override DiagnosticDescriptor Rule { get; } = new(
        DiagnosticId, Title, MessageFormat, Category, DiagnosticSeverity.Warning, isEnabledByDefault: true);

    protected override SyntaxKind[] TargetKinds => [
        SyntaxKind.StringLiteralExpression,
        SyntaxKind.InterpolatedStringText
    ];

    protected override void AnalyzeNode(SyntaxNodeAnalysisContext context)
    {
        if (IsGeneratedFile(context)) return;

        SyntaxNode targetNode = context.Node;

        // 代入の左辺（代入先）になっている場合は除外
        if (IsAssignmentTarget(targetNode)) return;

        // 所属する文を取得
        var statement = targetNode.Ancestors().OfType<StatementSyntax>().FirstOrDefault();
        if (statement is null) return;

        // 代入式が含まれる文全体の場合は除外
        if (IsInAssignmentStatement(statement)) return;

        string text = ExtractNodeText(targetNode);
        if (string.IsNullOrEmpty(text)) return;

        // 空白および特定の囲み記号や区切り文字を除外してトリム
        string trimmedText = TrimSurroundingSymbols(text);
        if (string.IsNullOrWhiteSpace(trimmedText)) return;

        // トリムした文字列全体が有効な識別子ではない場合は対象外
        if (!SyntaxFacts.IsValidIdentifier(trimmedText)) return;

        // 文に含まれる変数名・識別子を取得
        var variableNames = GetValidVariableNames(statement);
        if (variableNames.Count == 0) return;

        // 変数として存在しない場合は対象外
        if (!variableNames.Contains(trimmedText)) return;

        var diagnostic = Diagnostic.Create(Rule, targetNode.GetLocation(), trimmedText);
        context.ReportDiagnostic(diagnostic);
    }

    private static string ExtractNodeText(SyntaxNode node)
    {
        return node switch
        {
            LiteralExpressionSyntax literal => literal.Token.ValueText,
            InterpolatedStringTextSyntax textSyntax => textSyntax.TextToken.ValueText,
            _ => string.Empty
        };
    }

    private static string TrimSurroundingSymbols(string text)
    {
        char[] charsToTrim = [' ', '\t', '\r', '\n', '[', ']', '(', ')', '{', '}', '"', '\'', '：', ':', ';', ',', '.', '<', '>', '/', '\\', '|', '!', '@', '#', '$', '%', '^', '&', '*', '-', '+', '=', '~', '`'];
        return text.Trim(charsToTrim);
    }

    private static bool IsInAssignmentStatement(StatementSyntax statement)
    {
        return statement.DescendantNodes().OfType<AssignmentExpressionSyntax>().Any();
    }

    private static bool IsAssignmentTarget(SyntaxNode node)
    {
        var assignment = node.Ancestors().OfType<AssignmentExpressionSyntax>().FirstOrDefault();
        if (assignment is not null)
        {
            if (assignment.Left.Span.Contains(node.Span) || assignment.Left.FullSpan.Contains(node.FullSpan))
            {
                return true;
            }
        }
        return false;
    }

    private static HashSet<string> GetValidVariableNames(StatementSyntax statement)
    {
        var identifiers = new HashSet<string>();

        // 変数宣言やパラメータなどの変数名を取得
        var variableDeclarators = statement.DescendantNodes().OfType<VariableDeclaratorSyntax>();
        foreach (var declarator in variableDeclarators)
        {
            identifiers.Add(declarator.Identifier.ValueText);
        }

        // 式の中で使われている通常の識別子を取得（メソッド名等を除外）
        var identifierNames = statement.DescendantNodes().OfType<IdentifierNameSyntax>();
        foreach (var id in identifierNames)
        {
            if (id.Parent is InvocationExpressionSyntax invocation && invocation.Expression == id)
            {
                continue;
            }
            identifiers.Add(id.Identifier.ValueText);
        }

        return identifiers;
    }
}
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

        // 所属する文を取得する
        var statement = targetNode.Ancestors().OfType<StatementSyntax>().FirstOrDefault();
        if (statement is null) return;

        // 代入行全体を完全に除外する
        if (statement is ExpressionStatementSyntax exprStmt && exprStmt.Expression is AssignmentExpressionSyntax)
        {
            return;
        }

        // 代入の左辺（代入先）である場合は除外する
        if (IsAssignmentTarget(targetNode)) return;

        // 変数宣言の初期化子である場合は除外する
        if (IsVariableDeclarationInitializer(targetNode)) return;

        // 制御文の条件式部分に含まれている場合は除外する
        if (IsInControlStatementCondition(targetNode)) return;

        string text = ExtractNodeText(targetNode);
        if (string.IsNullOrEmpty(text)) return;

        // 空白および特定の囲み記号や区切り文字を除外してトリムする
        string trimmedText = TrimSurroundingSymbols(text);
        if (string.IsNullOrWhiteSpace(trimmedText)) return;

        // トリムした文字列全体が有効な識別子ではない場合は対象外とする
        if (!SyntaxFacts.IsValidIdentifier(trimmedText)) return;

        // スコープ内の有効なローカル変数・パラメータ名を取得する
        var localVariableNames = GetLocalVariableNames(statement, context.SemanticModel);
        if (localVariableNames.Count == 0) return;

        // ローカル変数として存在しない場合は対象外とする
        if (!localVariableNames.Contains(trimmedText)) return;

        // 同じ文の内部に、その変数名（IdentifierName）が実際に使用されている場合のみ許可する
        if (!IsVariableUsedInStatement(statement, trimmedText)) return;

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

    private static bool IsVariableDeclarationInitializer(SyntaxNode node)
    {
        var declarator = node.Ancestors().OfType<VariableDeclaratorSyntax>().FirstOrDefault();
        if (declarator?.Initializer is not null)
        {
            if (declarator.Initializer.Span.Contains(node.Span) || declarator.Initializer.FullSpan.Contains(node.FullSpan))
            {
                return true;
            }
        }
        return false;
    }

    private static bool IsInControlStatementCondition(SyntaxNode node)
    {
        foreach (var ancestor in node.Ancestors())
        {
            switch (ancestor)
            {
                case IfStatementSyntax ifStmt:
                    if (ifStmt.Condition.Span.Contains(node.Span)) return true;
                    break;
                case WhileStatementSyntax whileStmt:
                    if (whileStmt.Condition.Span.Contains(node.Span)) return true;
                    break;
                case DoStatementSyntax doStmt:
                    if (doStmt.Condition.Span.Contains(node.Span)) return true;
                    break;
                case ForStatementSyntax forStmt:
                    if (forStmt.Condition?.Span.Contains(node.Span) == true) return true;
                    break;
                case SwitchStatementSyntax switchStmt:
                    if (switchStmt.Expression.Span.Contains(node.Span)) return true;
                    break;
            }
        }
        return false;
    }

    /// <summary>
    /// 同じ文の内部に、対象の変数名と一致する識別子（IdentifierName）が実変数として存在するかを検証する
    /// </summary>
    private static bool IsVariableUsedInStatement(StatementSyntax statement, string variableName)
    {
        var identifiers = statement.DescendantNodes().OfType<IdentifierNameSyntax>();
        foreach (var id in identifiers)
        {
            if (id.Identifier.ValueText == variableName)
            {
                return true;
            }
        }
        return false;
    }

    private static HashSet<string> GetLocalVariableNames(StatementSyntax statement, SemanticModel model)
    {
        var identifiers = new HashSet<string>();

        var methodSymbol = model.GetEnclosingSymbol(statement.SpanStart) as IMethodSymbol;
        if (methodSymbol != null)
        {
            foreach (var param in methodSymbol.Parameters)
            {
                identifiers.Add(param.Name);
            }
        }

        var methodBody = statement.Ancestors().OfType<MethodDeclarationSyntax>().FirstOrDefault()?.Body;
        if (methodBody != null)
        {
            var variableDeclarators = methodBody.DescendantNodes().OfType<VariableDeclaratorSyntax>();
            foreach (var declarator in variableDeclarators)
            {
                if (model.GetDeclaredSymbol(declarator) is ILocalSymbol)
                {
                    identifiers.Add(declarator.Identifier.ValueText);
                }
            }
        }

        return identifiers;
    }
}
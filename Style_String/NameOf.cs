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
        string text = string.Empty;

        if (targetNode is LiteralExpressionSyntax literal)
        {
            text = literal.Token.ValueText;
        }
        else if (targetNode is InterpolatedStringTextSyntax textSyntax)
        {
            text = textSyntax.TextToken.ValueText;
        }

        if (string.IsNullOrWhiteSpace(text)) return;

        var statement = targetNode.Ancestors().OfType<StatementSyntax>().FirstOrDefault();
        if (statement is null) return;

        var identifiers = statement.DescendantNodes()
            .OfType<IdentifierNameSyntax>()
            .Select(id => id.Identifier.ValueText)
            .ToList();

        if (identifiers.Count == 0) return;

        var words = text.Split([' ', ':', '=', ',', ';', '\t', '\r', '\n'], System.StringSplitOptions.RemoveEmptyEntries);
        
        string matchedIdentifier = null;
        foreach (var word in words)
        {
            bool containsWord = false;
            foreach (var id in identifiers)
            {
                if (id == word)
                {
                    containsWord = true;
                    break;
                }
            }

            if (SyntaxFacts.IsValidIdentifier(word) && containsWord)
            {
                matchedIdentifier = word;
                break;
            }
        }

        if (matchedIdentifier is null) return;

        if (IsAssignmentTarget(targetNode)) return;

        var diagnostic = Diagnostic.Create(Rule, targetNode.GetLocation(), matchedIdentifier);
        context.ReportDiagnostic(diagnostic);
    }

    private static bool IsAssignmentTarget(SyntaxNode node)
    {
        var assignment = node.Ancestors().OfType<AssignmentExpressionSyntax>().FirstOrDefault();
        if (assignment is not null)
        {
            return assignment.Left.Span.Contains(node.Span);
        }
        return false;
    }
}
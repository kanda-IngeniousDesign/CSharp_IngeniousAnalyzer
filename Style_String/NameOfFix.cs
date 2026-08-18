using System.Collections.Immutable;
using System.Composition;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using CSharp_IngeniousAnalyzer.Style_String;
using CSharp_IngeniousAnalyzer.Style__Common;

namespace CSharp_IngeniousAnalyzer.Style_String;

[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(NameOfFix)), Shared]
public class NameOfFix : CodeFixProvider
{
    public sealed override ImmutableArray<string> FixableDiagnosticIds => [NameOf.DiagnosticId];

    public sealed override FixAllProvider GetFixAllProvider() => WellKnownFixAllProviders.BatchFixer;

    public sealed override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
        if (root is null) return;

        var diagnostic = context.Diagnostics.First();
        var span = diagnostic.Location.SourceSpan;

        SyntaxNode? targetNode = root.FindNodeAtSpan<LiteralExpressionSyntax>(span);
        if (targetNode is null)
        {
            targetNode = root.FindNodeAtSpan<InterpolatedStringTextSyntax>(span);
        }

        if (targetNode is null) return;

        context.RegisterCodeFix(
            CodeAction.Create(
                title: "Fix : nameof に書き換える",
                createChangedDocument: c => ReplaceWithNameofAsync(context.Document, targetNode, c),
                equivalenceKey: "ReplaceWithNameof"),
            diagnostic);
    }

    private async Task<Document> ReplaceWithNameofAsync(Document document, SyntaxNode targetNode, CancellationToken cancellationToken)
    {
        var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
        if (root is null) return document;

        var semanticModel = await document.GetSemanticModelAsync(cancellationToken).ConfigureAwait(false);
        if (semanticModel is null) return document;

        var statement = targetNode.Ancestors().OfType<StatementSyntax>().FirstOrDefault();
        var validVariableNames = GetLocalVariableNames(statement, semanticModel);

        return targetNode switch
        {
            LiteralExpressionSyntax literal => ReplaceLiteralWithNameof(root, document, literal, validVariableNames),
            InterpolatedStringTextSyntax textSyntax => ReplaceInterpolatedTextWithNameof(root, document, textSyntax, validVariableNames),
            _ => document
        };
    }

    /// <summary>
    /// 通常の文字列リテラルを nameof 表現に置き換える
    /// </summary>
    private static Document ReplaceLiteralWithNameof(SyntaxNode syntaxRoot, Document document, LiteralExpressionSyntax literal, HashSet<string> validVariableNames)
    {
        var text = literal.Token.ValueText;
        var variableName = ExtractMatchingVariableName(text, validVariableNames);
        if (string.IsNullOrEmpty(variableName)) return document;

        if (text.Trim() == variableName)
        {
            var nameofExpression = CreateNameofExpression(variableName).WithTriviaFrom(literal);
            var replacedRoot = syntaxRoot.ReplaceNode(literal, nameofExpression);
            return document.WithSyntaxRoot(replacedRoot);
        }

        var parts = text.Split([variableName], 2, System.StringSplitOptions.None);
        if (parts.Length < 2) return document;

        var interpolation = SyntaxFactory.Interpolation(CreateNameofExpression(variableName));

        var contents = new SyntaxList<InterpolatedStringContentSyntax>();
        AddLiteralTextTokenIfNotEmpty(ref contents, parts[0], literal);
        contents = contents.Add(interpolation);
        AddLiteralTextTokenIfNotEmpty(ref contents, parts[1], literal);

        var interpolatedString = SyntaxFactory.InterpolatedStringExpression(
            SyntaxFactory.Token(SyntaxKind.InterpolatedStringStartToken),
            contents,
            SyntaxFactory.Token(SyntaxKind.InterpolatedStringEndToken))
            .WithTriviaFrom(literal);

        var newRoot = syntaxRoot.ReplaceNode(literal, interpolatedString);
        return document.WithSyntaxRoot(newRoot);
    }

    /// <summary>
    /// 補間文字列内のテキスト部分を nameof 表現に置き換える
    /// </summary>
    private static Document ReplaceInterpolatedTextWithNameof(SyntaxNode syntaxRoot, Document document, InterpolatedStringTextSyntax textSyntax, HashSet<string> validVariableNames)
    {
        var text = textSyntax.TextToken.ValueText;
        var variableName = ExtractMatchingVariableName(text, validVariableNames);
        if (string.IsNullOrEmpty(variableName)) return document;

        var interpolatedString = textSyntax.Ancestors().OfType<InterpolatedStringExpressionSyntax>().FirstOrDefault();
        if (interpolatedString is null) return document;

        var interpolation = SyntaxFactory.Interpolation(CreateNameofExpression(variableName));
        var newContents = BuildNewInterpolatedContents(interpolatedString.Contents, textSyntax, text, variableName, interpolation);

        var newInterpolatedString = interpolatedString.WithContents(newContents);
        var replacedRoot = syntaxRoot.ReplaceNode(interpolatedString, newInterpolatedString);
        return document.WithSyntaxRoot(replacedRoot);
    }

    /// <summary>
    /// nameof(variableName) の式ノードを生成する
    /// </summary>
    private static InvocationExpressionSyntax CreateNameofExpression(string variableName)
    {
        // SyntaxFactory.IdentifierName("nameof") + InvocationExpression で手組みすると、
        // パーサーが通常付与する「nameofは文脈キーワードである」という認識がトークンに乗らず、
        // 見た目は同じ 'nameof(x)' でもバインダーが実在しない識別子として扱い CS0103 になる。
        // ParseExpression で実際にパースさせることで、この文脈キーワード認識を正しく持たせる。
        return (InvocationExpressionSyntax)SyntaxFactory.ParseExpression($"nameof({variableName})");
    }

    /// <summary>
    /// 補間文字列のコンテンツリストを再構築し、対象テキスト部分を interpolation に置き換える
    /// </summary>
    private static SyntaxList<InterpolatedStringContentSyntax> BuildNewInterpolatedContents(
        SyntaxList<InterpolatedStringContentSyntax> contents,
        InterpolatedStringTextSyntax targetTextSyntax,
        string text,
        string variableName,
        InterpolationSyntax interpolation)
    {
        var parts = text.Split([variableName], 2, System.StringSplitOptions.None);
        var newContents = new SyntaxList<InterpolatedStringContentSyntax>();

        foreach (var content in contents)
        {
            if (content == targetTextSyntax)
            {
                AddInterpolatedTextTokenIfNotEmpty(ref newContents, parts[0], targetTextSyntax);
                newContents = newContents.Add(interpolation);
                if (parts.Length > 1)
                {
                    AddInterpolatedTextTokenIfNotEmpty(ref newContents, parts[1], targetTextSyntax);
                }
            }
            else
            {
                newContents = newContents.Add(content);
            }
        }

        return newContents;
    }

    private static void AddInterpolatedTextTokenIfNotEmpty(ref SyntaxList<InterpolatedStringContentSyntax> contents, string textPart, InterpolatedStringTextSyntax templateSyntax)
    {
        if (string.IsNullOrEmpty(textPart)) return;

        var token = SyntaxFactory.Token(
            templateSyntax.TextToken.LeadingTrivia,
            SyntaxKind.InterpolatedStringTextToken,
            textPart,
            textPart,
            templateSyntax.TextToken.TrailingTrivia);

        contents = contents.Add(SyntaxFactory.InterpolatedStringText(token));
    }

    private static void AddLiteralTextTokenIfNotEmpty(ref SyntaxList<InterpolatedStringContentSyntax> contents, string textPart, LiteralExpressionSyntax templateLiteral)
    {
        if (string.IsNullOrEmpty(textPart)) return;

        var token = SyntaxFactory.Token(
            templateLiteral.Token.LeadingTrivia,
            SyntaxKind.InterpolatedStringTextToken,
            textPart,
            textPart,
            templateLiteral.Token.TrailingTrivia);

        contents = contents.Add(SyntaxFactory.InterpolatedStringText(token));
    }

    private static string ExtractMatchingVariableName(string text, HashSet<string> validVariableNames)
    {
        char[] charsToTrim = [' ', '\t', '\r', '\n', '[', ']', '(', ')', '{', '}', '"', '\'', '：', ':', ';', ',', '.', '<', '>', '/', '\\', '|', '!', '@', '#', '$', '%', '^', '&', '*', '-', '+', '=', '~', '`'];

        var words = text.Split([' ', ':', '=', ',', ';', '\t', '\r', '\n', '-', '>', '<', '+', '*'], System.StringSplitOptions.RemoveEmptyEntries);
        foreach (var word in words)
        {
            var trimmed = word.Trim(charsToTrim);
            if (validVariableNames.Contains(trimmed))
            {
                return trimmed;
            }
        }
        return string.Empty;
    }

    private static HashSet<string> GetLocalVariableNames(StatementSyntax statement, SemanticModel model)
    {
        var identifiers = new HashSet<string>();
        if (statement is null || model is null) return identifiers;

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
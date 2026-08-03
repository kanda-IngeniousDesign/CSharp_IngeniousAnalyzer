using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using System.Collections.Generic;
using System.Threading;
using CSharp_IngeniousAnalyzer.Style__Common;

namespace CSharp_IngeniousAnalyzer.Style_Collection;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public class ListCapacity : CommonAnalyzer
{
    public const string DiagnosticId = "COLL001";
    private const string Category = "Performance";
    private static readonly LocalizableString Title = CreateLocalStr(nameof(ResourceEnum.COLL001_Title));
    private static readonly LocalizableString MessageFormat = CreateLocalStr(nameof(ResourceEnum.COLL001_Message));

    protected override DiagnosticDescriptor Rule { get; } = new(
        DiagnosticId, Title, MessageFormat, Category, DiagnosticSeverity.Warning, isEnabledByDefault: true);

    protected override SyntaxKind[] TargetKinds => [SyntaxKind.ObjectCreationExpression];

    protected override void AnalyzeNode(SyntaxNodeAnalysisContext context)
    {
        if (IsGeneratedFile(context)) return;
        var objectCreation = (ObjectCreationExpressionSyntax)context.Node;

        // 1. 初期サイズ指定やコレクション初期化子がある場合は対象外とする
        if (objectCreation.ArgumentList?.Arguments.Count > 0 || objectCreation.Initializer != null) return;

        // 2. 生成されている型が「List<T>」であるかを高速に検証する
        var typeInfo = context.SemanticModel.GetTypeInfo(objectCreation, context.CancellationToken);
        if (typeInfo.Type is not INamedTypeSymbol typeSymbol || 
            typeSymbol.Arity != 1 || 
            typeSymbol.Name != "List" || 
            typeSymbol.ContainingNamespace?.ToDisplayString() != "System.Collections.Generic")
        {
            return;
        }

        // 3. ループ上限式を安全に逆引きする
        var limitExpression = GetLimitExpressionOrNull(objectCreation, context.SemanticModel, context.CancellationToken);
        if (limitExpression is null) return;

        // 4. 上限値がローカル変数であり、かつリスト生成よりも後に宣言されている場合は対象外とする
        if (IsVariableDeclaredAfter(limitExpression, objectCreation, context.SemanticModel, context.CancellationToken))
        {
            return;
        }

        // 警告を通知
        var diagnostic = Diagnostic.Create(Rule, objectCreation.GetLocation());
        context.ReportDiagnostic(diagnostic);
    }

    /// <summary>
    /// ループ上限値がローカル変数であり、かつリスト生成よりも後で宣言されているかを判定する
    /// </summary>
    private static bool IsVariableDeclaredAfter(ExpressionSyntax limitExpr, ObjectCreationExpressionSyntax objectCreation, SemanticModel semanticModel, CancellationToken cancellationToken)
    {
        var symbolInfo = semanticModel.GetSymbolInfo(limitExpr, cancellationToken);
        if (symbolInfo.Symbol is ILocalSymbol localSymbol)
        {
            var syntaxRef = localSymbol.DeclaringSyntaxReferences[0];
            return syntaxRef != null && syntaxRef.Span.Start > objectCreation.SpanStart;
        }
        return false;
    }

    /// <summary>
    /// ループ上限を逆引きする安全検証ロジック（ローカル変数・定数のみ許可）
    /// </summary>
    private static ExpressionSyntax? GetLimitExpressionOrNull(ObjectCreationExpressionSyntax objectCreation, SemanticModel semanticModel, CancellationToken cancellationToken)
    {
        var variableDeclarator = objectCreation.FirstAncestorOfType<VariableDeclaratorSyntax>();
        if (variableDeclarator is null) return null;

        var listSymbol = semanticModel.GetDeclaredSymbol(variableDeclarator, cancellationToken);
        if (listSymbol is null) return null;

        var methodBody = objectCreation.FirstAncestorOfType<BlockSyntax>();
        if (methodBody is null) return null;

        foreach (var forLoop in methodBody.DescendantNodes<ForStatementSyntax>())
        {
            if (UsesListInStatement(forLoop.Statement, listSymbol, semanticModel, cancellationToken))
            {
                if (forLoop.Condition is BinaryExpressionSyntax binaryExpression &&
                    binaryExpression.OperatorToken.IsKind(SyntaxKind.LessThanToken))
                {
                    var limitExpr = binaryExpression.Right;

                    var symbolInfo = semanticModel.GetSymbolInfo(limitExpr, cancellationToken);
                    if (symbolInfo.Symbol != null && symbolInfo.Symbol is not ILocalSymbol && symbolInfo.Symbol is not IFieldSymbol)
                    {
                        return null;
                    }
                    if (limitExpr is MemberAccessExpressionSyntax)
                    {
                        return null;
                    }

                    return limitExpr;
                }
            }
        }

        return null;
    }

    /// <summary>
    /// ステートメント内で指定されたリストシンボルが使用されているかを判定する
    /// </summary>
    private static bool UsesListInStatement(StatementSyntax statement, ISymbol listSymbol, SemanticModel semanticModel, CancellationToken cancellationToken)
    {
        foreach (var invocation in statement.DescendantNodes<InvocationExpressionSyntax>())
        {
            if (invocation.Expression is MemberAccessExpressionSyntax memberAccess)
            {
                var objSymbol = semanticModel.GetSymbolInfo(memberAccess.Expression, cancellationToken).Symbol;
                if (SymbolEqualityComparer.Default.Equals(objSymbol, listSymbol))
                {
                    return true;
                }
            }
        }
        return false;
    }
}

/// <summary>
/// パフォーマンス改善のための軽量な構文木走査ヘルパー
/// </summary>
internal static class SyntaxExtensions
{
    public static T? FirstAncestorOfType<T>(this SyntaxNode node) where T : SyntaxNode
    {
        var current = node.Parent;
        while (current != null)
        {
            if (current is T match) return match;
            current = current.Parent;
        }
        return null;
    }

    public static IEnumerable<T> DescendantNodes<T>(this SyntaxNode node) where T : SyntaxNode
    {
        foreach (var descendant in node.DescendantNodes())
        {
            if (descendant is T match) yield return match;
        }
    }
}
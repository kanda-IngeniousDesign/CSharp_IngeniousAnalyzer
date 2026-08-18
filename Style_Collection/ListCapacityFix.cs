using System.Collections.Immutable;
using System.Composition;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CSharp_IngeniousAnalyzer.Style__Common;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace CSharp_IngeniousAnalyzer.Style_Collection;

[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(ListCapacityFix)), Shared]
public class ListCapacityFix : CodeFixProvider
{
    public sealed override ImmutableArray<string> FixableDiagnosticIds => [ListCapacity.DiagnosticId];

    public sealed override FixAllProvider GetFixAllProvider() => WellKnownFixAllProviders.BatchFixer;

    public sealed override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
        var semanticModel = await context.Document.GetSemanticModelAsync(context.CancellationToken).ConfigureAwait(false);
        if (root is null || semanticModel is null) return;

        // 共通拡張メソッド「FindNodeAtSpan」で一撃取得
        var objectCreation = root.FindNodeAtSpan<ObjectCreationExpressionSyntax>(context.Diagnostics.First().Location.SourceSpan);
        if (objectCreation is null) return;

        // 電球を出す前に、安全性を事前チェック
        var limitExpression = GetLimitExpressionOrNull(objectCreation, semanticModel, context.CancellationToken);
        if (limitExpression is null) return;

        context.RegisterCodeFix(
            CodeAction.Create(
                title: "Fix : 初期キャパシティを指定する",
                createChangedDocument: c => FixCapacityAsync(context.Document, objectCreation, c),
                equivalenceKey: "AddCapacityArgument"),
            context.Diagnostics.First());
    }

    private async Task<Document> FixCapacityAsync(Document document, ObjectCreationExpressionSyntax objectCreation, CancellationToken cancellationToken)
    {
        var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
        var semanticModel = await document.GetSemanticModelAsync(cancellationToken).ConfigureAwait(false);
        if (root is null || semanticModel is null) return document;

        var limitExpression = GetLimitExpressionOrNull(objectCreation, semanticModel, cancellationToken);
        if (limitExpression is null) return document;

        var argument = SyntaxFactory.Argument(limitExpression);
        var argumentList = SyntaxFactory.ArgumentList(SyntaxFactory.SeparatedList(new[] { argument }));
        var newObjectCreation = objectCreation.WithArgumentList(argumentList);

        var newRoot = root.ReplaceNode(objectCreation, newObjectCreation);
        return document.WithSyntaxRoot(newRoot);
    }

    private static ExpressionSyntax? GetLimitExpressionOrNull(ObjectCreationExpressionSyntax objectCreation, SemanticModel semanticModel, CancellationToken cancellationToken)
    {
        var variableDeclarator = objectCreation.AncestorsAndSelf().OfType<VariableDeclaratorSyntax>().FirstOrDefault();
        if (variableDeclarator is null) return null;

        var listSymbol = semanticModel.GetDeclaredSymbol(variableDeclarator, cancellationToken);
        if (listSymbol is null) return null;

        var methodBlock = objectCreation.Ancestors().OfType<BlockSyntax>().FirstOrDefault();
        if (methodBlock is null) return null;

        ForStatementSyntax? targetForLoop = null;
        var allForLoops = methodBlock.DescendantNodes().OfType<ForStatementSyntax>();

        foreach (var forLoop in allForLoops)
        {
            var statements = forLoop.Statement.DescendantNodes().OfType<InvocationExpressionSyntax>();
            foreach (var invocation in statements)
            {
                if (invocation.Expression is MemberAccessExpressionSyntax memberAccess)
                {
                    var objSymbol = semanticModel.GetSymbolInfo(memberAccess.Expression, cancellationToken).Symbol;
                    if (SymbolEqualityComparer.Default.Equals(objSymbol, listSymbol))
                    {
                        targetForLoop = forLoop;
                        break;
                    }
                }
            }

            if (targetForLoop != null) break;
        }

        // 安全弁：未満（LessThan）関係のみを安全に対象にする
        if (targetForLoop?.Condition is BinaryExpressionSyntax binaryExpression && 
            binaryExpression.OperatorToken.IsKind(SyntaxKind.LessThanToken))
        {
            var limitExpr = binaryExpression.Right;

            // 上限値が変数であり、かつリスト生成よりも「後」に宣言されている場合はビルドエラーを防ぐため除外する
            if (IsVariableDeclaredAfter(limitExpr, objectCreation, semanticModel, cancellationToken))
            {
                return null;
            }

            return limitExpr;
        }

        return null;
    }

    /// <summary>
    /// ループ上限値が変数であり、かつリスト生成よりも後で宣言されているかを判定する補助メソッド
    /// </summary>
    private static bool IsVariableDeclaredAfter(ExpressionSyntax limitExpr, ObjectCreationExpressionSyntax objectCreation, SemanticModel semanticModel, CancellationToken cancellationToken)
    {
        var symbolInfo = semanticModel.GetSymbolInfo(limitExpr, cancellationToken);
        var symbol = symbolInfo.Symbol;

        if (symbol != null)
        {
            var syntaxRef = symbol.DeclaringSyntaxReferences.FirstOrDefault();
            if (syntaxRef != null)
            {
                // 変数の宣言位置が、リストのインスタンス化よりも「後」にある場合は真（＝危険なので弾く）
                return syntaxRef.Span.Start > objectCreation.SpanStart;
            }
        }

        // リテラルなどの場合は安全とみなす
        return false;
    }
}
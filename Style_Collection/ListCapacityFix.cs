using System.Collections.Immutable;
using System.Composition;
using CSharp_IngeniousAnalyzer.Style__Common;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace CSharp_IngeniousAnalyzer.Style_Collection;

[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(ListCapacityCodeFixProvider)), Shared]
public class ListCapacityCodeFixProvider : CodeFixProvider
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

        // 電球を出す前に、共通のデータフロー解析を走らせて安全性を事前チェック
        var limitExpression = GetLimitExpressionOrNull(objectCreation, semanticModel, context.CancellationToken);
        if (limitExpression is null) return; // 予測不能なループやスコープ逆転の変数は電球メニューを出さない（不発弾ガード）

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
            return limitExpr;
        }

        return null;
    }
}
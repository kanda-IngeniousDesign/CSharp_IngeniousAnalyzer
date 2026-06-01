using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using CSharp_IngeniousAnalyzer.Core;
using CSharp_IngeniousAnalyzer.Style__Common;

namespace CSharp_IngeniousAnalyzer.Style_Collection;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public class ListCapacity : CommonAnalyzer
{
    public const string DiagnosticId = "COLL001";
    private static readonly LocalizableString Title = CreateLocalStr(nameof(ResourceEnum.COLL001_Title));
    private static readonly LocalizableString MessageFormat = CreateLocalStr(nameof(ResourceEnum.COLL001_Message));
    private const string Category = "Performance";

    protected override DiagnosticDescriptor Rule { get; } = new(
        DiagnosticId, Title, MessageFormat, Category, DiagnosticSeverity.Warning, isEnabledByDefault: true);

    protected override SyntaxKind[] TargetKinds => [SyntaxKind.ObjectCreationExpression];

    protected override void AnalyzeNode(SyntaxNodeAnalysisContext context)
    {
        var objectCreation = (ObjectCreationExpressionSyntax)context.Node;

        // 1. 生成されている型が「List<T>」であるかを検証
        var typeSymbol = context.SemanticModel.GetTypeInfo(objectCreation, context.CancellationToken).Type as INamedTypeSymbol;
        if (typeSymbol is null || typeSymbol.OriginalDefinition.ToDisplayString() != "System.Collections.Generic.List<T>") return;

        // 2. すでに初期サイズが指定されている、またはコレクション初期化子がある場合はスルー
        if ((objectCreation.ArgumentList != null && objectCreation.ArgumentList.Arguments.Count > 0) || objectCreation.Initializer != null) return;

        // 🛠️ 【復活した安全弁】
        // ループ条件が「i != 100」のような予測不能なケースは、波線（警告）すら出さずに完全スルーする！
        var limitExpression = GetLimitExpressionOrNull(objectCreation, context.SemanticModel, context.CancellationToken);
        if (limitExpression is null) return;

        // 安全だと確定したケースのみ警告を通知
        var diagnostic = Diagnostic.Create(Rule, objectCreation.GetLocation());
        context.ReportDiagnostic(diagnostic);
    }

    /// <summary>
    /// CodeFix側と全く同じ、ループ上限を逆引きする安全検証ロジック（アナライザーのガード用）
    /// </summary>
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

        if (targetForLoop?.Condition is BinaryExpressionSyntax binaryExpression && 
            binaryExpression.OperatorToken.IsKind(SyntaxKind.LessThanToken))
        {
            return binaryExpression.Right;
        }

        return null;
    }
}
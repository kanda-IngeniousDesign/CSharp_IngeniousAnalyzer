using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
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

        // 1. 生成されている型が「List<T>」であるかを検証
        var typeSymbol = context.SemanticModel.GetTypeInfo(objectCreation, context.CancellationToken).Type as INamedTypeSymbol;
        if (typeSymbol is null || typeSymbol.OriginalDefinition.ToDisplayString() != "System.Collections.Generic.List<T>") return;

        // 2. すでに初期サイズが指定されている、或者コレクション初期化子がある場合はスルー
        if ((objectCreation.ArgumentList != null && objectCreation.ArgumentList.Arguments.Count > 0) || objectCreation.Initializer != null) return;

        // ループ条件が「i != 100」のような予測不能なケースは、波線（警告）すら出さずに完全スルーする！
        var limitExpression = GetLimitExpressionOrNull(objectCreation, context.SemanticModel, context.CancellationToken);
        if (limitExpression is null) return;

        // ★ 改良されたガード：上限値が「変数」であり、かつリスト生成よりも後で宣言されている場合は警告対象外とする
        // （リテラル定数の場合は位置に関わらず安全なため通過する）
        if (IsVariableDeclaredAfter(limitExpression, objectCreation, context.SemanticModel, context.CancellationToken))
        {
            return;
        }

        // 安全だと確定したケースのみ警告を通知
        var diagnostic = Diagnostic.Create(Rule, objectCreation.GetLocation());
        context.ReportDiagnostic(diagnostic);
    }

    /// <summary>
    /// ループ上限値が変数であり、かつリスト生成よりも後で宣言されているかを判定する補助メソッド
    /// </summary>
    private static bool IsVariableDeclaredAfter(ExpressionSyntax limitExpr, ObjectCreationExpressionSyntax objectCreation, SemanticModel semanticModel, CancellationToken cancellationToken)
    {
        // 上限値の式からシンボル情報を取得（リテラルの場合は Symbol が null になる）
        var symbolInfo = semanticModel.GetSymbolInfo(limitExpr, cancellationToken);
        var symbol = symbolInfo.Symbol;

        if (symbol != null)
        {
            // ローカル変数やパラメータなどの場合、その宣言位置を取得
            var syntaxRef = symbol.DeclaringSyntaxReferences.FirstOrDefault();
            if (syntaxRef != null)
            {
                // 変数の宣言位置が、リストのインスタンス化よりも「後」にある場合は真（＝危険なので弾く）
                return syntaxRef.Span.Start > objectCreation.SpanStart;
            }
        }

        // シンボルが取れない（リテラル等）場合は、位置の前後関係に関わらず安全とみなして通過させる
        return false;
    }

    /// <summary>
    /// ループ上限を逆引きする安全検証ロジック
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
            var invocations = forLoop.DescendantNodes().OfType<InvocationExpressionSyntax>();
            foreach (var invocation in invocations)
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
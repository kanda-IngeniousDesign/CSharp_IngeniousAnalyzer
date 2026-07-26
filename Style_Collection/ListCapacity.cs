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

        // 2. すでに初期サイズが指定されている、あるいはコレクション初期化子がある場合はスルー
        if ((objectCreation.ArgumentList != null && objectCreation.ArgumentList.Arguments.Count > 0) || objectCreation.Initializer != null) return;

        // 3. ループ上限式を安全逆引き（取れない場合はスルー）
        var limitExpression = GetLimitExpressionOrNull(objectCreation, context.SemanticModel, context.CancellationToken);
        if (limitExpression is null) return;

        // 4. 上限値が「変数」であり、かつリスト生成よりも「後」に宣言されている場合は警告対象外とする
        // （リテラル定数の場合はシンボルが取れないため安全として通過する）
        if (IsVariableDeclaredAfter(limitExpression, objectCreation, context.SemanticModel, context.CancellationToken))
        {
            return;
        }

        // 安全かつ確実と確定したケースのみ警告を通知
        var diagnostic = Diagnostic.Create(Rule, objectCreation.GetLocation());
        context.ReportDiagnostic(diagnostic);
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

        // メソッド内のすべての for ループを対象にするが、
        // 「このリストの .Add() を実際に呼び出しているループ」を厳密に探す
        var allForLoops = methodBlock.DescendantNodes().OfType<ForStatementSyntax>();

        foreach (var forLoop in allForLoops)
        {
            var invocations = forLoop.Statement.DescendantNodes().OfType<InvocationExpressionSyntax>();
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

        if (targetForLoop is null) return null;

        // ターゲットとなった for ループのさらに内側に、別の for ループが存在する場合は、
        // どちらのループ上限を指しているか曖昧になるためスルーする
        if (targetForLoop.Statement.DescendantNodes().OfType<ForStatementSyntax>().Any())
        {
            return null;
        }

        // 未満（LessThan）関係のみを安全に対象にする
        if (targetForLoop.Condition is BinaryExpressionSyntax binaryExpression &&
            binaryExpression.OperatorToken.IsKind(SyntaxKind.LessThanToken))
        {
            var limitExpr = binaryExpression.Right;

            // 上限値が変数であり、かつリスト生成よりも「後」に宣言されている場合は除外する
            if (IsVariableDeclaredAfter(limitExpr, objectCreation, semanticModel, cancellationToken))
            {
                return null;
            }

            return limitExpr;
        }

        return null;
    }
}
using CSharp_IngeniousAnalyzer.Style__Common;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace CSharp_IngeniousAnalyzer.Style_Async;

// TODO: 第2弾として await foreach（IAsyncEnumerable<T>）/ await using（IAsyncDisposable）の
//       ConfigureAwait(false) 未使用も検知対象にする（GitHub Issueで管理）
// ".ConfigureAwait(false)" を追記するFixは提供しない（理由は MissingConfigureAwaitFix を参照）。
// 意図的に元のコンテキストへ戻す必要がある場合は "Ignore ASYNC001" のFixで抑制コメントを挿入する。
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public class MissingConfigureAwait : CommonAnalyzer
{
    public const string DiagnosticId = "ASYNC001";
    private const string Category = "Async";
    private static readonly LocalizableString Title = CreateLocalStr(nameof(ResourceEnum.ASYNC001_Title));
    private static readonly LocalizableString MessageFormat = CreateLocalStr(nameof(ResourceEnum.ASYNC001_Message));

    private static readonly string[] TaskLikeMetadataNames =
    [
        "System.Threading.Tasks.Task",
        "System.Threading.Tasks.Task`1",
        "System.Threading.Tasks.ValueTask",
        "System.Threading.Tasks.ValueTask`1"
    ];

    protected override DiagnosticDescriptor Rule { get; } = new(
        DiagnosticId, Title, MessageFormat, Category, DiagnosticSeverity.Warning, isEnabledByDefault: true);

    protected override SyntaxKind[] TargetKinds => [SyntaxKind.AwaitExpression];

    protected override void AnalyzeNode(SyntaxNodeAnalysisContext context)
    {
        if (IsGeneratedFile(context)) return;

        var awaitExpr = (AwaitExpressionSyntax)context.Node;

        // 構文だけで判定できる除外条件を先に評価し、セマンティック解析のコストを避ける
        if (HasConfigureAwaitCall(awaitExpr.Expression)) return;
        if (IsInsideEntryPoint(awaitExpr)) return;

        // UIスレッドへの復帰が必要なメソッド等、意図的に ConfigureAwait を付けない場合は
        // それを含むメソッド単位で "// Ignore ASYNC001" による抑制を許容する
        if (awaitExpr.FirstAncestorOrSelf<BaseMethodDeclarationSyntax>()?.HasIgnoreComment(DiagnosticId) == true) return;
        if (IsInsideAsyncVoidContext(awaitExpr, context.SemanticModel, context.CancellationToken)) return;

        var type = context.SemanticModel.GetTypeInfo(awaitExpr.Expression, context.CancellationToken).Type;
        if (!IsTaskLikeType(type, context.Compilation)) return;

        context.ReportDiagnostic(Diagnostic.Create(Rule, awaitExpr.GetLocation(), awaitExpr.Expression.ToString()));
    }

    /// <summary>
    /// await対象の式が既に ConfigureAwait(...) の呼び出しであるかを判定する。
    /// 引数が true/false のどちらでも、明示的に意図表明されているものとして扱う。
    /// </summary>
    private static bool HasConfigureAwaitCall(ExpressionSyntax expression)
    {
        // "await (task.ConfigureAwait(false))" のように括弧で包まれている場合も考慮する
        while (expression is ParenthesizedExpressionSyntax parenthesized)
        {
            expression = parenthesized.Expression;
        }

        return expression is InvocationExpressionSyntax
        {
            Expression: MemberAccessExpressionSyntax { Name.Identifier.Text: "ConfigureAwait" }
        };
    }

    /// <summary>
    /// static な Main メソッド内、またはトップレベルステートメント内（コンソールアプリのエントリポイント）であるかを判定する
    /// </summary>
    private static bool IsInsideEntryPoint(AwaitExpressionSyntax awaitExpr)
    {
        foreach (var node in awaitExpr.Ancestors())
        {
            if (node is GlobalStatementSyntax) return true;

            if (node is MethodDeclarationSyntax method)
            {
                return method.Identifier.Text == "Main" && method.Modifiers.Any(SyntaxKind.StaticKeyword);
            }
        }
        return false;
    }

    /// <summary>
    /// await を直接含む関数（メソッド・ローカル関数・ラムダ式・匿名メソッド）が async void であるかを判定する。
    /// ラムダ式は Action 系デリゲートに変換されている場合に戻り値が void となる。
    /// </summary>
    private static bool IsInsideAsyncVoidContext(AwaitExpressionSyntax awaitExpr, SemanticModel semanticModel, CancellationToken cancellationToken)
    {
        var function = awaitExpr.Ancestors().FirstOrDefault(
            n => n is AnonymousFunctionExpressionSyntax or LocalFunctionStatementSyntax or BaseMethodDeclarationSyntax);

        var symbol = function switch
        {
            AnonymousFunctionExpressionSyntax lambda => semanticModel.GetSymbolInfo(lambda, cancellationToken).Symbol,
            null => null,
            _ => semanticModel.GetDeclaredSymbol(function, cancellationToken)
        };

        return symbol is IMethodSymbol { ReturnsVoid: true };
    }

    /// <summary>
    /// 型が Task / Task&lt;T&gt; / ValueTask / ValueTask&lt;T&gt; のいずれかであるかを判定する
    /// </summary>
    private static bool IsTaskLikeType(ITypeSymbol? type, Compilation compilation)
    {
        if (type is not INamedTypeSymbol namedType) return false;

        var originalDefinition = namedType.OriginalDefinition;
        foreach (var metadataName in TaskLikeMetadataNames)
        {
            if (SymbolEqualityComparer.Default.Equals(originalDefinition, compilation.GetTypeByMetadataName(metadataName))) return true;
        }
        return false;
    }
}

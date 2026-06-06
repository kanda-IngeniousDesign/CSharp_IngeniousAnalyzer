using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using CSharp_IngeniousAnalyzer.Style__Common;

namespace CSharp_IngeniousAnalyzer.Style_Linq;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public class ToListToArrayDelete : CommonAnalyzer
{
    public const string DiagnosticId = "LINQ002";
    private const string Category = "Performance";
    private static readonly LocalizableString Title = CreateLocalStr(nameof(ResourceEnum.LINQ002_Title));
    private static readonly LocalizableString MessageFormat = CreateLocalStr(nameof(ResourceEnum.LINQ002_Message));

    protected override DiagnosticDescriptor Rule { get; } = new(
        DiagnosticId, Title, MessageFormat, Category, DiagnosticSeverity.Warning, isEnabledByDefault: true);

    protected override SyntaxKind[] TargetKinds => [SyntaxKind.InvocationExpression];

    protected override void AnalyzeNode(SyntaxNodeAnalysisContext context)
    {
        var invocation = (InvocationExpressionSyntax)context.Node;
        var model = context.SemanticModel;

        // 1. ToList / ToArray か確認
        var methodSymbol = model.GetSymbolInfo(invocation, context.CancellationToken).Symbol as IMethodSymbol;
        if (methodSymbol == null || methodSymbol.ContainingType.ToDisplayString() != "System.Linq.Enumerable")
            return;

        var name = methodSymbol.Name;
        if (name != "ToList" && name != "ToArray") return;

        // 2. 判定ロジック：不要な実体化かどうか
        bool isRedundant = false;

        // パターンA: foreach の式として直接使われている
        if (invocation.Parent is ForEachStatementSyntax forEach && forEach.Expression == invocation)
        {
            isRedundant = true;
        }
        // パターンB: 変数に代入されているが、その変数が foreach でのみ消費されている
        else if (invocation.Parent is EqualsValueClauseSyntax { Parent: VariableDeclaratorSyntax variableDeclarator })
        {
            var symbol = model.GetDeclaredSymbol(variableDeclarator, context.CancellationToken);
            if (symbol != null)
            {
                var methodBody = invocation.Ancestors().OfType<MethodDeclarationSyntax>().FirstOrDefault()?.Body;
                if (methodBody != null)
                {
                    // 変数の参照を検索
                    var references = methodBody.DescendantNodes().OfType<IdentifierNameSyntax>()
                        .Where(id => SymbolEqualityComparer.Default.Equals(model.GetSymbolInfo(id).Symbol, symbol));

                    // 「変数の定義」以外に参照が1つだけあり、それが foreach の in にある場合
                    if (references.Count() == 1 && references.First().Parent is ForEachStatementSyntax forEachStmt && forEachStmt.Expression == references.First())
                    {
                        isRedundant = true;
                    }
                }
            }
        }

        if (isRedundant)
        {
            var receiverName = invocation.Expression is MemberAccessExpressionSyntax memberAccess 
            ? memberAccess.Expression.ToString() 
            : "expression"; // 変数名が取れない場合はデフォルト値

            context.ReportDiagnostic(Diagnostic.Create(Rule, invocation.GetLocation(), receiverName));
        }
    }
}
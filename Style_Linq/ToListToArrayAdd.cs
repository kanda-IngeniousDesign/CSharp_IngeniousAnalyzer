using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using CSharp_IngeniousAnalyzer.Style__Common;

namespace CSharp_IngeniousAnalyzer.Style_Linq;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public class ToListToArrayAdd : CommonAnalyzer
{
    public const string DiagnosticId = "LINQ003";
    private const string Category = "Performance";
    private static readonly LocalizableString Title = CreateLocalStr(nameof(ResourceEnum.LINQ003_Title));
    private static readonly LocalizableString MessageFormat = CreateLocalStr(nameof(ResourceEnum.LINQ003_Message));

    protected override DiagnosticDescriptor Rule { get; } = new(
        DiagnosticId, Title, MessageFormat, Category, DiagnosticSeverity.Warning, isEnabledByDefault: true);

    // 変数宣言を起点にする
    protected override SyntaxKind[] TargetKinds => [SyntaxKind.VariableDeclarator];

    protected override void AnalyzeNode(SyntaxNodeAnalysisContext context)
    {
        var declarator = (VariableDeclaratorSyntax)context.Node;
        if (declarator.Initializer?.Value is not InvocationExpressionSyntax invocation) return;

        var model = context.SemanticModel;
        var methodSymbol = model.GetSymbolInfo(invocation, context.CancellationToken).Symbol as IMethodSymbol;
        
        // 1. LINQメソッドであること（かつ ToList/ToArray ではないこと）を確認
        if (methodSymbol == null || methodSymbol.ContainingType.ToDisplayString() != "System.Linq.Enumerable")
            return;

        var name = methodSymbol.Name;
        if (name == "ToList" || name == "ToArray") return; // すでに確定済みなら対象外（LINQ002の領分）

        // 2. 変数のシンボルを取得
        var symbol = model.GetDeclaredSymbol(declarator, context.CancellationToken);
        if (symbol == null) return;

        // 3. メソッド内で変数が何回「列挙」されているかカウント
        var methodBody = declarator.Ancestors().OfType<MethodDeclarationSyntax>().FirstOrDefault()?.Body;
        if (methodBody == null) return;

        var allReferences = methodBody.DescendantNodes().OfType<IdentifierNameSyntax>()
            .Where(id => SymbolEqualityComparer.Default.Equals(model.GetSymbolInfo(id).Symbol, symbol));

        int enumerationCount = 0;
        foreach (var refNode in allReferences)
        {
            if (IsEnumerated(refNode))
            {
                enumerationCount++;
            }
        }

        // 4. 2回以上列挙されている場合に警告
        if (enumerationCount >= 2)
        {
            context.ReportDiagnostic(Diagnostic.Create(Rule, declarator.GetLocation(), symbol.Name));
        }
    }

    private static readonly HashSet<string> EnumerationMethods = new(StringComparer.Ordinal)
    {
        "Any", "All", "Contains", "Count", "LongCount",
        "First", "FirstOrDefault", "Last", "LastOrDefault",
        "Single", "SingleOrDefault", "ElementAt", "ElementAtOrDefault",
        "ToList", "ToArray", "ToDictionary", "ToLookup",
        "Aggregate", "Sum", "Min", "Max", "Average", "Select", "Where", 
        "OrderBy", "OrderByDescending", "ThenBy", "ThenByDescending"
    };

    private static bool IsEnumerated(SyntaxNode node)
    {
        // 1. 基本チェック：foreach
        if (node.Parent is ForEachStatementSyntax forEach && forEach.Expression == node)
            return true;

        // 2. メソッドチェーンを一番上まで遡る
        var current = node.Parent;
        while (current is MemberAccessExpressionSyntax memberAccess)
        {
            var parent = memberAccess.Parent;
            if (parent is InvocationExpressionSyntax invocation)
            {
                var methodName = memberAccess.Name.Identifier.ValueText;

                // 終端（列挙メソッド）なら true
                if (EnumerationMethods.Contains(methodName)) return true;

                // 変換メソッドなら、その結果(Invocation)を次の起点にしてさらに上に遡る
                if (TransformationMethods.Contains(methodName))
                {
                    current = invocation.Parent;
                    continue;
                }
            }
            break;
        }
        return false;
    }

    // 変換メソッド（それ自体は列挙しないが、列挙チェーンを継続させるもの）
    private static readonly HashSet<string> TransformationMethods = new(StringComparer.Ordinal)
    {
        "Select", "SelectMany", "Where", "OrderBy", "OrderByDescending",
        "ThenBy", "ThenByDescending", "GroupBy", "Take", "Skip",
        "Reverse", "OfType", "Cast"
    };

    private static bool IsTransformationMethod(string methodName)
    {
        return TransformationMethods.Contains(methodName);
    }
}
using System;
using System.Collections.Generic;
using System.Linq;
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

    protected override SyntaxKind[] TargetKinds => [SyntaxKind.VariableDeclarator];

    protected override void AnalyzeNode(SyntaxNodeAnalysisContext context)
    {
        var declarator = (VariableDeclaratorSyntax)context.Node;
        if (!TryGetValidInitialization(declarator, out var invocation)) return;

        var model = context.SemanticModel;
        var cancellationToken = context.CancellationToken;

        if (!IsTargetLinqMethod(invocation, model, cancellationToken)) return;

        var symbol = model.GetDeclaredSymbol(declarator, cancellationToken);
        if (symbol == null) return;

        var methodBody = declarator.Ancestors().OfType<MethodDeclarationSyntax>().FirstOrDefault()?.Body;
        if (methodBody == null) return;

        if (IsReassigned(declarator, methodBody)) return;

        int enumerationCount = CountEnumerations(symbol, methodBody, model);

        if (enumerationCount >= 2)
        {
            context.ReportDiagnostic(Diagnostic.Create(Rule, declarator.GetLocation(), symbol.Name));
        }
    }

    /// <summary>
    /// 変数宣言の初期化式が有効なLINQメソッド呼び出しであるかを検証します
    /// </summary>
    private static bool TryGetValidInitialization(VariableDeclaratorSyntax declarator, out InvocationExpressionSyntax invocation)
    {
        invocation = declarator.Initializer?.Value as InvocationExpressionSyntax;
        return invocation != null;
    }

    /// <summary>
    /// 対象のメソッド呼び出しがLINQの遅延評価メソッドであり、すでに実体化されていないかを判定します
    /// </summary>
    private static bool IsTargetLinqMethod(InvocationExpressionSyntax invocation, SemanticModel model, System.Threading.CancellationToken cancellationToken)
    {
        var methodSymbol = model.GetSymbolInfo(invocation, cancellationToken).Symbol as IMethodSymbol;
        if (methodSymbol == null || methodSymbol.ContainingType.ToDisplayString() != "System.Linq.Enumerable")
        {
            return false;
        }

        var name = methodSymbol.Name;
        // すでに実体化されている場合はLINQ003の対象外
        if (name == "ToList" || name == "ToArray")
        {
            return false;
        }

        return true;
    }

    /// <summary>
    /// メソッド内で変数が再代入されているかを判定します
    /// </summary>
    private static bool IsReassigned(VariableDeclaratorSyntax declaration, BlockSyntax methodBody)
    {
        var variableName = declaration.Identifier.ValueText;

        return methodBody.DescendantNodes()
            .OfType<AssignmentExpressionSyntax>()
            .Any(assignment => assignment.Left is IdentifierNameSyntax id && id.Identifier.ValueText == variableName);
    }

    /// <summary>
    /// メソッド内で変数が何回列挙されているかをカウントします
    /// </summary>
    private static int CountEnumerations(ISymbol symbol, BlockSyntax methodBody, SemanticModel model)
    {
        var allReferences = methodBody.DescendantNodes().OfType<IdentifierNameSyntax>()
            .Where(id => SymbolEqualityComparer.Default.Equals(model.GetSymbolInfo(id).Symbol, symbol));

        int count = 0;
        foreach (var refNode in allReferences)
        {
            if (IsEnumerated(refNode))
            {
                count++;
            }
        }
        return count;
    }

    private static readonly HashSet<string> EnumerationMethods = new(StringComparer.Ordinal)
    {
        "Any", "All", "Contains", "Count", "LongCount",
        "First", "FirstOrDefault", "Last", "LastOrDefault",
        "Single", "SingleOrDefault", "ElementAt", "ElementAtOrDefault",
        "ToList", "ToArray", "ToDictionary", "ToLookup",
        "Aggregate", "Sum", "Min", "Max", "Average"
    };

    /// <summary>
    /// 指定された参照ノードが列挙操作の対象となっているかを判定します
    /// </summary>
    private static bool IsEnumerated(SyntaxNode node)
    {
        if (node.Parent is ForEachStatementSyntax forEach && forEach.Expression == node)
        {
            return true;
        }

        var current = node.Parent;
        while (current is MemberAccessExpressionSyntax memberAccess)
        {
            var parent = memberAccess.Parent;
            if (parent is InvocationExpressionSyntax invocation)
            {
                var methodName = memberAccess.Name.Identifier.ValueText;

                if (EnumerationMethods.Contains(methodName))
                {
                    return true;
                }

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

    private static readonly HashSet<string> TransformationMethods = new(StringComparer.Ordinal)
    {
        "Select", "SelectMany", "Where", "OrderBy", "OrderByDescending",
        "ThenBy", "ThenByDescending", "GroupBy", "Take", "Skip",
        "Reverse", "OfType", "Cast"
    };
}
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using CSharp_IngeniousAnalyzer.Style__Common;

namespace CSharp_IngeniousAnalyzer.Style_Complexity;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public class MethodComplexity : CommonAnalyzer
{
    private const int DefaultThreshold = 17;

    public const string DiagnosticId = "CPX001";
    private const string Category = "Complexity";
    private static readonly LocalizableString Title = CreateLocalStr(nameof(ResourceEnum.CPX001_Title));
    private static readonly LocalizableString MessageFormat = CreateLocalStr(nameof(ResourceEnum.CPX001_Message));

    protected override DiagnosticDescriptor Rule { get; } = new(
        DiagnosticId, Title, MessageFormat, Category, DiagnosticSeverity.Warning, isEnabledByDefault: true);

    protected override SyntaxKind[] TargetKinds => [SyntaxKind.MethodDeclaration];

    protected override void AnalyzeNode(SyntaxNodeAnalysisContext context)
    {
        if (!context.Node.IsKind(SyntaxKind.MethodDeclaration)) return;

        if (IsGeneratedFile(context)) return;
        var method = (MethodDeclarationSyntax)context.Node;
        if (method.Body == null) return;

        // メソッドの先頭に `// Ignore` コメントがある場合はスキップ
        if (HasIgnoreComment(method)) return;

        // セマンティックモデルを使って複雑度を算出
        var complexity = CalculateComplexity(method, context.SemanticModel);

        if (complexity > DefaultThreshold)
        {
            var diagnostic = Diagnostic.Create(Rule, method.Identifier.GetLocation(), method.Identifier.Text, complexity);
            context.ReportDiagnostic(diagnostic);
        }
    }

    /// <summary>
    /// メソッド宣言の直前、またはボディの先頭に // Ignore コメントが存在するかを判定します
    /// </summary>
    private static bool HasIgnoreComment(MethodDeclarationSyntax method)
    {
        // 1. メソッド宣言自体に紐付く先行トリビアをチェック
        foreach (var trivia in method.GetLeadingTrivia())
        {
            if (IsIgnoreCommentTrivia(trivia)) return true;
        }

        // 2. メソッドボディの最初のステートメントの先行トリビア、またはボディ直後のトリビアをチェック
        if (method.Body != null)
        {
            foreach (var trivia in method.Body.GetLeadingTrivia())
            {
                if (IsIgnoreCommentTrivia(trivia)) return true;
            }

            var firstStmt = method.Body.Statements.FirstOrDefault();
            if (firstStmt != null)
            {
                foreach (var trivia in firstStmt.GetLeadingTrivia())
                {
                    if (IsIgnoreCommentTrivia(trivia)) return true;
                }
            }
        }

        return false;
    }

    /// <summary>
    /// 指定されたトリビアが // Ignore であるかを判定します
    /// </summary>
    private static bool IsIgnoreCommentTrivia(SyntaxTrivia trivia)
    {
        if (trivia.IsKind(SyntaxKind.SingleLineCommentTrivia) || trivia.IsKind(SyntaxKind.MultiLineCommentTrivia))
        {
            var text = trivia.ToString().Trim();
            return text.Equals("// Ignore CPX001", System.StringComparison.OrdinalIgnoreCase);
        }
        return false;
    }

    private static int CalculateComplexity(MethodDeclarationSyntax method, SemanticModel model)
    {
        int count = 1;

        // DescendantNodes() の重複走査（Linqによる分割）を避け、
        // 1回のループで Invocation と複雑な構文ノードを同時に処理してアロケーションと走査コストを激減させる
        foreach (var node in method.DescendantNodes())
        {
            // 1. LINQ メソッド呼び出しの検出
            if (node is InvocationExpressionSyntax inv)
            {
                var symbol = model.GetSymbolInfo(inv).Symbol as IMethodSymbol;
                if (symbol?.ContainingNamespace?.ToDisplayString() == "System.Linq")
                {
                    count++;
                }
            }
            // 2. 複雑度を加算する構文ノードの検出
            else if (IsComplexNode(node))
            {
                int depth = 0;
                var parent = node.Parent;
                while (parent != null && parent != method)
                {
                    if (IsComplexNode(parent))
                    {
                        depth++;
                    }
                    parent = parent.Parent;
                }

                count += (1 + depth);
            }
        }

        return count;
    }

    /// <summary>
    /// 複雑度計算の対象となる構文ノードかを判定するヘルパー
    /// </summary>
    private static bool IsComplexNode(SyntaxNode node)
    {
        return node is IfStatementSyntax 
            or ForStatementSyntax 
            or ForEachStatementSyntax 
            or WhileStatementSyntax 
            or DoStatementSyntax 
            or SwitchSectionSyntax 
            or CatchClauseSyntax 
            or ConditionalExpressionSyntax;
    }
}
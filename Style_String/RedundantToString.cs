using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using CSharp_IngeniousAnalyzer.Style__Common;

namespace CSharp_IngeniousAnalyzer.Style_String;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public class RedundantToString : CommonAnalyzer
{
    public const string DiagnosticId = "STR003";
    private const string Category = "Style";
    private static readonly LocalizableString Title = CreateLocalStr(nameof(ResourceEnum.STR003_Title));
    private static readonly LocalizableString MessageFormat = CreateLocalStr(nameof(ResourceEnum.STR003_Message));

    protected override DiagnosticDescriptor Rule { get; } = new(
        DiagnosticId, Title, MessageFormat, Category, DiagnosticSeverity.Warning, isEnabledByDefault: true);

    protected override SyntaxKind[] TargetKinds => [SyntaxKind.InvocationExpression];

    protected override void AnalyzeNode(SyntaxNodeAnalysisContext context)
    {
        if (IsGeneratedFile(context)) return;
        var invocation = (InvocationExpressionSyntax)context.Node;

        // 引数ありの呼び出し（string型にはそもそも存在しないが、念のため単純な呼び出しのみに限定）
        if (invocation.ArgumentList.Arguments.Count > 0) return;

        ExpressionSyntax receiver;
        bool isConditional;
        SyntaxNode reportNode;

        if (invocation.Expression is MemberAccessExpressionSyntax memberAccess && memberAccess.Name.Identifier.ValueText == "ToString")
        {
            // 通常の s.ToString()
            receiver = memberAccess.Expression;
            isConditional = false;
            reportNode = invocation;
        }
        else if (invocation.Expression is MemberBindingExpressionSyntax memberBinding &&
                 memberBinding.Name.Identifier.ValueText == "ToString" &&
                 invocation.Parent is ConditionalAccessExpressionSyntax conditionalAccess &&
                 conditionalAccess.WhenNotNull == invocation)
        {
            // null条件演算子の s?.ToString()
            receiver = conditionalAccess.Expression;
            isConditional = true;
            reportNode = conditionalAccess;
        }
        else
        {
            return;
        }

        // 呼び出し元が既に string 型であることをセマンティックモデルで確定
        var typeInfo = context.SemanticModel.GetTypeInfo(receiver, context.CancellationToken);
        if (typeInfo.Type?.SpecialType != SpecialType.System_String) return;

        // 通常呼び出しの場合のみ、コンパイラが NotNull と証明できるケースに限定する
        // （?. の場合は null なら結果も null に伝播するだけなので、この確認は不要）
        if (!isConditional && typeInfo.Nullability.FlowState != NullableFlowState.NotNull) return;

        var diagnostic = Diagnostic.Create(Rule, reportNode.GetLocation(), receiver.ToString());
        context.ReportDiagnostic(diagnostic);
    }
}

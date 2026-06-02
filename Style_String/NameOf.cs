using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using CSharp_IngeniousAnalyzer.Core;
using CSharp_IngeniousAnalyzer.Style__Common;

namespace CSharp_IngeniousAnalyzer.Style_String;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public class Nameof : CommonAnalyzer
{
    public const string DiagnosticId = "STR002";
    private const string Category = "Style";
    private static readonly LocalizableString Title = CreateLocalStr(nameof(ResourceEnum.STR002_Title));
    private static readonly LocalizableString MessageFormat = CreateLocalStr(nameof(ResourceEnum.STR002_Message));

    protected override DiagnosticDescriptor Rule { get; } = new(
        DiagnosticId, Title, MessageFormat, Category, DiagnosticSeverity.Warning, isEnabledByDefault: true);

    // 今回のターゲットは「文字列リテラル（StringLiteralExpression）」を片っ端からスキャン！
    protected override SyntaxKind[] TargetKinds => [SyntaxKind.StringLiteralExpression];

    protected override void AnalyzeNode(SyntaxNodeAnalysisContext context)
    {
        if (IsGeneratedFile(context)) return;
        var stringLiteral = (LiteralExpressionSyntax)context.Node;
        var stringValue = stringLiteral.Token.ValueText;

        // 空文字や、C#の識別子（変数名）として使えない文字列（空白や記号入り）は即スルー
        if (string.IsNullOrWhiteSpace(stringValue) || !SyntaxFacts.IsValidIdentifier(stringValue)) return;

        // 【スコープ解析】自分が属している直近のメソッド（またはローカル関数）を取得
        var methodDeclaration = stringLiteral.Ancestors().OfType<MethodDeclarationSyntax>().FirstOrDefault();
        if (methodDeclaration is null) return;

        // 1. メソッドの「引数名」に同じ名前があるかスキャン
        bool hasMatchingSymbol = methodDeclaration.ParameterList.Parameters
            .Any(p => p.Identifier.ValueText == stringValue);

        // 2. 引数に無ければ、メソッド内部の「ローカル変数名」もスキャン
        if (!hasMatchingSymbol)
        {
            hasMatchingSymbol = methodDeclaration.DescendantNodes()
                .OfType<VariableDeclaratorSyntax>()
                .Any(v => v.Identifier.ValueText == stringValue);
        }

        // 【製品としての安全弁（パターン④対策）】
        // スコープ内に一致する引数・変数が1つも無い場合は、ただの一般的な文字列なので完全スルー！
        if (!hasMatchingSymbol) return;

        // 一致するものが見つかった場合のみ、安全に波線を引く
        var diagnostic = Diagnostic.Create(Rule, stringLiteral.GetLocation(), stringValue);
        context.ReportDiagnostic(diagnostic);
    }
}
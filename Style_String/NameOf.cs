using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
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

    protected override SyntaxKind[] TargetKinds => [SyntaxKind.StringLiteralExpression];

    protected override void AnalyzeNode(SyntaxNodeAnalysisContext context)
    {
        if (IsGeneratedFile(context)) return;
        var stringLiteral = (LiteralExpressionSyntax)context.Node;
        var stringValue = stringLiteral.Token.ValueText;

        if (string.IsNullOrWhiteSpace(stringValue) || !SyntaxFacts.IsValidIdentifier(stringValue)) return;

        if (IsInsideOwnInitializer(stringLiteral, stringValue)) return;

        var methodDeclaration = stringLiteral.Ancestors().OfType<MethodDeclarationSyntax>().FirstOrDefault();
        if (methodDeclaration is null) return;

        // 1. メソッドの「引数名」に同じ名前があるかスキャン
        bool hasMatchingSymbol = methodDeclaration.ParameterList.Parameters
            .Any(p => p.Identifier.ValueText == stringValue);

        // 2. 引数に無ければ、メソッド内部の「ローカル変数名」もスキャン
        VariableDeclaratorSyntax? targetVariable = null;
        if (!hasMatchingSymbol)
        {
            targetVariable = methodDeclaration.DescendantNodes()
                .OfType<VariableDeclaratorSyntax>()
                .FirstOrDefault(v => v.Identifier.ValueText == stringValue);

            hasMatchingSymbol = targetVariable is not null;
        }

        if (!hasMatchingSymbol) return;

        // 追加ガード：もしローカル変数が対象の場合、その文字列リテラルが「変数の宣言位置よりも上（手前）」にあれば警告しない
        if (targetVariable is not null && IsBeforeDeclaration(stringLiteral, targetVariable))
        {
            return;
        }

        var diagnostic = Diagnostic.Create(Rule, stringLiteral.GetLocation(), stringValue);
        context.ReportDiagnostic(diagnostic);
    }

    /// <summary>
    /// 文字列リテラルが、該当する変数の宣言位置よりもソースコード上で手前（上側）にあるかを判定する
    /// </summary>
    private static bool IsBeforeDeclaration(LiteralExpressionSyntax stringLiteral, VariableDeclaratorSyntax declarator)
    {
        // 構文木のソーススパン（文字位置）を比較して、リテラルが変数の宣言より前にあるかチェック
        return stringLiteral.SpanStart < declarator.SpanStart;
    }

    /// <summary>
    /// 文字列リテラルが、自分自身と同名の変数を宣言している VariableDeclarator の初期化子（右辺）の中に存在するかを判定する
    /// </summary>
    private static bool IsInsideOwnInitializer(LiteralExpressionSyntax stringLiteral, string stringValue)
    {
        // 直近の VariableDeclarator を取得
        var declarator = stringLiteral.Ancestors().OfType<VariableDeclaratorSyntax>().FirstOrDefault();
        if (declarator is null) return false;

        // 宣言されている変数名と、文字列リテラルの値が一致しているか
        if (declarator.Identifier.ValueText != stringValue) return false;

        // 初期化子（= の右側）が存在するか
        if (declarator.Initializer?.Value is { } initializerValue)
        {
            // 文字列リテラルが初期化子そのものである、または初期化子の子孫（内部）に含まれているか
            return initializerValue == stringLiteral || stringLiteral.Ancestors().Contains(initializerValue);
        }

        return false;
    }
}
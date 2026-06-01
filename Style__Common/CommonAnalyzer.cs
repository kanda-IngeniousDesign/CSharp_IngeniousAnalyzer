using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using System.Resources;
using System.Globalization;

namespace CSharp_IngeniousAnalyzer.Style__Common;

public abstract class CommonAnalyzer : DiagnosticAnalyzer
{
    protected abstract DiagnosticDescriptor Rule { get; }
    protected abstract SyntaxKind[] TargetKinds { get; }
    protected abstract void AnalyzeNode(SyntaxNodeAnalysisContext context);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => [Rule];

    // ✨ ゆうじさんが設計された、タイポを防ぐための強型の定義
    protected enum ResourceEnum
    {
        NULL001_Title,
        NULL001_Message,

        STR001_Title,
        STR001_Message,
        STR002_Title,
        STR002_Message,

        LINQ001_Title,
        LINQ001_Message,

        COLL001_Title,
        COLL001_Message,
    }

    // ✨ Enumの文字列（.ToString()）を受け取り、リソースから安全に引き出す仕組み
    protected static LocalizableResourceString CreateLocalStr(string resourceKey)
    {
        return new LocalizableResourceString(
            resourceKey, 
            Resources.ResourceManager, 
            typeof(Resources));
    }
    
    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();

        context.RegisterSyntaxNodeAction(AnalyzeNode, TargetKinds);
    }
}
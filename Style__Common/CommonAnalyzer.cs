using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;

namespace CSharp_IngeniousAnalyzer.Style__Common;

public abstract class CommonAnalyzer : DiagnosticAnalyzer
{
    protected abstract DiagnosticDescriptor Rule { get; }
    protected abstract SyntaxKind[] TargetKinds { get; }
    protected abstract void AnalyzeNode(SyntaxNodeAnalysisContext context);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => [Rule];

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
        LINQ002_Title,
        LINQ002_Message,
        LINQ003_Title,
        LINQ003_Message,

        COLL001_Title,
        COLL001_Message,

        COMM001_Title,
        COMM001_Message,
        COMM002_Title,
        COMM002_Message,
    }

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

    public static bool IsGeneratedFile(SyntaxNodeAnalysisContext context)
    {
        var filePath = context.Node.SyntaxTree.FilePath;
        return filePath.EndsWith(".Designer.cs", StringComparison.OrdinalIgnoreCase) ||
            filePath.EndsWith(".g.cs", StringComparison.OrdinalIgnoreCase) || // WPF/XAML自動生成用
            filePath.EndsWith(".generated.cs", StringComparison.OrdinalIgnoreCase);
    }
}
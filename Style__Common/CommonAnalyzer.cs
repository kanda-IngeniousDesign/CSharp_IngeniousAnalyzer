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
        // Style_Null
        NULL001_Title,
        NULL001_Message,

        // Style_String
        STR001_Title,
        STR001_Message,
        STR002_Title,
        STR002_Message,
        STR003_Title,
        STR003_Message,

        // Style_Linq
        LINQ001_Title,
        LINQ001_Message,
        LINQ002_Title,
        LINQ002_Message,

        // Style_Collection
        COLL001_Title,
        COLL001_Message,

        // Style_Comment
        COMM001_Title,
        COMM001_Message,
        COMM002_Title,
        COMM002_Message,

        // Style_Complexity
        CPX001_Title,
        CPX001_Message,
        CPX002_Title,
        CPX002_Message,

        // Style_Compare
        COMP001_Title,
        COMP001_Message,

        // Style_Exception
        EXC001_Title,
        EXC001_Message
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
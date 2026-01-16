using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace NetCorePal.Extensions.CodeAnalysis.Tools;

internal static class AppCsContentGenerator
{
    internal static string GenerateAppCsContent(List<string> projectPaths, string outputPath, string title, bool withHistory = true, string? snapshotDir = null)
    {
        var sb = new StringBuilder();

        // Add #:project directives for each project
        foreach (var projectPath in projectPaths)
        {
            sb.AppendLine($"#:project {projectPath}");
        }

        sb.AppendLine();
        sb.AppendLine("using NetCorePal.Extensions.CodeAnalysis;");
        sb.AppendLine("using NetCorePal.Extensions.CodeAnalysis.Snapshots;");
        sb.AppendLine("using System;");
        sb.AppendLine("using System.Collections.Generic;");
        sb.AppendLine("using System.IO;");
        sb.AppendLine("using System.Linq;");
        sb.AppendLine("using System.Reflection;");
        sb.AppendLine();
        sb.AppendLine();
        sb.AppendLine("var baseDir = AppDomain.CurrentDomain.BaseDirectory;");

        // Generate assembly names from project paths
        var assemblyNames = projectPaths
            .Select(p => Path.GetFileNameWithoutExtension(p) + ".dll")
            .Distinct()
            .ToList();

        sb.AppendLine("var assemblyNames = new[]");
        sb.AppendLine("{");
        foreach (var assemblyName in assemblyNames)
        {
            sb.AppendLine($"    \"{assemblyName}\",");
        }
        sb.AppendLine("};");
        sb.AppendLine();

        sb.AppendLine("var assemblies = assemblyNames");
        sb.AppendLine("    .Select(name => Path.Combine(baseDir, name))");
        sb.AppendLine("    .Where(File.Exists)");
        sb.AppendLine("    .Select(Assembly.LoadFrom)");
        sb.AppendLine("    .Distinct()");
        sb.AppendLine("    .ToArray();");
        sb.AppendLine();

        sb.AppendLine("var result = CodeFlowAnalysisHelper.GetResultFromAssemblies(assemblies);");
        sb.AppendLine();

        // Use verbatim strings with proper escaping for title and outputPath
        var escapedTitle = title.Replace("\"", "\"\"");
        var normalizedOutputPath = Path.GetFullPath(outputPath);
        var escapedOutputPath = normalizedOutputPath.Replace("\"", "\"\"");

        // Handle snapshots if withHistory is true
        if (withHistory && !string.IsNullOrEmpty(snapshotDir))
        {
            var escapedSnapshotDir = snapshotDir.Replace("\"", "\"\"");
            sb.AppendLine("// Load existing snapshots if available");
            sb.AppendLine($"var snapshotDir = @\"{escapedSnapshotDir}\";");
            sb.AppendLine("List<CodeFlowAnalysisSnapshot> snapshots = null;");
            sb.AppendLine("// Note: Snapshot loading would require additional logic here");
            sb.AppendLine("// For now, pass null and let GenerateVisualizationHtml create a current snapshot");
            sb.AppendLine($"var html = VisualizationHtmlBuilder.GenerateVisualizationHtml(result, @\"{escapedTitle}\", withHistory: true, snapshots: null);");
        }
        else
        {
            sb.AppendLine($"var html = VisualizationHtmlBuilder.GenerateVisualizationHtml(result, @\"{escapedTitle}\", withHistory: false);");
        }
        
        sb.AppendLine($"File.WriteAllText(@\"{escapedOutputPath}\", html);");

        return sb.ToString();
    }
}

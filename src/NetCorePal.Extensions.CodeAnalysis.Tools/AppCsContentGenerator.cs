using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;

namespace NetCorePal.Extensions.CodeAnalysis.Tools;

internal static class AppCsContentGenerator
{
    internal static string GenerateAppCsContent(List<string> projectPaths, string outputPath, string title, bool withHistory = true)
    {
        var sb = new StringBuilder();

        // Add #:project directives for each project
        foreach (var projectPath in projectPaths)
        {
            sb.AppendLine($"#:project {projectPath}");
        }
        var nugetversion = ProjectAnalysisHelpers.GetVersionWithoutGithash();
        if(!string.IsNullOrEmpty(nugetversion))
        {
            sb.AppendLine($"#:package NetCorePal.Extensions.CodeAnalysis@{nugetversion}");
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

        sb.AppendLine("// Get MetadataAttributes from assemblies for snapshot creation");
        sb.AppendLine("var metadataAttributesList = CodeFlowAnalysisHelper.GetAllMetadataAttributes(assemblies);");
        sb.AppendLine("var metadataAttributes = metadataAttributesList.ToArray();");
        sb.AppendLine("var result = CodeFlowAnalysisHelper.GetResultFromAttributes(metadataAttributes);");
        sb.AppendLine();

        // Use verbatim strings with proper escaping for title and outputPath
        var escapedTitle = title.Replace("\"", "\"\"");
        var normalizedOutputPath = Path.GetFullPath(outputPath);
        var escapedOutputPath = normalizedOutputPath.Replace("\"", "\"\"");

        // Handle snapshots if withHistory is true
        if (withHistory)
        {
            sb.AppendLine("// Create current snapshot from MetadataAttributes");
            sb.AppendLine($"var currentSnapshot = CodeFlowAnalysisSnapshotHelper.CreateSnapshot(metadataAttributes, \"当前版本\");");
            sb.AppendLine();
            sb.AppendLine("// Find all snapshot classes from assemblies via reflection");
            sb.AppendLine("var snapshots = new List<CodeFlowAnalysisSnapshot>();");
            sb.AppendLine("foreach (var assembly in assemblies)");
            sb.AppendLine("{");
            sb.AppendLine("    var snapshotTypes = assembly.GetTypes()");
            sb.AppendLine("        .Where(t => t.IsClass && !t.IsAbstract && t.IsSubclassOf(typeof(CodeFlowAnalysisSnapshot)))");
            sb.AppendLine("        .ToList();");
            sb.AppendLine("    ");
            sb.AppendLine("    foreach (var snapshotType in snapshotTypes)");
            sb.AppendLine("    {");
            sb.AppendLine("        try");
            sb.AppendLine("        {");
            sb.AppendLine("            var snapshot = (CodeFlowAnalysisSnapshot)Activator.CreateInstance(snapshotType);");
            sb.AppendLine("            if (snapshot != null)");
            sb.AppendLine("            {");
            sb.AppendLine("                snapshots.Add(snapshot);");
            sb.AppendLine("            }");
            sb.AppendLine("        }");
            sb.AppendLine("        catch");
            sb.AppendLine("        {");
            sb.AppendLine("            // Skip snapshots that fail to instantiate");
            sb.AppendLine("        }");
            sb.AppendLine("    }");
            sb.AppendLine("}");
            sb.AppendLine();
            sb.AppendLine("// Sort snapshots by version (newest first)");
            sb.AppendLine("snapshots = snapshots.OrderByDescending(s => s.Metadata.Version).ToList();");
            sb.AppendLine();
            sb.AppendLine("// Check if current snapshot's hash already exists in the snapshot list");
            sb.AppendLine("var currentHash = currentSnapshot.Metadata.Hash;");
            sb.AppendLine("var hashExists = snapshots.Any(s => s.Metadata.Hash == currentHash);");
            sb.AppendLine();
            sb.AppendLine("// Add current snapshot to the list if hash doesn't exist");
            sb.AppendLine("if (!hashExists)");
            sb.AppendLine("{");
            sb.AppendLine("    snapshots.Insert(0, currentSnapshot);");
            sb.AppendLine("}");
            sb.AppendLine();
            sb.AppendLine($"var html = VisualizationHtmlBuilder.GenerateVisualizationHtml(result, @\"{escapedTitle}\", withHistory: true, snapshots: snapshots);");
        }
        else
        {
            sb.AppendLine($"var html = VisualizationHtmlBuilder.GenerateVisualizationHtml(result, @\"{escapedTitle}\", withHistory: false);");
        }
        
        sb.AppendLine($"File.WriteAllText(@\"{escapedOutputPath}\", html);");

        return sb.ToString();
    }
}

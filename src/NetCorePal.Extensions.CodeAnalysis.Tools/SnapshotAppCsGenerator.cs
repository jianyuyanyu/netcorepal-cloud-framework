using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace NetCorePal.Extensions.CodeAnalysis.Tools;

/// <summary>
/// 生成用于快照分析的app.cs文件
/// </summary>
public static class SnapshotAppCsGenerator
{
    /// <summary>
    /// 生成用于分析项目并输出JSON结果的app.cs内容
    /// </summary>
    public static string GenerateSnapshotAppCsContent(List<string> projectPaths, string outputPath)
    {
        var sb = new StringBuilder();

        // Add #:project directives
        foreach (var projectPath in projectPaths)
        {
            sb.AppendLine($"#:project {projectPath}");
        }

        sb.AppendLine();
        sb.AppendLine("using NetCorePal.Extensions.CodeAnalysis;");
        sb.AppendLine("using System.IO;");
        sb.AppendLine("using System.Linq;");
        sb.AppendLine("using System.Reflection;");
        sb.AppendLine("using System.Text.Json;");
        sb.AppendLine("using System.Text.Json.Serialization;");
        sb.AppendLine();
        sb.AppendLine("var baseDir = AppDomain.CurrentDomain.BaseDirectory;");

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

        var escapedOutputPath = outputPath.Replace("\\", "\\\\").Replace("\"", "\\\"");
        sb.AppendLine("var options = new JsonSerializerOptions");
        sb.AppendLine("{");
        sb.AppendLine("    WriteIndented = true,");
        sb.AppendLine("    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,");
        sb.AppendLine("    Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }");
        sb.AppendLine("};");
        sb.AppendLine($"var json = JsonSerializer.Serialize(result, options);");
        sb.AppendLine($"File.WriteAllText(@\"{escapedOutputPath}\", json);");

        return sb.ToString();
    }
}

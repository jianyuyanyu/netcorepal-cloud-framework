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
    /// 生成用于创建快照C#文件的app.cs内容
    /// 该app.cs会分析项目并直接生成快照类文件到指定目录
    /// </summary>
    /// <param name="projectPaths">要分析的项目路径列表</param>
    /// <param name="snapshotDir">快照目录</param>
    /// <param name="description">快照描述</param>
    /// <param name="version">快照版本号</param>
    /// <param name="snapshotName">快照名称（可选，用于生成更具描述性的文件名）</param>
    /// <returns>app.cs文件内容</returns>
    public static string GenerateSnapshotAppCsContent(
        List<string> projectPaths, 
        string snapshotDir, 
        string description,
        string version,
        string? snapshotName = null)
    {
        var sb = new StringBuilder();

        // Add #:project directives
        foreach (var projectPath in projectPaths)
        {
            sb.AppendLine($"#:project {projectPath}");
        }

        sb.AppendLine();
        sb.AppendLine("using NetCorePal.Extensions.CodeAnalysis;");
        sb.AppendLine("using NetCorePal.Extensions.CodeAnalysis.Snapshots;");
        sb.AppendLine("using System;");
        sb.AppendLine("using System.IO;");
        sb.AppendLine("using System.Linq;");
        sb.AppendLine("using System.Reflection;");
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

        sb.AppendLine("// 分析项目");
        sb.AppendLine("var result = CodeFlowAnalysisHelper.GetResultFromAssemblies(assemblies);");
        sb.AppendLine();

        // 生成快照代码
        var escapedSnapshotDir = snapshotDir.Replace("\\", "\\\\").Replace("\"", "\\\"");
        var escapedDescription = description.Replace("\\", "\\\\").Replace("\"", "\\\"");
        var escapedSnapshotName = snapshotName != null ? snapshotName.Replace("\\", "\\\\").Replace("\"", "\\\"") : null;
        
        sb.AppendLine("// 创建快照实例");
        sb.AppendLine($"var snapshot = CodeFlowAnalysisSnapshotHelper.CreateSnapshot(result, \"{escapedDescription}\", \"{version}\");");
        sb.AppendLine();
        
        sb.AppendLine("// 生成快照C#代码");
        if (escapedSnapshotName != null)
        {
            sb.AppendLine($"var snapshotCode = CodeFlowAnalysisSnapshotHelper.GenerateSnapshotCode(snapshot, \"{escapedSnapshotName}\");");
        }
        else
        {
            sb.AppendLine("var snapshotCode = CodeFlowAnalysisSnapshotHelper.GenerateSnapshotCode(snapshot);");
        }
        sb.AppendLine();
        
        sb.AppendLine("// 保存快照文件");
        sb.AppendLine($"var snapshotDir = @\"{escapedSnapshotDir}\";");
        sb.AppendLine("if (!Directory.Exists(snapshotDir))");
        sb.AppendLine("{");
        sb.AppendLine("    Directory.CreateDirectory(snapshotDir);");
        sb.AppendLine("}");
        sb.AppendLine();
        if (escapedSnapshotName != null)
        {
            sb.AppendLine($"var fileName = CodeFlowAnalysisSnapshotHelper.GenerateFileName(\"{version}\", \"{escapedSnapshotName}\");");
        }
        else
        {
            sb.AppendLine($"var fileName = CodeFlowAnalysisSnapshotHelper.GenerateFileName(\"{version}\");");
        }
        sb.AppendLine("var filePath = Path.Combine(snapshotDir, fileName);");
        sb.AppendLine("File.WriteAllText(filePath, snapshotCode);");
        sb.AppendLine();
        sb.AppendLine("Console.WriteLine($\"快照文件已生成: {{filePath}}\");");
        sb.AppendLine("Console.WriteLine($\"  版本: {snapshot.Metadata.Version}\");");
        sb.AppendLine("Console.WriteLine($\"  节点数: {snapshot.Metadata.NodeCount}\");");
        sb.AppendLine("Console.WriteLine($\"  关系数: {snapshot.Metadata.RelationshipCount}\");");

        return sb.ToString();
    }
}

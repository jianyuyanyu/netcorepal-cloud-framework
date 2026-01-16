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
    /// <returns>app.cs文件内容</returns>
    public static string GenerateSnapshotAppCsContent(
        List<string> projectPaths, 
        string snapshotDir, 
        string description,
        string version)
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
        
        sb.AppendLine("// 创建快照元数据");
        sb.AppendLine("var metadata = new SnapshotMetadata");
        sb.AppendLine("{");
        sb.AppendLine($"    Version = \"{version}\",");
        sb.AppendLine("    Timestamp = DateTime.Now,");
        sb.AppendLine($"    Description = \"{escapedDescription}\",");
        sb.AppendLine("    Hash = ComputeHash(result),");
        sb.AppendLine("    NodeCount = result.Nodes.Count,");
        sb.AppendLine("    RelationshipCount = result.Relationships.Count");
        sb.AppendLine("};");
        sb.AppendLine();
        
        sb.AppendLine("// 生成快照C#代码");
        sb.AppendLine("var snapshotCode = SnapshotCodeGenerator.GenerateSnapshotClass(result, metadata);");
        sb.AppendLine();
        
        sb.AppendLine("// 保存快照文件");
        sb.AppendLine($"var snapshotDir = @\"{escapedSnapshotDir}\";");
        sb.AppendLine("if (!Directory.Exists(snapshotDir))");
        sb.AppendLine("{");
        sb.AppendLine("    Directory.CreateDirectory(snapshotDir);");
        sb.AppendLine("}");
        sb.AppendLine();
        sb.AppendLine($"var fileName = $\"Snapshot_{{{version}}}.cs\";");
        sb.AppendLine("var filePath = Path.Combine(snapshotDir, fileName);");
        sb.AppendLine("File.WriteAllText(filePath, snapshotCode);");
        sb.AppendLine();
        sb.AppendLine("Console.WriteLine($\"快照文件已生成: {{filePath}}\");");
        sb.AppendLine("Console.WriteLine($\"  版本: {metadata.Version}\");");
        sb.AppendLine("Console.WriteLine($\"  节点数: {metadata.NodeCount}\");");
        sb.AppendLine("Console.WriteLine($\"  关系数: {metadata.RelationshipCount}\");");
        sb.AppendLine();
        
        // 添加计算哈希的辅助函数
        sb.AppendLine("// 计算哈希值的辅助函数");
        sb.AppendLine("static string ComputeHash(CodeFlowAnalysisResult analysisResult)");
        sb.AppendLine("{");
        sb.AppendLine("    var sb = new System.Text.StringBuilder();");
        sb.AppendLine("    foreach (var node in analysisResult.Nodes.OrderBy(n => n.Id))");
        sb.AppendLine("    {");
        sb.AppendLine("        sb.Append($\"{node.Id}|{node.Name}|{node.Type}|\");");
        sb.AppendLine("    }");
        sb.AppendLine("    foreach (var rel in analysisResult.Relationships.OrderBy(r => r.FromNode.Id).ThenBy(r => r.ToNode.Id))");
        sb.AppendLine("    {");
        sb.AppendLine("        sb.Append($\"{rel.FromNode.Id}->{rel.ToNode.Id}|{rel.Type}|\");");
        sb.AppendLine("    }");
        sb.AppendLine("    using var sha256 = System.Security.Cryptography.SHA256.Create();");
        sb.AppendLine("    var hashBytes = sha256.ComputeHash(System.Text.Encoding.UTF8.GetBytes(sb.ToString()));");
        sb.AppendLine("    return Convert.ToHexString(hashBytes).ToLowerInvariant();");
        sb.AppendLine("}");

        return sb.ToString();
    }
}

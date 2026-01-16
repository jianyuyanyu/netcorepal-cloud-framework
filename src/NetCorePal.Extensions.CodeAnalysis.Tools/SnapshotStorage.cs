using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using NetCorePal.Extensions.CodeAnalysis;
using NetCorePal.Extensions.CodeAnalysis.Snapshots;

namespace NetCorePal.Extensions.CodeAnalysis.Tools;

/// <summary>
/// 快照存储服务，负责快照的读写操作（通过app.cs生成.cs文件，避免额外依赖）
/// </summary>
public class SnapshotStorage
{
    private const string DefaultSnapshotDirectory = "Snapshots";
    private readonly string _snapshotDirectory;

    public SnapshotStorage(string? snapshotDirectory = null)
    {
        _snapshotDirectory = snapshotDirectory ?? DefaultSnapshotDirectory;
    }

    /// <summary>
    /// 保存快照（通过app.cs生成.cs文件）
    /// </summary>
    public string SaveSnapshot(CodeFlowAnalysisResult analysisResult, string description, bool verbose = false)
    {
        // 创建快照目录
        if (!Directory.Exists(_snapshotDirectory))
        {
            Directory.CreateDirectory(_snapshotDirectory);
            if (verbose)
                Console.WriteLine($"Created snapshot directory: {_snapshotDirectory}");
        }

        // 生成版本号（基于时间戳）
        var version = DateTime.Now.ToString("yyyyMMddHHmmss");
        
        // 计算哈希值
        var hash = ComputeHash(analysisResult);

        // 创建快照元数据
        var metadata = new SnapshotMetadata
        {
            Version = version,
            Timestamp = DateTime.Now,
            Description = description,
            Hash = hash,
            NodeCount = analysisResult.Nodes.Count,
            RelationshipCount = analysisResult.Relationships.Count
        };

        // 生成C#代码
        var csharpCode = SnapshotCodeGenerator.GenerateSnapshotClass(analysisResult, metadata);
        
        // 保存到.cs文件
        var fileName = $"Snapshot_{version}.cs";
        var filePath = Path.Combine(_snapshotDirectory, fileName);
        File.WriteAllText(filePath, csharpCode);

        if (verbose)
        {
            Console.WriteLine($"Snapshot saved: {filePath}");
            Console.WriteLine($"  Version: {version}");
            Console.WriteLine($"  Nodes: {metadata.NodeCount}");
            Console.WriteLine($"  Relationships: {metadata.RelationshipCount}");
        }

        return version;
    }

    /// <summary>
    /// 加载指定版本的快照（从.cs文件读取元数据）
    /// </summary>
    public CodeFlowAnalysisSnapshot? LoadSnapshot(string version)
    {
        var fileName = $"Snapshot_{version}.cs";
        var filePath = Path.Combine(_snapshotDirectory, fileName);

        if (!File.Exists(filePath))
        {
            return null;
        }

        try
        {
            // 使用app.cs方式加载快照
            return LoadSnapshotViaAppCs(filePath, version);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error loading snapshot {version}: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// 获取所有快照的元数据列表（通过解析.cs文件）
    /// </summary>
    public List<SnapshotMetadata> ListSnapshots()
    {
        if (!Directory.Exists(_snapshotDirectory))
        {
            return new List<SnapshotMetadata>();
        }

        var files = Directory.GetFiles(_snapshotDirectory, "Snapshot_*.cs")
            .OrderByDescending(f => f);

        var snapshots = new List<SnapshotMetadata>();
        foreach (var file in files)
        {
            try
            {
                // Extract version from filename
                var fileName = Path.GetFileNameWithoutExtension(file);
                var version = fileName.Replace("Snapshot_", "");
                
                // 从文件内容中解析元数据（避免加载整个快照）
                var metadata = ExtractMetadataFromFile(file, version);
                if (metadata != null)
                {
                    snapshots.Add(metadata);
                }
            }
            catch
            {
                // Skip files that cannot be loaded
            }
        }

        return snapshots;
    }

    /// <summary>
    /// 获取最新的快照
    /// </summary>
    public CodeFlowAnalysisSnapshot? GetLatestSnapshot()
    {
        var snapshots = ListSnapshots();
        if (snapshots.Count == 0)
        {
            return null;
        }

        return LoadSnapshot(snapshots[0].Version);
    }

    /// <summary>
    /// 加载所有快照（用于历史版本展示）
    /// </summary>
    public List<CodeFlowAnalysisSnapshot> LoadAllSnapshots()
    {
        var metadata = ListSnapshots();
        var snapshots = new List<CodeFlowAnalysisSnapshot>();
        
        foreach (var meta in metadata)
        {
            var snapshot = LoadSnapshot(meta.Version);
            if (snapshot != null)
            {
                snapshots.Add(snapshot);
            }
        }
        
        return snapshots;
    }

    /// <summary>
    /// 通过app.cs方式加载快照
    /// </summary>
    private CodeFlowAnalysisSnapshot? LoadSnapshotViaAppCs(string snapshotFilePath, string version)
    {
        // 创建临时工作目录
        var tempWorkDir = Path.Combine(Path.GetTempPath(), $"netcorepal-snapshot-load-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempWorkDir);

        try
        {
            // 生成app.cs文件
            var tempOutputPath = Path.Combine(tempWorkDir, "snapshot-output.json");
            var appCsContent = GenerateSnapshotLoaderAppCs(snapshotFilePath, version, tempOutputPath);
            var tempAppCsPath = Path.Combine(tempWorkDir, "app.cs");
            
            File.WriteAllText(tempAppCsPath, appCsContent);

            // 执行app.cs
            var processStartInfo = new System.Diagnostics.ProcessStartInfo
            {
                FileName = "dotnet",
                Arguments = $"run {tempAppCsPath} --no-launch-profile",
                WorkingDirectory = tempWorkDir,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            using var process = System.Diagnostics.Process.Start(processStartInfo);
            if (process == null)
            {
                return null;
            }

            process.WaitForExit(30000); // 30 second timeout

            if (process.ExitCode != 0)
            {
                var error = process.StandardError.ReadToEnd();
                Console.Error.WriteLine($"Failed to load snapshot: {error}");
                return null;
            }

            // 读取序列化的快照
            if (!File.Exists(tempOutputPath))
            {
                return null;
            }

            var json = File.ReadAllText(tempOutputPath);
            var snapshot = System.Text.Json.JsonSerializer.Deserialize<CodeFlowAnalysisSnapshot>(json,
                new System.Text.Json.JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true,
                    Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() }
                });

            return snapshot;
        }
        finally
        {
            try
            {
                if (Directory.Exists(tempWorkDir))
                {
                    Directory.Delete(tempWorkDir, recursive: true);
                }
            }
            catch
            {
                // Ignore cleanup errors
            }
        }
    }

    /// <summary>
    /// 从.cs文件中提取元数据（解析注释）
    /// </summary>
    private SnapshotMetadata? ExtractMetadataFromFile(string filePath, string version)
    {
        try
        {
            var lines = File.ReadAllLines(filePath);
            var metadata = new SnapshotMetadata { Version = version };

            foreach (var line in lines.Take(20)) // Only check first 20 lines
            {
                if (line.Contains("Snapshot created:"))
                {
                    var dateStr = line.Split(new[] { "Snapshot created:" }, StringSplitOptions.None)[1].Trim();
                    if (DateTime.TryParse(dateStr, out var timestamp))
                    {
                        metadata.Timestamp = timestamp;
                    }
                }
                else if (line.Contains("Description:"))
                {
                    metadata.Description = line.Split(new[] { "Description:" }, StringSplitOptions.None)[1].Trim();
                }
                else if (line.Contains("NodeCount ="))
                {
                    var countStr = line.Split('=')[1].Trim().TrimEnd(',');
                    if (int.TryParse(countStr, out var count))
                    {
                        metadata.NodeCount = count;
                    }
                }
                else if (line.Contains("RelationshipCount ="))
                {
                    var countStr = line.Split('=')[1].Trim();
                    if (int.TryParse(countStr, out var count))
                    {
                        metadata.RelationshipCount = count;
                    }
                }
                else if (line.Contains("Hash ="))
                {
                    var hashStr = line.Split('=')[1].Trim().Trim('"').TrimEnd(',');
                    metadata.Hash = hashStr;
                }
            }

            return metadata;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// 生成用于加载快照的app.cs文件
    /// </summary>
    private string GenerateSnapshotLoaderAppCs(string snapshotFilePath, string version, string outputPath)
    {
        var sb = new StringBuilder();
        
        // 复制快照文件内容
        sb.AppendLine(File.ReadAllText(snapshotFilePath));
        sb.AppendLine();
        
        // 添加加载和序列化逻辑
        sb.AppendLine("using System.IO;");
        sb.AppendLine("using System.Text.Json;");
        sb.AppendLine("using System.Text.Json.Serialization;");
        sb.AppendLine();
        sb.AppendLine($"var snapshot = CodeAnalysisSnapshots.Snapshot_{version}.BuildSnapshot();");
        sb.AppendLine();
        sb.AppendLine("var options = new JsonSerializerOptions");
        sb.AppendLine("{");
        sb.AppendLine("    WriteIndented = true,");
        sb.AppendLine("    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,");
        sb.AppendLine("    Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }");
        sb.AppendLine("};");
        sb.AppendLine();
        var escapedPath = outputPath.Replace("\\", "\\\\").Replace("\"", "\\\"");
        sb.AppendLine($"var json = JsonSerializer.Serialize(snapshot, options);");
        sb.AppendLine($"File.WriteAllText(@\"{escapedPath}\", json);");
        sb.AppendLine($"Console.WriteLine($\"Snapshot {version} loaded and serialized\");");
        
        return sb.ToString();
    }

    /// <summary>
    /// 计算分析结果的哈希值
    /// </summary>
    private string ComputeHash(CodeFlowAnalysisResult analysisResult)
    {
        var sb = new StringBuilder();
        
        // 对节点排序后计算哈希
        foreach (var node in analysisResult.Nodes.OrderBy(n => n.Id))
        {
            sb.Append($"{node.Id}|{node.Name}|{node.Type}|");
        }
        
        // 对关系排序后计算哈希
        foreach (var rel in analysisResult.Relationships.OrderBy(r => r.FromNode.Id).ThenBy(r => r.ToNode.Id))
        {
            sb.Append($"{rel.FromNode.Id}->{rel.ToNode.Id}|{rel.Type}|");
        }

        using var sha256 = SHA256.Create();
        var hashBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(sb.ToString()));
        return Convert.ToHexString(hashBytes).ToLowerInvariant();
    }
}

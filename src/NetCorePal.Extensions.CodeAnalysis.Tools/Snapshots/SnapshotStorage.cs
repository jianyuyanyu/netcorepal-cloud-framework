using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using NetCorePal.Extensions.CodeAnalysis;

namespace NetCorePal.Extensions.CodeAnalysis.Tools.Snapshots;

/// <summary>
/// 快照存储服务，负责快照的读写操作
/// </summary>
public class SnapshotStorage
{
    private const string DefaultSnapshotDirectory = "snapshots";
    private readonly string _snapshotDirectory;
    
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters =
        {
            new JsonStringEnumConverter(JsonNamingPolicy.CamelCase)
        }
    };

    public SnapshotStorage(string? snapshotDirectory = null)
    {
        _snapshotDirectory = snapshotDirectory ?? DefaultSnapshotDirectory;
    }

    /// <summary>
    /// 保存快照
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

        // 创建快照
        var snapshot = new CodeFlowAnalysisSnapshot
        {
            Metadata = new SnapshotMetadata
            {
                Version = version,
                Timestamp = DateTime.Now,
                Description = description,
                Hash = hash,
                NodeCount = analysisResult.Nodes.Count,
                RelationshipCount = analysisResult.Relationships.Count
            },
            AnalysisResult = analysisResult
        };

        // 保存到文件
        var fileName = $"{version}.json";
        var filePath = Path.Combine(_snapshotDirectory, fileName);
        var json = JsonSerializer.Serialize(snapshot, JsonOptions);
        File.WriteAllText(filePath, json);

        if (verbose)
        {
            Console.WriteLine($"Snapshot saved: {filePath}");
            Console.WriteLine($"  Version: {version}");
            Console.WriteLine($"  Nodes: {snapshot.Metadata.NodeCount}");
            Console.WriteLine($"  Relationships: {snapshot.Metadata.RelationshipCount}");
        }

        return version;
    }

    /// <summary>
    /// 加载指定版本的快照
    /// </summary>
    public CodeFlowAnalysisSnapshot? LoadSnapshot(string version)
    {
        var fileName = $"{version}.json";
        var filePath = Path.Combine(_snapshotDirectory, fileName);

        if (!File.Exists(filePath))
        {
            return null;
        }

        var json = File.ReadAllText(filePath);
        return JsonSerializer.Deserialize<CodeFlowAnalysisSnapshot>(json, JsonOptions);
    }

    /// <summary>
    /// 获取所有快照的元数据列表
    /// </summary>
    public List<SnapshotMetadata> ListSnapshots()
    {
        if (!Directory.Exists(_snapshotDirectory))
        {
            return new List<SnapshotMetadata>();
        }

        var files = Directory.GetFiles(_snapshotDirectory, "*.json")
            .OrderByDescending(f => f);

        var snapshots = new List<SnapshotMetadata>();
        foreach (var file in files)
        {
            try
            {
                var json = File.ReadAllText(file);
                var snapshot = JsonSerializer.Deserialize<CodeFlowAnalysisSnapshot>(json, JsonOptions);
                if (snapshot?.Metadata != null)
                {
                    snapshots.Add(snapshot.Metadata);
                }
            }
            catch (System.Text.Json.JsonException)
            {
                // Skip files that cannot be parsed as valid snapshots
            }
            catch (IOException)
            {
                // Skip files that cannot be read
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

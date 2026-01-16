using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using NetCorePal.Extensions.CodeAnalysis;
using NetCorePal.Extensions.CodeAnalysis.Snapshots;

namespace NetCorePal.Extensions.CodeAnalysis.Tools;

/// <summary>
/// 快照存储服务，负责快照的读写操作（生成.cs文件类似EF Core迁移）
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
    /// 保存快照（生成.cs文件）
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
    /// 加载指定版本的快照（从.cs文件编译加载）
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
            var code = File.ReadAllText(filePath);
            return CompileAndLoadSnapshot(code, version);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error loading snapshot {version}: {ex.Message}");
            return null;
        }
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
                
                // Load the snapshot to get metadata
                var snapshot = LoadSnapshot(version);
                if (snapshot?.Metadata != null)
                {
                    snapshots.Add(snapshot.Metadata);
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
    /// 使用Roslyn编译并加载快照
    /// </summary>
    private CodeFlowAnalysisSnapshot? CompileAndLoadSnapshot(string code, string version)
    {
        var syntaxTree = CSharpSyntaxTree.ParseText(code);
        
        var references = new List<MetadataReference>
        {
            MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(Console).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(List<>).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(CodeFlowAnalysisResult).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(CodeFlowAnalysisSnapshot).Assembly.Location),
            MetadataReference.CreateFromFile(Assembly.Load("System.Runtime").Location),
            MetadataReference.CreateFromFile(Assembly.Load("System.Collections").Location),
        };

        var compilation = CSharpCompilation.Create(
            $"SnapshotAssembly_{version}",
            new[] { syntaxTree },
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        using var ms = new MemoryStream();
        var result = compilation.Emit(ms);

        if (!result.Success)
        {
            var failures = result.Diagnostics.Where(diagnostic =>
                diagnostic.IsWarningAsError ||
                diagnostic.Severity == DiagnosticSeverity.Error);

            foreach (var diagnostic in failures)
            {
                Console.Error.WriteLine($"{diagnostic.Id}: {diagnostic.GetMessage()}");
            }
            return null;
        }

        ms.Seek(0, SeekOrigin.Begin);
        var assembly = Assembly.Load(ms.ToArray());
        
        var type = assembly.GetType($"CodeAnalysisSnapshots.Snapshot_{version}");
        if (type == null)
        {
            return null;
        }

        var method = type.GetMethod("BuildSnapshot", BindingFlags.Public | BindingFlags.Static);
        if (method == null)
        {
            return null;
        }

        return method.Invoke(null, null) as CodeFlowAnalysisSnapshot;
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

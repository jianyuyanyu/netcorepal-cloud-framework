using System;

namespace NetCorePal.Extensions.CodeAnalysis.Snapshots;

/// <summary>
/// 快照元数据，包含版本号、时间戳、描述等信息
/// </summary>
public class SnapshotMetadata
{
    /// <summary>
    /// 快照版本号（格式：yyyyMMddHHmmss，例如：20260116120000）
    /// </summary>
    public string Version { get; set; } = string.Empty;
    
    /// <summary>
    /// 快照创建时间
    /// </summary>
    public DateTime Timestamp { get; set; }
    
    /// <summary>
    /// 快照描述
    /// </summary>
    public string Description { get; set; } = string.Empty;
    
    /// <summary>
    /// 分析结果的哈希值，用于快速比较
    /// </summary>
    public string Hash { get; set; } = string.Empty;
    
    /// <summary>
    /// 节点总数
    /// </summary>
    public int NodeCount { get; set; }
    
    /// <summary>
    /// 关系总数
    /// </summary>
    public int RelationshipCount { get; set; }
}

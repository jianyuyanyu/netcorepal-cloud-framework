using System.Collections.Generic;
using NetCorePal.Extensions.CodeAnalysis.Attributes;

namespace NetCorePal.Extensions.CodeAnalysis.Snapshots;

/// <summary>
/// 代码流分析快照抽象基类，存储MetadataAttribute集合并提供生成分析结果的方法
/// </summary>
public abstract class CodeFlowAnalysisSnapshot
{
    /// <summary>
    /// 快照元数据
    /// </summary>
    public SnapshotMetadata Metadata { get; protected set; } = new();
    
    /// <summary>
    /// MetadataAttribute集合，包含所有元数据特性
    /// </summary>
    public List<MetadataAttribute> MetadataAttributes { get; protected set; } = new();
    
    /// <summary>
    /// 获取代码流分析结果（从MetadataAttribute集合生成）
    /// </summary>
    /// <returns>代码流分析结果</returns>
    public CodeFlowAnalysisResult GetCodeFlowAnalysisResult()
    {
        return CodeFlowAnalysisHelper.GetResultFromMetadataAttributes(MetadataAttributes);
    }
    
    /// <summary>
    /// 构造函数，用于初始化快照的元数据和MetadataAttribute集合
    /// </summary>
    protected CodeFlowAnalysisSnapshot()
    {
    }
}

using NetCorePal.Extensions.CodeAnalysis.Attributes;

namespace NetCorePal.Extensions.CodeAnalysis.Snapshots;

/// <summary>
/// 代码流分析快照抽象基类，包含完整的分析结果和元数据
/// </summary>
public abstract class CodeFlowAnalysisSnapshot
{
    /// <summary>
    /// 快照元数据
    /// </summary>
    public SnapshotMetadata Metadata { get; protected set; } = new();

    public MetadataAttribute[] MetadataAttributes { get; protected set; } = Array.Empty<MetadataAttribute>();
    
    /// <summary>
    /// 分析结果
    /// </summary>
    public CodeFlowAnalysisResult GetAnalysisResult()
    {
        return CodeFlowAnalysisHelper.GetResultFromAttributes(MetadataAttributes);
    }
}

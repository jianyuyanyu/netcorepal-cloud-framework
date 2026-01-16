namespace NetCorePal.Extensions.CodeAnalysis.Snapshots;

/// <summary>
/// 代码流分析快照，包含完整的分析结果和元数据
/// </summary>
public class CodeFlowAnalysisSnapshot
{
    /// <summary>
    /// 快照元数据
    /// </summary>
    public SnapshotMetadata Metadata { get; set; } = new();
    
    /// <summary>
    /// 分析结果
    /// </summary>
    public CodeFlowAnalysisResult AnalysisResult { get; set; } = new();
}

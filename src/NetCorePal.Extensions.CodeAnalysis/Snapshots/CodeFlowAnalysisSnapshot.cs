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
    
    /// <summary>
    /// 分析结果
    /// </summary>
    public CodeFlowAnalysisResult AnalysisResult { get; protected set; } = new();
    
    /// <summary>
    /// 构造函数，用于初始化快照的元数据和分析结果
    /// </summary>
    protected CodeFlowAnalysisSnapshot()
    {
    }
}

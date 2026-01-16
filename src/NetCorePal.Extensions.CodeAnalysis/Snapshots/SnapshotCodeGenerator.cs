using System;
using System.Linq;
using System.Text;

namespace NetCorePal.Extensions.CodeAnalysis.Snapshots;

/// <summary>
/// 生成C#快照代码文件，类似EF Core的迁移快照
/// </summary>
[Obsolete("Use CodeFlowAnalysisSnapshotHelper.GenerateSnapshotCode instead")]
public static class SnapshotCodeGenerator
{
    [Obsolete("Use CodeFlowAnalysisSnapshotHelper.GenerateSnapshotCode instead")]
    public static string GenerateSnapshotClass(CodeFlowAnalysisResult analysisResult, SnapshotMetadata metadata)
    {
        // Delegate to the new helper
        return CodeFlowAnalysisSnapshotHelper.GenerateSnapshotCode(analysisResult, metadata);
    }
}

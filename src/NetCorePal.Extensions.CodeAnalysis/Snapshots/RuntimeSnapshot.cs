namespace NetCorePal.Extensions.CodeAnalysis.Snapshots;

/// <summary>
/// Runtime snapshot implementation used by helper methods
/// </summary>
internal class RuntimeSnapshot : CodeFlowAnalysisSnapshot
{
    public RuntimeSnapshot(SnapshotMetadata metadata, Attributes.MetadataAttribute[] metadataAttributes)
    {
        Metadata = metadata;
        MetadataAttributes = metadataAttributes;
    }
}

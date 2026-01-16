using System.Collections.Generic;
using System.Linq;
using NetCorePal.Extensions.CodeAnalysis;

namespace NetCorePal.Extensions.CodeAnalysis.Tools.Snapshots;

/// <summary>
/// 快照差异类型
/// </summary>
public enum DiffType
{
    Added,
    Removed,
    Unchanged
}

/// <summary>
/// 节点差异
/// </summary>
public class NodeDiff
{
    public Node Node { get; set; } = new();
    public DiffType DiffType { get; set; }
}

/// <summary>
/// 关系差异
/// </summary>
public class RelationshipDiff
{
    public Relationship Relationship { get; set; } = new(new Node(), new Node(), RelationshipType.ControllerToCommand);
    public DiffType DiffType { get; set; }
}

/// <summary>
/// 快照比较结果
/// </summary>
public class SnapshotComparison
{
    public SnapshotMetadata? FromSnapshot { get; set; }
    public SnapshotMetadata? ToSnapshot { get; set; }
    
    public List<NodeDiff> NodeDiffs { get; set; } = new();
    public List<RelationshipDiff> RelationshipDiffs { get; set; } = new();
    
    public int AddedNodes => NodeDiffs.Count(d => d.DiffType == DiffType.Added);
    public int RemovedNodes => NodeDiffs.Count(d => d.DiffType == DiffType.Removed);
    public int UnchangedNodes => NodeDiffs.Count(d => d.DiffType == DiffType.Unchanged);
    
    public int AddedRelationships => RelationshipDiffs.Count(d => d.DiffType == DiffType.Added);
    public int RemovedRelationships => RelationshipDiffs.Count(d => d.DiffType == DiffType.Removed);
    public int UnchangedRelationships => RelationshipDiffs.Count(d => d.DiffType == DiffType.Unchanged);
}

/// <summary>
/// 快照比较服务
/// </summary>
public class SnapshotComparer
{
    /// <summary>
    /// 比较两个快照
    /// </summary>
    public SnapshotComparison Compare(CodeFlowAnalysisSnapshot fromSnapshot, CodeFlowAnalysisSnapshot toSnapshot)
    {
        var comparison = new SnapshotComparison
        {
            FromSnapshot = fromSnapshot.Metadata,
            ToSnapshot = toSnapshot.Metadata
        };

        // 比较节点
        CompareNodes(fromSnapshot.AnalysisResult, toSnapshot.AnalysisResult, comparison);
        
        // 比较关系
        CompareRelationships(fromSnapshot.AnalysisResult, toSnapshot.AnalysisResult, comparison);

        return comparison;
    }

    private void CompareNodes(CodeFlowAnalysisResult from, CodeFlowAnalysisResult to, SnapshotComparison comparison)
    {
        var fromNodes = from.Nodes.ToDictionary(n => n.Id);
        var toNodes = to.Nodes.ToDictionary(n => n.Id);

        // 查找新增的节点
        foreach (var node in toNodes.Values)
        {
            if (!fromNodes.ContainsKey(node.Id))
            {
                comparison.NodeDiffs.Add(new NodeDiff
                {
                    Node = node,
                    DiffType = DiffType.Added
                });
            }
            else
            {
                comparison.NodeDiffs.Add(new NodeDiff
                {
                    Node = node,
                    DiffType = DiffType.Unchanged
                });
            }
        }

        // 查找删除的节点
        foreach (var node in fromNodes.Values)
        {
            if (!toNodes.ContainsKey(node.Id))
            {
                comparison.NodeDiffs.Add(new NodeDiff
                {
                    Node = node,
                    DiffType = DiffType.Removed
                });
            }
        }
    }

    private void CompareRelationships(CodeFlowAnalysisResult from, CodeFlowAnalysisResult to, SnapshotComparison comparison)
    {
        var fromRels = from.Relationships
            .Select(r => $"{r.FromNode.Id}|{r.ToNode.Id}|{r.Type}")
            .ToHashSet();
        var toRels = to.Relationships
            .Select(r => $"{r.FromNode.Id}|{r.ToNode.Id}|{r.Type}")
            .ToHashSet();

        var fromRelDict = from.Relationships.ToDictionary(r => $"{r.FromNode.Id}|{r.ToNode.Id}|{r.Type}");
        var toRelDict = to.Relationships.ToDictionary(r => $"{r.FromNode.Id}|{r.ToNode.Id}|{r.Type}");

        // 查找新增的关系
        foreach (var relKey in toRels)
        {
            if (!fromRels.Contains(relKey))
            {
                comparison.RelationshipDiffs.Add(new RelationshipDiff
                {
                    Relationship = toRelDict[relKey],
                    DiffType = DiffType.Added
                });
            }
            else
            {
                comparison.RelationshipDiffs.Add(new RelationshipDiff
                {
                    Relationship = toRelDict[relKey],
                    DiffType = DiffType.Unchanged
                });
            }
        }

        // 查找删除的关系
        foreach (var relKey in fromRels)
        {
            if (!toRels.Contains(relKey))
            {
                comparison.RelationshipDiffs.Add(new RelationshipDiff
                {
                    Relationship = fromRelDict[relKey],
                    DiffType = DiffType.Removed
                });
            }
        }
    }
}

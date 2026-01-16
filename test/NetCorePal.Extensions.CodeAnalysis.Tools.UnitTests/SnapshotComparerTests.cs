using System.Collections.Generic;
using System.Linq;
using NetCorePal.Extensions.CodeAnalysis;
using NetCorePal.Extensions.CodeAnalysis.Snapshots;
using Xunit;

namespace NetCorePal.Extensions.CodeAnalysis.Tools.UnitTests;

public class SnapshotComparerTests
{
    private readonly SnapshotComparer _comparer;

    public SnapshotComparerTests()
    {
        _comparer = new SnapshotComparer();
    }

    [Fact]
    public void Compare_IdenticalSnapshots_ReturnsNoDifferences()
    {
        // Arrange
        var snapshot1 = CreateSnapshot("v1", CreateSampleNodes(), CreateSampleRelationships());
        var snapshot2 = CreateSnapshot("v2", CreateSampleNodes(), CreateSampleRelationships());

        // Act
        var comparison = _comparer.Compare(snapshot1, snapshot2);

        // Assert
        Assert.Equal(0, comparison.AddedNodes);
        Assert.Equal(0, comparison.RemovedNodes);
        Assert.Equal(2, comparison.UnchangedNodes);
        Assert.Equal(0, comparison.AddedRelationships);
        Assert.Equal(0, comparison.RemovedRelationships);
        Assert.Equal(1, comparison.UnchangedRelationships);
    }

    [Fact]
    public void Compare_WithAddedNodes_ReturnsCorrectDifferences()
    {
        // Arrange
        var nodes1 = CreateSampleNodes();
        var nodes2 = CreateSampleNodes();
        nodes2.Add(new Node
        {
            Id = "NewNode",
            Name = "NewNode",
            FullName = "Test.NewNode",
            Type = NodeType.Command
        });

        var snapshot1 = CreateSnapshot("v1", nodes1, CreateSampleRelationships());
        var snapshot2 = CreateSnapshot("v2", nodes2, CreateSampleRelationships());

        // Act
        var comparison = _comparer.Compare(snapshot1, snapshot2);

        // Assert
        Assert.Equal(1, comparison.AddedNodes);
        Assert.Equal(0, comparison.RemovedNodes);
        Assert.Equal(2, comparison.UnchangedNodes);
    }

    [Fact]
    public void Compare_WithRemovedNodes_ReturnsCorrectDifferences()
    {
        // Arrange
        var nodes1 = CreateSampleNodes();
        var nodes2 = new List<Node> { nodes1[0] }; // Remove one node

        var snapshot1 = CreateSnapshot("v1", nodes1, CreateSampleRelationships());
        var snapshot2 = CreateSnapshot("v2", nodes2, new List<Relationship>());

        // Act
        var comparison = _comparer.Compare(snapshot1, snapshot2);

        // Assert
        Assert.Equal(0, comparison.AddedNodes);
        Assert.Equal(1, comparison.RemovedNodes);
        Assert.Equal(1, comparison.UnchangedNodes);
    }

    [Fact]
    public void Compare_WithAddedRelationships_ReturnsCorrectDifferences()
    {
        // Arrange
        var nodes = CreateSampleNodes();
        var rels1 = CreateSampleRelationships();
        var rels2 = CreateSampleRelationships();
        
        var newNode = new Node
        {
            Id = "Node3",
            Name = "Node3",
            FullName = "Test.Node3",
            Type = NodeType.Command
        };
        nodes.Add(newNode);
        
        rels2.Add(new Relationship(nodes[0], newNode, RelationshipType.ControllerToCommand));

        var snapshot1 = CreateSnapshot("v1", CreateSampleNodes(), rels1);
        var snapshot2 = CreateSnapshot("v2", nodes, rels2);

        // Act
        var comparison = _comparer.Compare(snapshot1, snapshot2);

        // Assert
        Assert.Equal(1, comparison.AddedRelationships);
        Assert.Equal(0, comparison.RemovedRelationships);
        Assert.Equal(1, comparison.UnchangedRelationships);
    }

    [Fact]
    public void Compare_WithRemovedRelationships_ReturnsCorrectDifferences()
    {
        // Arrange
        var nodes = CreateSampleNodes();
        var rels1 = CreateSampleRelationships();
        var rels2 = new List<Relationship>(); // Remove all relationships

        var snapshot1 = CreateSnapshot("v1", nodes, rels1);
        var snapshot2 = CreateSnapshot("v2", nodes, rels2);

        // Act
        var comparison = _comparer.Compare(snapshot1, snapshot2);

        // Assert
        Assert.Equal(0, comparison.AddedRelationships);
        Assert.Equal(1, comparison.RemovedRelationships);
        Assert.Equal(0, comparison.UnchangedRelationships);
    }

    [Fact]
    public void Compare_ComplexChanges_ReturnsCorrectDifferences()
    {
        // Arrange
        var nodes1 = CreateSampleNodes();
        var nodes2 = new List<Node> { nodes1[0] }; // Remove one node
        nodes2.Add(new Node // Add a new node
        {
            Id = "NewNode",
            Name = "NewNode",
            FullName = "Test.NewNode",
            Type = NodeType.CommandHandler
        });

        var rels1 = CreateSampleRelationships();
        var rels2 = new List<Relationship>
        {
            new Relationship(nodes2[0], nodes2[1], RelationshipType.ControllerToCommand)
        };

        var snapshot1 = CreateSnapshot("v1", nodes1, rels1);
        var snapshot2 = CreateSnapshot("v2", nodes2, rels2);

        // Act
        var comparison = _comparer.Compare(snapshot1, snapshot2);

        // Assert
        Assert.Equal(1, comparison.AddedNodes);
        Assert.Equal(1, comparison.RemovedNodes);
        Assert.Equal(1, comparison.UnchangedNodes);
        Assert.Equal(1, comparison.AddedRelationships);
        Assert.Equal(1, comparison.RemovedRelationships);
    }

    private static List<Node> CreateSampleNodes()
    {
        return new List<Node>
        {
            new Node
            {
                Id = "Node1",
                Name = "Node1",
                FullName = "Test.Node1",
                Type = NodeType.Controller
            },
            new Node
            {
                Id = "Node2",
                Name = "Node2",
                FullName = "Test.Node2",
                Type = NodeType.Command
            }
        };
    }

    private static List<Relationship> CreateSampleRelationships()
    {
        var nodes = CreateSampleNodes();
        return new List<Relationship>
        {
            new Relationship(nodes[0], nodes[1], RelationshipType.ControllerToCommand)
        };
    }

    private static CodeFlowAnalysisSnapshot CreateSnapshot(string version, List<Node> nodes, List<Relationship> relationships)
    {
        return new CodeFlowAnalysisSnapshot
        {
            Metadata = new SnapshotMetadata
            {
                Version = version,
                Timestamp = System.DateTime.Now,
                Description = $"Test snapshot {version}",
                NodeCount = nodes.Count,
                RelationshipCount = relationships.Count
            },
            AnalysisResult = new CodeFlowAnalysisResult
            {
                Nodes = nodes,
                Relationships = relationships
            }
        };
    }
}

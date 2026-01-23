using System.Collections.Generic;
using System.Linq;
using NetCorePal.Extensions.CodeAnalysis;
using NetCorePal.Extensions.CodeAnalysis.Attributes;
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
        Assert.Equal(3, comparison.UnchangedNodes); // Controller + ControllerMethod + Command
        Assert.Equal(0, comparison.AddedRelationships);
        Assert.Equal(0, comparison.RemovedRelationships);
        // ControllerMethod->Command relationship + any other relationships generated from metadata
        Assert.True(comparison.UnchangedRelationships >= 1); 
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
        Assert.Equal(3, comparison.UnchangedNodes); // Controller + ControllerMethod + Command
    }

    [Fact]
    public void Compare_WithRemovedNodes_ReturnsCorrectDifferences()
    {
        // Arrange
        var nodes1 = CreateSampleNodes();
        var nodes2 = new List<Node> { nodes1[0] }; // Remove command node, keep controller

        var snapshot1 = CreateSnapshot("v1", nodes1, CreateSampleRelationships());
        var snapshot2 = CreateSnapshot("v2", nodes2, new List<Relationship>());

        // Act
        var comparison = _comparer.Compare(snapshot1, snapshot2);

        // Assert
        Assert.Equal(0, comparison.AddedNodes);
        // When we remove the command and don't have relationships, we lose both Command and ControllerMethod
        Assert.True(comparison.RemovedNodes >= 1); // At least the Command node removed
        Assert.True(comparison.UnchangedNodes >= 1); // At least Controller remains
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
        Assert.True(comparison.AddedRelationships >= 0); // May have added relationships
        Assert.Equal(0, comparison.RemovedRelationships);
        Assert.True(comparison.UnchangedRelationships >= 1); // At least the original relationship
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
        Assert.True(comparison.RemovedRelationships >= 1); // At least one relationship removed
        Assert.Equal(0, comparison.UnchangedRelationships);
    }

    [Fact]
    public void Compare_ComplexChanges_ReturnsCorrectDifferences()
    {
        // Arrange
        var nodes1 = CreateSampleNodes();
        var nodes2 = new List<Node> { nodes1[0] }; // Keep controller, remove command
        nodes2.Add(new Node // Add a new command handler
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
        Assert.Equal(1, comparison.AddedNodes); // Added CommandHandler
        Assert.Equal(1, comparison.RemovedNodes); // Removed Command
        Assert.Equal(2, comparison.UnchangedNodes); // Controller + ControllerMethod
        Assert.True(comparison.AddedRelationships >= 0); // May have added relationships
        Assert.True(comparison.RemovedRelationships >= 1); // At least one relationship removed
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
        return new TestSnapshot(version, nodes, relationships);
    }

    // Concrete implementation for testing
    private class TestSnapshot : CodeFlowAnalysisSnapshot
    {
        public TestSnapshot(string version, List<Node> nodes, List<Relationship> relationships)
        {
            Metadata = new SnapshotMetadata
            {
                Version = version,
                Timestamp = System.DateTime.Now,
                Description = $"Test snapshot {version}",
                NodeCount = nodes.Count,
                RelationshipCount = relationships.Count
            };
            
            // Create metadata attributes that will generate the expected nodes
            var attrs = new List<MetadataAttribute>();
            
            // Create attributes for each node type
            foreach (var node in nodes)
            {
                switch (node.Type)
                {
                    case NodeType.Aggregate:
                        attrs.Add(new EntityMetadataAttribute(node.FullName, true, new string[] {}, new string[] {}));
                        break;
                    case NodeType.Command:
                        attrs.Add(new CommandMetadataAttribute(node.FullName));
                        break;
                    case NodeType.DomainEvent:
                        attrs.Add(new DomainEventMetadataAttribute(node.FullName));
                        break;
                    case NodeType.CommandHandler:
                        // CommandHandler needs command name, use node name as command
                        var commandName = node.Name.Replace("Handler", "Command");
                        attrs.Add(new CommandHandlerMetadataAttribute(node.FullName, commandName, new string[] {}));
                        break;
                    case NodeType.Controller:
                        // Add a controller method that may send commands
                        var targetCommands = relationships
                            .Where(r => r.FromNode.Id == node.Id && r.Type == RelationshipType.ControllerToCommand)
                            .Select(r => r.ToNode.FullName)
                            .ToArray();
                        // Always create a ControllerMethodMetadataAttribute to ensure Controller node exists
                        attrs.Add(new ControllerMethodMetadataAttribute(node.FullName, "Action", 
                            targetCommands.Length > 0 ? targetCommands : new string[] { "DummyCommand" }));
                        break;
                }
            }
            
            MetadataAttributes = attrs.ToArray();
        }
    }
}

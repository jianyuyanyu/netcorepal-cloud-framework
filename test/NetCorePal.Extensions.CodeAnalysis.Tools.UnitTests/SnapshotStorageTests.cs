using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NetCorePal.Extensions.CodeAnalysis;
using NetCorePal.Extensions.CodeAnalysis.Tools.Snapshots;
using Xunit;

namespace NetCorePal.Extensions.CodeAnalysis.Tools.UnitTests;

public class SnapshotStorageTests : IDisposable
{
    private readonly string _tempDir;
    private readonly SnapshotStorage _storage;

    public SnapshotStorageTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"snapshot-test-{Guid.NewGuid():N}");
        _storage = new SnapshotStorage(_tempDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
        {
            Directory.Delete(_tempDir, true);
        }
    }

    [Fact]
    public void SaveSnapshot_CreatesSnapshotDirectory()
    {
        // Arrange
        var result = CreateSampleAnalysisResult();

        // Act
        var version = _storage.SaveSnapshot(result, "Test snapshot");

        // Assert
        Assert.True(Directory.Exists(_tempDir));
        Assert.NotEmpty(version);
    }

    [Fact]
    public void SaveSnapshot_CreatesJsonFile()
    {
        // Arrange
        var result = CreateSampleAnalysisResult();

        // Act
        var version = _storage.SaveSnapshot(result, "Test snapshot");
        var expectedFile = Path.Combine(_tempDir, $"{version}.json");

        // Assert
        Assert.True(File.Exists(expectedFile));
    }

    [Fact]
    public void LoadSnapshot_ReturnsNullForNonExistentVersion()
    {
        // Act
        var snapshot = _storage.LoadSnapshot("20200101000000");

        // Assert
        Assert.Null(snapshot);
    }

    [Fact]
    public void LoadSnapshot_ReturnsCorrectSnapshot()
    {
        // Arrange
        var result = CreateSampleAnalysisResult();
        var version = _storage.SaveSnapshot(result, "Test snapshot", verbose: false);

        // Act
        var loadedSnapshot = _storage.LoadSnapshot(version);

        // Assert
        Assert.NotNull(loadedSnapshot);
        Assert.Equal(version, loadedSnapshot.Metadata.Version);
        Assert.Equal("Test snapshot", loadedSnapshot.Metadata.Description);
        Assert.Equal(result.Nodes.Count, loadedSnapshot.AnalysisResult.Nodes.Count);
        Assert.Equal(result.Relationships.Count, loadedSnapshot.AnalysisResult.Relationships.Count);
    }

    [Fact]
    public void ListSnapshots_ReturnsEmptyListWhenNoSnapshots()
    {
        // Act
        var snapshots = _storage.ListSnapshots();

        // Assert
        Assert.Empty(snapshots);
    }

    [Fact]
    public void ListSnapshots_ReturnsAllSnapshots()
    {
        // Arrange
        var result1 = CreateSampleAnalysisResult();
        var result2 = CreateSampleAnalysisResult();
        result2.Nodes.Add(new Node { Id = "extra", Name = "Extra", Type = NodeType.Command });

        _storage.SaveSnapshot(result1, "Snapshot 1", verbose: false);
        System.Threading.Thread.Sleep(1000); // Ensure different timestamps
        _storage.SaveSnapshot(result2, "Snapshot 2", verbose: false);

        // Act
        var snapshots = _storage.ListSnapshots();

        // Assert
        Assert.Equal(2, snapshots.Count);
        Assert.Equal("Snapshot 2", snapshots[0].Description); // Most recent first
        Assert.Equal("Snapshot 1", snapshots[1].Description);
    }

    [Fact]
    public void GetLatestSnapshot_ReturnsNullWhenNoSnapshots()
    {
        // Act
        var snapshot = _storage.GetLatestSnapshot();

        // Assert
        Assert.Null(snapshot);
    }

    [Fact]
    public void GetLatestSnapshot_ReturnsLatestSnapshot()
    {
        // Arrange
        var result1 = CreateSampleAnalysisResult();
        var result2 = CreateSampleAnalysisResult();

        _storage.SaveSnapshot(result1, "Old snapshot", verbose: false);
        System.Threading.Thread.Sleep(1000);
        _storage.SaveSnapshot(result2, "New snapshot", verbose: false);

        // Act
        var latest = _storage.GetLatestSnapshot();

        // Assert
        Assert.NotNull(latest);
        Assert.Equal("New snapshot", latest.Metadata.Description);
    }

    private static CodeFlowAnalysisResult CreateSampleAnalysisResult()
    {
        var node1 = new Node
        {
            Id = "TestController",
            Name = "TestController",
            FullName = "MyApp.Controllers.TestController",
            Type = NodeType.Controller
        };

        var node2 = new Node
        {
            Id = "TestCommand",
            Name = "TestCommand",
            FullName = "MyApp.Commands.TestCommand",
            Type = NodeType.Command
        };

        var relationship = new Relationship(node1, node2, RelationshipType.ControllerToCommand);

        return new CodeFlowAnalysisResult
        {
            Nodes = new List<Node> { node1, node2 },
            Relationships = new List<Relationship> { relationship }
        };
    }
}

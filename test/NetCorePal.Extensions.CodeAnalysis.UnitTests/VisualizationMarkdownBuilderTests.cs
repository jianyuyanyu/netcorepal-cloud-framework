using System;
using System.Collections.Generic;
using NetCorePal.Extensions.CodeAnalysis;
using NetCorePal.Extensions.CodeAnalysis.Attributes;
using NetCorePal.Extensions.CodeAnalysis.Snapshots;
using Xunit;

namespace NetCorePal.Extensions.CodeAnalysis.UnitTests;

public class VisualizationMarkdownBuilderTests
{
    [Fact]
    public void GenerateVisualizationMarkdown_WithSimpleData_GeneratesValidMarkdown()
    {
        // Arrange
        var result = new CodeFlowAnalysisResult
        {
            Nodes = new List<Node>
            {
                new Node { Id = "1", Name = "TestController", FullName = "MyApp.Controllers.TestController", Type = NodeType.Controller },
                new Node { Id = "2", Name = "TestCommand", FullName = "MyApp.Commands.TestCommand", Type = NodeType.Command },
                new Node { Id = "3", Name = "TestAggregate", FullName = "MyApp.Aggregates.TestAggregate", Type = NodeType.Aggregate }
            },
            Relationships = new List<Relationship>
            {
                new Relationship(
                    new Node { Id = "1", Name = "TestController", Type = NodeType.Controller },
                    new Node { Id = "2", Name = "TestCommand", Type = NodeType.Command },
                    RelationshipType.ControllerToCommand
                ),
                new Relationship(
                    new Node { Id = "2", Name = "TestCommand", Type = NodeType.Command },
                    new Node { Id = "3", Name = "TestAggregate", Type = NodeType.Aggregate },
                    RelationshipType.CommandToAggregate
                )
            }
        };

        // Act
        var markdown = VisualizationMarkdownBuilder.GenerateVisualizationMarkdown(
            result,
            "Test Architecture",
            includeMermaid: true,
            withHistory: false);

        // Assert
        Assert.NotNull(markdown);
        Assert.NotEmpty(markdown);
        
        // Check for main sections
        Assert.Contains("# Test Architecture", markdown);
        Assert.Contains("## 📈 概览统计", markdown);
        Assert.Contains("## 🏗️ 架构元素", markdown);
        Assert.Contains("## 🔗 组件关系", markdown);
        Assert.Contains("## 📊 架构图表", markdown);
        
        // Check for node types
        Assert.Contains("控制器 (Controller)", markdown);
        Assert.Contains("命令 (Command)", markdown);
        Assert.Contains("聚合根 (Aggregate)", markdown);
        
        // Check for nodes
        Assert.Contains("TestController", markdown);
        Assert.Contains("TestCommand", markdown);
        Assert.Contains("TestAggregate", markdown);
        
        // Check for relationships
        Assert.Contains("控制器 → 命令", markdown);
        Assert.Contains("命令 → 聚合根", markdown);
        
        // Check for Mermaid diagrams
        Assert.Contains("```mermaid", markdown);
    }

    [Fact]
    public void GenerateVisualizationMarkdown_WithNoMermaid_DoesNotIncludeMermaidDiagrams()
    {
        // Arrange
        var result = new CodeFlowAnalysisResult
        {
            Nodes = new List<Node>
            {
                new Node { Id = "1", Name = "TestController", Type = NodeType.Controller }
            },
            Relationships = new List<Relationship>()
        };

        // Act
        var markdown = VisualizationMarkdownBuilder.GenerateVisualizationMarkdown(
            result,
            "Test",
            includeMermaid: false,
            withHistory: false);

        // Assert
        Assert.DoesNotContain("## 📊 架构图表", markdown);
        Assert.DoesNotContain("```mermaid", markdown);
    }

    [Fact]
    public void GenerateVisualizationMarkdown_WithEmptyData_GeneratesValidMarkdown()
    {
        // Arrange
        var result = new CodeFlowAnalysisResult
        {
            Nodes = new List<Node>(),
            Relationships = new List<Relationship>()
        };

        // Act
        var markdown = VisualizationMarkdownBuilder.GenerateVisualizationMarkdown(
            result,
            "Empty Architecture",
            includeMermaid: false,
            withHistory: false);

        // Assert
        Assert.NotNull(markdown);
        Assert.NotEmpty(markdown);
        Assert.Contains("# Empty Architecture", markdown);
        Assert.Contains("## 📈 概览统计", markdown);
    }

    [Fact]
    public void GenerateVisualizationMarkdown_EscapesSpecialCharacters()
    {
        // Arrange
        var result = new CodeFlowAnalysisResult
        {
            Nodes = new List<Node>
            {
                new Node { Id = "1", Name = "Test*Controller_With#Special[Chars]", Type = NodeType.Controller }
            },
            Relationships = new List<Relationship>()
        };

        // Act
        var markdown = VisualizationMarkdownBuilder.GenerateVisualizationMarkdown(
            result,
            "Test",
            includeMermaid: false,
            withHistory: false);

        // Assert
        // Special characters should be escaped
        Assert.Contains("\\*", markdown);
        Assert.Contains("\\_", markdown);
        Assert.Contains("\\#", markdown);
        Assert.Contains("\\[", markdown);
        Assert.Contains("\\]", markdown);
    }

    [Fact]
    public void GenerateVisualizationMarkdown_WithHistory_IncludesVersionHistory()
    {
        // Arrange
        var result = new CodeFlowAnalysisResult
        {
            Nodes = new List<Node>
            {
                new Node { Id = "1", Name = "TestController", Type = NodeType.Controller }
            },
            Relationships = new List<Relationship>()
        };

        var attributes = new MetadataAttribute[] { };
        var snapshot = CodeFlowAnalysisSnapshotHelper.CreateSnapshot(attributes, "Initial version", "20260125120000");
        var snapshots = new List<CodeFlowAnalysisSnapshot> { snapshot };

        // Act
        var markdown = VisualizationMarkdownBuilder.GenerateVisualizationMarkdown(
            result,
            "Test Architecture",
            includeMermaid: false,
            withHistory: true,
            snapshots: snapshots);

        // Assert
        Assert.Contains("## 📊 版本历史", markdown);
        Assert.Contains("当前分析包含 1 个版本快照", markdown);
        Assert.Contains("Initial version", markdown);
        Assert.Contains("2026-01-25 12:00:00", markdown);
    }

    [Fact]
    public void GenerateVisualizationMarkdown_WithHistory_EscapesPipeCharactersInDescriptions()
    {
        // Arrange
        var result = new CodeFlowAnalysisResult
        {
            Nodes = new List<Node>
            {
                new Node { Id = "1", Name = "TestController", Type = NodeType.Controller }
            },
            Relationships = new List<Relationship>()
        };

        var attributes = new MetadataAttribute[] { };
        var snapshot = CodeFlowAnalysisSnapshotHelper.CreateSnapshot(attributes, "Added feature | with pipe", "20260125120000");
        var snapshots = new List<CodeFlowAnalysisSnapshot> { snapshot };

        // Act
        var markdown = VisualizationMarkdownBuilder.GenerateVisualizationMarkdown(
            result,
            "Test",
            includeMermaid: false,
            withHistory: true,
            snapshots: snapshots);

        // Assert
        // Pipe character should be escaped
        Assert.Contains("\\|", markdown);
        Assert.Contains("Added feature \\| with pipe", markdown);
    }

    [Fact]
    public void GenerateVisualizationMarkdown_WithMultipleSnapshots_GeneratesTrendTables()
    {
        // Arrange
        var result = new CodeFlowAnalysisResult
        {
            Nodes = new List<Node>
            {
                new Node { Id = "1", Name = "TestController", Type = NodeType.Controller },
                new Node { Id = "2", Name = "TestCommand", Type = NodeType.Command }
            },
            Relationships = new List<Relationship>()
        };

        var attributes = new MetadataAttribute[] { };
        var snapshot1 = CodeFlowAnalysisSnapshotHelper.CreateSnapshot(attributes, "Version 1", "20260125100000");
        var snapshot2 = CodeFlowAnalysisSnapshotHelper.CreateSnapshot(attributes, "Version 2", "20260125120000");
        var snapshots = new List<CodeFlowAnalysisSnapshot> { snapshot1, snapshot2 };

        // Act
        var markdown = VisualizationMarkdownBuilder.GenerateVisualizationMarkdown(
            result,
            "Test",
            includeMermaid: false,
            withHistory: true,
            snapshots: snapshots);

        // Assert
        Assert.Contains("## 📈 演进趋势", markdown);
        Assert.Contains("### 各类型节点数量变化", markdown);
        Assert.Contains("Version 1", markdown);
        Assert.Contains("Version 2", markdown);
    }

    [Fact]
    public void GenerateVisualizationMarkdown_WithMalformedVersionString_HandlesGracefully()
    {
        // Arrange
        var result = new CodeFlowAnalysisResult
        {
            Nodes = new List<Node>
            {
                new Node { Id = "1", Name = "TestController", Type = NodeType.Controller }
            },
            Relationships = new List<Relationship>()
        };

        var attributes = new MetadataAttribute[] { };
        var snapshot = CodeFlowAnalysisSnapshotHelper.CreateSnapshot(attributes, "Test description", "invalid|version");
        var snapshots = new List<CodeFlowAnalysisSnapshot> { snapshot };

        // Act
        var markdown = VisualizationMarkdownBuilder.GenerateVisualizationMarkdown(
            result,
            "Test",
            includeMermaid: false,
            withHistory: true,
            snapshots: snapshots);

        // Assert
        // Should not crash and should escape the pipe character
        Assert.Contains("\\|", markdown);
        Assert.DoesNotContain("invalid|version", markdown); // Unescaped version should not appear
    }

    [Fact]
    public void GenerateVisualizationMarkdown_WithSpecialCharsInDescriptions_EscapesInTables()
    {
        // Arrange
        var result = new CodeFlowAnalysisResult
        {
            Nodes = new List<Node>
            {
                new Node { Id = "1", Name = "TestController", Type = NodeType.Controller }
            },
            Relationships = new List<Relationship>()
        };

        var attributes = new MetadataAttribute[] { };
        var snapshot = CodeFlowAnalysisSnapshotHelper.CreateSnapshot(attributes, "Added **bold** and *italic* and `code`", "20260125120000");
        var snapshots = new List<CodeFlowAnalysisSnapshot> { snapshot };

        // Act
        var markdown = VisualizationMarkdownBuilder.GenerateVisualizationMarkdown(
            result,
            "Test",
            includeMermaid: false,
            withHistory: true,
            snapshots: snapshots);

        // Assert
        // Special markdown characters should be escaped
        Assert.Contains("\\*", markdown);
        Assert.Contains("\\`", markdown);
    }

    [Fact]
    public void GenerateVisualizationMarkdown_WithMermaid_IncludesConfigurationNote()
    {
        // Arrange
        var result = new CodeFlowAnalysisResult
        {
            Nodes = new List<Node>
            {
                new Node { Id = "1", Name = "TestController", Type = NodeType.Controller }
            },
            Relationships = new List<Relationship>()
        };

        // Act
        var markdown = VisualizationMarkdownBuilder.GenerateVisualizationMarkdown(
            result,
            "Test",
            maxEdges: 3000,
            maxTextSize: 500000,
            includeMermaid: true,
            withHistory: false);

        // Assert
        // Should include configuration note with custom values
        Assert.Contains("maxEdges: 3000", markdown);
        Assert.Contains("maxTextSize: 500000", markdown);
        Assert.Contains("注意", markdown);
    }
}

using System;
using System.Collections.Generic;
using NetCorePal.Extensions.CodeAnalysis;
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
}

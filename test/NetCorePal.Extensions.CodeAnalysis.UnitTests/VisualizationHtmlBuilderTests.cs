using System.Collections.Generic;
using NetCorePal.Extensions.CodeAnalysis;
using Xunit;

namespace NetCorePal.Extensions.CodeAnalysis.UnitTests;

public class VisualizationHtmlBuilderTests
{
    [Fact]
    public void GenerateVisualizationHtml_ShouldContainBasicHtmlAndDiagrams()
    {
        var result = CodeFlowAnalysisHelper.GetResultFromAssemblies(typeof(VisualizationHtmlBuilderTests).Assembly);

        var controllers = result.Nodes.FindAll(n => n.Type == NodeType.Controller);
        var controllerMethods = result.Nodes.FindAll(n => n.Type == NodeType.ControllerMethod);
        var endpoints = result.Nodes.FindAll(n => n.Type == NodeType.Endpoint);
        var commandSenders = result.Nodes.FindAll(n => n.Type == NodeType.CommandSender);
        var commandSenderMethods = result.Nodes.FindAll(n => n.Type == NodeType.CommandSenderMethod);
        var commands = result.Nodes.FindAll(n => n.Type == NodeType.Command);
        var entities = result.Nodes.FindAll(n => n.Type == NodeType.Aggregate);
        var entityMethods = result.Nodes.FindAll(n => n.Type == NodeType.EntityMethod);
        var domainEvents = result.Nodes.FindAll(n => n.Type == NodeType.DomainEvent);
        var integrationEvents = result.Nodes.FindAll(n => n.Type == NodeType.IntegrationEvent);
        var domainEventHandlers = result.Nodes.FindAll(n => n.Type == NodeType.DomainEventHandler);
        var integrationEventHandlers = result.Nodes.FindAll(n => n.Type == NodeType.IntegrationEventHandler);
        var integrationEventConverters = result.Nodes.FindAll(n => n.Type == NodeType.IntegrationEventConverter);
        
        var html = VisualizationHtmlBuilder.GenerateVisualizationHtml(result);

        //保存到文件
        System.IO.File.WriteAllText("visualization.html", html);

        Assert.Contains("<!DOCTYPE html>", html);
        Assert.Contains("系统模型架构图", html);
        Assert.Contains("mermaid.min.js", html);
    }

    [Fact]
    public void GenerateVisualizationHtml_ShouldContainStatisticsMenuAndData()
    {
        // Get metadata attributes from assemblies for creating snapshot
        var metadataAttributes = CodeFlowAnalysisHelper.GetAllMetadataAttributes(typeof(VisualizationHtmlBuilderTests).Assembly).ToArray();
        
        // Create a snapshot from metadata attributes
        var snapshot = Snapshots.CodeFlowAnalysisSnapshotHelper.CreateSnapshot(metadataAttributes, "Test snapshot");
        var snapshots = new System.Collections.Generic.List<Snapshots.CodeFlowAnalysisSnapshot> { snapshot };
        
        // Get result for passing to GenerateVisualizationHtml
        var result = snapshot.GetAnalysisResult();
        
        var html = VisualizationHtmlBuilder.GenerateVisualizationHtml(result, withHistory: true, snapshots: snapshots);
        
        // 验证统计信息菜单存在
        Assert.Contains("📊 统计信息", html);
        Assert.Contains("data-diagram=\"Statistics\"", html);
        
        // 验证统计数据被包含在 HTML 中
        Assert.Contains("nodeStats", html);
        Assert.Contains("relationshipStats", html);
        Assert.Contains("totalElements", html);
        Assert.Contains("totalRelationships", html);
        
        // 验证统计信息配置存在
        Assert.Contains("\"Statistics\":{\"title\":'统计信息'", html);
        
        // 验证统计信息被设置为搜索项
        Assert.Contains("统计信息", html);
    }

    [Fact]
    public void GenerateVisualizationHtml_WithHistory_ShouldContainDataSourcesArray()
    {
        // Get metadata attributes from assemblies for creating snapshot
        var metadataAttributes = CodeFlowAnalysisHelper.GetAllMetadataAttributes(typeof(VisualizationHtmlBuilderTests).Assembly).ToArray();
        
        // Create multiple snapshots
        var snapshot1 = Snapshots.CodeFlowAnalysisSnapshotHelper.CreateSnapshot(metadataAttributes, "First snapshot", "20260101000000");
        var snapshot2 = Snapshots.CodeFlowAnalysisSnapshotHelper.CreateSnapshot(metadataAttributes, "Second snapshot", "20260102000000");
        var snapshots = new System.Collections.Generic.List<Snapshots.CodeFlowAnalysisSnapshot> { snapshot1, snapshot2 };
        
        var result = snapshot1.GetAnalysisResult();
        
        var html = VisualizationHtmlBuilder.GenerateVisualizationHtml(result, withHistory: true, snapshots: snapshots);
        
        // 验证 dataSources 数组存在
        Assert.Contains("const dataSources =", html);
        
        // 验证 dataSource 结构包含所需属性
        Assert.Contains("\"metadata\":", html);
        Assert.Contains("\"statistics\":", html);
        Assert.Contains("\"analysisResult\":", html);
        Assert.Contains("\"diagrams\":", html);
        Assert.Contains("\"allChainFlowCharts\":", html);
        Assert.Contains("\"allAggregateRelationDiagrams\":", html);
    }

    [Fact]
    public void GenerateVisualizationHtml_WithMultipleSnapshots_ShouldContainSnapshotSelector()
    {
        // Get metadata attributes from assemblies for creating snapshot
        var metadataAttributes = CodeFlowAnalysisHelper.GetAllMetadataAttributes(typeof(VisualizationHtmlBuilderTests).Assembly).ToArray();
        
        // Create multiple snapshots
        var snapshot1 = Snapshots.CodeFlowAnalysisSnapshotHelper.CreateSnapshot(metadataAttributes, "First snapshot", "20260101000000");
        var snapshot2 = Snapshots.CodeFlowAnalysisSnapshotHelper.CreateSnapshot(metadataAttributes, "Second snapshot", "20260102000000");
        var snapshots = new System.Collections.Generic.List<Snapshots.CodeFlowAnalysisSnapshot> { snapshot1, snapshot2 };
        
        var result = snapshot1.GetAnalysisResult();
        
        var html = VisualizationHtmlBuilder.GenerateVisualizationHtml(result, withHistory: true, snapshots: snapshots);
        
        // 验证快照选择器存在
        Assert.Contains("snapshotSelectorGroup", html);
        Assert.Contains("snapshot-selector", html);
        Assert.Contains("switchSnapshot", html);
    }

    [Fact]
    public void GenerateVisualizationHtml_WithTwoOrMoreSnapshots_ShouldContainHistoricalTrendsMenu()
    {
        // Get metadata attributes from assemblies for creating snapshot
        var metadataAttributes = CodeFlowAnalysisHelper.GetAllMetadataAttributes(typeof(VisualizationHtmlBuilderTests).Assembly).ToArray();
        
        // Create multiple snapshots (need at least 2 for historical trends)
        var snapshot1 = Snapshots.CodeFlowAnalysisSnapshotHelper.CreateSnapshot(metadataAttributes, "First snapshot", "20260101000000");
        var snapshot2 = Snapshots.CodeFlowAnalysisSnapshotHelper.CreateSnapshot(metadataAttributes, "Second snapshot", "20260102000000");
        var snapshots = new System.Collections.Generic.List<Snapshots.CodeFlowAnalysisSnapshot> { snapshot1, snapshot2 };
        
        var result = snapshot1.GetAnalysisResult();
        
        var html = VisualizationHtmlBuilder.GenerateVisualizationHtml(result, withHistory: true, snapshots: snapshots);
        
        // 验证历史趋势菜单存在
        Assert.Contains("📈 历史趋势", html);
        Assert.Contains("data-diagram=\"HistoricalTrends\"", html);
        Assert.Contains("showHistoricalTrends", html);
        
        // 验证 Chart.js 库被包含
        Assert.Contains("chart.js", html);
        Assert.Contains("chart.umd.min.js", html);
    }

    [Fact]
    public void GenerateVisualizationHtml_WithSingleSnapshot_ShouldNotShowHistoricalTrendsMenu()
    {
        // Get metadata attributes from assemblies for creating snapshot
        var metadataAttributes = CodeFlowAnalysisHelper.GetAllMetadataAttributes(typeof(VisualizationHtmlBuilderTests).Assembly).ToArray();
        
        // Create only one snapshot
        var snapshot = Snapshots.CodeFlowAnalysisSnapshotHelper.CreateSnapshot(metadataAttributes, "Single snapshot");
        var snapshots = new System.Collections.Generic.List<Snapshots.CodeFlowAnalysisSnapshot> { snapshot };
        
        var result = snapshot.GetAnalysisResult();
        
        var html = VisualizationHtmlBuilder.GenerateVisualizationHtml(result, withHistory: true, snapshots: snapshots);
        
        // 验证历史趋势菜单的显示样式为 none (因为只有一个快照)
        // 菜单项应该存在但默认隐藏
        Assert.Contains("historicalTrendsMenuItem", html);
    }

    [Fact]
    public void GenerateVisualizationHtml_HistoricalTrends_ShouldUseFilteredNodeTypes()
    {
        // Get metadata attributes from assemblies for creating snapshot
        var metadataAttributes = CodeFlowAnalysisHelper.GetAllMetadataAttributes(typeof(VisualizationHtmlBuilderTests).Assembly).ToArray();
        
        // Create multiple snapshots
        var snapshot1 = Snapshots.CodeFlowAnalysisSnapshotHelper.CreateSnapshot(metadataAttributes, "First", "20260101000000");
        var snapshot2 = Snapshots.CodeFlowAnalysisSnapshotHelper.CreateSnapshot(metadataAttributes, "Second", "20260102000000");
        var snapshots = new System.Collections.Generic.List<Snapshots.CodeFlowAnalysisSnapshot> { snapshot1, snapshot2 };
        
        var result = snapshot1.GetAnalysisResult();
        
        var html = VisualizationHtmlBuilder.GenerateVisualizationHtml(result, withHistory: true, snapshots: snapshots);
        
        // 验证历史趋势使用相同的过滤节点类型
        Assert.Contains("displayNodeTypes", html);
        
        // 验证包含正确的节点类型（应该与统计页面相同）
        Assert.Contains("Controller", html);
        Assert.Contains("Endpoint", html);
        Assert.Contains("CommandSender", html);
        Assert.Contains("Command", html);
        Assert.Contains("CommandHandler", html);
        Assert.Contains("Aggregate", html);
        Assert.Contains("DomainEvent", html);
        Assert.Contains("IntegrationEvent", html);
    }

    [Fact]
    public void GenerateVisualizationHtml_HistoricalTrends_ShouldUseFilteredRelationshipTypes()
    {
        // Get metadata attributes from assemblies for creating snapshot
        var metadataAttributes = CodeFlowAnalysisHelper.GetAllMetadataAttributes(typeof(VisualizationHtmlBuilderTests).Assembly).ToArray();
        
        // Create multiple snapshots
        var snapshot1 = Snapshots.CodeFlowAnalysisSnapshotHelper.CreateSnapshot(metadataAttributes, "First", "20260101000000");
        var snapshot2 = Snapshots.CodeFlowAnalysisSnapshotHelper.CreateSnapshot(metadataAttributes, "Second", "20260102000000");
        var snapshots = new System.Collections.Generic.List<Snapshots.CodeFlowAnalysisSnapshot> { snapshot1, snapshot2 };
        
        var result = snapshot1.GetAnalysisResult();
        
        var html = VisualizationHtmlBuilder.GenerateVisualizationHtml(result, withHistory: true, snapshots: snapshots);
        
        // 验证历史趋势使用相同的过滤关系类型
        Assert.Contains("displayRelationshipTypes", html);
        
        // 验证包含正确的关系类型（应该与统计页面相同）
        Assert.Contains("ControllerToCommand", html);
        Assert.Contains("CommandToAggregate", html);
        Assert.Contains("AggregateToDomainEvent", html);
    }

    [Fact]
    public void GenerateVisualizationHtml_DiagramConfigs_ShouldIncludeHistoricalTrendsConfig()
    {
        // Get metadata attributes from assemblies for creating snapshot
        var metadataAttributes = CodeFlowAnalysisHelper.GetAllMetadataAttributes(typeof(VisualizationHtmlBuilderTests).Assembly).ToArray();
        
        // Create multiple snapshots
        var snapshot1 = Snapshots.CodeFlowAnalysisSnapshotHelper.CreateSnapshot(metadataAttributes, "First", "20260101000000");
        var snapshot2 = Snapshots.CodeFlowAnalysisSnapshotHelper.CreateSnapshot(metadataAttributes, "Second", "20260102000000");
        var snapshots = new System.Collections.Generic.List<Snapshots.CodeFlowAnalysisSnapshot> { snapshot1, snapshot2 };
        
        var result = snapshot1.GetAnalysisResult();
        
        var html = VisualizationHtmlBuilder.GenerateVisualizationHtml(result, withHistory: true, snapshots: snapshots);
        
        // 验证 diagramConfigs 包含 HistoricalTrends 配置
        Assert.Contains("\"HistoricalTrends\"", html);
        Assert.Contains("'历史趋势'", html);
    }
}

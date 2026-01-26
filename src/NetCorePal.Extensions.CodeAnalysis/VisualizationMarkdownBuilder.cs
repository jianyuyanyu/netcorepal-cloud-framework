using System;
using System.Linq;
using System.Text;

namespace NetCorePal.Extensions.CodeAnalysis
{
    /// <summary>
    /// 负责生成架构可视化 Markdown 文件
    /// </summary>
    public static class VisualizationMarkdownBuilder
    {
        /// <summary>
        /// 生成架构可视化Markdown文件
        /// </summary>
        /// <param name="analysisResult">分析结果</param>
        /// <param name="title">文档标题</param>
        /// <param name="maxEdges">最大边数（用于Mermaid配置注释）</param>
        /// <param name="maxTextSize">最大文本大小（用于Mermaid配置注释）</param>
        /// <param name="includeMermaid">是否包含Mermaid图表（默认true）</param>
        /// <param name="withHistory">是否包含历史快照（默认true）</param>
        /// <param name="snapshots">历史快照列表（当withHistory=true时使用）</param>
        /// <returns>Markdown内容</returns>
        public static string GenerateVisualizationMarkdown(
            CodeFlowAnalysisResult analysisResult,
            string title = "系统架构分析",
            int maxEdges = 5000,
            int maxTextSize = 1000000,
            bool includeMermaid = true,
            bool withHistory = true,
            System.Collections.Generic.List<Snapshots.CodeFlowAnalysisSnapshot>? snapshots = null)
        {
            var sb = new StringBuilder();
            
            // Title
            sb.AppendLine($"# {title}");
            sb.AppendLine();
            sb.AppendLine($"生成时间: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            sb.AppendLine();
            
            // 准备快照集合
            var snapshotList = new System.Collections.Generic.List<Snapshots.CodeFlowAnalysisSnapshot>();
            
            if (withHistory && snapshots != null && snapshots.Count > 0)
            {
                snapshotList = snapshots;
                
                // Add history summary
                sb.AppendLine("## 📊 版本历史");
                sb.AppendLine();
                sb.AppendLine($"当前分析包含 {snapshotList.Count} 个版本快照：");
                sb.AppendLine();
                
                foreach (var snapshot in snapshotList)
                {
                    var metadata = snapshot.Metadata;
                    // Parse version string as DateTime
                    var timestampStr = TryParseVersionAsDateTime(metadata.Version, out var timestamp)
                        ? timestamp.ToString("yyyy-MM-dd HH:mm:ss")
                        : EscapeMarkdown(metadata.Version);
                    sb.AppendLine($"- **{timestampStr}**: {EscapeMarkdown(metadata.Description)}");
                    sb.AppendLine($"  - 节点数: {metadata.NodeCount}, 关系数: {metadata.RelationshipCount}");
                    sb.AppendLine($"  - Hash: `{metadata.Hash}`");
                }
                sb.AppendLine();
            }
            
            // Overview Statistics
            sb.AppendLine("## 📈 概览统计");
            sb.AppendLine();
            GenerateStatistics(sb, analysisResult);
            
            // Nodes by Type
            sb.AppendLine("## 🏗️ 架构元素");
            sb.AppendLine();
            GenerateNodesByType(sb, analysisResult);
            
            // Relationships
            sb.AppendLine("## 🔗 组件关系");
            sb.AppendLine();
            GenerateRelationships(sb, analysisResult);
            
            // Mermaid Diagrams
            if (includeMermaid)
            {
                sb.AppendLine("## 📊 架构图表");
                sb.AppendLine();
                GenerateMermaidDiagrams(sb, analysisResult, maxEdges, maxTextSize);
            }
            
            // History Trends (if multiple snapshots)
            if (withHistory && snapshotList.Count >= 2)
            {
                sb.AppendLine("## 📈 演进趋势");
                sb.AppendLine();
                GenerateHistoryTrends(sb, snapshotList);
            }
            
            return sb.ToString();
        }
        
        // 定义要显示的节点类型（与HTML模板保持一致）
        private static readonly NodeType[] DisplayNodeTypes = new[]
        {
            NodeType.Controller,
            NodeType.Endpoint,
            NodeType.CommandSender,
            NodeType.Command,
            NodeType.CommandHandler,
            NodeType.Aggregate,
            NodeType.DomainEvent,
            NodeType.IntegrationEvent,
            NodeType.DomainEventHandler,
            NodeType.IntegrationEventHandler,
            NodeType.IntegrationEventConverter
        };
        
        // 定义要显示的关系类型（与HTML模板保持一致）
        private static readonly RelationshipType[] DisplayRelationshipTypes = new[]
        {
            RelationshipType.ControllerToCommand,
            RelationshipType.EndpointToCommand,
            RelationshipType.CommandSenderToCommand,
            RelationshipType.CommandToAggregate,
            RelationshipType.AggregateToDomainEvent,
            RelationshipType.DomainEventToHandler,
            RelationshipType.DomainEventHandlerToCommand,
            RelationshipType.IntegrationEventToHandler,
            RelationshipType.IntegrationEventHandlerToCommand,
            RelationshipType.DomainEventToIntegrationEvent
        };
        
        private static void GenerateStatistics(StringBuilder sb, CodeFlowAnalysisResult analysisResult)
        {
            // 过滤节点和关系类型
            var nodesByType = analysisResult.Nodes
                .Where(n => DisplayNodeTypes.Contains(n.Type))
                .GroupBy(n => n.Type)
                .OrderBy(g => Array.IndexOf(DisplayNodeTypes, g.Key));
            
            var relationshipsByType = analysisResult.Relationships
                .Where(r => DisplayRelationshipTypes.Contains(r.Type))
                .GroupBy(r => r.Type)
                .OrderBy(g => Array.IndexOf(DisplayRelationshipTypes, g.Key));
            
            sb.AppendLine("### 节点统计");
            sb.AppendLine();
            sb.AppendLine("| 类型 | 数量 |");
            sb.AppendLine("|------|------|");
            
            var filteredNodeCount = 0;
            foreach (var group in nodesByType)
            {
                var count = group.Count();
                filteredNodeCount += count;
                sb.AppendLine($"| {GetNodeTypeDisplayName(group.Key)} | {count} |");
            }
            
            sb.AppendLine($"| **总计** | **{filteredNodeCount}** |");
            sb.AppendLine();
            
            sb.AppendLine("### 关系统计");
            sb.AppendLine();
            sb.AppendLine("| 类型 | 数量 |");
            sb.AppendLine("|------|------|");
            
            var filteredRelationshipCount = 0;
            foreach (var group in relationshipsByType)
            {
                var count = group.Count();
                filteredRelationshipCount += count;
                sb.AppendLine($"| {GetRelationshipTypeDisplayName(group.Key)} | {count} |");
            }
            
            sb.AppendLine($"| **总计** | **{filteredRelationshipCount}** |");
            sb.AppendLine();
        }
        
        private static void GenerateNodesByType(StringBuilder sb, CodeFlowAnalysisResult analysisResult)
        {
            // 过滤并按定义顺序排序
            var nodesByType = analysisResult.Nodes
                .Where(n => DisplayNodeTypes.Contains(n.Type))
                .GroupBy(n => n.Type)
                .OrderBy(g => Array.IndexOf(DisplayNodeTypes, g.Key));
            
            foreach (var group in nodesByType)
            {
                sb.AppendLine($"### {GetNodeTypeDisplayName(group.Key)} ({group.Count()})");
                sb.AppendLine();
                
                var nodes = group.OrderBy(n => n.Name).ToList();
                
                if (nodes.Count > 0)
                {
                    foreach (var node in nodes)
                    {
                        sb.AppendLine($"- **{EscapeMarkdown(node.Name)}**");
                        if (!string.IsNullOrEmpty(node.FullName) && node.FullName != node.Name)
                        {
                            sb.AppendLine($"  - 完整名称: `{EscapeMarkdown(node.FullName)}`");
                        }
                    }
                }
                else
                {
                    sb.AppendLine("*无*");
                }
                
                sb.AppendLine();
            }
        }
        
        private static void GenerateRelationships(StringBuilder sb, CodeFlowAnalysisResult analysisResult)
        {
            // 过滤并按定义顺序排序
            var relationshipsByType = analysisResult.Relationships
                .Where(r => DisplayRelationshipTypes.Contains(r.Type))
                .GroupBy(r => r.Type)
                .OrderBy(g => Array.IndexOf(DisplayRelationshipTypes, g.Key));
            
            foreach (var group in relationshipsByType)
            {
                sb.AppendLine($"### {GetRelationshipTypeDisplayName(group.Key)} ({group.Count()})");
                sb.AppendLine();
                
                var relationships = group.OrderBy(r => r.FromNode.Name).ThenBy(r => r.ToNode.Name).ToList();
                
                if (relationships.Count > 0)
                {
                    foreach (var rel in relationships)
                    {
                        sb.AppendLine($"- `{EscapeMarkdown(rel.FromNode.Name)}` → `{EscapeMarkdown(rel.ToNode.Name)}`");
                    }
                }
                else
                {
                    sb.AppendLine("*无*");
                }
                
                sb.AppendLine();
            }
        }
        
        private static void GenerateMermaidDiagrams(StringBuilder sb, CodeFlowAnalysisResult analysisResult, int maxEdges, int maxTextSize)
        {
            // Add Mermaid configuration note
            sb.AppendLine("> **注意**: 以下 Mermaid 图表建议配置参数: `maxEdges: {maxEdges}, maxTextSize: {maxTextSize}`".Replace("{maxEdges}", maxEdges.ToString()).Replace("{maxTextSize}", maxTextSize.ToString()));
            sb.AppendLine();
            
            // Architecture Overview
            sb.AppendLine("### 架构总览图");
            sb.AppendLine();
            var architectureOverview = MermaidVisualizers.ArchitectureOverviewMermaidVisualizer.GenerateMermaid(analysisResult);
            sb.AppendLine("```mermaid");
            sb.AppendLine(architectureOverview);
            sb.AppendLine("```");
            sb.AppendLine();
            
            // Processing Flow - returns list of diagrams
            sb.AppendLine("### 处理流程图");
            sb.AppendLine();
            var processingFlows = MermaidVisualizers.ProcessingFlowMermaidVisualizer.GenerateMermaid(analysisResult);
            if (processingFlows.Count > 0)
            {
                foreach (var (chainName, diagram) in processingFlows)
                {
                    sb.AppendLine($"#### {EscapeMarkdown(chainName)}");
                    sb.AppendLine();
                    sb.AppendLine("```mermaid");
                    sb.AppendLine(diagram);
                    sb.AppendLine("```");
                    sb.AppendLine();
                }
            }
            else
            {
                sb.AppendLine("*无处理流程*");
                sb.AppendLine();
            }
            
            // Aggregate Relations - returns list of diagrams
            sb.AppendLine("### 聚合关系图");
            sb.AppendLine();
            var aggregateRelations = MermaidVisualizers.AggregateRelationMermaidVisualizer.GenerateAllAggregateMermaid(analysisResult);
            if (aggregateRelations.Count > 0)
            {
                foreach (var (aggregateName, diagram) in aggregateRelations)
                {
                    sb.AppendLine($"#### {EscapeMarkdown(aggregateName)}");
                    sb.AppendLine();
                    sb.AppendLine("```mermaid");
                    sb.AppendLine(diagram);
                    sb.AppendLine("```");
                    sb.AppendLine();
                }
            }
            else
            {
                sb.AppendLine("*无聚合关系*");
                sb.AppendLine();
            }
        }
        
        private static void GenerateHistoryTrends(StringBuilder sb, System.Collections.Generic.List<Snapshots.CodeFlowAnalysisSnapshot> snapshots)
        {
            sb.AppendLine("### 节点数量变化");
            sb.AppendLine();
            sb.AppendLine("| 版本 | 描述 | 总节点数 | 总关系数 |");
            sb.AppendLine("|------|------|----------|----------|");
            
            foreach (var snapshot in snapshots.OrderBy(s => s.Metadata.Version))
            {
                var timestampStr = TryParseVersionAsDateTime(snapshot.Metadata.Version, out var timestamp)
                    ? timestamp.ToString("yyyy-MM-dd HH:mm")
                    : EscapeMarkdown(snapshot.Metadata.Version);
                
                // 计算过滤后的数量
                var result = snapshot.GetAnalysisResult();
                var filteredNodeCount = result.Nodes.Count(n => DisplayNodeTypes.Contains(n.Type));
                var filteredRelationshipCount = result.Relationships.Count(r => DisplayRelationshipTypes.Contains(r.Type));
                
                sb.AppendLine($"| {timestampStr} | {EscapeMarkdown(snapshot.Metadata.Description)} | {filteredNodeCount} | {filteredRelationshipCount} |");
            }
            
            sb.AppendLine();
            
            // Detailed type breakdown
            sb.AppendLine("### 各类型节点数量变化");
            sb.AppendLine();
            
            // Collect all node types across all snapshots (filtered)
            var allNodeTypes = new System.Collections.Generic.HashSet<NodeType>();
            foreach (var snapshot in snapshots)
            {
                var result = snapshot.GetAnalysisResult();
                allNodeTypes.UnionWith(result.Nodes.Where(n => DisplayNodeTypes.Contains(n.Type)).Select(n => n.Type));
            }
            
            // Build header (按DisplayNodeTypes定义的顺序)
            var orderedNodeTypes = DisplayNodeTypes.Where(t => allNodeTypes.Contains(t)).ToArray();
            sb.Append("| 版本 |");
            foreach (var nodeType in orderedNodeTypes)
            {
                sb.Append($" {GetNodeTypeDisplayName(nodeType)} |");
            }
            sb.AppendLine();
            
            // Build separator
            sb.Append("|------|");
            foreach (var _ in orderedNodeTypes)
            {
                sb.Append("------|");
            }
            sb.AppendLine();
            
            // Build data rows
            foreach (var snapshot in snapshots.OrderBy(s => s.Metadata.Version))
            {
                var timestampStr = TryParseVersionAsDateTime(snapshot.Metadata.Version, out var timestamp)
                    ? timestamp.ToString("yyyy-MM-dd HH:mm")
                    : EscapeMarkdown(snapshot.Metadata.Version);
                var result = snapshot.GetAnalysisResult();
                var nodesByType = result.Nodes
                    .Where(n => DisplayNodeTypes.Contains(n.Type))
                    .GroupBy(n => n.Type)
                    .ToDictionary(g => g.Key, g => g.Count());
                
                sb.Append($"| {timestampStr} |");
                foreach (var nodeType in orderedNodeTypes)
                {
                    var count = nodesByType.TryGetValue(nodeType, out var value) ? value : 0;
                    sb.Append($" {count} |");
                }
                sb.AppendLine();
            }
            
            sb.AppendLine();
        }
        
        private static string GetNodeTypeDisplayName(NodeType type)
        {
            return type switch
            {
                NodeType.Controller => "控制器 (Controller)",
                NodeType.ControllerMethod => "控制器方法 (ControllerMethod)",
                NodeType.Endpoint => "端点 (Endpoint)",
                NodeType.CommandSender => "命令发送者 (CommandSender)",
                NodeType.CommandSenderMethod => "命令发送者方法 (CommandSenderMethod)",
                NodeType.Command => "命令 (Command)",
                NodeType.CommandHandler => "命令处理器 (CommandHandler)",
                NodeType.Aggregate => "聚合根 (Aggregate)",
                NodeType.EntityMethod => "实体方法 (EntityMethod)",
                NodeType.DomainEvent => "领域事件 (DomainEvent)",
                NodeType.DomainEventHandler => "领域事件处理器 (DomainEventHandler)",
                NodeType.IntegrationEventConverter => "集成事件转换器 (IntegrationEventConverter)",
                NodeType.IntegrationEvent => "集成事件 (IntegrationEvent)",
                NodeType.IntegrationEventHandler => "集成事件处理器 (IntegrationEventHandler)",
                _ => type.ToString()
            };
        }
        
        private static string GetRelationshipTypeDisplayName(RelationshipType type)
        {
            return type switch
            {
                RelationshipType.ControllerToCommand => "控制器 → 命令",
                RelationshipType.ControllerMethodToCommand => "控制器方法 → 命令",
                RelationshipType.EndpointToCommand => "端点 → 命令",
                RelationshipType.CommandSenderToCommand => "命令发送者 → 命令",
                RelationshipType.CommandSenderMethodToCommand => "命令发送者方法 → 命令",
                RelationshipType.CommandToAggregate => "命令 → 聚合根",
                RelationshipType.CommandToEntityMethod => "命令 → 实体方法",
                RelationshipType.AggregateToDomainEvent => "聚合根 → 领域事件",
                RelationshipType.EntityMethodToEntityMethod => "实体方法 → 实体方法",
                RelationshipType.EntityMethodToDomainEvent => "实体方法 → 领域事件",
                RelationshipType.DomainEventToHandler => "领域事件 → 处理器",
                RelationshipType.DomainEventHandlerToCommand => "领域事件处理器 → 命令",
                RelationshipType.DomainEventToIntegrationEvent => "领域事件 → 集成事件",
                RelationshipType.IntegrationEventToHandler => "集成事件 → 处理器",
                RelationshipType.IntegrationEventHandlerToCommand => "集成事件处理器 → 命令",
                _ => type.ToString()
            };
        }
        
        private static string EscapeMarkdown(string text)
        {
            if (string.IsNullOrEmpty(text))
                return text;
            
            // Escape special markdown characters including pipe for tables
            return text
                .Replace("\\", "\\\\")
                .Replace("|", "\\|")
                .Replace("`", "\\`")
                .Replace("*", "\\*")
                .Replace("_", "\\_")
                .Replace("{", "\\{")
                .Replace("}", "\\}")
                .Replace("[", "\\[")
                .Replace("]", "\\]")
                .Replace("(", "\\(")
                .Replace(")", "\\)")
                .Replace("#", "\\#")
                .Replace("+", "\\+")
                .Replace("-", "\\-")
                .Replace(".", "\\.")
                .Replace("!", "\\!");
        }
        
        /// <summary>
        /// Safely parses a version string (format: yyyyMMddHHmmss) to DateTime
        /// </summary>
        private static bool TryParseVersionAsDateTime(string version, out DateTime dateTime)
        {
            return DateTime.TryParseExact(
                version, 
                "yyyyMMddHHmmss", 
                System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.None,
                out dateTime);
        }
    }
}

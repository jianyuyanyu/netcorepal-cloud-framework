using System;
using System.Linq;
using System.Text;

namespace NetCorePal.Extensions.CodeAnalysis
{
    /// <summary>
    /// 负责生成架构可视化 HTML 页面及相关样式
    /// </summary>
    public static class VisualizationHtmlBuilder
    {
        /// <summary>
        /// 生成架构可视化HTML页面
        /// </summary>
        /// <param name="analysisResult">分析结果（当withHistory=false或无快照时使用）</param>
        /// <param name="title">页面标题</param>
        /// <param name="maxEdges">最大边数</param>
        /// <param name="maxTextSize">最大文本大小</param>
        /// <param name="withHistory">是否包含历史快照（默认true）</param>
        /// <param name="snapshots">历史快照列表（当withHistory=true时使用）</param>
        /// <returns>HTML内容</returns>
        public static string GenerateVisualizationHtml(
            CodeFlowAnalysisResult analysisResult,
            string title = "系统模型架构图",
            int maxEdges = 5000,
            int maxTextSize = 1000000,
            bool withHistory = true,
            System.Collections.Generic.List<Snapshots.CodeFlowAnalysisSnapshot>? snapshots = null)
        {
            // 准备快照集合
            var snapshotList = new System.Collections.Generic.List<Snapshots.CodeFlowAnalysisSnapshot>();
            
            if (withHistory && snapshots != null && snapshots.Count > 0)
            {
                // 使用提供的快照
                snapshotList = snapshots;
            }
            else
            {
                // 没有快照时，使用当前分析结果构造一个快照
                var currentMetadata = new Snapshots.SnapshotMetadata
                {
                    Version = DateTime.Now.ToString("yyyyMMddHHmmss"),
                    Timestamp = DateTime.Now,
                    Description = "当前版本",
                    Hash = "",
                    NodeCount = analysisResult.Nodes.Count,
                    RelationshipCount = analysisResult.Relationships.Count
                };
                
                var currentSnapshot = new Snapshots.CodeFlowAnalysisSnapshot
                {
                    Metadata = currentMetadata,
                    AnalysisResult = analysisResult
                };
                
                snapshotList.Add(currentSnapshot);
            }

            // 使用最新快照生成Mermaid图表
            var latestResult = snapshotList[0].AnalysisResult;
            var architectureOverviewMermaid =
                MermaidVisualizers.ArchitectureOverviewMermaidVisualizer.GenerateMermaid(latestResult);
            var allProcessingFlowMermaid =
                MermaidVisualizers.ProcessingFlowMermaidVisualizer.GenerateMermaid(latestResult);
            var allAggregateMermaid =
                MermaidVisualizers.AggregateRelationMermaidVisualizer.GenerateAllAggregateMermaid(latestResult);

            // 读取嵌入资源模板内容
            var assembly = typeof(VisualizationHtmlBuilder).Assembly;
            var resourceName = assembly.GetManifestResourceNames()
                .FirstOrDefault(n => n.EndsWith("visualization-template.html", StringComparison.OrdinalIgnoreCase));
            if (resourceName == null)
            {
                throw new InvalidOperationException(
                    $"未找到嵌入的 visualization-template.html 资源。可用资源: {string.Join(", ", assembly.GetManifestResourceNames())}");
            }

            string template;
            using (var stream = assembly.GetManifestResourceStream(resourceName))
            {
                if (stream == null)
                {
                    throw new InvalidOperationException($"无法获取资源流: {resourceName}");
                }

                using (var reader = new System.IO.StreamReader(stream))
                {
                    template = reader.ReadToEnd();
                }
            }

            // 构建快照集合的JSON
            var snapshotsJson = BuildSnapshotsJson(snapshotList);
            var diagramConfigsJson = BuildDiagramConfigsJson();
            var diagramsJson = BuildArchitectureOverviewMermaidJson(architectureOverviewMermaid);
            var allChainFlowChartsJson = BuildProcessingFlowMermaidJson(allProcessingFlowMermaid);
            var allAggregateRelationDiagramsJson = BuildAllAggregateRelationDiagramsJson(allAggregateMermaid);

            // 替换模板中的占位符
            var html = template
                .Replace("{{TITLE}}", EscapeHtml(title))
                .Replace("{{MAX_EDGES}}", maxEdges.ToString())
                .Replace("{{MAX_TEXT_SIZE}}", maxTextSize.ToString())
                .Replace("{{SNAPSHOTS}}", snapshotsJson)
                .Replace("{{DIAGRAM_CONFIGS}}", diagramConfigsJson)
                .Replace("{{DIAGRAMS}}", diagramsJson)
                .Replace("{{ALL_CHAIN_FLOW_CHARTS}}", allChainFlowChartsJson)
                .Replace("{{ALL_AGGREGATE_RELATION_DIAGRAMS}}", allAggregateRelationDiagramsJson);

            return html;
        }

        // 构建快照集合的JSON字符串
        private static string BuildSnapshotsJson(System.Collections.Generic.List<Snapshots.CodeFlowAnalysisSnapshot> snapshots)
        {
            var sb = new StringBuilder();
            sb.Append("[");
            
            for (int i = 0; i < snapshots.Count; i++)
            {
                var snapshot = snapshots[i];
                sb.Append("{");
                
                // 元数据
                sb.Append("\"metadata\":{");
                sb.Append($"\"version\":\"{EscapeJavaScript(snapshot.Metadata.Version)}\",");
                sb.Append($"\"timestamp\":\"{snapshot.Metadata.Timestamp:yyyy-MM-dd HH:mm:ss}\",");
                sb.Append($"\"description\":\"{EscapeJavaScript(snapshot.Metadata.Description)}\",");
                sb.Append($"\"hash\":\"{EscapeJavaScript(snapshot.Metadata.Hash)}\",");
                sb.Append($"\"nodeCount\":{snapshot.Metadata.NodeCount},");
                sb.Append($"\"relationshipCount\":{snapshot.Metadata.RelationshipCount}");
                sb.Append("},");
                
                // 分析结果
                sb.Append("\"analysisResult\":");
                sb.Append(BuildAnalysisResultJson(snapshot.AnalysisResult));
                
                // 统计信息
                sb.Append(",\"statistics\":");
                sb.Append(BuildStatisticsJson(snapshot.AnalysisResult));
                
                sb.Append("}");
                
                if (i < snapshots.Count - 1)
                {
                    sb.Append(",");
                }
            }
            
            sb.Append("]");
            return sb.ToString();
        }

        // 构建 analysisResult 的 JSON 字符串
        private static string BuildAnalysisResultJson(CodeFlowAnalysisResult analysisResult)
        {
            var sb = new StringBuilder();
            sb.Append("{");
            sb.Append("\"nodes\":[");
            for (int i = 0; i < analysisResult.Nodes.Count; i++)
            {
                var node = analysisResult.Nodes[i];
                string nodeTypeStr = node.Type.ToString();
                sb.Append(
                    $"{{\"id\":\"{EscapeJavaScript(node.Id ?? string.Empty)}\",\"name\":\"{EscapeJavaScript(node.Name ?? string.Empty)}\",\"fullName\":\"{EscapeJavaScript(node.FullName ?? string.Empty)}\",\"type\":\"{EscapeJavaScript(nodeTypeStr)}\"}}");
                if (i < analysisResult.Nodes.Count - 1) sb.Append(",");
            }

            sb.Append("],\"relationships\":[");
            for (int i = 0; i < analysisResult.Relationships.Count; i++)
            {
                var rel = analysisResult.Relationships[i];
                string relTypeStr = rel.Type.ToString();
                sb.Append(
                    $"{{\"from\":\"{EscapeJavaScript(rel.FromNode?.Id ?? string.Empty)}\",\"to\":\"{EscapeJavaScript(rel.ToNode?.Id ?? string.Empty)}\",\"type\":\"{EscapeJavaScript(relTypeStr)}\"}}");
                if (i < analysisResult.Relationships.Count - 1) sb.Append(",");
            }

            sb.Append("]}");
            return sb.ToString();
        }

        // 构建统计信息的 JSON 字符串
        private static string BuildStatisticsJson(CodeFlowAnalysisResult analysisResult)
        {
            var nodeStats = analysisResult.Nodes
                .GroupBy(n => n.Type)
                .ToDictionary(g => g.Key.ToString(), g => g.Count());

            var relationshipStats = analysisResult.Relationships
                .GroupBy(r => r.Type)
                .ToDictionary(g => g.Key.ToString(), g => g.Count());

            var sb = new StringBuilder();
            sb.Append("{");
            sb.Append("\"nodeStats\":{");
            var nodeStatsArray = nodeStats.ToArray();
            for (int i = 0; i < nodeStatsArray.Length; i++)
            {
                var kvp = nodeStatsArray[i];
                sb.Append($"\"{EscapeJavaScript(kvp.Key)}\":{kvp.Value}");
                if (i < nodeStatsArray.Length - 1) sb.Append(",");
            }
            sb.Append("},");
            
            sb.Append("\"relationshipStats\":{");
            var relationshipStatsArray = relationshipStats.ToArray();
            for (int i = 0; i < relationshipStatsArray.Length; i++)
            {
                var kvp = relationshipStatsArray[i];
                sb.Append($"\"{EscapeJavaScript(kvp.Key)}\":{kvp.Value}");
                if (i < relationshipStatsArray.Length - 1) sb.Append(",");
            }
            sb.Append("},");
            
            sb.Append($"\"totalElements\":{analysisResult.Nodes.Count},");
            sb.Append($"\"totalRelationships\":{analysisResult.Relationships.Count}");
            sb.Append("}");
            return sb.ToString();
        }

        // 构建 diagramConfigs 的 JSON 字符串
        private static string BuildDiagramConfigsJson()
        {
            return "{" +
                   "\"Statistics\":{\"title\":'统计信息',\"description\":'展示各个要素的统计信息'}," +
                   "\"ArchitectureOverview\":{\"title\":'架构大图',\"description\":'展示系统中所有类型及其关系的完整视图'}," +
                   "\"command\":{\"title\":'命令关系图',\"description\":'展示命令在系统中的完整流转与关系'}" +
                   "}";
        }

        // 构建 diagrams 的 JSON 字符串
        private static string BuildArchitectureOverviewMermaidJson(string classDiagram)
        {
            return $"{{\"ArchitectureOverview\":`{EscapeJavaScriptTemplate(classDiagram)}`}}";
        }

        // 构建 allChainFlowCharts 的 JSON 字符串
        private static string BuildProcessingFlowMermaidJson(
            System.Collections.Generic.List<(string ChainName, string Diagram)> allProcessingFlowDiagrams)
        {
            var sb = new StringBuilder();
            sb.Append("[");
            for (int i = 0; i < allProcessingFlowDiagrams.Count; i++)
            {
                var (chainName, diagram) = allProcessingFlowDiagrams[i];
                sb.Append(
                    $"{{\"name\":\"{EscapeJavaScript(chainName)}\",\"diagram\":`{EscapeJavaScriptTemplate(diagram)}`}}");
                if (i < allProcessingFlowDiagrams.Count - 1) sb.Append(",");
            }

            sb.Append("]");
            return sb.ToString();
        }

        // 构建 allAggregateRelationDiagrams 的 JSON 字符串
        private static string BuildAllAggregateRelationDiagramsJson(
            System.Collections.Generic.List<(string AggregateName, string Diagram)> allAggregateRelationDiagrams)
        {
            var sb = new StringBuilder();
            sb.Append("[");
            for (int i = 0; i < allAggregateRelationDiagrams.Count; i++)
            {
                var (aggName, diagram) = allAggregateRelationDiagrams[i];
                sb.Append(
                    $"{{\"name\":\"{EscapeJavaScript(aggName)}\",\"diagram\":`{EscapeJavaScriptTemplate(diagram)}`}}");
                if (i < allAggregateRelationDiagrams.Count - 1) sb.Append(",");
            }

            sb.Append("]");
            return sb.ToString();
        }

        /// <summary>
        /// HTML转义
        /// </summary>
        private static string EscapeHtml(string text)
        {
            if (string.IsNullOrEmpty(text)) return text;
            return text.Replace("&", "&amp;")
                .Replace("<", "&lt;")
                .Replace(">", "&gt;")
                .Replace("\"", "&quot;")
                .Replace("'", "&#39;");
        }

        /// <summary>
        /// JavaScript字符串转义
        /// </summary>
        private static string EscapeJavaScript(string text)
        {
            if (string.IsNullOrEmpty(text)) return text;
            return text.Replace("\\", "\\\\")
                .Replace("\"", "\\\"")
                .Replace("'", "\\'")
                .Replace("\n", "\\n")
                .Replace("\r", "\\r")
                .Replace("\t", "\\t")
                .Replace("<", "\\u003c")
                .Replace(">", "\\u003e");
        }

        /// <summary>
        /// JavaScript模板字符串转义
        /// </summary>
        private static string EscapeJavaScriptTemplate(string text)
        {
            if (string.IsNullOrEmpty(text)) return text;
            return text.Replace("\\", "\\\\")
                .Replace("`", "\\`")
                .Replace("${", "\\${");
        }
    }
}

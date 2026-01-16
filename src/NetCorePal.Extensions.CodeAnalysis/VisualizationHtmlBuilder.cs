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
        public static string GenerateVisualizationHtml(CodeFlowAnalysisResult analysisResult,
            string title = "系统模型架构图",
            int maxEdges = 5000,
            int maxTextSize = 1000000)
        {
            // 生成所有类型的图表，直接调用各 Visualizer
            var architectureOverviewMermaid =
                MermaidVisualizers.ArchitectureOverviewMermaidVisualizer.GenerateMermaid(analysisResult);
            var allProcessingFlowMermaid =
                MermaidVisualizers.ProcessingFlowMermaidVisualizer.GenerateMermaid(analysisResult);
            var allAggregateMermaid =
                MermaidVisualizers.AggregateRelationMermaidVisualizer.GenerateAllAggregateMermaid(analysisResult);

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

            // 构建各部分内容
            var analysisResultJson = BuildAnalysisResultJson(analysisResult);
            var statisticsJson = BuildStatisticsJson(analysisResult);
            var diagramConfigsJson = BuildDiagramConfigsJson();
            var diagramsJson = BuildArchitectureOverviewMermaidJson(architectureOverviewMermaid);
            var allChainFlowChartsJson = BuildProcessingFlowMermaidJson(allProcessingFlowMermaid);
            var allAggregateRelationDiagramsJson = BuildAllAggregateRelationDiagramsJson(allAggregateMermaid);

            // 替换模板中的占位符
            var html = template
                .Replace("{{TITLE}}", EscapeHtml(title))
                .Replace("{{MAX_EDGES}}", maxEdges.ToString())
                .Replace("{{MAX_TEXT_SIZE}}", maxTextSize.ToString())
                .Replace("{{ANALYSIS_RESULT}}", analysisResultJson)
                .Replace("{{STATISTICS}}", statisticsJson)
                .Replace("{{DIAGRAM_CONFIGS}}", diagramConfigsJson)
                .Replace("{{DIAGRAMS}}", diagramsJson)
                .Replace("{{ALL_CHAIN_FLOW_CHARTS}}", allChainFlowChartsJson)
                .Replace("{{ALL_AGGREGATE_RELATION_DIAGRAMS}}", allAggregateRelationDiagramsJson);

            return html;
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

        /// <summary>
        /// 生成支持多版本快照的HTML可视化页面
        /// </summary>
        /// <param name="snapshots">历史快照列表（最新的在前面）</param>
        /// <param name="title">页面标题</param>
        /// <param name="maxEdges">最大边数</param>
        /// <param name="maxTextSize">最大文本大小</param>
        /// <returns>HTML内容</returns>
        public static string GenerateVisualizationHtmlWithHistory(
            System.Collections.Generic.List<Snapshots.CodeFlowAnalysisSnapshot> snapshots,
            string title = "系统架构演进图",
            int maxEdges = 5000,
            int maxTextSize = 1000000)
        {
            if (snapshots == null || snapshots.Count == 0)
            {
                throw new ArgumentException("至少需要一个快照", nameof(snapshots));
            }

            // 使用最新的快照生成基础HTML
            var latestSnapshot = snapshots[0];
            var baseHtml = GenerateVisualizationHtml(latestSnapshot.AnalysisResult, title, maxEdges, maxTextSize);

            // 如果只有一个快照，直接返回基础HTML
            if (snapshots.Count == 1)
            {
                return baseHtml;
            }

            // 生成历史数据的JavaScript代码
            var historyScript = BuildHistoryScript(snapshots);

            // 在</body>标签前注入历史功能
            var closingBodyIndex = baseHtml.LastIndexOf("</body>", StringComparison.OrdinalIgnoreCase);
            if (closingBodyIndex > 0)
            {
                return baseHtml.Insert(closingBodyIndex, historyScript);
            }

            return baseHtml + historyScript;
        }

        private static string BuildHistoryScript(System.Collections.Generic.List<Snapshots.CodeFlowAnalysisSnapshot> snapshots)
        {
            var sb = new StringBuilder();
            sb.AppendLine("<script>");
            sb.AppendLine("// 历史快照数据");
            sb.AppendLine("const historySnapshots = [");

            for (int i = 0; i < snapshots.Count; i++)
            {
                var snapshot = snapshots[i];
                sb.AppendLine($"  {{");
                sb.AppendLine($"    version: '{EscapeJavaScript(snapshot.Metadata.Version)}',");
                sb.AppendLine($"    timestamp: '{EscapeJavaScript(snapshot.Metadata.Timestamp.ToString("yyyy-MM-dd HH:mm:ss"))}',");
                sb.AppendLine($"    description: '{EscapeJavaScript(snapshot.Metadata.Description)}',");
                sb.AppendLine($"    nodeCount: {snapshot.Metadata.NodeCount},");
                sb.AppendLine($"    relationshipCount: {snapshot.Metadata.RelationshipCount}");
                sb.Append($"  }}");
                if (i < snapshots.Count - 1)
                {
                    sb.AppendLine(",");
                }
                else
                {
                    sb.AppendLine();
                }
            }

            sb.AppendLine("];");
            sb.AppendLine();
            sb.AppendLine(@"
// 当前选择的快照索引
let currentSnapshotIndex = 0;

// 初始化历史版本选择器
function initHistoryVersionSelector() {
    const controlPanel = document.createElement('div');
    controlPanel.id = 'history-control-panel';
    controlPanel.style.cssText = `
        position: fixed;
        top: 60px;
        right: 10px;
        background: white;
        border: 1px solid #ccc;
        border-radius: 5px;
        padding: 15px;
        box-shadow: 0 2px 10px rgba(0,0,0,0.1);
        z-index: 9999;
        min-width: 280px;
        font-family: Arial, sans-serif;
    `;

    const versionOptions = historySnapshots.map((s, i) => 
        `<option value='${i}' ${i === 0 ? 'selected' : ''}>${s.version} - ${s.description}</option>`
    ).join('');

    controlPanel.innerHTML = `
        <div style='font-weight: bold; margin-bottom: 10px; font-size: 14px; color: #333;'>
            📊 版本历史
        </div>
        <div style='margin-bottom: 10px;'>
            <select id='snapshot-selector' style='width: 100%; padding: 5px; border: 1px solid #ccc; border-radius: 3px; font-size: 13px;'>
                ${versionOptions}
            </select>
        </div>
        <div id='snapshot-info' style='font-size: 12px; color: #666; padding: 8px; background: #f5f5f5; border-radius: 3px;'>
            <div style='margin-bottom: 5px;'><strong>时间:</strong> <span id='snapshot-time'></span></div>
            <div style='margin-bottom: 5px;'><strong>节点:</strong> <span id='snapshot-nodes'></span></div>
            <div><strong>关系:</strong> <span id='snapshot-rels'></span></div>
        </div>
        <div style='margin-top: 10px; font-size: 11px; color: #999; padding: 5px; background: #fffbe6; border-radius: 3px; border: 1px solid #ffe58f;'>
            💡 选择版本后请重新点击要查看的图表
        </div>
    `;

    document.body.appendChild(controlPanel);

    // 绑定选择器变化事件
    document.getElementById('snapshot-selector').addEventListener('change', (e) => {
        currentSnapshotIndex = parseInt(e.target.value);
        updateSnapshotInfo();
        console.log('Switched to snapshot version:', historySnapshots[currentSnapshotIndex].version);
    });

    updateSnapshotInfo();
}

// 更新快照信息显示
function updateSnapshotInfo() {
    const snapshot = historySnapshots[currentSnapshotIndex];
    document.getElementById('snapshot-time').textContent = snapshot.timestamp;
    document.getElementById('snapshot-nodes').textContent = snapshot.nodeCount;
    document.getElementById('snapshot-rels').textContent = snapshot.relationshipCount;
}

// 页面加载后初始化
if (document.readyState === 'loading') {
    document.addEventListener('DOMContentLoaded', initHistoryVersionSelector);
} else {
    initHistoryVersionSelector();
}
");
            sb.AppendLine("</script>");
            return sb.ToString();
        }
    }
}
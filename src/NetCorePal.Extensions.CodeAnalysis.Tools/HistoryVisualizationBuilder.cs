using System;
using System.Collections.Generic;
using System.Linq;
using NetCorePal.Extensions.CodeAnalysis;
using NetCorePal.Extensions.CodeAnalysis.Tools.Snapshots;

namespace NetCorePal.Extensions.CodeAnalysis.Tools;

/// <summary>
/// 生成带历史记录的HTML可视化页面
/// </summary>
public static class HistoryVisualizationBuilder
{
    /// <summary>
    /// 生成包含历史快照的HTML可视化页面
    /// </summary>
    public static string GenerateHistoryVisualizationHtml(
        string snapshotDir,
        string title = "系统架构演进图",
        int maxEdges = 5000,
        int maxTextSize = 1000000)
    {
        var storage = new SnapshotStorage(snapshotDir);
        var snapshots = storage.ListSnapshots();

        if (snapshots.Count == 0)
        {
            throw new InvalidOperationException("No snapshots found. Please create a snapshot first using 'snapshot add' command.");
        }

        // 加载所有快照数据
        var snapshotDataList = new List<(SnapshotMetadata Metadata, CodeFlowAnalysisResult Result)>();
        foreach (var metadata in snapshots)
        {
            var snapshot = storage.LoadSnapshot(metadata.Version);
            if (snapshot != null)
            {
                snapshotDataList.Add((snapshot.Metadata, snapshot.AnalysisResult));
            }
        }

        // 使用最新快照生成基础HTML
        var latestSnapshot = snapshotDataList[0];
        var baseHtml = VisualizationHtmlBuilder.GenerateVisualizationHtml(
            latestSnapshot.Result,
            title,
            maxEdges,
            maxTextSize);

        // 注入历史数据和控制逻辑
        var historyScript = GenerateHistoryScript(snapshotDataList);
        var historyHtml = InjectHistoryFeatures(baseHtml, historyScript);

        return historyHtml;
    }

    private static string GenerateHistoryScript(List<(SnapshotMetadata Metadata, CodeFlowAnalysisResult Result)> snapshots)
    {
        var script = @"
<script>
// 历史快照数据
const historySnapshots = [";

        for (int i = 0; i < snapshots.Count; i++)
        {
            var (metadata, result) = snapshots[i];
            script += $@"
    {{
        version: '{metadata.Version}',
        timestamp: '{metadata.Timestamp:yyyy-MM-dd HH:mm:ss}',
        description: '{EscapeJavaScript(metadata.Description)}',
        nodeCount: {metadata.NodeCount},
        relationshipCount: {metadata.RelationshipCount},
        nodes: {SerializeNodes(result.Nodes)},
        relationships: {SerializeRelationships(result.Relationships)}
    }}";
            if (i < snapshots.Count - 1)
            {
                script += ",";
            }
        }

        script += @"
];

// 当前显示的快照索引
let currentSnapshotIndex = 0;

// 初始化历史控制面板
function initHistoryPanel() {
    const controlPanel = document.createElement('div');
    controlPanel.id = 'history-control-panel';
    controlPanel.style.cssText = `
        position: fixed;
        top: 10px;
        right: 10px;
        background: white;
        border: 1px solid #ccc;
        border-radius: 5px;
        padding: 15px;
        box-shadow: 0 2px 10px rgba(0,0,0,0.1);
        z-index: 10000;
        min-width: 300px;
    `;

    controlPanel.innerHTML = `
        <div style='font-weight: bold; margin-bottom: 10px; font-size: 16px;'>📊 历史版本</div>
        <div style='margin-bottom: 10px;'>
            <label for='snapshot-selector'>版本选择:</label>
            <select id='snapshot-selector' style='width: 100%; padding: 5px; margin-top: 5px;'>
                ${historySnapshots.map((s, i) => 
                    `<option value='${i}'>${s.version} - ${s.description}</option>`
                ).join('')}
            </select>
        </div>
        <div id='snapshot-info' style='font-size: 12px; color: #666; margin-top: 10px;'>
            <div>时间: <span id='snapshot-time'></span></div>
            <div>节点: <span id='snapshot-nodes'></span></div>
            <div>关系: <span id='snapshot-rels'></span></div>
        </div>
        <div style='margin-top: 10px; display: flex; gap: 5px;'>
            <button id='prev-snapshot' style='flex: 1; padding: 5px;'>← 上一版本</button>
            <button id='next-snapshot' style='flex: 1; padding: 5px;'>下一版本 →</button>
        </div>
        <div style='margin-top: 10px;'>
            <button id='show-trend' style='width: 100%; padding: 8px; background: #4CAF50; color: white; border: none; border-radius: 3px; cursor: pointer;'>
                📈 查看趋势图
            </button>
        </div>
    `;

    document.body.appendChild(controlPanel);

    // 绑定事件
    document.getElementById('snapshot-selector').addEventListener('change', (e) => {
        currentSnapshotIndex = parseInt(e.target.value);
        updateVisualization();
    });

    document.getElementById('prev-snapshot').addEventListener('click', () => {
        if (currentSnapshotIndex < historySnapshots.length - 1) {
            currentSnapshotIndex++;
            document.getElementById('snapshot-selector').selectedIndex = currentSnapshotIndex;
            updateVisualization();
        }
    });

    document.getElementById('next-snapshot').addEventListener('click', () => {
        if (currentSnapshotIndex > 0) {
            currentSnapshotIndex--;
            document.getElementById('snapshot-selector').selectedIndex = currentSnapshotIndex;
            updateVisualization();
        }
    });

    document.getElementById('show-trend').addEventListener('click', () => {
        showTrendChart();
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

// 更新可视化（简化版，仅更新数据引用）
function updateVisualization() {
    const snapshot = historySnapshots[currentSnapshotIndex];
    
    // 更新全局 analysisResult 变量
    if (window.analysisResult) {
        window.analysisResult.nodes = snapshot.nodes;
        window.analysisResult.relationships = snapshot.relationships;
    }
    
    updateSnapshotInfo();
    
    // 提示用户需要重新渲染图表
    alert('快照已切换至 ' + snapshot.version + '\\n请刷新页面或重新选择要显示的图表以查看该版本的架构。');
}

// 显示趋势图
function showTrendChart() {
    const trendWindow = window.open('', '趋势图', 'width=1000,height=600');
    const trendHtml = generateTrendChart();
    trendWindow.document.write(trendHtml);
}

// 生成趋势图HTML
function generateTrendChart() {
    const nodeData = historySnapshots.map(s => s.nodeCount).reverse();
    const relData = historySnapshots.map(s => s.relationshipCount).reverse();
    const labels = historySnapshots.map(s => s.version).reverse();

    return `
<!DOCTYPE html>
<html>
<head>
    <title>架构演进趋势图</title>
    <script src='https://cdn.jsdelivr.net/npm/chart.js@3.9.1/dist/chart.min.js'></script>
</head>
<body style='padding: 20px;'>
    <h2>架构演进趋势图</h2>
    <canvas id='trendChart' style='max-height: 500px;'></canvas>
    
    <script>
        const ctx = document.getElementById('trendChart').getContext('2d');
        new Chart(ctx, {
            type: 'line',
            data: {
                labels: ${JSON.stringify(labels)},
                datasets: [
                    {
                        label: '节点数量',
                        data: ${JSON.stringify(nodeData)},
                        borderColor: 'rgb(75, 192, 192)',
                        backgroundColor: 'rgba(75, 192, 192, 0.2)',
                        tension: 0.1
                    },
                    {
                        label: '关系数量',
                        data: ${JSON.stringify(relData)},
                        borderColor: 'rgb(255, 99, 132)',
                        backgroundColor: 'rgba(255, 99, 132, 0.2)',
                        tension: 0.1
                    }
                ]
            },
            options: {
                responsive: true,
                plugins: {
                    title: {
                        display: true,
                        text: '架构复杂度变化趋势'
                    }
                },
                scales: {
                    y: {
                        beginAtZero: true
                    }
                }
            }
        });
    </script>
</body>
</html>
    `;
}

// 页面加载后初始化
if (document.readyState === 'loading') {
    document.addEventListener('DOMContentLoaded', initHistoryPanel);
} else {
    initHistoryPanel();
}
</script>
";
        return script;
    }

    private static string InjectHistoryFeatures(string baseHtml, string historyScript)
    {
        // 在 </body> 标签前注入历史功能脚本
        var closingBodyIndex = baseHtml.LastIndexOf("</body>", StringComparison.OrdinalIgnoreCase);
        if (closingBodyIndex > 0)
        {
            return baseHtml.Insert(closingBodyIndex, historyScript);
        }
        return baseHtml + historyScript;
    }

    private static string SerializeNodes(List<Node> nodes)
    {
        var items = nodes.Select(n =>
            $"{{id:'{EscapeJavaScript(n.Id)}',name:'{EscapeJavaScript(n.Name)}',fullName:'{EscapeJavaScript(n.FullName)}',type:'{n.Type}'}}");
        return "[" + string.Join(",", items) + "]";
    }

    private static string SerializeRelationships(List<Relationship> relationships)
    {
        var items = relationships.Select(r =>
            $"{{from:'{EscapeJavaScript(r.FromNode.Id)}',to:'{EscapeJavaScript(r.ToNode.Id)}',type:'{r.Type}'}}");
        return "[" + string.Join(",", items) + "]";
    }

    private static string EscapeJavaScript(string text)
    {
        if (string.IsNullOrEmpty(text)) return string.Empty;
        return text.Replace("\\", "\\\\")
            .Replace("'", "\\'")
            .Replace("\"", "\\\"")
            .Replace("\n", "\\n")
            .Replace("\r", "\\r");
    }
}

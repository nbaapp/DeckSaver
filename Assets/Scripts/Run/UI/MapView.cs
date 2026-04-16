using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Renders the run map inside a UI panel.
///
/// Layout:
///   _mapArea  — a child RectTransform that defines the drawable map space.
///               All node and edge views are spawned as children of this transform.
///               Set anchors to fill the panel or a fixed sub-area.
///
/// Virtual coordinates (0..mapWidth, 0..mapHeight) are scaled to fit _mapArea's rect.
/// Edges are drawn first (behind nodes) via MapEdgeView; nodes are drawn second.
///
/// The panel is shown/hidden via Show() and Hide().
/// </summary>
public class MapView : MonoBehaviour
{
    [Header("Map Area")]
    [Tooltip("RectTransform that defines the drawable map space. All nodes/edges spawn here.")]
    [SerializeField] private RectTransform _mapArea;

    [Header("Prefabs")]
    [SerializeField] private GameObject _nodeViewPrefab;
    [SerializeField] private GameObject _edgeViewPrefab;

    [Header("Edge Colors")]
    [SerializeField] private Color _edgeColor          = new Color(0.6f, 0.6f, 0.6f, 0.8f);
    [SerializeField] private Color _edgeReachableColor = new Color(1f, 1f, 1f, 0.9f);

    // ── State ─────────────────────────────────────────────────────────────────

    private MapGraph              _map;
    private RunConfig             _config;
    private Action<MapNode>       _onNodeSelected;

    private readonly List<GameObject> _spawnedViews = new();
    // Maps node Id → anchored position in _mapArea local space
    private readonly Dictionary<int, Vector2> _nodeCanvasPositions = new();

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>Render the map and start accepting player input.</summary>
    public void Show(MapGraph map, RunConfig config, Action<MapNode> onNodeSelected)
    {
        _map            = map;
        _config         = config;
        _onNodeSelected = onNodeSelected;
        gameObject.SetActive(true);

        // Ensure layout is computed before we read rects
        Canvas.ForceUpdateCanvases();
        Rebuild();
    }

    public void Hide() => gameObject.SetActive(false);

    // ── Build ─────────────────────────────────────────────────────────────────

    private void Rebuild()
    {
        // Clear previous views
        foreach (var v in _spawnedViews) Destroy(v);
        _spawnedViews.Clear();
        _nodeCanvasPositions.Clear();

        if (_map == null || _mapArea == null) return;

        Rect area = _mapArea.rect;

        // Pre-compute canvas positions for all nodes
        foreach (var node in _map.Nodes)
            _nodeCanvasPositions[node.Id] = VirtualToCanvas(node.Position, area);

        var reachable   = new HashSet<int>();
        var reachNodes  = _map.GetReachableNodes();
        foreach (var n in reachNodes) reachable.Add(n.Id);

        // Spawn edges (behind nodes — add before nodes so they render beneath)
        SpawnEdges(reachable);

        // Spawn nodes
        SpawnNodes(reachable);
    }

    private void SpawnEdges(HashSet<int> reachableIds)
    {
        if (_edgeViewPrefab == null) return;

        // Only draw lines from the current node to each reachable neighbor.
        // Players see all node dots but only the connections they can actually take.
        var currentNode = _map.CurrentNode;
        if (currentNode == null) return;

        if (!_nodeCanvasPositions.TryGetValue(currentNode.Id, out var currentPos)) return;

        foreach (var neighborId in currentNode.NeighborIds)
        {
            if (!reachableIds.Contains(neighborId)) continue;
            if (!_nodeCanvasPositions.TryGetValue(neighborId, out var neighborPos)) continue;

            var go   = Instantiate(_edgeViewPrefab, _mapArea);
            var edge = go.GetComponent<MapEdgeView>();
            edge?.Set(currentPos, neighborPos, _edgeReachableColor);
            _spawnedViews.Add(go);
        }
    }

    private void SpawnNodes(HashSet<int> reachableIds)
    {
        if (_nodeViewPrefab == null) return;

        int currentId = _map.CurrentNodeId;

        foreach (var node in _map.Nodes)
        {
            if (!_nodeCanvasPositions.TryGetValue(node.Id, out var pos)) continue;

            bool isReachable = reachableIds.Contains(node.Id);
            bool isCurrent   = node.Id == currentId;

            var capturedNode = node;
            var go           = Instantiate(_nodeViewPrefab, _mapArea);
            var view         = go.GetComponent<MapNodeView>();

            view?.Setup(node, isReachable, isCurrent, () => OnNodeClicked(capturedNode));

            var rt = go.GetComponent<RectTransform>();
            if (rt != null) rt.anchoredPosition = pos;

            _spawnedViews.Add(go);
        }
    }

    // ── Interaction ───────────────────────────────────────────────────────────

    private void OnNodeClicked(MapNode node)
    {
        _onNodeSelected?.Invoke(node);
    }

    // ── Coordinate conversion ─────────────────────────────────────────────────

    /// <summary>
    /// Convert a virtual map position to a local anchored position within _mapArea.
    /// Assumes _mapArea pivot is at its centre (0.5, 0.5).
    /// </summary>
    private Vector2 VirtualToCanvas(Vector2 virtualPos, Rect area)
    {
        float x = (virtualPos.x / _config.mapWidth)  * area.width  + area.xMin;
        float y = (virtualPos.y / _config.mapHeight) * area.height + area.yMin;
        return new Vector2(x, y);
    }
}

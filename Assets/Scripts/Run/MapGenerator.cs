using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// Procedurally generates the run map graph.
///
/// Generation steps:
///   1. Place the Start node at the far left, vertically centred. No encounter.
///   2. Place the Boss node at the far right.
///   3. Scatter (N-2) content nodes across the middle of the map.
///   4. Assign node types and encounters to content nodes (before edges, to avoid type confusion).
///   5. Build edges among non-Start nodes within reachRadius.
///   6. Guarantee connectivity among non-Start nodes.
///   7. Connect Start to its nearest few content nodes only.
///   8. Pre-enter the Start node so GetReachableNodes() returns its neighbors immediately.
/// </summary>
public static class MapGenerator
{
    public static MapGraph Generate(RunConfig config)
    {
        int totalNodes   = Random.Range(config.minNodes, config.maxNodes + 1);
        int contentCount = totalNodes - 2; // excludes Start and Boss

        var graph = new MapGraph();

        // ── 1. Start node (far left, vertically centred) ──────────────────────
        var startNode = new MapNode
        {
            Id       = 0,
            Type     = NodeType.Start,
            Position = new Vector2(config.mapWidth * 0.05f, config.mapHeight * 0.5f),
        };
        graph.Nodes.Add(startNode);
        graph.StartNodeId = 0;

        // ── 2. Boss node (far right) ──────────────────────────────────────────
        float bossY = Random.Range(config.mapHeight * 0.25f, config.mapHeight * 0.75f);
        var bossNode = new MapNode
        {
            Id        = 1,
            Type      = NodeType.Boss,
            Position  = new Vector2(config.mapWidth * 0.92f, bossY),
            Encounter = config.bossEncounter,
        };
        graph.Nodes.Add(bossNode);
        graph.BossNodeId = 1;

        // ── 3. Content nodes ──────────────────────────────────────────────────
        float minX = config.mapWidth * 0.15f;
        float maxX = config.mapWidth * 0.82f;

        for (int i = 2; i < contentCount + 2; i++)
        {
            Vector2 pos = FindValidPosition(graph.Nodes, config, minX, maxX);
            graph.Nodes.Add(new MapNode { Id = i, Position = pos });
        }

        // ── 4. Assign types and encounters (before edges, uses ID-based filter)
        AssignTypesAndEncounters(graph, config);

        // ── 5. Build edges (content + boss only; Start wired separately) ──────
        BuildEdges(graph, config.reachRadius);

        // ── 6. Guarantee connectivity among content+boss nodes ────────────────
        EnsureConnectivity(graph, excludeId: graph.StartNodeId);

        // ── 7. Connect Start to its nearest content nodes only ────────────────
        ConnectStartNode(graph, startNode, connectionCount: 3);

        // ── 8. Pre-enter Start so its neighbors are immediately reachable ──────
        graph.CurrentNodeId = startNode.Id;
        startNode.Visited   = true;

        return graph;
    }

    // ── Step 3: Position placement ────────────────────────────────────────────

    private static Vector2 FindValidPosition(List<MapNode> existing, RunConfig config,
        float minX, float maxX, int maxAttempts = 60)
    {
        for (int attempt = 0; attempt < maxAttempts; attempt++)
        {
            var candidate = new Vector2(
                Random.Range(minX, maxX),
                Random.Range(0f, config.mapHeight)
            );

            bool tooClose = false;
            foreach (var node in existing)
            {
                if (Vector2.Distance(candidate, node.Position) < config.minNodeSpacing)
                {
                    tooClose = true;
                    break;
                }
            }

            if (!tooClose) return candidate;
        }

        return new Vector2(Random.Range(minX, maxX), Random.Range(0f, config.mapHeight));
    }

    // ── Step 4: Type and encounter assignment (ID-based filter) ──────────────

    private static void AssignTypesAndEncounters(MapGraph graph, RunConfig config)
    {
        var typePool = new List<NodeType>();
        AddWeighted(typePool, NodeType.StandardConflict, config.weightStandard);
        AddWeighted(typePool, NodeType.HardConflict,     config.weightHard);
        AddWeighted(typePool, NodeType.Camp,             config.weightCamp);
        AddWeighted(typePool, NodeType.Shop,             config.weightShop);
        AddWeighted(typePool, NodeType.Event,            config.weightEvent);

        Shuffle(typePool);

        // Filter by ID — avoids relying on default enum values for untyped nodes
        var contentNodes = graph.Nodes
            .Where(n => n.Id != graph.StartNodeId && n.Id != graph.BossNodeId)
            .ToList();

        for (int i = 0; i < contentNodes.Count; i++)
        {
            var node = contentNodes[i];
            node.Type = typePool[i % typePool.Count];

            node.Encounter = node.Type switch
            {
                NodeType.StandardConflict => PickRandom(config.standardConflictPool),
                NodeType.HardConflict     => PickRandom(config.hardConflictPool),
                NodeType.Event            => PickRandom(config.eventPool),
                _                         => null,
            };
        }
    }

    // ── Step 5: Edge building (skips the Start node) ─────────────────────────

    private static void BuildEdges(MapGraph graph, float reachRadius)
    {
        foreach (var node in graph.Nodes)
            node.NeighborIds.Clear();

        var eligible = graph.Nodes.FindAll(n => n.Id != graph.StartNodeId);

        for (int i = 0; i < eligible.Count; i++)
        {
            for (int j = i + 1; j < eligible.Count; j++)
            {
                float dist = Vector2.Distance(eligible[i].Position, eligible[j].Position);
                if (dist <= reachRadius)
                {
                    eligible[i].NeighborIds.Add(eligible[j].Id);
                    eligible[j].NeighborIds.Add(eligible[i].Id);
                }
            }
        }
    }

    // ── Step 6: Connectivity guarantee (among non-Start nodes) ───────────────

    private static void EnsureConnectivity(MapGraph graph, int excludeId)
    {
        var nodes = graph.Nodes.FindAll(n => n.Id != excludeId);
        if (nodes.Count == 0) return;

        for (int safetyLimit = 0; safetyLimit < nodes.Count; safetyLimit++)
        {
            var reachable = BfsReachable(graph, nodes[0]);
            if (reachable.Count >= nodes.Count) break;

            MapNode isolated = null;
            foreach (var node in nodes)
            {
                if (!reachable.Contains(node)) { isolated = node; break; }
            }
            if (isolated == null) break;

            MapNode nearest     = null;
            float   nearestDist = float.MaxValue;
            foreach (var r in reachable)
            {
                if (r.Id == excludeId) continue;
                float d = Vector2.Distance(isolated.Position, r.Position);
                if (d < nearestDist) { nearestDist = d; nearest = r; }
            }

            if (nearest != null)
            {
                isolated.NeighborIds.Add(nearest.Id);
                nearest.NeighborIds.Add(isolated.Id);
            }
        }
    }

    private static HashSet<MapNode> BfsReachable(MapGraph graph, MapNode start)
    {
        var visited = new HashSet<MapNode> { start };
        var queue   = new Queue<MapNode>();
        queue.Enqueue(start);

        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            foreach (var neighborId in current.NeighborIds)
            {
                var neighbor = graph.GetNode(neighborId);
                if (neighbor != null && visited.Add(neighbor))
                    queue.Enqueue(neighbor);
            }
        }

        return visited;
    }

    // ── Step 7: Connect Start to nearest content nodes ────────────────────────

    private static void ConnectStartNode(MapGraph graph, MapNode startNode, int connectionCount)
    {
        var candidates = graph.Nodes
            .FindAll(n => n.Id != startNode.Id && n.Id != graph.BossNodeId);

        candidates.Sort((a, b) =>
            Vector2.Distance(a.Position, startNode.Position)
                .CompareTo(Vector2.Distance(b.Position, startNode.Position)));

        int connections = System.Math.Min(connectionCount, candidates.Count);
        for (int i = 0; i < connections; i++)
        {
            startNode.NeighborIds.Add(candidates[i].Id);
            candidates[i].NeighborIds.Add(startNode.Id);
        }
    }

    // ── Utilities ─────────────────────────────────────────────────────────────

    private static void AddWeighted<T>(List<T> list, T item, int weight)
    {
        for (int i = 0; i < weight; i++) list.Add(item);
    }

    private static T PickRandom<T>(List<T> pool) where T : class
    {
        if (pool == null || pool.Count == 0) return null;
        return pool[Random.Range(0, pool.Count)];
    }

    private static void Shuffle<T>(List<T> list)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
    }
}

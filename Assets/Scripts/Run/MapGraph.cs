using System.Collections.Generic;

/// <summary>
/// The full map graph for a run. Holds all nodes, their connections, and the
/// player's current position. Generated once at run start and mutated as the
/// player moves through it.
///
/// CurrentNodeId is always valid after generation — MapGenerator pre-enters the
/// Start node, so GetReachableNodes() immediately returns Start's neighbors.
/// </summary>
public class MapGraph
{
    public List<MapNode> Nodes      = new();
    public int           StartNodeId = -1;
    public int           BossNodeId  = -1;

    /// <summary>The node the player is currently at. Set to the Start node at generation time.</summary>
    public int CurrentNodeId = -1;

    // ── Accessors ─────────────────────────────────────────────────────────────

    public MapNode CurrentNode => GetNode(CurrentNodeId);

    public MapNode GetNode(int id)
    {
        foreach (var node in Nodes)
            if (node.Id == id) return node;
        return null;
    }

    // ── Reachability ──────────────────────────────────────────────────────────

    /// <summary>
    /// Returns the unvisited nodes the player can move to from their current position.
    /// </summary>
    public List<MapNode> GetReachableNodes()
    {
        var result  = new List<MapNode>();
        var current = CurrentNode;
        if (current == null) return result;

        foreach (var neighborId in current.NeighborIds)
        {
            var neighbor = GetNode(neighborId);
            if (neighbor != null && !neighbor.Visited)
                result.Add(neighbor);
        }
        return result;
    }

    // ── Mutation ──────────────────────────────────────────────────────────────

    /// <summary>Set the player's active position to this node.</summary>
    public void EnterNode(int nodeId) => CurrentNodeId = nodeId;

    /// <summary>Mark the current node as visited (permanently unavailable).</summary>
    public void MarkCurrentNodeVisited()
    {
        var node = CurrentNode;
        if (node != null) node.Visited = true;
    }

    // ── Stats ─────────────────────────────────────────────────────────────────

    /// <summary>Total number of nodes the player has visited so far (excluding Start).</summary>
    public int VisitedCount
    {
        get
        {
            int count = 0;
            foreach (var node in Nodes)
                if (node.Visited && node.Type != NodeType.Start) count++;
            return count;
        }
    }
}

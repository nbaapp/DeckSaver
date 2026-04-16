using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// One node on the run map.
/// Position is in virtual map space (0..mapWidth, 0..mapHeight).
/// NeighborIds lists all nodes within reach distance — used both for display
/// (drawing edge lines) and for determining which nodes are reachable from here.
/// </summary>
public class MapNode
{
    public int      Id;
    public NodeType Type;
    public Vector2  Position;
    public bool     Visited;

    /// <summary>IDs of all nodes within reach radius of this one.</summary>
    public List<int> NeighborIds = new();

    /// <summary>
    /// For StandardConflict, HardConflict, and Boss nodes — the encounter to load.
    /// Null for Camp, Shop, and Event nodes.
    /// </summary>
    public EncounterDefinition Encounter;
}

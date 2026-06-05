using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>
/// Owns the per-battle isometric playfield: a Grid + Ground/Overlay Tilemaps
/// instantiated from an EncounterDefinition's painted map prefab (or the
/// programmatic default rectangle when none is assigned).
///
/// The "shape" of the playfield is whatever cells the Ground tilemap has
/// painted — a cell is in-bounds (and walkable) iff the ground tilemap
/// has any tile at that cell. Future obstacle tile types will only need
/// to extend IsInBounds with a per-tile attribute lookup.
///
/// Per-cell visual feedback (move/range/hover/hazard) is driven through a
/// parallel Overlay tilemap whose cells are tinted via Tilemap.SetColor.
/// </summary>
public class GridManager : MonoBehaviour
{
    public static GridManager Instance { get; private set; }

    /// <summary>The Grid component of the currently loaded map.</summary>
    public Grid Grid { get; private set; }

    /// <summary>The painted ground tilemap. Defines the playfield shape.</summary>
    public Tilemap Ground { get; private set; }

    /// <summary>The overlay tilemap used to draw cell tints.</summary>
    public Tilemap Overlay { get; private set; }

    private readonly Dictionary<Vector2Int, GridTile> _tiles = new();

    private void Awake()
    {
        Instance = this;

        // Y-sort so entities and iso tile rows compose correctly.
        if (Camera.main != null)
        {
            Camera.main.transparencySortMode = TransparencySortMode.CustomAxis;
            Camera.main.transparencySortAxis = new Vector3(0f, 1f, 0f);
        }
    }

    // ── Map registration ──────────────────────────────────────────────────────

    /// <summary>
    /// Register the tilemaps from a freshly instantiated map prefab.
    /// Builds a GridTile for every cell that has a ground tile, paints the
    /// matching overlay placeholder, and clears any leftover overlay tint.
    /// </summary>
    public void RegisterMap(Grid grid, Tilemap ground, Tilemap overlay)
    {
        Grid    = grid;
        Ground  = ground;
        Overlay = overlay;

        _tiles.Clear();

        var overlayTile = IsoMapBuilder.GetOverlayTile();
        var bounds      = ground.cellBounds;

        for (int x = bounds.xMin; x < bounds.xMax; x++)
        {
            for (int y = bounds.yMin; y < bounds.yMax; y++)
            {
                var cell = new Vector3Int(x, y, 0);
                if (ground.GetTile(cell) == null) continue;

                var pos = new Vector2Int(x, y);

                // Paint overlay placeholder so SetColor has something to tint.
                if (overlay != null)
                {
                    overlay.SetTile(cell, overlayTile);
                    overlay.SetColor(cell, new Color(0f, 0f, 0f, 0f));
                }

                _tiles[pos] = new GridTile(pos, SetOverlayColor);
            }
        }
    }

    private void SetOverlayColor(Vector2Int pos, Color color)
    {
        if (Overlay == null) return;
        Overlay.SetColor(new Vector3Int(pos.x, pos.y, 0), color);
    }

    // ── Public API (kept stable for callers) ──────────────────────────────────

    public GridTile GetTile(int x, int y) => GetTile(new Vector2Int(x, y));

    public GridTile GetTile(Vector2Int pos) =>
        _tiles.TryGetValue(pos, out var tile) ? tile : null;

    /// <summary>
    /// Returns true if the cell is part of the playfield. With shape-only
    /// walkability, this is equivalent to "the ground tilemap has a tile here".
    /// </summary>
    public bool IsInBounds(Vector2Int pos) => _tiles.ContainsKey(pos);
    public bool IsInBounds(int x, int y) => IsInBounds(new Vector2Int(x, y));

    /// <summary>World-space center of a grid cell. Uses iso projection from the Grid component.</summary>
    public Vector3 GridToWorld(Vector2Int pos)
    {
        if (Grid == null) return Vector3.zero;
        return Grid.GetCellCenterWorld(new Vector3Int(pos.x, pos.y, 0));
    }

    /// <summary>Returns the cell under <paramref name="worldPos"/>, or null if no playfield cell.</summary>
    public GridTile WorldToTile(Vector3 worldPos)
    {
        if (Grid == null) return null;
        var cell = Grid.WorldToCell(worldPos);
        return GetTile(new Vector2Int(cell.x, cell.y));
    }

    /// <summary>Iterates every walkable cell. Useful for bulk overlay clears.</summary>
    public IEnumerable<GridTile> AllTiles => _tiles.Values;

    /// <summary>Remove the player movement overlay from every tile.</summary>
    public void ClearAllPlayerMoves()
    {
        foreach (var tile in _tiles.Values)
            tile.SetPlayerMove(PlayerMoveOverlay.None);
    }

    /// <summary>Remove the enemy range overlay from every tile.</summary>
    public void ClearAllEnemyRanges()
    {
        foreach (var tile in _tiles.Values)
            tile.SetEnemyRange(EnemyRangeOverlay.None);
    }

    /// <summary>Clear all hover highlights, leaving persistent states (Occupied, Hazard) intact.</summary>
    public void ResetAllTiles()
    {
        foreach (var tile in _tiles.Values)
            tile.ClearHover();
    }

    /// <summary>
    /// Preview a modifier card's area pattern anchored at <paramref name="anchor"/>.
    /// Pattern tiles are set to Targeted; the anchor is set to Highlighted if not
    /// already covered by the pattern.
    /// </summary>
    public void HighlightPattern(Vector2Int anchor, ModifierFragmentData modifier)
    {
        foreach (TileData td in modifier.tiles)
        {
            GridTile tile = GetTile(anchor + td.position);
            if (tile != null)
                tile.SetState(TileVisualState.Targeted);
        }

        GridTile anchorTile = GetTile(anchor);
        if (anchorTile != null && anchorTile.GetState() != TileVisualState.Targeted)
            anchorTile.SetState(TileVisualState.Highlighted);
    }
}

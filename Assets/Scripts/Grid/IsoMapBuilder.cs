using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>
/// Helpers for spinning up an isometric map at runtime: the default fallback
/// playfield (rectangle of ground tiles) and the procedurally-generated
/// overlay-tint tile used to colour cells for hover/range/move feedback.
/// </summary>
public static class IsoMapBuilder
{
    public const int DefaultWidth  = 6;
    public const int DefaultHeight = 5;

    private static readonly Vector3 IsoCellSize = new(1f, 0.5f, 0f);

    private static Tile _cachedOverlayTile;

    /// <summary>
    /// Build a Grid + Ground/Overlay Tilemap GameObject filled with
    /// <paramref name="groundTile"/> across a width × height rectangle, plus
    /// SpawnMarkers down the left and right edges.
    /// Used as the migration fallback when an EncounterDefinition has no
    /// authored mapPrefab assigned.
    /// </summary>
    public static GameObject BuildDefault(TileBase groundTile, int width = DefaultWidth, int height = DefaultHeight)
    {
        if (groundTile == null)
        {
            Debug.LogError("[IsoMapBuilder] No defaultGroundTile assigned — assign one on EntityManager.");
            return null;
        }

        var root = new GameObject("EncounterMap (default)");
        var grid = root.AddComponent<Grid>();
        grid.cellLayout = GridLayout.CellLayout.Isometric;
        grid.cellSize   = IsoCellSize;

        // Ground tilemap — filled rectangle.
        var ground   = CreateTilemapChild(root.transform, "Ground", sortingOrder: 0);
        var groundTm = ground.GetComponent<Tilemap>();
        for (int x = 0; x < width; x++)
            for (int y = 0; y < height; y++)
                groundTm.SetTile(new Vector3Int(x, y, 0), groundTile);

        // Overlay tilemap — left empty here; GridManager paints placeholders
        // matching the ground shape when it loads the map.
        CreateTilemapChild(root.transform, "Overlay", sortingOrder: 1);

        // Spawn markers — three player slots on the left column,
        // three enemy slots on the right column.
        for (int i = 0; i < 3 && i < height; i++)
        {
            int y = (height - 1) - i;
            CreateMarker(root.transform, grid, SpawnMarkerKind.Player, i, new Vector3Int(0, y, 0));
            CreateMarker(root.transform, grid, SpawnMarkerKind.Enemy,  i, new Vector3Int(width - 1, y, 0));
        }

        return root;
    }

    /// <summary>
    /// Returns the runtime-generated diamond Tile used to draw cell tints
    /// (move zones, attack range, hover, hazard) on top of the ground tilemap.
    /// Cached — same Tile asset is reused across maps.
    /// </summary>
    public static Tile GetOverlayTile()
    {
        if (_cachedOverlayTile != null) return _cachedOverlayTile;
        _cachedOverlayTile           = ScriptableObject.CreateInstance<Tile>();
        _cachedOverlayTile.sprite    = CreateDiamondSprite(32, 16, 32);
        _cachedOverlayTile.color     = Color.white;
        _cachedOverlayTile.colliderType = Tile.ColliderType.None;
        // Use a Default flags value so per-cell SetColor isn't locked.
        _cachedOverlayTile.flags     = TileFlags.None;
        return _cachedOverlayTile;
    }

    // ── Internals ─────────────────────────────────────────────────────────────

    private static GameObject CreateTilemapChild(Transform parent, string name, int sortingOrder)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.AddComponent<Tilemap>();
        var renderer = go.AddComponent<TilemapRenderer>();
        renderer.sortingOrder = sortingOrder;
        // Individual mode lets per-cell positions feed transparency-sort,
        // which we need so entities sort correctly between iso rows.
        renderer.mode = TilemapRenderer.Mode.Individual;
        return go;
    }

    private static void CreateMarker(Transform parent, Grid grid, SpawnMarkerKind kind, int slot, Vector3Int cell)
    {
        var go = new GameObject($"{kind}Spawn_{slot}");
        go.transform.SetParent(parent, false);
        go.transform.position = grid.GetCellCenterWorld(cell);
        var marker        = go.AddComponent<SpawnMarker>();
        marker.kind       = kind;
        marker.slotIndex  = slot;
    }

    private static Sprite CreateDiamondSprite(int width, int height, int pixelsPerUnit)
    {
        var tex        = new Texture2D(width, height, TextureFormat.RGBA32, mipChain: false);
        tex.filterMode = FilterMode.Point;
        tex.wrapMode   = TextureWrapMode.Clamp;

        float hw = (width - 1) / 2f;
        float hh = (height - 1) / 2f;

        for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
            {
                // Standard 2:1 diamond: |dx|/hw + |dy|/hh <= 1
                float dx = Mathf.Abs(x - hw) / hw;
                float dy = Mathf.Abs(y - hh) / hh;
                bool inside = (dx + dy) <= 1f + 0.001f;
                tex.SetPixel(x, y, inside ? Color.white : new Color(0, 0, 0, 0));
            }
        tex.Apply();

        return Sprite.Create(tex,
                             new Rect(0, 0, width, height),
                             new Vector2(0.5f, 0.5f),
                             pixelsPerUnit);
    }
}

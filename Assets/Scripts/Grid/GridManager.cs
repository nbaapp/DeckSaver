using System.Collections.Generic;
using UnityEngine;

[ExecuteAlways]
public class GridManager : MonoBehaviour
{
    public static GridManager Instance { get; private set; }

    [Header("Grid Settings")]
    public int width = 5;
    public int height = 5;
    public float tileSize = 1f;
    public float tileGap = 0f;

    [Header("Visual")]
    public Color lineColor = new(0.45f, 0.45f, 0.45f, 1f);
    public float lineWidth = 0.04f;

    [Header("References")]
    public GameObject tilePrefab;

    private GridTile[,] _tiles;

    private void Awake()
    {
        if (Application.isPlaying)
            Instance = this;

        RebuildGrid();
    }

    private void RebuildGrid()
    {
        ClearGrid();
        if (tilePrefab == null) return;
        SpawnGrid();
        SpawnGridLines();
    }

    private void ClearGrid()
    {
        _tiles = null;
        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            var child = transform.GetChild(i).gameObject;
            if (Application.isPlaying)
                Destroy(child);
            else
                DestroyImmediate(child);
        }
    }

    private void SpawnGrid()
    {
        _tiles = new GridTile[width, height];
        float step = tileSize + tileGap;

        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                Vector3 localPos = new(
                    -(width  - 1) * step / 2f + x * step,
                    -(height - 1) * step / 2f + y * step,
                    0f);

                GameObject tileGO = Instantiate(
                    tilePrefab,
                    transform.TransformPoint(localPos),
                    Quaternion.identity,
                    transform);

                tileGO.name = $"Tile ({x},{y})";

                GridTile tile = tileGO.GetComponent<GridTile>();
                tile.Init(new Vector2Int(x, y), tileSize);
                _tiles[x, y] = tile;
            }
        }
    }

    private void SpawnGridLines()
    {
        float step = tileSize + tileGap;
        float halfW = width  * step / 2f;
        float halfH = height * step / 2f;

        var linesGO = new GameObject("GridLines");
        linesGO.transform.SetParent(transform, false);

        // Horizontal lines
        for (int row = 0; row <= height; row++)
        {
            float y = -halfH + row * step;
            CreateLine(linesGO.transform,
                new Vector3(-halfW, y, 0f),
                new Vector3( halfW, y, 0f));
        }

        // Vertical lines
        for (int col = 0; col <= width; col++)
        {
            float x = -halfW + col * step;
            CreateLine(linesGO.transform,
                new Vector3(x, -halfH, 0f),
                new Vector3(x,  halfH, 0f));
        }
    }

    private void CreateLine(Transform parent, Vector3 start, Vector3 end)
    {
        var go = new GameObject("Line");
        go.transform.SetParent(parent, false);

        var lr = go.AddComponent<LineRenderer>();
        lr.useWorldSpace = false;
        lr.positionCount = 2;
        lr.SetPosition(0, start);
        lr.SetPosition(1, end);
        lr.startWidth = lineWidth;
        lr.endWidth   = lineWidth;
        lr.startColor = lineColor;
        lr.endColor   = lineColor;
        lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        lr.receiveShadows = false;
        lr.sortingOrder = 1; // draw on top of tile fills

        var shader = Shader.Find("Sprites/Default");
        if (shader != null)
            lr.material = new Material(shader) { color = lineColor };
    }

    // --- Public API ---

    public GridTile GetTile(int x, int y)
    {
        if (!IsInBounds(x, y)) return null;
        return _tiles[x, y];
    }

    public GridTile GetTile(Vector2Int pos) => GetTile(pos.x, pos.y);

    public bool IsInBounds(int x, int y) => x >= 0 && x < width && y >= 0 && y < height;
    public bool IsInBounds(Vector2Int pos) => IsInBounds(pos.x, pos.y);

    public Vector3 GridToWorld(Vector2Int pos)
    {
        float step = tileSize + tileGap;
        return transform.TransformPoint(new Vector3(
            -(width  - 1) * step / 2f + pos.x * step,
            -(height - 1) * step / 2f + pos.y * step,
            0f));
    }

    /// <summary>
    /// Clears Highlighted and Targeted states back to Normal.
    /// Occupied and Hazard states are left intact so entities remain visible.
    /// </summary>
    /// <summary>Remove the player movement overlay from every tile.</summary>
    public void ClearAllPlayerMoves()
    {
        if (_tiles == null) return;
        foreach (GridTile tile in _tiles)
            tile.SetPlayerMove(PlayerMoveOverlay.None);
    }

    /// <summary>Remove the enemy range overlay from every tile.</summary>
    public void ClearAllEnemyRanges()
    {
        if (_tiles == null) return;
        foreach (GridTile tile in _tiles)
            tile.SetEnemyRange(EnemyRangeOverlay.None);
    }

    /// <summary>Clear all hover highlights, leaving persistent states (Occupied, Hazard) intact.</summary>
    public void ResetAllTiles()
    {
        if (_tiles == null) return;
        foreach (GridTile tile in _tiles)
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

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (!Application.isPlaying)
            UnityEditor.EditorApplication.delayCall += () => { if (this) RebuildGrid(); };
    }
#endif
}

using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Handles mouse input on the grid and drives tile highlighting.
///
/// When a card is pending (selected in hand), the highlight behaviour depends
/// on the card's PlacementType:
///
///   CenteredOnPlayer   — pattern is always shown centred on the player tile.
///   DirectionalFromPlayer — pattern is rotated to face the direction the mouse
///                           is in relative to the player.  Default direction
///                           is right; if the mouse is within a small radius
///                           of the player the last known direction is kept.
///   FreelyPlaceable    — pattern follows the hovered tile.  If the mouse is
///                         off the grid no highlight is shown.
///
/// In all cases tiles that fall outside the grid are silently clipped.
/// </summary>
[RequireComponent(typeof(GridManager))]
public class GridInputHandler : MonoBehaviour
{
    public static GridInputHandler Instance { get; private set; }

    /// <summary>Fired when the player left-clicks a tile.</summary>
    public static event Action<GridTile> OnTileClicked;

    /// <summary>Fired whenever the tile under the cursor changes (including to null).</summary>
    public static event Action<GridTile> OnTileHovered;

    private Camera        _cam;
    private GridTile      _hoveredTile;
    private CardData      _pendingCard;
    private CommanderData _pendingCommanderActive;
    private Vector2Int    _currentDir = Vector2Int.right;

    // Abstractions so Update/RefreshHighlight work for both cards and commander active
    private ModifierFragmentData PendingModifier    => _pendingCard?.modifierFragment ?? _pendingCommanderActive?.activeArea;
    private PlacementType        PendingPlacementType =>
        _pendingCard?.PlacementType ??
        _pendingCommanderActive?.activeArea?.placementType ??
        PlacementType.CenteredOnPlayer;

    // ── Unity lifecycle ───────────────────────────────────────────────────────

    private void Awake()
    {
        Instance = this;
        _cam     = Camera.main;
    }

    private void Update()
    {
        GridTile   tile    = RaycastTile();
        bool       changed = tile != _hoveredTile;
        _hoveredTile = tile;

        if (PendingModifier != null &&
            PendingPlacementType == PlacementType.DirectionalFromPlayer)
        {
            Vector2Int dir = ComputeDirection();
            if (dir != _currentDir) { _currentDir = dir; changed = true; }
        }

        if (changed)
        {
            OnTileHovered?.Invoke(_hoveredTile);
            RefreshHighlight();
        }

        if (_hoveredTile != null && Mouse.current.leftButton.wasPressedThisFrame)
            OnTileClicked?.Invoke(_hoveredTile);
    }

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>The cardinal direction the pattern is currently facing.</summary>
    public Vector2Int CurrentDirection => _currentDir;

    /// <summary>
    /// Set the card whose area will be previewed on the grid.
    /// Pass null to clear the preview and return to plain tile hover.
    /// </summary>
    public void SetPendingCard(CardData card)
    {
        _pendingCard            = card;
        _pendingCommanderActive = null;
        _currentDir             = Vector2Int.right;
        RefreshHighlight();
    }

    /// <summary>Begin targeting mode for a commander active ability.</summary>
    public void SetPendingCommanderActive(CommanderData commander)
    {
        _pendingCommanderActive = commander;
        _pendingCard            = null;
        _currentDir             = Vector2Int.right;
        RefreshHighlight();
    }

    /// <summary>Clear commander active targeting and return to plain tile hover.</summary>
    public void ClearPendingCommanderActive()
    {
        _pendingCommanderActive = null;
        _currentDir             = Vector2Int.right;
        RefreshHighlight();
    }

    /// <summary>
    /// Returns affected tiles for the pending commander active ability.
    /// </summary>
    public List<(GridTile tile, TileData tileData)> GetAffectedTilesForCommander(Vector2Int clickedPos)
    {
        var result = new List<(GridTile, TileData)>();
        var mod = _pendingCommanderActive?.activeArea;
        if (mod == null) return result;

        var        placement = mod.placementType;
        Vector2Int anchor    = placement == PlacementType.FreelyPlaceable ? clickedPos : PlayerPos();
        Vector2Int dir       = placement == PlacementType.DirectionalFromPlayer ? _currentDir : Vector2Int.up;

        foreach (var td in mod.tiles)
        {
            var tile = GridManager.Instance.GetTile(anchor + RotateOffset(td.position, dir));
            if (tile != null)
                result.Add((tile, td));
        }
        return result;
    }

    /// <summary>
    /// Returns each (GridTile, TileData) pair affected by the current pending card.
    /// For CenteredOnPlayer / DirectionalFromPlayer the anchor is the player's tile.
    /// For FreelyPlaceable the anchor is <paramref name="clickedPos"/>.
    /// Out-of-bounds tiles are silently excluded.
    /// </summary>
    public List<(GridTile tile, TileData tileData)> GetAffectedTiles(Vector2Int clickedPos)
    {
        var result = new List<(GridTile, TileData)>();
        if (_pendingCard?.modifierFragment == null) return result;

        var        mod    = _pendingCard.modifierFragment;
        Vector2Int anchor = _pendingCard.PlacementType == PlacementType.FreelyPlaceable
            ? clickedPos
            : PlayerPos();
        Vector2Int dir    = _pendingCard.PlacementType == PlacementType.DirectionalFromPlayer
            ? _currentDir
            : Vector2Int.up;

        foreach (var td in mod.tiles)
        {
            var tile = GridManager.Instance.GetTile(anchor + RotateOffset(td.position, dir));
            if (tile != null)
                result.Add((tile, td));
        }
        return result;
    }

    // ── Private helpers ───────────────────────────────────────────────────────

    private GridTile RaycastTile()
    {
        if (_cam == null) return null;
        Vector2 screenPos = Mouse.current.position.ReadValue();
        Vector2 worldPos  = _cam.ScreenToWorldPoint(screenPos);
        // RaycastAll so entity colliders (enemies) don't block the tile beneath them.
        foreach (var hit in Physics2D.RaycastAll(worldPos, Vector2.zero))
        {
            var tile = hit.collider.GetComponent<GridTile>();
            if (tile != null) return tile;
        }
        return null;
    }

    /// <summary>
    /// Determine the cardinal direction the pattern should face, based on where
    /// the mouse is relative to the player.  Keeps the last direction when the
    /// mouse is very close to the player (avoids jitter).
    /// </summary>
    private Vector2Int ComputeDirection()
    {
        PlayerEntity player = PlayerEntity.Instance;
        if (player == null || _cam == null) return _currentDir;

        Vector2 screenPos   = Mouse.current.position.ReadValue();
        Vector2 mouseWorld  = _cam.ScreenToWorldPoint(screenPos);
        Vector2 playerWorld = GridManager.Instance.GridToWorld(player.GridPosition);
        Vector2 delta       = mouseWorld - playerWorld;

        if (delta.sqrMagnitude < 0.01f) return _currentDir; // too close — keep last

        return Mathf.Abs(delta.x) >= Mathf.Abs(delta.y)
            ? (delta.x >= 0 ? Vector2Int.right : Vector2Int.left)
            : (delta.y >= 0 ? Vector2Int.up    : Vector2Int.down);
    }

    private void RefreshHighlight()
    {
        GridManager.Instance.ResetAllTiles();

        var mod = PendingModifier;
        if (mod == null) return;

        switch (PendingPlacementType)
        {
            case PlacementType.CenteredOnPlayer:
            {
                Vector2Int center = PlayerPos();
                ShowPattern(center, mod, Vector2Int.up);
                break;
            }

            case PlacementType.DirectionalFromPlayer:
            {
                ShowPattern(PlayerPos(), mod, _currentDir);
                break;
            }

            case PlacementType.FreelyPlaceable:
            {
                if (_hoveredTile != null)
                    ShowPattern(_hoveredTile.GridPosition, mod, Vector2Int.up);
                break;
            }
        }
    }

    // ── Pattern rendering ─────────────────────────────────────────────────────

    /// <summary>
    /// Highlight all pattern tiles relative to <paramref name="anchor"/> after
    /// rotating their offsets to face <paramref name="dir"/>.
    /// Tiles outside the grid are silently skipped (clipping).
    /// </summary>
    private static void ShowPattern(Vector2Int anchor, ModifierFragmentData mod, Vector2Int dir)
    {
        foreach (var td in mod.tiles)
        {
            GridTile tile = GridManager.Instance.GetTile(anchor + RotateOffset(td.position, dir));
            if (tile != null)
                tile.SetState(TileVisualState.Targeted);
        }

        // Show anchor tile as Highlighted only if the pattern doesn't cover it
        GridTile anchorTile = GridManager.Instance.GetTile(anchor);
        if (anchorTile != null && anchorTile.GetState() != TileVisualState.Targeted)
            anchorTile.SetState(TileVisualState.Highlighted);
    }

    /// <summary>
    /// Rotate a tile offset so the pattern faces <paramref name="dir"/>.
    /// The unrotated (default) direction is up (+y), matching how modifier
    /// tile positions are authored.
    /// </summary>
    private static Vector2Int RotateOffset(Vector2Int o, Vector2Int dir)
    {
        if (dir == Vector2Int.up)    return o;                                    // 0°  (identity)
        if (dir == Vector2Int.right) return new Vector2Int( o.y, -o.x);          // 90° CW
        if (dir == Vector2Int.down)  return new Vector2Int(-o.x, -o.y);          // 180°
        if (dir == Vector2Int.left)  return new Vector2Int(-o.y,  o.x);          // 90° CCW
        return o;
    }

    private static Vector2Int PlayerPos()
    {
        return PlayerEntity.Instance?.GridPosition
            ?? new Vector2Int(GridManager.Instance.width / 2, GridManager.Instance.height / 2);
    }
}

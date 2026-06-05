using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>Player movement overlay drawn on tiles when the player is choosing where to move.</summary>
public enum PlayerMoveOverlay
{
    None,
    Zone1,  // 1-stamina cost — dim blue
    Zone2,  // 2-stamina cost — dim yellow
    Zone3,  // 3+-stamina cost — dim orange
    Path,       // the highlighted path — bright blue  (Zone1 range)
    PathZone2,  // the highlighted path — bright yellow (Zone2 range)
    PathZone3,  // the highlighted path — bright orange (Zone3 range)
}

/// <summary>Which enemy-range overlay, if any, is active on this tile.</summary>
public enum EnemyRangeOverlay
{
    None,
    ZoneMove,    // possible move area   — dim blue
    ZoneAttack,  // possible attack area — dim orange
    PlanMove,    // planned move path    — bright blue
    PlanAttack,  // planned attack tiles — bright orange
}

/// <summary>
/// Per-cell logical state for one playfield cell. Not a MonoBehaviour — the
/// playfield is a Unity Tilemap now, so a GridTile is a pure data record kept
/// in a dictionary on GridManager.
///
/// Visual feedback (move zones, attack range, hover, hazard) is rendered by
/// tinting an overlay tilemap cell via the GridManager-injected tinter.
/// </summary>
public class GridTile
{
    public Vector2Int GridPosition { get; }

    private readonly System.Action<Vector2Int, Color> _setOverlayColor;

    private TileVisualState   _baseState   = TileVisualState.Normal;
    private TileVisualState   _hoverState  = TileVisualState.Normal;
    private EnemyRangeOverlay _enemyRange  = EnemyRangeOverlay.None;
    private PlayerMoveOverlay _playerMove  = PlayerMoveOverlay.None;

    private static readonly Color ColorClear       = new(0f, 0f, 0f, 0f);
    private static readonly Color ColorHighlighted = new(0.95f, 0.90f, 0.40f, 0.55f);
    private static readonly Color ColorTargeted    = new(0.90f, 0.35f, 0.35f, 0.60f);
    private static readonly Color ColorHazard      = new(0.80f, 0.45f, 0.15f, 0.60f);
    // Enemy range
    private static readonly Color ColorZoneMove    = new(0.30f, 0.75f, 1.00f, 0.30f);
    private static readonly Color ColorZoneAttack  = new(0.95f, 0.55f, 0.10f, 0.30f);
    private static readonly Color ColorPlanMove    = new(0.30f, 0.75f, 1.00f, 0.70f);
    private static readonly Color ColorPlanAttack  = new(0.95f, 0.55f, 0.10f, 0.70f);
    // Player movement
    private static readonly Color ColorMoveZone1   = new(0.20f, 0.65f, 1.00f, 0.35f);
    private static readonly Color ColorMoveZone2   = new(0.95f, 0.90f, 0.20f, 0.35f);
    private static readonly Color ColorMoveZone3   = new(0.95f, 0.50f, 0.10f, 0.35f);
    private static readonly Color ColorMovePath    = new(0.20f, 0.65f, 1.00f, 0.75f);
    private static readonly Color ColorMovePath2   = new(0.95f, 0.90f, 0.20f, 0.75f);
    private static readonly Color ColorMovePath3   = new(0.95f, 0.50f, 0.10f, 0.75f);

    public GridTile(Vector2Int gridPosition, System.Action<Vector2Int, Color> setOverlayColor)
    {
        GridPosition      = gridPosition;
        _setOverlayColor  = setOverlayColor;
        Apply();
    }

    /// <summary>
    /// Set the tile's visual state.
    /// Occupied and Hazard write to the persistent base layer.
    /// Highlighted and Targeted write to the transient hover layer.
    /// Normal clears both layers.
    /// </summary>
    public void SetState(TileVisualState state)
    {
        switch (state)
        {
            case TileVisualState.Occupied:
            case TileVisualState.Hazard:
                _baseState = state;
                break;
            case TileVisualState.Normal:
                _baseState  = TileVisualState.Normal;
                _hoverState = TileVisualState.Normal;
                break;
            default: // Highlighted, Targeted
                _hoverState = state;
                break;
        }
        Apply();
    }

    /// <summary>Clear any hover highlight, restoring the base state visually.</summary>
    public void ClearHover()
    {
        _hoverState = TileVisualState.Normal;
        Apply();
    }

    /// <summary>Set or clear the enemy move/attack range overlay for this tile.</summary>
    public void SetEnemyRange(EnemyRangeOverlay range)
    {
        _enemyRange = range;
        Apply();
    }

    /// <summary>Set or clear the player movement overlay for this tile.</summary>
    public void SetPlayerMove(PlayerMoveOverlay overlay)
    {
        _playerMove = overlay;
        Apply();
    }

    /// <summary>Returns the effective state (hover takes priority over base).</summary>
    public TileVisualState GetState() =>
        _hoverState != TileVisualState.Normal ? _hoverState : _baseState;

    private void Apply()
    {
        _setOverlayColor?.Invoke(GridPosition, ResolveColor());
    }

    private Color ResolveColor()
    {
        // 1. Card targeting hover always wins.
        if (_hoverState == TileVisualState.Highlighted) return ColorHighlighted;
        if (_hoverState == TileVisualState.Targeted)    return ColorTargeted;

        // 2. Persistent hazard.
        if (_baseState == TileVisualState.Hazard) return ColorHazard;

        // 3. Player movement path (brightest player overlay).
        if (_playerMove == PlayerMoveOverlay.Path)      return ColorMovePath;
        if (_playerMove == PlayerMoveOverlay.PathZone2) return ColorMovePath2;
        if (_playerMove == PlayerMoveOverlay.PathZone3) return ColorMovePath3;

        // 4. Enemy specific plan (bright).
        if (_enemyRange == EnemyRangeOverlay.PlanMove)   return ColorPlanMove;
        if (_enemyRange == EnemyRangeOverlay.PlanAttack) return ColorPlanAttack;

        // 5. Player movement zones (dim).
        switch (_playerMove)
        {
            case PlayerMoveOverlay.Zone1: return ColorMoveZone1;
            case PlayerMoveOverlay.Zone2: return ColorMoveZone2;
            case PlayerMoveOverlay.Zone3: return ColorMoveZone3;
        }

        // 6. Enemy general zone (dim).
        switch (_enemyRange)
        {
            case EnemyRangeOverlay.ZoneMove:   return ColorZoneMove;
            case EnemyRangeOverlay.ZoneAttack: return ColorZoneAttack;
        }

        return ColorClear;
    }
}

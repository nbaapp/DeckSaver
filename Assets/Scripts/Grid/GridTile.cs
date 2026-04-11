using UnityEngine;

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

public class GridTile : MonoBehaviour
{
    public Vector2Int GridPosition { get; private set; }

    private SpriteRenderer _renderer;

    // Base state: persistent game-logic state (Occupied, Hazard).
    // Hover state: transient highlight drawn on top (Highlighted, Targeted).
    // Enemy range: orthogonal overlay shown when hovering an enemy (Move > Attack).
    // Priority: Hover > Hazard > MoveRange > AttackRange > Occupied/Normal.
    private TileVisualState   _baseState   = TileVisualState.Normal;
    private TileVisualState   _hoverState  = TileVisualState.Normal;
    private EnemyRangeOverlay _enemyRange  = EnemyRangeOverlay.None;
    private PlayerMoveOverlay _playerMove  = PlayerMoveOverlay.None;

    private static readonly Color ColorNormal      = new(0.00f, 0.00f, 0.00f, 0.00f);
    private static readonly Color ColorHighlighted = new(0.95f, 0.90f, 0.40f, 0.40f);
    private static readonly Color ColorTargeted    = new(0.90f, 0.35f, 0.35f, 0.45f);
    private static readonly Color ColorHazard      = new(0.80f, 0.45f, 0.15f, 0.45f);
    // Enemy range
    private static readonly Color ColorZoneMove    = new(0.30f, 0.75f, 1.00f, 0.18f);
    private static readonly Color ColorZoneAttack  = new(0.95f, 0.55f, 0.10f, 0.18f);
    private static readonly Color ColorPlanMove    = new(0.30f, 0.75f, 1.00f, 0.55f);
    private static readonly Color ColorPlanAttack  = new(0.95f, 0.55f, 0.10f, 0.55f);
    // Player movement
    private static readonly Color ColorMoveZone1   = new(0.20f, 0.65f, 1.00f, 0.22f); // blue
    private static readonly Color ColorMoveZone2   = new(0.95f, 0.90f, 0.20f, 0.22f); // yellow
    private static readonly Color ColorMoveZone3   = new(0.95f, 0.50f, 0.10f, 0.22f); // orange
    private static readonly Color ColorMovePath    = new(0.20f, 0.65f, 1.00f, 0.65f); // bright blue
    private static readonly Color ColorMovePath2   = new(0.95f, 0.90f, 0.20f, 0.65f); // bright yellow
    private static readonly Color ColorMovePath3   = new(0.95f, 0.50f, 0.10f, 0.65f); // bright orange

    /// <param name="tileSize">World-unit size of one cell, used to scale the sprite to fit.</param>
    public void Init(Vector2Int gridPosition, float tileSize)
    {
        GridPosition = gridPosition;
        _renderer = GetComponent<SpriteRenderer>();

        if (_renderer.sprite != null)
        {
            Vector2 naturalSize = _renderer.sprite.bounds.size;
            transform.localScale = new Vector3(
                tileSize / naturalSize.x,
                tileSize / naturalSize.y,
                1f);

            var col = GetComponent<BoxCollider2D>();
            if (col != null)
                col.size = naturalSize;
        }

        SetState(TileVisualState.Normal);
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
        RefreshColor();
    }

    /// <summary>Clear any hover highlight, restoring the base state visually.</summary>
    public void ClearHover()
    {
        _hoverState = TileVisualState.Normal;
        RefreshColor();
    }

    /// <summary>Set or clear the enemy move/attack range overlay for this tile.</summary>
    public void SetEnemyRange(EnemyRangeOverlay range)
    {
        _enemyRange = range;
        RefreshColor();
    }

    /// <summary>Set or clear the player movement overlay for this tile.</summary>
    public void SetPlayerMove(PlayerMoveOverlay overlay)
    {
        _playerMove = overlay;
        RefreshColor();
    }

    /// <summary>Returns the effective state (hover takes priority over base).</summary>
    public TileVisualState GetState() =>
        _hoverState != TileVisualState.Normal ? _hoverState : _baseState;

    private void RefreshColor()
    {
        // 1. Card targeting hover always wins.
        if (_hoverState != TileVisualState.Normal)
        {
            _renderer.color = _hoverState == TileVisualState.Highlighted
                ? ColorHighlighted
                : ColorTargeted;
            return;
        }

        // 2. Persistent hazard.
        if (_baseState == TileVisualState.Hazard)
        {
            _renderer.color = ColorHazard;
            return;
        }

        // 3. Player movement path (brightest player overlay).
        if (_playerMove == PlayerMoveOverlay.Path)      { _renderer.color = ColorMovePath;  return; }
        if (_playerMove == PlayerMoveOverlay.PathZone2) { _renderer.color = ColorMovePath2; return; }
        if (_playerMove == PlayerMoveOverlay.PathZone3) { _renderer.color = ColorMovePath3; return; }

        // 4. Enemy specific plan (bright).
        if (_enemyRange == EnemyRangeOverlay.PlanMove)   { _renderer.color = ColorPlanMove;   return; }
        if (_enemyRange == EnemyRangeOverlay.PlanAttack) { _renderer.color = ColorPlanAttack; return; }

        // 5. Player movement zones (dim).
        _renderer.color = _playerMove switch
        {
            PlayerMoveOverlay.Zone1 => ColorMoveZone1,
            PlayerMoveOverlay.Zone2 => ColorMoveZone2,
            PlayerMoveOverlay.Zone3 => ColorMoveZone3,
            _                       => ColorNormal,
        };
        if (_playerMove != PlayerMoveOverlay.None) return;

        // 6. Enemy general zone (dim).
        _renderer.color = _enemyRange switch
        {
            EnemyRangeOverlay.ZoneMove   => ColorZoneMove,
            EnemyRangeOverlay.ZoneAttack => ColorZoneAttack,
            _                            => ColorNormal,
        };
    }
}

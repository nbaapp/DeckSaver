using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Handles player movement on the grid.
///
/// Click the player's tile (when no card is selected) to enter move mode.
/// The grid shows reachable tiles colour-coded by stamina cost:
///   Zone1 (dim blue)   — 1 stamina, tiles within 1×MoveSpeed distance
///   Zone2 (dim yellow) — 2 stamina, tiles within 2×MoveSpeed distance
///   Zone3 (dim orange) — 3+ stamina, tiles within N×MoveSpeed distance
///   Path  (bright blue)— hovered path
///
/// Hover a reachable tile to preview the BFS path.
/// Click a reachable tile to confirm movement (spends stamina).
/// Click the player tile (or select a card) to exit move mode.
/// </summary>
public class PlayerMovementHandler : MonoBehaviour
{
    public static PlayerMovementHandler Instance { get; private set; }

    public bool IsInMoveMode { get; private set; }

    // BFS result: gridPos → (distance, parent pos)
    private Dictionary<Vector2Int, (int dist, Vector2Int parent)> _reachable;

    // Currently highlighted path tiles (so we can clear them cheaply)
    private readonly List<GridTile> _pathTiles = new();

    // ── Unity lifecycle ───────────────────────────────────────────────────────

    private void Awake() => Instance = this;

    private void OnEnable()
    {
        GridInputHandler.OnTileClicked += HandleTileClicked;
        GridInputHandler.OnTileHovered += HandleTileHovered;
    }

    private void OnDisable()
    {
        GridInputHandler.OnTileClicked -= HandleTileClicked;
        GridInputHandler.OnTileHovered -= HandleTileHovered;
    }

    // ── Public API ────────────────────────────────────────────────────────────

    public void EnterMoveMode()
    {
        if (TurnManager.Instance?.CurrentPhase != TurnPhase.PlayerTurn) return;
        if (IsInMoveMode) return;

        HandDisplay.Instance?.DeselectCard();

        IsInMoveMode = true;
        GridManager.Instance.ClearAllPlayerMoves(); // clear any hover preview
        ComputeReachable();
        ShowZones();
    }

    public void ExitMoveMode()
    {
        if (!IsInMoveMode) return;
        IsInMoveMode = false;
        ClearPath();
        GridManager.Instance.ClearAllPlayerMoves();
        _reachable = null;
    }

    // ── Input handlers ────────────────────────────────────────────────────────

    private void HandleTileClicked(GridTile tile)
    {
        if (TurnManager.Instance?.CurrentPhase != TurnPhase.PlayerTurn) return;

        // ── Unit selection ────────────────────────────────────────────────────
        // Check whether the clicked tile contains any player unit.
        var entity      = EntityManager.Instance.GetEntityAt(tile.GridPosition);
        var clickedUnit = entity as PlayerEntity;

        if (clickedUnit != null)
        {
            var selected    = PlayerParty.Instance?.SelectedUnit;
            var pendingCard = HandDisplay.Instance?.SelectedCard;

            if (pendingCard != null)
            {
                // Card is selected: clicking any unit tile switches the acting unit
                // without entering move mode. Re-set the pending card so the attack
                // pattern re-anchors on the newly selected unit.
                if (clickedUnit != selected)
                {
                    ExitMoveMode();
                    PlayerParty.Instance?.SelectUnit(clickedUnit);
                    GridInputHandler.Instance?.SetPendingCard(pendingCard.Data);
                }
                // Whether same or different unit, do NOT enter move mode.
                return;
            }

            if (clickedUnit != selected)
            {
                // No card — select a different unit.
                ExitMoveMode();
                PlayerParty.Instance?.SelectUnit(clickedUnit);
                return;
            }

            // Clicked the already-selected unit with no card: toggle move mode.
            if (IsInMoveMode)
                ExitMoveMode();
            else
                EnterMoveMode();
            return;
        }

        // ── Movement resolution ───────────────────────────────────────────────
        if (!IsInMoveMode) return;
        // A selected card always takes priority — exit move mode and let CardPlayManager handle it.
        if (HandDisplay.Instance?.SelectedCard != null) { ExitMoveMode(); return; }

        var player = PlayerParty.Instance?.SelectedUnit;
        if (player == null) return;

        if (_reachable != null && _reachable.TryGetValue(tile.GridPosition, out var info))
        {
            int staminaCost = StaminaCostFor(info.dist);
            if (PlayerParty.Instance.TrySpendStamina(staminaCost))
            {
                List<Vector2Int> path = BuildPath(tile.GridPosition);
                ExitMoveMode();
                ExecuteMove(player, path);
            }
        }
        else
        {
            ExitMoveMode();
        }
    }

    private void HandleTileHovered(GridTile tile)
    {
        // Show/hide dim zones when hovering over the player tile (without entering move mode).
        if (!IsInMoveMode)
        {
            // Never show movement preview while a card is selected.
            if (HandDisplay.Instance?.SelectedCard != null)
            {
                if (_reachable != null)
                {
                    GridManager.Instance.ClearAllPlayerMoves();
                    _reachable = null;
                }
                return;
            }

            var player = PlayerParty.Instance?.SelectedUnit;
            if (player == null) return;

            if (tile != null && tile.GridPosition == player.GridPosition)
            {
                // Hovering over player — show dim zones as a preview.
                ComputeReachable();
                ShowZones();
            }
            else
            {
                // Moved off player tile — clear preview if we had one.
                if (_reachable != null)
                {
                    GridManager.Instance.ClearAllPlayerMoves();
                    _reachable = null;
                }
            }
            return;
        }

        ClearPath();

        if (tile == null) return;
        if (_reachable == null || !_reachable.ContainsKey(tile.GridPosition)) return;

        // Trace path from tile back to player and paint each tile the bright
        // version of its zone colour (blue / yellow / orange).
        var player2 = PlayerParty.Instance?.SelectedUnit;
        int speed2  = player2 != null ? player2.GetEffectiveMoveSpeed(player2.MoveSpeed) : 3;

        List<Vector2Int> path = BuildPath(tile.GridPosition);
        foreach (var pos in path)
        {
            var t = GridManager.Instance.GetTile(pos);
            if (t == null) continue;

            PlayerMoveOverlay pathOverlay = PlayerMoveOverlay.Path; // default bright blue
            if (_reachable.TryGetValue(pos, out var pInfo))
            {
                int d = pInfo.dist;
                pathOverlay = d <= speed2      ? PlayerMoveOverlay.Path
                            : d <= speed2 * 2  ? PlayerMoveOverlay.PathZone2
                            :                    PlayerMoveOverlay.PathZone3;
            }

            t.SetPlayerMove(pathOverlay);
            _pathTiles.Add(t);
        }
    }

    // ── BFS ──────────────────────────────────────────────────────────────────

    private void ComputeReachable()
    {
        var player  = PlayerParty.Instance?.SelectedUnit;
        if (player == null) { _reachable = null; return; }

        int stamina = PlayerParty.Instance?.CurrentStamina ?? 0;
        int speed   = player.GetEffectiveMoveSpeed(player.MoveSpeed);
        int maxDist   = stamina * speed; // max tiles reachable

        _reachable = new Dictionary<Vector2Int, (int, Vector2Int)>();

        if (maxDist <= 0) return;

        // BFS — cardinal neighbours, skip occupied tiles (except player's own start)
        var queue   = new Queue<Vector2Int>();
        var visited = new HashSet<Vector2Int>();

        Vector2Int start = player.GridPosition;
        queue.Enqueue(start);
        visited.Add(start);
        _reachable[start] = (0, start); // distance 0, no parent

        var dirs = new[] { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right };

        while (queue.Count > 0)
        {
            Vector2Int cur = queue.Dequeue();
            int curDist = _reachable[cur].dist;
            if (curDist >= maxDist) continue;

            foreach (var d in dirs)
            {
                Vector2Int next = cur + d;
                if (visited.Contains(next)) continue;
                visited.Add(next);

                var nextTile = GridManager.Instance.GetTile(next);
                if (nextTile == null) continue;

                // Block movement through occupied tiles (entities).
                // We still allow landing ON the destination if it's occupied — but
                // we'll prevent that too to keep things simple (can't share a tile).
                if (nextTile.GetState() == TileVisualState.Occupied) continue;

                int nextDist = curDist + 1;
                _reachable[next] = (nextDist, cur);
                if (nextDist < maxDist)
                    queue.Enqueue(next);
            }
        }

        // Remove start (player's own tile) — can't "move" there.
        _reachable.Remove(start);
    }

    // ── Visualisation ─────────────────────────────────────────────────────────

    private void ShowZones()
    {
        if (_reachable == null) return;

        var player = PlayerParty.Instance?.SelectedUnit;
        if (player == null) return;

        int speed = player.GetEffectiveMoveSpeed(player.MoveSpeed);

        foreach (var kv in _reachable)
        {
            var tile = GridManager.Instance.GetTile(kv.Key);
            if (tile == null) continue;

            int dist = kv.Value.dist;
            PlayerMoveOverlay overlay = dist <= speed          ? PlayerMoveOverlay.Zone1
                                      : dist <= speed * 2     ? PlayerMoveOverlay.Zone2
                                      :                         PlayerMoveOverlay.Zone3;
            tile.SetPlayerMove(overlay);
        }
    }

    private void ClearPath()
    {
        // Restore each path tile back to its zone colour (not None).
        var player = PlayerParty.Instance?.SelectedUnit;
        int speed  = player != null ? player.GetEffectiveMoveSpeed(player.MoveSpeed) : 3;

        foreach (var t in _pathTiles)
        {
            if (t == null) continue;
            if (_reachable != null && _reachable.TryGetValue(t.GridPosition, out var info))
            {
                int dist = info.dist;
                PlayerMoveOverlay overlay = dist <= speed      ? PlayerMoveOverlay.Zone1
                                          : dist <= speed * 2 ? PlayerMoveOverlay.Zone2
                                          :                     PlayerMoveOverlay.Zone3;
                t.SetPlayerMove(overlay);
            }
            else
            {
                t.SetPlayerMove(PlayerMoveOverlay.None);
            }
        }
        _pathTiles.Clear();
    }

    // ── Path tracing ──────────────────────────────────────────────────────────

    private List<Vector2Int> BuildPath(Vector2Int destination)
    {
        var path = new List<Vector2Int>();
        if (_reachable == null) return path;

        var player = PlayerParty.Instance?.SelectedUnit;
        if (player == null) return path;

        Vector2Int cur = destination;
        Vector2Int start = player.GridPosition;

        // Walk parent pointers back to start (exclusive).
        while (cur != start)
        {
            path.Add(cur);
            if (!_reachable.TryGetValue(cur, out var info)) break;
            cur = info.parent;
        }

        path.Reverse();
        return path;
    }

    // ── Movement execution ────────────────────────────────────────────────────

    private static void ExecuteMove(PlayerEntity player, List<Vector2Int> path)
    {
        if (path.Count == 0) return;
        player.PlaceAt(path[path.Count - 1]);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    /// <summary>How much stamina does a move of <paramref name="dist"/> tiles cost?</summary>
    private static int StaminaCostFor(int dist)
    {
        var player = PlayerParty.Instance?.SelectedUnit;
        int speed  = player != null ? player.GetEffectiveMoveSpeed(player.MoveSpeed) : 3;
        if (speed <= 0) return dist; // degenerate: 1 stamina per tile
        return Mathf.CeilToInt((float)dist / speed);
    }
}

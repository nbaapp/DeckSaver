using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>An enemy entity on the grid.</summary>
public class EnemyEntity : Entity, IPointerEnterHandler, IPointerExitHandler
{
    public EnemyData data;

    /// <summary>The attack this enemy has telegraphed for the current round.</summary>
    public EnemyAttack SelectedAttack { get; private set; }

    private TextMeshPro _intentLabel;
    private readonly HashSet<Vector2Int> _rangeTiles = new();

    // ── Unity lifecycle ───────────────────────────────────────────────────────

    protected override void Awake()
    {
        base.Awake();
        // OnMouseEnter/Exit require a Collider2D on the entity.
        if (GetComponent<Collider2D>() == null)
        {
            var col = gameObject.AddComponent<CircleCollider2D>();
            col.radius = 0.4f;
        }
    }

    // ── Initialisation ────────────────────────────────────────────────────────

    public void Init(EnemyData enemyData)
    {
        data          = enemyData;
        maxHealth     = enemyData.maxHealth;
        currentHealth = maxHealth;

        if (enemyData.artwork != null)
            GetComponent<SpriteRenderer>().sprite = enemyData.artwork;

        BuildIntentLabel();
        OnDeath += HandleDeath;
    }

    private void BuildIntentLabel()
    {
        var go = new GameObject("IntentLabel");
        go.transform.SetParent(transform, false);
        go.transform.localPosition = new Vector3(0f, -0.5f, 0f);

        _intentLabel              = go.AddComponent<TextMeshPro>();
        _intentLabel.fontSize     = 1.8f;
        _intentLabel.alignment    = TextAlignmentOptions.Center;
        _intentLabel.sortingOrder = 3;
        _intentLabel.enabled      = false;
    }

    // ── Turn logic ────────────────────────────────────────────────────────────

    /// <summary>Called during EnemySelect phase — picks an attack for this round.</summary>
    public void SelectAttack()
    {
        if (data == null || data.attacks == null || data.attacks.Count == 0)
        {
            SelectedAttack = null;
            return;
        }
        SelectedAttack = data.attacks[Random.Range(0, data.attacks.Count)];
    }

    // ── Range display (shown on mouse hover) ──────────────────────────────────

    public void OnPointerEnter(PointerEventData _)
    {
        // Don't show range while the player is aiming a card — that would
        // block tile targeting and is visually noisy.
        if (HandDisplay.Instance?.SelectedCard != null) return;
        ShowRange();
    }

    public void OnPointerExit(PointerEventData _) => ClearRange();

    /// <summary>
    /// Highlights the selected attack's threat zone on hover.
    ///
    /// Dim layer  — full possible zone: blue = reachable move tiles,
    ///              orange = attackable tiles from anywhere in that zone.
    /// Bright layer — specific plan: the actual move path the enemy will take
    ///              (bright blue) and the exact attack tiles from the landing
    ///              position (bright orange).
    ///
    /// Does nothing if no attack has been selected yet.
    /// </summary>
    public void ShowRange()
    {
        if (SelectedAttack == null) return;
        ClearRange();

        // ── Dim zone ──────────────────────────────────────────────────────────
        int move = GetEffectiveMoveSpeed(SelectedAttack.moveRange);

        var moveTiles = new HashSet<Vector2Int>();
        for (int dx = -move; dx <= move; dx++)
            for (int dy = -move; dy <= move; dy++)
            {
                if (Mathf.Abs(dx) + Mathf.Abs(dy) > move) continue;
                if (dx == 0 && dy == 0) continue;
                var pos = GridPosition + new Vector2Int(dx, dy);
                if (GridManager.Instance.IsInBounds(pos))
                    moveTiles.Add(pos);
            }

        var attackOffsets = GetAttackOffsets(SelectedAttack);

        var zoneTiles = new HashSet<Vector2Int>();
        var origins = new HashSet<Vector2Int>(moveTiles) { GridPosition };
        foreach (var origin in origins)
            foreach (var offset in attackOffsets)
            {
                var pos = origin + offset;
                if (!GridManager.Instance.IsInBounds(pos)) continue;
                if (moveTiles.Contains(pos) || pos == GridPosition) continue;
                zoneTiles.Add(pos);
            }

        foreach (var pos in moveTiles)
        {
            GridManager.Instance.GetTile(pos)?.SetEnemyRange(EnemyRangeOverlay.ZoneMove);
            _rangeTiles.Add(pos);
        }
        foreach (var pos in zoneTiles)
        {
            GridManager.Instance.GetTile(pos)?.SetEnemyRange(EnemyRangeOverlay.ZoneAttack);
            _rangeTiles.Add(pos);
        }

        // ── Bright plan ───────────────────────────────────────────────────────
        var planPath   = GetPlannedMovePath();           // tiles walked through
        var landingPos = planPath.Count > 0 ? planPath[^1] : GridPosition;
        var planAttack = GetPlannedAttackTiles(landingPos);

        // Path tiles upgrade ZoneMove → PlanMove (or add PlanMove if not in zone).
        foreach (var pos in planPath)
        {
            GridManager.Instance.GetTile(pos)?.SetEnemyRange(EnemyRangeOverlay.PlanMove);
            _rangeTiles.Add(pos);
        }
        // Attack tiles upgrade ZoneAttack → PlanAttack.
        foreach (var pos in planAttack)
        {
            GridManager.Instance.GetTile(pos)?.SetEnemyRange(EnemyRangeOverlay.PlanAttack);
            _rangeTiles.Add(pos);
        }

        // ── Intent label ──────────────────────────────────────────────────────
        if (_intentLabel != null)
        {
            string summary = BuildAttackSummary(SelectedAttack);
            string text    = $"<color=#ffaa44>{SelectedAttack.attackName}</color>";
            if (summary.Length > 0)
                text += $"\n<color=#dddddd>{summary}</color>";
            _intentLabel.text    = text;
            _intentLabel.enabled = true;
        }
    }

    // ── Plan simulation ───────────────────────────────────────────────────────

    /// <summary>
    /// Simulates the enemy's planned movement and returns each tile stepped
    /// through (including the landing tile), using the same destination logic
    /// as ExecuteTurn so the highlight always matches the actual behaviour.
    /// </summary>
    private List<Vector2Int> GetPlannedMovePath()
    {
        int steps       = GetEffectiveMoveSpeed(SelectedAttack.moveRange);
        var destination = ChooseLandingPosition(SelectedAttack, steps);
        var pos         = GridPosition;
        var path        = new List<Vector2Int>();

        for (int i = 0; i < steps; i++)
        {
            if (pos == destination) break;
            var next = StepToward(pos, destination);
            if (next == pos) break;
            if (EntityManager.Instance.GetEntityAt(next) != null) break;
            pos = next;
            path.Add(pos);
        }
        return path;
    }

    /// <summary>
    /// Returns the tiles this attack will target from <paramref name="fromPos"/>:
    /// all pattern tiles for pattern attacks, or just the nearest entity's tile
    /// for range attacks (mirroring execution logic so the highlight is accurate).
    /// </summary>
    private List<Vector2Int> GetPlannedAttackTiles(Vector2Int fromPos)
    {
        // Delegate to the same logic used during execution so the two always agree.
        return GetAttackPositions(SelectedAttack, fromPos);
    }

    private static string BuildAttackSummary(EnemyAttack attack)
    {
        if (attack.effects == null || attack.effects.Count == 0) return "";
        var sb = new System.Text.StringBuilder();
        foreach (var e in attack.effects)
        {
            if (sb.Length > 0) sb.Append(", ");
            sb.Append(e.type switch
            {
                EffectType.Strike => e.hits > 1 ? $"{e.baseValue} dmg ×{e.hits}" : $"{e.baseValue} dmg",
                EffectType.Block  => $"+{e.baseValue} block",
                EffectType.Heal   => $"+{e.baseValue} heal",
                EffectType.Status => $"{e.statusType}({e.baseValue})",
                _                 => e.type.ToString(),
            });
        }
        return sb.ToString();
    }

    /// <summary>Removes the range overlay and hides the intent label.</summary>
    public void ClearRange()
    {
        foreach (var pos in _rangeTiles)
            GridManager.Instance?.GetTile(pos)?.SetEnemyRange(EnemyRangeOverlay.None);
        _rangeTiles.Clear();

        if (_intentLabel != null)
            _intentLabel.enabled = false;
    }

    /// <summary>
    /// Returns the set of offsets used to paint the dim zone for this attack.
    /// RangedSingle/FixedPattern → range radius (where the target/center can be).
    /// DirectionalPattern → union of all 4 rotations of the pattern.
    /// </summary>
    private static HashSet<Vector2Int> GetAttackOffsets(EnemyAttack attack)
    {
        var offsets = new HashSet<Vector2Int>();

        switch (attack.patternType)
        {
            case EnemyAttackPatternType.RangedSingle:
            case EnemyAttackPatternType.FixedPattern:
            {
                int r = attack.attackRange;
                for (int dx = -r; dx <= r; dx++)
                    for (int dy = -r; dy <= r; dy++)
                        if (Mathf.Abs(dx) + Mathf.Abs(dy) <= r && (dx != 0 || dy != 0))
                            offsets.Add(new Vector2Int(dx, dy));
                break;
            }

            case EnemyAttackPatternType.DirectionalPattern:
            {
                // Show all 4 possible rotations so the player can see the full threat shape.
                var dirs = new[] { Vector2Int.up, Vector2Int.right, Vector2Int.down, Vector2Int.left };
                foreach (var dir in dirs)
                    foreach (var offset in attack.attackPattern)
                        if (offset != Vector2Int.zero)
                            offsets.Add(RotateOffset(offset, dir));
                break;
            }
        }

        return offsets;
    }

    /// <summary>Called during EnemyExecute phase — move then attack.</summary>
    public void ExecuteTurn()
    {
        if (SelectedAttack == null) return;

        ClearRange();
        int steps       = GetEffectiveMoveSpeed(SelectedAttack.moveRange);
        var destination = ChooseLandingPosition(SelectedAttack, steps);
        MoveToDestination(destination, steps);
        PerformAttack(SelectedAttack);
    }

    // ── Movement AI ───────────────────────────────────────────────────────────

    /// <summary>
    /// Picks the best landing tile for this attack:
    ///   • Among all reachable tiles that can hit the player, choose the farthest
    ///     from the player (kite — stay at maximum safe distance while still in range).
    ///   • If no reachable tile can hit the player, choose the closest reachable tile
    ///     (close the gap).
    /// </summary>
    private Vector2Int ChooseLandingPosition(EnemyAttack attack, int steps)
    {
        var player = PlayerEntity.Instance;
        if (player == null) return GridPosition;

        var reachable = ComputeReachableTiles(steps);

        var canHit = reachable
            .Where(p => CanHitPlayerFrom(p, attack))
            .ToList();

        if (canHit.Count > 0)
            return canHit.OrderByDescending(p => Manhattan(p, player.GridPosition)).First();

        return reachable
            .OrderBy(p => Manhattan(p, player.GridPosition))
            .First();
    }

    /// <summary>
    /// BFS from the enemy's current position. Returns every tile the enemy can
    /// legally land on within <paramref name="maxSteps"/> cardinal steps.
    /// Occupied tiles (entities) are not valid landing spots and block further
    /// exploration through them.
    /// </summary>
    private HashSet<Vector2Int> ComputeReachableTiles(int maxSteps)
    {
        var visited = new HashSet<Vector2Int> { GridPosition };
        var queue   = new Queue<(Vector2Int pos, int steps)>();
        queue.Enqueue((GridPosition, 0));

        while (queue.Count > 0)
        {
            var (pos, steps) = queue.Dequeue();
            if (steps >= maxSteps) continue;

            foreach (var dir in CardinalDirs)
            {
                var next = pos + dir;
                if (!GridManager.Instance.IsInBounds(next)) continue;
                if (visited.Contains(next)) continue;
                if (EntityManager.Instance.GetEntityAt(next) != null) continue;
                visited.Add(next);
                queue.Enqueue((next, steps + 1));
            }
        }

        return visited;
    }

    private static readonly Vector2Int[] CardinalDirs =
    {
        Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right
    };

    /// <summary>Returns true if this attack can reach the player from <paramref name="pos"/>.</summary>
    private static bool CanHitPlayerFrom(Vector2Int pos, EnemyAttack attack)
    {
        var player = PlayerEntity.Instance;
        if (player == null) return false;
        var playerPos = player.GridPosition;

        switch (attack.patternType)
        {
            case EnemyAttackPatternType.RangedSingle:
            case EnemyAttackPatternType.FixedPattern:
                return Manhattan(pos, playerPos) <= attack.attackRange;

            case EnemyAttackPatternType.DirectionalPattern:
            {
                var dir = CardinalDirection(pos, playerPos);
                foreach (var offset in attack.attackPattern)
                    if (pos + RotateOffset(offset, dir) == playerPos)
                        return true;
                return false;
            }
        }
        return false;
    }

    private static int Manhattan(Vector2Int a, Vector2Int b) =>
        Mathf.Abs(a.x - b.x) + Mathf.Abs(a.y - b.y);

    private void MoveToDestination(Vector2Int destination, int steps)
    {
        for (int i = 0; i < steps; i++)
        {
            if (GridPosition == destination) break;
            var next = StepToward(GridPosition, destination);
            if (next == GridPosition) break;
            if (EntityManager.Instance.GetEntityAt(next) != null) break;
            PlaceAt(next);
        }
    }

    /// <summary>One cardinal step toward <paramref name="target"/>.</summary>
    private static Vector2Int StepToward(Vector2Int from, Vector2Int to)
    {
        int dx = to.x - from.x;
        int dy = to.y - from.y;
        if (Mathf.Abs(dx) >= Mathf.Abs(dy) && dx != 0)
            return from + new Vector2Int((int)Mathf.Sign(dx), 0);
        if (dy != 0)
            return from + new Vector2Int(0, (int)Mathf.Sign(dy));
        return from;
    }

    // ── Attack ────────────────────────────────────────────────────────────────

    private void PerformAttack(EnemyAttack attack)
    {
        if (attack.effects == null || attack.effects.Count == 0) return;

        var positions = GetAttackPositions(attack);

        foreach (var effect in attack.effects)
        {
            foreach (var pos in positions)
            {
                var entity = EntityManager.Instance.GetEntityAt(pos);
                if (entity == null) continue;
                ResolveEffect(effect, this, entity);
            }
        }
    }

    /// <summary>
    /// Resolves a single CardEffect from attacker against target.
    /// Strikes route through StatusResolver for full status processing.
    /// Raw values are used (no tile-modifier pipeline — that is player-card territory).
    /// </summary>
    private static void ResolveEffect(CardEffect effect, Entity attacker, Entity target)
    {
        int count = Mathf.Max(1, effect.hits);
        switch (effect.type)
        {
            case EffectType.Strike:
                for (int i = 0; i < count; i++)
                    StatusResolver.ApplyStrike(attacker, target, attacker.GridPosition, effect.baseValue, out _);
                break;

            case EffectType.Block:
                for (int i = 0; i < count; i++)
                    target.GainBlock(effect.baseValue);
                break;

            case EffectType.Heal:
                for (int i = 0; i < count; i++)
                    target.Heal(effect.baseValue);
                break;

            case EffectType.Status:
                for (int i = 0; i < count; i++)
                    target.ApplyStatus(effect.statusType, effect.baseValue);
                break;

            default:
                Debug.Log($"[EnemyEntity] Effect {effect.type} not yet handled.");
                break;
        }
    }

    private List<Vector2Int> GetAttackPositions(EnemyAttack attack) =>
        GetAttackPositions(attack, GridPosition);

    private static List<Vector2Int> GetAttackPositions(EnemyAttack attack, Vector2Int fromPos)
    {
        var list = new List<Vector2Int>();

        switch (attack.patternType)
        {
            case EnemyAttackPatternType.RangedSingle:
            {
                // Target the nearest entity within range (single tile).
                var target = NearestEntityInRange(fromPos, attack.attackRange);
                if (target.HasValue)
                    list.Add(target.Value);
                break;
            }

            case EnemyAttackPatternType.DirectionalPattern:
            {
                // Rotate the pattern to face the nearest entity (always from landing tile).
                var target = NearestEntityInRange(fromPos, int.MaxValue);
                var dir    = target.HasValue
                    ? CardinalDirection(fromPos, target.Value)
                    : Vector2Int.up;
                foreach (var offset in attack.attackPattern)
                    list.Add(fromPos + RotateOffset(offset, dir));
                break;
            }

            case EnemyAttackPatternType.FixedPattern:
            {
                // Place the pattern center on the nearest entity within range.
                var center = NearestEntityInRange(fromPos, attack.attackRange);
                if (center.HasValue)
                    foreach (var offset in attack.attackPattern)
                        list.Add(center.Value + offset);
                break;
            }
        }

        return list.Where(p => p != fromPos && GridManager.Instance.IsInBounds(p)).ToList();
    }

    /// <summary>
    /// Returns the nearest entity's grid position within Manhattan <paramref name="range"/>
    /// of <paramref name="fromPos"/>, or null if none qualify.
    /// Excludes any entity sitting on <paramref name="fromPos"/> itself (the attacker).
    /// </summary>
    private static Vector2Int? NearestEntityInRange(Vector2Int fromPos, int range)
    {
        Vector2Int? best     = null;
        int         bestDist = int.MaxValue;

        var player = PlayerEntity.Instance;
        if (player != null)
        {
            int d = Manhattan(player.GridPosition, fromPos);
            if (d > 0 && d <= range && d < bestDist) { best = player.GridPosition; bestDist = d; }
        }

        foreach (var enemy in EntityManager.Instance.Enemies)
        {
            if (enemy == null) continue;
            int d = Manhattan(enemy.GridPosition, fromPos);
            if (d > 0 && d <= range && d < bestDist) { best = enemy.GridPosition; bestDist = d; }
        }

        return best;
    }

    /// <summary>Returns the dominant cardinal direction from <paramref name="from"/> toward <paramref name="to"/>.</summary>
    private static Vector2Int CardinalDirection(Vector2Int from, Vector2Int to)
    {
        int dx = to.x - from.x;
        int dy = to.y - from.y;
        if (Mathf.Abs(dx) >= Mathf.Abs(dy) && dx != 0)
            return new Vector2Int((int)Mathf.Sign(dx), 0);
        if (dy != 0)
            return new Vector2Int(0, (int)Mathf.Sign(dy));
        return Vector2Int.up;
    }

    /// <summary>
    /// Rotates an offset so a pattern authored facing up (+y) instead faces <paramref name="dir"/>.
    /// </summary>
    private static Vector2Int RotateOffset(Vector2Int o, Vector2Int dir)
    {
        if (dir == Vector2Int.up)    return o;
        if (dir == Vector2Int.right) return new Vector2Int( o.y, -o.x);
        if (dir == Vector2Int.down)  return new Vector2Int(-o.x, -o.y);
        if (dir == Vector2Int.left)  return new Vector2Int(-o.y,  o.x);
        return o;
    }

    // ── Death ─────────────────────────────────────────────────────────────────

    private void HandleDeath()
    {
        ClearRange();
        BattleEvents.FireEnemyKilled(this);
        EntityManager.Instance?.RemoveEnemy(this);
        GridManager.Instance?.GetTile(GridPosition)?.SetState(TileVisualState.Normal);
        Destroy(gameObject);
    }
}

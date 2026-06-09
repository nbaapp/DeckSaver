using UnityEngine;

/// <summary>
/// Resolves Push and Pull movement (cards, statuses, commander actives — anything that displaces a unit).
///
/// Direction snaps to the dominant cardinal axis of (target - anchor); ties prefer horizontal. Push moves
/// away from the anchor, Pull reverses that. Rooted absorbs knockback like Block absorbs damage: each
/// stack soaks one tile of displacement and is consumed in the process. Movement halts at the first
/// blocked step (grid edge or another entity); both colliders take damage equal to the remaining
/// distance (wall collisions damage only the target).
///
/// Active <see cref="KnockbackRules"/> (from boons / the Commander) can bend this: ignore Rooted,
/// remove the distance falloff on collision damage, grant player units immunity to collision damage,
/// or deal per-tile damage to enemies for every square they travel. After resolving, fires
/// <see cref="BattleEvents.OnForcedMovement"/> with the tiles actually traveled.
/// </summary>
public static class KnockbackResolver
{
    /// <summary>
    /// Displace <paramref name="target"/> up to <paramref name="distance"/> tiles from <paramref name="anchor"/>.
    /// Push by default; pass isPull=true to invert direction.
    /// </summary>
    public static void Resolve(Entity target, Vector2Int anchor, int distance, bool isPull = false)
    {
        if (target == null || distance <= 0) return;

        // Rooted soaks displacement and decays by the amount absorbed — unless rules ignore Rooted.
        if (!KnockbackRules.IgnoresRooted)
        {
            int rooted   = target.GetStatusValue(StatusType.Rooted);
            int absorbed = Mathf.Min(rooted, distance);
            for (int i = 0; i < absorbed; i++) target.DecrementStatus(StatusType.Rooted);
            distance -= absorbed;
            if (distance <= 0) return;
        }

        Vector2Int dir = GetDirection(anchor, target.GridPosition);
        if (dir == Vector2Int.zero) return;
        if (isPull) dir = -dir;

        int intended = distance;   // distance the unit was set to travel (post-Rooted)
        int traveled = 0;          // tiles actually moved before stopping

        for (int i = 0; i < distance; i++)
        {
            Vector2Int next      = target.GridPosition + dir;
            int        remaining = distance - i;
            // Falloff: collision damage normally decays as the unit slides; the rule pins it to full intended distance.
            int        impact    = KnockbackRules.IgnoreDistanceFalloff ? intended : remaining;

            if (!GridManager.Instance.IsInBounds(next))
            {
                DealCollisionDamage(target, impact); // wall collision — only target
                break;
            }

            var blocker = EntityManager.Instance.GetEntityAt(next);
            if (blocker != null)
            {
                DealCollisionDamage(target,  impact);
                DealCollisionDamage(blocker, impact);
                break;
            }

            target.PlaceAt(next);
            traveled++;
        }

        // Per-tile travel damage to enemies ("1 dmg per square of forced movement traveled").
        int perTile = KnockbackRules.DamagePerTileVsEnemies;
        if (traveled > 0 && perTile > 0 && target is EnemyEntity)
            target.TakeDamage(traveled * perTile);

        // Notify boons / Commander (OnForcedMovement) of the actual displacement.
        if (traveled > 0)
            BattleEvents.FireForcedMovement(target, traveled);
    }

    /// <summary>Collision damage, gated by the player-unit immunity rule. Enemies are never immune.</summary>
    private static void DealCollisionDamage(Entity entity, int amount)
    {
        if (entity == null || amount <= 0) return;
        if (KnockbackRules.PlayerImmuneToCollisionDamage && !(entity is EnemyEntity)) return;
        entity.TakeDamage(amount);
    }

    /// <summary>
    /// Direction from <paramref name="anchor"/> toward <paramref name="targetPos"/> — i.e., the direction
    /// the target flies when pushed. When the target is exactly diagonal from the anchor (|dx| == |dy|,
    /// both nonzero), pushes along the diagonal; otherwise snaps to the dominant cardinal axis.
    /// </summary>
    private static Vector2Int GetDirection(Vector2Int anchor, Vector2Int targetPos)
    {
        int dx = targetPos.x - anchor.x;
        int dy = targetPos.y - anchor.y;

        if (dx == 0 && dy == 0) return Vector2Int.zero;

        // Exact diagonal — push along it. Each diagonal step still counts as 1 tile of knockback.
        if (dx != 0 && dy != 0 && Mathf.Abs(dx) == Mathf.Abs(dy))
            return new Vector2Int((int)Mathf.Sign(dx), (int)Mathf.Sign(dy));

        if (Mathf.Abs(dx) > Mathf.Abs(dy))
            return new Vector2Int((int)Mathf.Sign(dx), 0);
        return new Vector2Int(0, (int)Mathf.Sign(dy));
    }
}

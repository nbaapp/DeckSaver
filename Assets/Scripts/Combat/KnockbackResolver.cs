using UnityEngine;

/// <summary>
/// Handles knockback movement and collision damage.
///
/// Direction is always cardinal (N/S/E/W), directly away from the attacker.
/// Rooted status on the target reduces the knockback distance.
/// If the target hits a wall or another entity, both take damage equal to
/// the remaining knockback distance (tiles that couldn't be travelled).
/// </summary>
public static class KnockbackResolver
{
    /// <summary>
    /// Moves <paramref name="target"/> away from <paramref name="attackerPos"/>
    /// by up to <paramref name="distance"/> tiles.
    /// </summary>
    public static void Apply(Entity target, Vector2Int attackerPos, int distance)
    {
        if (distance <= 0) return;

        // Rooted reduces knockback
        distance = Mathf.Max(0, distance - target.GetStatusValue(StatusType.Rooted));
        if (distance <= 0) return;

        Vector2Int dir = GetDirection(attackerPos, target.GridPosition);
        if (dir == Vector2Int.zero) return; // same tile — no meaningful direction

        for (int i = 0; i < distance; i++)
        {
            Vector2Int next  = target.GridPosition + dir;
            int        remaining = distance - i; // tiles still to travel including this one

            if (!GridManager.Instance.IsInBounds(next))
            {
                // Hit the edge — impact damage
                target.TakeDamage(remaining);
                return;
            }

            Entity blocker = EntityManager.Instance.GetEntityAt(next);
            if (blocker != null)
            {
                // Hit another entity — split impact damage
                target.TakeDamage(remaining);
                blocker.TakeDamage(remaining);
                return;
            }

            target.PlaceAt(next);
        }
    }

    /// <summary>
    /// Cardinal direction from <paramref name="attackerPos"/> toward (and past)
    /// <paramref name="targetPos"/> — i.e., the direction the target flies.
    /// Prefers the axis with the greater distance; ties go horizontal.
    /// </summary>
    private static Vector2Int GetDirection(Vector2Int attackerPos, Vector2Int targetPos)
    {
        int dx = targetPos.x - attackerPos.x;
        int dy = targetPos.y - attackerPos.y;

        if (dx == 0 && dy == 0) return Vector2Int.zero;

        if (Mathf.Abs(dx) >= Mathf.Abs(dy) && dx != 0)
            return new Vector2Int((int)Mathf.Sign(dx), 0);
        if (dy != 0)
            return new Vector2Int(0, (int)Mathf.Sign(dy));

        return Vector2Int.zero;
    }
}

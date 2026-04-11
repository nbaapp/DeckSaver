using UnityEngine;

/// <summary>
/// Central hub for all status-effect logic.
/// Both CardPlayManager (player Strikes) and EnemyEntity (enemy Strikes) route
/// through here so every status is processed identically for both sides.
/// </summary>
public static class StatusResolver
{
    private const float CritMultiplier = 2f;

    // ── Start of turn ─────────────────────────────────────────────────────────

    /// <summary>
    /// Resolves Poison and Burn damage at the start of an entity's turn.
    /// Call before the entity takes any actions.
    /// </summary>
    public static void ResolveStartOfTurn(Entity entity)
    {
        if (entity == null) return;

        // Poison: deal X damage, then -1 value
        int poison = entity.GetStatusValue(StatusType.Poison);
        if (poison > 0)
        {
            entity.TakeDamage(poison);
            entity.DecrementStatus(StatusType.Poison);
        }

        // Burn: deal X damage, then halve value
        int burn = entity.GetStatusValue(StatusType.Burn);
        if (burn > 0)
        {
            entity.TakeDamage(burn);
            entity.HalveStatus(StatusType.Burn);
        }
    }

    // ── Strike pipeline ───────────────────────────────────────────────────────

    /// <summary>
    /// Applies a single Strike hit from attacker to target, processing all status modifiers.
    ///
    /// Modifier order:
    ///   1. Attacker's Weak (–) and Strong (+) affect outgoing damage.
    ///   2. Target's Hard (–) affects incoming damage.
    ///   3. Targeted (guaranteed crit) or Focused (% crit chance) → ×CritMultiplier.
    ///   4. Warded zeroes the hit damage; secondary effects still apply.
    ///   5. TakeDamage called.
    ///   6. Secondary on-hit effects: Bleed, Shattered, Spikes, OffBalance, Targeted decrement.
    /// </summary>
    /// <param name="attacker">Entity dealing the Strike (null = environmental / no attacker).</param>
    /// <param name="target">Entity receiving the Strike.</param>
    /// <param name="attackerPos">Grid position of attacker (for knockback direction).</param>
    /// <param name="baseDamage">Base damage before status modifiers.</param>
    /// <param name="wasCrit">Output: true if the hit was a critical strike.</param>
    public static void ApplyStrike(
        Entity attacker, Entity target,
        Vector2Int attackerPos, int baseDamage,
        out bool wasCrit)
    {
        wasCrit = false;
        int damage = baseDamage;

        // ── Attacker modifiers ────────────────────────────────────────────────
        if (attacker != null)
        {
            damage -= attacker.GetStatusValue(StatusType.Weak);   // Weak: deal less
            damage += attacker.GetStatusValue(StatusType.Strong);  // Strong: deal more
        }

        // ── Target modifiers ──────────────────────────────────────────────────
        damage -= target.GetStatusValue(StatusType.Hard);   // Hard: take less

        // ── Critical hit ──────────────────────────────────────────────────────
        bool isCrit = target.HasStatus(StatusType.Targeted) ||
                      (attacker != null &&
                       attacker.HasStatus(StatusType.Focused) &&
                       Random.Range(0, 100) < attacker.GetStatusValue(StatusType.Focused));
        if (isCrit)
        {
            damage  = Mathf.RoundToInt(damage * CritMultiplier);
            wasCrit = true;
            Debug.Log($"[Combat] Critical hit! {attacker?.name ?? "?"} → {target.name} ({damage} dmg)");
        }

        // ── Warded: zero the damage; secondary effects still apply ────────────
        if (target.HasStatus(StatusType.Warded))
        {
            target.DecrementStatus(StatusType.Warded);
            damage = 0;
        }

        int finalDamage = Mathf.Max(0, damage);
        target.TakeDamage(finalDamage);

        // Fire battle events for the player's perspective
        if (attacker is PlayerEntity) BattleEvents.FirePlayerStrike(target, finalDamage);
        if (target   is PlayerEntity) BattleEvents.FirePlayerHit(attacker, finalDamage);

        // ── Secondary on-hit effects (Warded does not suppress these) ─────────

        // Bleed: extra direct damage per hit
        int bleed = target.GetStatusValue(StatusType.Bleed);
        if (bleed > 0)
            target.TakeDamage(bleed);

        // Shattered: extra block-only damage per hit (no HP overflow)
        int shattered = target.GetStatusValue(StatusType.Shatter);
        if (shattered > 0)
            target.TakeBlockDamageOnly(shattered);

        // Spikes: damage reflected to attacker; –1 per hit
        if (attacker != null && target.HasStatus(StatusType.Spikes))
        {
            attacker.TakeDamage(target.GetStatusValue(StatusType.Spikes));
            target.DecrementStatus(StatusType.Spikes);
        }

        // OffBalance: knock target away from attacker (value = knockback tiles)
        int offBalance = target.GetStatusValue(StatusType.OffBalance);
        if (offBalance > 0)
            KnockbackResolver.Apply(target, attackerPos, offBalance);

        // Targeted: consume one charge regardless of damage dealt
        if (target.HasStatus(StatusType.Targeted))
            target.DecrementStatus(StatusType.Targeted);
    }

    // ── End of turn tick ──────────────────────────────────────────────────────

    /// <summary>
    /// Decrements or halves statuses that decay at the end of an entity's turn.
    ///
    /// NOT handled here (they have their own timing):
    ///   Stunned    — decrements after the skip in TurnManager.
    ///   Poison     — decrements after dealing damage at start of turn.
    ///   Burn       — halves after dealing damage at start of turn.
    ///   Warded     — decrements per hit (in ApplyStrike).
    ///   Spikes     — decrements per hit (in ApplyStrike).
    ///   Targeted   — decrements per hit (in ApplyStrike).
    ///   Warded     — decrements per hit (in ApplyStrike).
    ///   Spikes     — decrements per hit (in ApplyStrike).
    ///   Targeted   — decrements per hit (in ApplyStrike).
    /// </summary>
    public static void TickEndOfTurn(Entity entity)
    {
        if (entity == null) return;
        entity.DecrementStatus(StatusType.Shatter);    // –1 per turn
        entity.DecrementStatus(StatusType.Bleed);      // –1 per turn
        entity.DecrementStatus(StatusType.Weak);       // –1 per turn
        entity.DecrementStatus(StatusType.Strong);     // –1 per turn
        entity.DecrementStatus(StatusType.Hard);       // –1 per turn
        entity.DecrementStatus(StatusType.OffBalance); // –1 per turn
        entity.DecrementStatus(StatusType.Rooted);     // –1 per turn
        entity.DecrementStatus(StatusType.Slow);       // –1 per turn
        entity.DecrementStatus(StatusType.Haste);      // –1 per turn
        entity.HalveStatus(StatusType.Focused);        // halve per turn
    }
}

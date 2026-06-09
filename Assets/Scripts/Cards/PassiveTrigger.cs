public enum PassiveTrigger
{
    StatModifier,       // Always-on stat change; not event-driven
    StatusImmunity,     // Permanent immunity to a specific status (uses specificStatus field)
    OnStrike,           // When player lands a Strike
    OnBlockGain,        // When player gains block
    OnTurnStart,        // Start of player's turn
    OnBattleStart,      // Start of battle
    OnCardPlay,         // When any card is played from hand
    OnKill,             // When any enemy is killed
    OnStatusApplied,    // When player receives a status effect
    OnHit,              // When player is struck
    OnDamage,           // When player loses HP (net > 0)
    OnDraw,             // When player draws a card
    OnDiscard,          // When player discards a card
    OnForcedMovement,   // When a unit is displaced by Push/Pull/OffBalance. Context entity = the moved unit; amount = tiles actually traveled.

    // ── Forced-movement config passives (presence/statValue scanned at resolve time; no effects/target) ──
    KnockbackDamagePerTile,         // Each tile an ENEMY is forcibly moved deals statValue damage to it ("1 dmg per square traveled").
    KnockbackIgnoreDistanceFalloff, // Collision damage uses the full intended distance instead of the distance remaining at impact.
    KnockbackIgnoresRooted,         // Rooted no longer soaks displacement — knockback lands at full distance.
    KnockbackDamageImmunity,        // Player units take no knockback collision damage.

    Special             // Custom behavior; handled in code by Commander name/ID
}

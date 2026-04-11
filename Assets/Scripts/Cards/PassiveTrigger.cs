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
    Special             // Custom behavior; handled in code by Commander name/ID
}

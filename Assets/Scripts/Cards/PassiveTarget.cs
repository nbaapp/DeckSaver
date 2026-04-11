public enum PassiveTarget
{
    Self,
    AllEnemies,
    StrongestEnemy,     // Highest current HP
    WeakestEnemy,       // Lowest current HP
    NearestEnemy,       // Closest by Manhattan distance to player
    StrikeTarget,       // The entity that was just struck (OnStrike context)
    Attacker            // The entity that just hit the player (OnHit context)
}

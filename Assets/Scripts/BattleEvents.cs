using System;

/// <summary>
/// Static event hub for game-wide battle events.
///
/// Systems fire events here; the CommanderController (and any future listeners)
/// subscribe to respond. Using static events avoids requiring references between
/// every system pair.
/// </summary>
public static class BattleEvents
{
    // ── Lifecycle ─────────────────────────────────────────────────────────────
    public static event Action OnBattleStart;
    public static event Action OnPlayerTurnStart;

    // ── Card events ───────────────────────────────────────────────────────────
    public static event Action<CardData> OnCardPlayed;
    public static event Action<CardData> OnCardDrawn;
    public static event Action<CardData> OnCardDiscarded;

    // ── Combat events ─────────────────────────────────────────────────────────

    /// <summary>Fired when the player lands a Strike. Args: target, final damage dealt.</summary>
    public static event Action<Entity, int> OnPlayerStrike;

    /// <summary>Fired when the player gains block. Arg: amount gained.</summary>
    public static event Action<int> OnPlayerBlockGain;

    /// <summary>Fired when the player is hit by a Strike. Args: attacker (may be null), damage before block.</summary>
    public static event Action<Entity, int> OnPlayerHit;

    /// <summary>Fired when the player loses HP (net damage after block > 0). Arg: net HP lost.</summary>
    public static event Action<int> OnPlayerDamaged;

    /// <summary>Fired when an enemy is killed.</summary>
    public static event Action<EnemyEntity> OnEnemyKilled;

    /// <summary>Fired when a player unit is permanently killed (removed from the run).</summary>
    public static event Action<PlayerEntity> OnUnitDied;

    /// <summary>Fired when a status is applied to the player. Args: type, stacks.</summary>
    public static event Action<StatusType, int> OnPlayerStatusReceived;

    // ── Fire helpers ──────────────────────────────────────────────────────────

    public static void FireBattleStart()                                  => OnBattleStart?.Invoke();
    public static void FirePlayerTurnStart()                              => OnPlayerTurnStart?.Invoke();
    public static void FireCardPlayed(CardData c)                         => OnCardPlayed?.Invoke(c);
    public static void FireCardDrawn(CardData c)                          => OnCardDrawn?.Invoke(c);
    public static void FireCardDiscarded(CardData c)                      => OnCardDiscarded?.Invoke(c);
    public static void FirePlayerStrike(Entity target, int damage)        => OnPlayerStrike?.Invoke(target, damage);
    public static void FirePlayerBlockGain(int amount)                    => OnPlayerBlockGain?.Invoke(amount);
    public static void FirePlayerHit(Entity attacker, int damage)         => OnPlayerHit?.Invoke(attacker, damage);
    public static void FirePlayerDamaged(int net)                         => OnPlayerDamaged?.Invoke(net);
    public static void FireEnemyKilled(EnemyEntity enemy)                 => OnEnemyKilled?.Invoke(enemy);
    public static void FireUnitDied(PlayerEntity unit)                    => OnUnitDied?.Invoke(unit);
    public static void FirePlayerStatusReceived(StatusType t, int stacks) => OnPlayerStatusReceived?.Invoke(t, stacks);
}

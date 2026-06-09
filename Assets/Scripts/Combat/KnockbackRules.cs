/// <summary>
/// Aggregates the active forced-movement rules contributed by the player's boons and Commander.
/// Queried by <see cref="KnockbackResolver"/> at resolve time, mirroring how status immunity is
/// scanned on demand (BoonManager.IsImmuneToStatus / CommanderController.IsImmuneToStatus) rather
/// than cached — so there is no battle-lifecycle state to reset and no risk of stale flags.
///
/// Each rule maps to a config PassiveTrigger:
///   • KnockbackIgnoreDistanceFalloff → IgnoreDistanceFalloff   (Commander "Pushy McPushface" passive)
///   • KnockbackIgnoresRooted         → IgnoresRooted           (reworked "knockback ignores Rooted" boon)
///   • KnockbackDamagePerTile         → DamagePerTileVsEnemies  ("enemies take N dmg per square traveled" boon)
///   • KnockbackDamageImmunity        → PlayerImmuneToCollisionDamage ("immune to knockback damage" boon)
/// </summary>
public static class KnockbackRules
{
    public static bool IgnoreDistanceFalloff =>
        (BoonManager.Instance?.KnockbackIgnoresDistanceFalloff() ?? false) ||
        (CommanderController.Instance?.KnockbackIgnoresDistanceFalloff() ?? false);

    public static bool IgnoresRooted =>
        (BoonManager.Instance?.KnockbackIgnoresRooted() ?? false) ||
        (CommanderController.Instance?.KnockbackIgnoresRooted() ?? false);

    public static bool PlayerImmuneToCollisionDamage =>
        (BoonManager.Instance?.PlayerImmuneToKnockbackDamage() ?? false) ||
        (CommanderController.Instance?.PlayerImmuneToKnockbackDamage() ?? false);

    public static int DamagePerTileVsEnemies =>
        (BoonManager.Instance?.KnockbackDamagePerTile() ?? 0) +
        (CommanderController.Instance?.KnockbackDamagePerTile() ?? 0);
}

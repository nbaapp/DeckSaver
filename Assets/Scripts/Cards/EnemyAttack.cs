using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>How the attack pattern is positioned when the attack resolves.</summary>
public enum EnemyAttackPatternType
{
    /// <summary>
    /// No explicit pattern. Targets the nearest entity within attackRange
    /// (single-tile strike).
    /// </summary>
    RangedSingle,

    /// <summary>
    /// The attackPattern shape is rotated to face the player and applied
    /// directly from the enemy's landing tile. attackRange is ignored.
    /// </summary>
    DirectionalPattern,

    /// <summary>
    /// The attackPattern shape is NOT rotated. Its center is placed on the
    /// nearest entity within attackRange of the enemy's landing tile.
    /// </summary>
    FixedPattern,
}

/// <summary>
/// One attack option for an enemy.
/// The enemy selects an attack at the start of each round, telegraphs it to the
/// player, then executes it at the end of the round.
///
/// Damage and other effects are expressed as CardEffects — the same structure used
/// by player cards — so that buffs, triggers, and animations work identically on both.
/// A typical damaging attack has a Strike CardEffect in the effects list with the
/// desired baseValue (damage per hit) and hits count.
/// </summary>
[Serializable]
public class EnemyAttack
{
    public string attackName = "Attack";

    [Tooltip("How the attack pattern is positioned relative to the enemy/target.")]
    public EnemyAttackPatternType patternType = EnemyAttackPatternType.RangedSingle;

    [Tooltip("Tiles the enemy can move before attacking (Manhattan steps).")]
    public int moveRange = 1;

    [Tooltip("RangedSingle: max Manhattan distance to the target.\n" +
             "FixedPattern: max Manhattan distance the pattern center can be placed.\n" +
             "DirectionalPattern: ignored (always emanates from landing tile).")]
    public int attackRange = 1;

    [Tooltip("Tile offsets that define the attack shape for Directional/FixedPattern types.\n" +
             "Ignored for RangedSingle.\n" +
             "DirectionalPattern: offsets are rotated to face the player (default facing = up).\n" +
             "FixedPattern: offsets are fixed — the center is placed on the nearest target in range.")]
    public List<Vector2Int> attackPattern = new();

    [Tooltip("Effects applied to each entity on an affected tile.")]
    public List<CardEffect> effects = new();
}

using System.Collections.Generic;
using UnityEngine;

public enum EncounterType
{
    Battle,      // Normal encounter: a fight followed by a reward
    RewardOnly,  // No fight — player just picks a reward
}

/// <summary>
/// Describes one encounter in a run segment: what kind it is,
/// which enemies appear (for Battle type), and which reward pool
/// to draw from after completion.
///
/// Assign enemies with explicit grid positions so designers control
/// the layout without requiring separate scene files per encounter.
/// </summary>
[CreateAssetMenu(fileName = "NewEncounter", menuName = "DeckSaver/Run/Encounter Definition")]
public class EncounterDefinition : ScriptableObject
{
    public string encounterName = "Encounter";
    public EncounterType type = EncounterType.Battle;

    [Header("Battle (ignored for RewardOnly)")]
    [Tooltip("Enemies to spawn and where on the grid.")]
    public List<EnemySpawnEntry> enemySpawns = new();

    [Header("Reward")]
    [Tooltip("Override the run-wide reward pool for this encounter. Leave null to use RunConfig's pool.")]
    public RewardPoolData rewardPoolOverride;
}

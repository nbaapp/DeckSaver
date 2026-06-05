using System.Collections.Generic;
using UnityEngine;

public enum EncounterType
{
    Battle,      // Normal encounter: a fight followed by a reward
    RewardOnly,  // No fight — player just picks a reward
}

/// <summary>
/// Describes one encounter in a run segment: what kind it is, which painted
/// map prefab to load, which enemies populate it, and which reward pool to
/// draw from after completion.
///
/// The map prefab supplies the playfield shape and the spawn-marker GameObjects
/// that determine where each player unit and enemy stands. EnemyData entries in
/// <see cref="enemyRoster"/> are paired with EnemySpawn markers in slotIndex
/// order. If the map has no authored prefab, EntityManager falls back to a
/// programmatic default rectangle.
/// </summary>
[CreateAssetMenu(fileName = "NewEncounter", menuName = "DeckSaver/Run/Encounter Definition")]
public class EncounterDefinition : ScriptableObject
{
    public string encounterName = "Encounter";
    public EncounterType type = EncounterType.Battle;

    [Header("Battle (ignored for RewardOnly)")]
    [Tooltip("Painted map prefab containing a Grid + Ground/Overlay tilemaps " +
             "and SpawnMarker children. Leave null to use the programmatic default.")]
    public GameObject mapPrefab;

    [Tooltip("Enemies to place on this map, paired with EnemySpawn markers in slotIndex order.")]
    public List<EnemyData> enemyRoster = new();

    [Header("Reward")]
    [Tooltip("Override the run-wide reward pool for this encounter. Leave null to use RunConfig's pool.")]
    public RewardPoolData rewardPoolOverride;

    [Header("Audio")]
    [Tooltip("Music played while this encounter is active. Leave null for silence.")]
    public SoundData musicTrack;

    [Tooltip("How to swap from the previous track when this encounter starts.")]
    public MusicTransition musicTransition = MusicTransition.Fade;

    [Tooltip("Crossfade duration for Fade transitions, in seconds. <=0 uses AudioManager's default.")]
    public float musicFadeSeconds = -1f;
}

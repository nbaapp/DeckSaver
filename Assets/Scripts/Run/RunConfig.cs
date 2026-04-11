using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Designer-facing configuration for an entire run.
/// Tweak encountersPerSegment, the encounter pools, and reward pools
/// here without touching any code.
///
/// A run is divided into segments: [N regular encounters] → [boss].
/// Saving is always offered immediately after beating the boss.
/// Post-save, enemies are drawn from postSaveEncounterPool with no rewards.
/// </summary>
[CreateAssetMenu(fileName = "NewRunConfig", menuName = "DeckSaver/Run/Run Config")]
public class RunConfig : ScriptableObject
{
    [Header("Segment Structure")]
    [Tooltip("Number of regular encounters before the boss in each segment.")]
    public int encountersPerSegment = 8;

    [Tooltip("Pool of encounters to randomly draw from for regular (non-boss) slots.")]
    public List<EncounterDefinition> encounterPool = new();

    [Tooltip("The boss encounter that ends each segment.")]
    public EncounterDefinition bossEncounter;

    [Header("Post-Save")]
    [Tooltip("Encounters used in the post-save linear section. No rewards are given here.")]
    public List<EncounterDefinition> postSaveEncounterPool = new();

    [Tooltip("How many encounters appear in the post-save section before the final boss.")]
    public int postSaveEncounterCount = 4;

    [Tooltip("The final boss encounter shown at the end of the post-save section.")]
    public EncounterDefinition postSaveFinalBoss;

    [Header("Rewards")]
    [Tooltip("Default reward pool used when an encounter has no override.")]
    public RewardPoolData defaultRewardPool;

    [Tooltip("How many reward options are offered after a regular encounter.")]
    public int regularOfferCount = 2;

    [Tooltip("How many reward options are offered after beating the boss (before save prompt).")]
    public int bossOfferCount = 1;
}

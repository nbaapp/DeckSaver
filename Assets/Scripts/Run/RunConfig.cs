using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Designer-facing configuration for an entire run.
/// Controls map generation, node type distribution, encounter pools, rewards,
/// economy, and unit stats — all without touching code.
/// </summary>
[CreateAssetMenu(fileName = "NewRunConfig", menuName = "DeckSaver/Run/Run Config")]
public class RunConfig : ScriptableObject
{
    // ── Map Generation ────────────────────────────────────────────────────────

    [Header("Map Generation")]
    [Tooltip("Minimum total node count on the map (including boss).")]
    public int minNodes = 18;

    [Tooltip("Maximum total node count on the map (including boss).")]
    public int maxNodes = 22;

    [Tooltip("Minimum distance between any two nodes in virtual map space.")]
    public float minNodeSpacing = 80f;

    [Tooltip("Width of the virtual map coordinate space. MapView scales this to fit the panel.")]
    public float mapWidth = 1000f;

    [Tooltip("Height of the virtual map coordinate space.")]
    public float mapHeight = 600f;

    [Tooltip("A node is reachable if within this distance (virtual space) of the current node.")]
    public float reachRadius = 220f;

    // ── Node Type Weights ─────────────────────────────────────────────────────

    [Header("Node Type Weights (non-boss)")]
    [Tooltip("Relative frequency of Standard Conflict nodes.")]
    public int weightStandard = 4;

    [Tooltip("Relative frequency of Hard Conflict nodes.")]
    public int weightHard = 2;

    [Tooltip("Relative frequency of Camp nodes.")]
    public int weightCamp = 1;

    [Tooltip("Relative frequency of Shop nodes.")]
    public int weightShop = 1;

    [Tooltip("Relative frequency of Event nodes.")]
    public int weightEvent = 2;

    // ── Encounter Pools ───────────────────────────────────────────────────────

    [Header("Encounter Pools")]
    [Tooltip("Encounters randomly drawn for Standard Conflict nodes.")]
    public List<EncounterDefinition> standardConflictPool = new();

    [Tooltip("Encounters randomly drawn for Hard Conflict nodes.")]
    public List<EncounterDefinition> hardConflictPool = new();

    [Tooltip("The boss encounter — always placed at the rightmost node.")]
    public EncounterDefinition bossEncounter;

    [Tooltip("Encounters randomly drawn for Event nodes. If empty, Event nodes show no combat.")]
    public List<EncounterDefinition> eventPool = new();

    // ── Rewards: Fragment Swaps ───────────────────────────────────────────────

    [Header("Rewards — Fragment Swaps")]
    [Tooltip("Pool used to generate the three fragment choices in a fragment swap.")]
    public RewardPoolData fragmentSwapPool;

    // ── Rewards: Boons ────────────────────────────────────────────────────────

    [Header("Rewards — Boons")]
    [Tooltip("How many boon options to offer the player when a boon reward is triggered.")]
    public int boonOfferCount = 3;

    [Tooltip("Boon pool used after Hard Conflicts and Boss battles (unless boss pool overrides).")]
    public List<BoonData> boonPool = new();

    [Tooltip("Optional boon pool used exclusively after Boss battles. Falls back to boonPool if empty.")]
    public List<BoonData> bossBoonPool = new();

    // ── Shop ──────────────────────────────────────────────────────────────────

    [Header("Shop")]
    [Tooltip("Effect fragments that can appear for sale in shops.")]
    public List<EffectFragmentData> shopEffectFragments = new();

    [Tooltip("Modifier fragments that can appear for sale in shops.")]
    public List<ModifierFragmentData> shopModifierFragments = new();

    [Tooltip("Boons that can appear for sale in shops.")]
    public List<BoonData> shopBoons = new();

    [Tooltip("How many items from each shop category are offered per visit.")]
    public int shopItemsPerCategory = 3;

    // ── Economy ───────────────────────────────────────────────────────────────

    [Header("Economy")]
    [Tooltip("Gold the player starts the run with.")]
    public int startingMoney = 100;

    [Tooltip("Gold earned for completing a Standard Conflict.")]
    public int moneyPerStandard = 20;

    [Tooltip("Gold earned for completing a Hard Conflict.")]
    public int moneyPerHard = 40;

    [Tooltip("Gold earned for defeating the Boss.")]
    public int moneyPerBoss = 50;

    [Tooltip("Gold cost to add one unit at a Camp.")]
    public int campAddUnitCost = 100;

    // ── Units ─────────────────────────────────────────────────────────────────

    [Header("Units")]
    [Tooltip("Number of player units at run start.")]
    public int startingUnitCount = 3;

    [Tooltip("Maximum HP for each unit.")]
    public int unitMaxHealth = 10;

    [Tooltip("HP restored to each surviving unit when using the Camp heal option.")]
    public int campHealAmount = 3;
}

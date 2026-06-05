using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Configuration for a single Front — one of three route options within an act.
/// Each Front defines its own encounter pools and boss, giving each route a distinct identity.
/// </summary>
[CreateAssetMenu(fileName = "NewFrontConfig", menuName = "DeckSaver/Run/Front Config")]
public class FrontConfig : ScriptableObject
{
    [Tooltip("Display name shown to the player during front selection.")]
    public string frontName = "Unnamed Front";

    // ── Encounter Pools ───────────────────────────────────────────────────────

    [Header("Encounter Pools")]
    [Tooltip("Encounters randomly drawn for Standard Conflict nodes.")]
    public List<EncounterDefinition> standardConflictPool = new();

    [Tooltip("Encounters randomly drawn for Hard Conflict nodes.")]
    public List<EncounterDefinition> hardConflictPool = new();

    [Tooltip("The boss encounter for this front.")]
    public EncounterDefinition bossEncounter;

    [Tooltip("Encounters randomly drawn for Event nodes. If empty, Event nodes show no combat.")]
    public List<EncounterDefinition> eventPool = new();

    // ── Boons ─────────────────────────────────────────────────────────────────

    [Header("Boons")]
    [Tooltip("Optional boon pool used exclusively after this front's Boss. Falls back to RunConfig.boonPool if empty.")]
    public List<BoonData> bossBoonPool = new();
}

using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// The set of boons and fragments that can be offered as rewards.
/// RunConfig holds a default pool; individual EncounterDefinitions can
/// override it for specific encounters (e.g. a boss loot table).
/// </summary>
[CreateAssetMenu(fileName = "NewRewardPool", menuName = "DeckSaver/Run/Reward Pool")]
public class RewardPoolData : ScriptableObject
{
    [Tooltip("Boons that may appear as reward options.")]
    public List<BoonData> boonPool = new();

    [Tooltip("Effect fragments that may appear in a fragment-swap offer.")]
    public List<EffectFragmentData> effectFragmentPool = new();

    [Tooltip("Modifier fragments that may appear in a fragment-swap offer.")]
    public List<ModifierFragmentData> modifierFragmentPool = new();

    [Tooltip("If true, 'Upgrade a Fragment' can appear as a reward option (only offered when the deck has at least one upgradeable fragment).")]
    public bool includeFragmentUpgrade = false;
}

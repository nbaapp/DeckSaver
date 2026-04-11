using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

[CreateAssetMenu(fileName = "NewEffectFragment", menuName = "DeckSaver/Effect Fragment")]
public class EffectFragmentData : ScriptableObject
{
    // fragmentName should be an adjective (e.g. "Blazing", "Frozen", "Swift")
    public string fragmentName;
    [TextArea] public string flavorText;

    // Color that represents this effect visually on a composite card
    public Color effectColor = Color.white;

    // ── Mana cost (designer-facing; hidden from player) ───────────────────────
    // The composite card's final cost = Clamp(effect.baseCost + modifier.baseCost, minCost, maxCost)

    [Header("Mana Cost (hidden from player)")]
    [Tooltip("Base cost contribution from this effect fragment. Can be negative.")]
    [FormerlySerializedAs("manaCost")]
    public int baseCost = 1;

    [Tooltip("The composite card's final mana cost will never fall below this value.")]
    public int minCost = 0;

    [Tooltip("The composite card's final mana cost will never exceed this value.")]
    public int maxCost = 10;

    public List<CardEffect> effects = new();

    // ── Upgrade chain ─────────────────────────────────────────────────────────
    [Header("Upgrade Chain")]
    [Tooltip("The upgraded version of this fragment. Null if this is already the top tier.")]
    public EffectFragmentData upgradeVersion;

    [Tooltip("The base version of this fragment. Null if this is the base tier.")]
    public EffectFragmentData baseVersion;

    public bool CanUpgrade  => upgradeVersion != null;
    public bool IsUpgraded  => baseVersion    != null;
}

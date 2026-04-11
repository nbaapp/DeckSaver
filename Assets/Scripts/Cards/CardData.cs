using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// A composite card created by combining one Effect Fragment and one Modifier Fragment.
/// Name = effectFragment.fragmentName + " " + modifierFragment.fragmentName
/// Visual = effectFragment.effectColor tinted over modifierFragment.artwork
/// </summary>
[CreateAssetMenu(fileName = "NewCard", menuName = "DeckSaver/Card")]
public class CardData : ScriptableObject
{
    public EffectFragmentData effectFragment;
    public ModifierFragmentData modifierFragment;

    // --- Generated identity ---

    public string CardName =>
        effectFragment != null && modifierFragment != null
            ? $"{effectFragment.fragmentName} {modifierFragment.fragmentName}".Trim()
            : "Unnamed Card";

    // --- Generated descriptions ---

    public string CondensedDescription => CardDescriptionGenerator.Condensed(this);
    public string FullDescription      => CardDescriptionGenerator.Full(this);

    // --- Convenience accessors (delegates to fragments) ---

    public int ManaCost
    {
        get
        {
            if (effectFragment == null) return 0;
            int raw = effectFragment.baseCost + (modifierFragment?.baseCost ?? 0);
            return Mathf.Clamp(raw, effectFragment.minCost, effectFragment.maxCost);
        }
    }
    public List<CardEffect> Effects           => effectFragment?.effects;
    public PlacementType    PlacementType     => modifierFragment != null ? modifierFragment.placementType : default;
    public List<TileData>   Tiles             => modifierFragment?.tiles;
    public List<TileModifier> GlobalModifiers => modifierFragment?.globalModifiers;
}

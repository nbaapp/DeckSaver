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

    // --- Keywords ---
    // Effective keyword set =
    //   manual (effect + modifier) ∪ auto-derived (from effect's CardEffects),
    //   then mutated by any active KeywordOverlay rules (grants/strips from boons/commander).
    public HashSet<Keyword> GetKeywords()
    {
        var set = new HashSet<Keyword>();
        if (effectFragment != null)
        {
            foreach (var k in effectFragment.keywords) set.Add(k);
            foreach (var k in KeywordHelpers.DeriveFromEffects(effectFragment.effects)) set.Add(k);
        }
        if (modifierFragment != null)
        {
            foreach (var k in modifierFragment.keywords) set.Add(k);
        }
        KeywordOverlay.Apply(set);
        return set;
    }

    public bool HasKeyword(Keyword k) => GetKeywords().Contains(k);

    public bool HasAnyKeyword(params Keyword[] ks)
    {
        if (ks == null || ks.Length == 0) return false;
        var set = GetKeywords();
        foreach (var k in ks) if (set.Contains(k)) return true;
        return false;
    }

    public bool HasAllKeywords(params Keyword[] ks)
    {
        if (ks == null || ks.Length == 0) return true;
        var set = GetKeywords();
        foreach (var k in ks) if (!set.Contains(k)) return false;
        return true;
    }
}

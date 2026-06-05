using System;
using System.Collections.Generic;
using UnityEngine;

// A bundle of CardEffects injected into card resolution when keyword filters pass.
// Lives in two places, with different spatial semantics:
//   • TileData.tileEffects             → tile-scoped (fires only on the tile that owns it)
//                                        Use for Volatile, Echoing, Anchored, Binding, Draining.
//   • ModifierFragmentData.globalConditionalEffects → formation-wide (fires on every affected tile)
//                                        Use for Ignition (Burn splash), Corruption (debuff spread),
//                                        Crushing (collision damage).
//
// Filter semantics: requiredKeywords = AND-of-all, excludedKeywords = NONE-of. Empty = no constraint.
[Serializable]
public class ConditionalEffect
{
    [Tooltip("Card must have ALL of these keywords for this effect to fire. Empty = no required keywords.")]
    public List<Keyword> requiredKeywords = new();

    [Tooltip("Card must have NONE of these keywords. Empty = no exclusion. Lets you author 'non-X Orders do Y' rules.")]
    public List<Keyword> excludedKeywords = new();

    [Tooltip("Effects injected into the card's resolution when filters pass. Resolved after the Order's effects.")]
    public List<CardEffect> effects = new();

    public bool Matches(HashSet<Keyword> cardKeywords)
    {
        if (requiredKeywords != null && requiredKeywords.Count > 0)
        {
            if (cardKeywords == null) return false;
            foreach (var k in requiredKeywords) if (!cardKeywords.Contains(k)) return false;
        }
        if (excludedKeywords != null && excludedKeywords.Count > 0 && cardKeywords != null)
        {
            foreach (var k in excludedKeywords) if (cardKeywords.Contains(k)) return false;
        }
        return true;
    }
}

using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Designer-populated registry of every possible Commander in the game.
/// Used by the hub deckbuilder to detect valid forge combinations and filter fragments.
/// </summary>
[CreateAssetMenu(fileName = "CommanderRegistry", menuName = "DeckSaver/Commander Registry")]
public class CommanderRegistry : ScriptableObject
{
    public List<CommanderData> allCommanders = new();

    public CommanderData FindMatch(EffectFragmentData effect, ModifierFragmentData modifier)
    {
        if (effect == null || modifier == null) return null;
        return allCommanders.Find(c => c.sourceEffect == effect && c.sourceModifier == modifier);
    }

    /// <summary>Effect fragments that could pair with <paramref name="modifier"/> to forge a Commander.</summary>
    public List<EffectFragmentData> CompatibleEffects(ModifierFragmentData modifier)
    {
        var list = new List<EffectFragmentData>();
        foreach (var c in allCommanders)
            if (c.sourceModifier == modifier) list.Add(c.sourceEffect);
        return list;
    }

    /// <summary>Modifier fragments that could pair with <paramref name="effect"/> to forge a Commander.</summary>
    public List<ModifierFragmentData> CompatibleModifiers(EffectFragmentData effect)
    {
        var list = new List<ModifierFragmentData>();
        foreach (var c in allCommanders)
            if (c.sourceEffect == effect) list.Add(c.sourceModifier);
        return list;
    }
}

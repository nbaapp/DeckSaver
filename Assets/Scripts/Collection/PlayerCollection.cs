using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Tracks how many of each fragment the player currently has available (not yet used in a card).</summary>
[Serializable]
public class EffectFragmentStack
{
    public EffectFragmentData fragment;
    public int count;
}

/// <summary>Tracks how many of each modifier fragment the player currently has available.</summary>
[Serializable]
public class ModifierFragmentStack
{
    public ModifierFragmentData fragment;
    public int count;
}

/// <summary>
/// ScriptableObject representing everything the player owns between runs:
/// available fragments and built decks.
///
/// Fragment counts decrease when a card is built (fragments are consumed).
/// They can be restored when a run ends (depending on the chosen end-of-run rule).
/// </summary>
[CreateAssetMenu(fileName = "PlayerCollection", menuName = "DeckSaver/Player Collection")]
public class PlayerCollection : ScriptableObject
{
    public List<EffectFragmentStack>   effectFragments   = new();
    public List<ModifierFragmentStack> modifierFragments = new();

    // Decks the player has built
    public List<DeckData> decks = new();

    // --- Fragment queries ---

    public int CountEffect(EffectFragmentData fragment)
    {
        var stack = effectFragments.Find(s => s.fragment == fragment);
        return stack?.count ?? 0;
    }

    public int CountModifier(ModifierFragmentData fragment)
    {
        var stack = modifierFragments.Find(s => s.fragment == fragment);
        return stack?.count ?? 0;
    }

    public bool HasEffect(EffectFragmentData fragment)   => CountEffect(fragment)   > 0;
    public bool HasModifier(ModifierFragmentData fragment) => CountModifier(fragment) > 0;

    // --- Fragment management ---

    public void AddEffect(EffectFragmentData fragment, int amount = 1)
    {
        var stack = effectFragments.Find(s => s.fragment == fragment);
        if (stack != null) stack.count += amount;
        else effectFragments.Add(new EffectFragmentStack { fragment = fragment, count = amount });
    }

    public void AddModifier(ModifierFragmentData fragment, int amount = 1)
    {
        var stack = modifierFragments.Find(s => s.fragment == fragment);
        if (stack != null) stack.count += amount;
        else modifierFragments.Add(new ModifierFragmentStack { fragment = fragment, count = amount });
    }

    /// <summary>
    /// Removes one copy of each fragment and returns a new CardData instance.
    /// Returns null if either fragment is unavailable.
    /// </summary>
    public CardData BuildCard(EffectFragmentData effect, ModifierFragmentData modifier)
    {
        if (!HasEffect(effect) || !HasModifier(modifier))
        {
            Debug.LogWarning("Cannot build card: insufficient fragments.");
            return null;
        }

        ConsumeEffect(effect);
        ConsumeModifier(modifier);

        var card = CreateInstance<CardData>();
        card.effectFragment   = effect;
        card.modifierFragment = modifier;
        card.name = card.CardName;
        return card;
    }

    /// <summary>
    /// Dismantles a card back into its fragments, returning them to the collection.
    /// </summary>
    public void DismantleCard(CardData card)
    {
        if (card.effectFragment   != null) AddEffect(card.effectFragment);
        if (card.modifierFragment != null) AddModifier(card.modifierFragment);
    }

    // Commanders permanently unlocked between runs
    public List<CommanderData> ownedCommanders = new();

    // --- Helpers ---

    public void ConsumeEffect(EffectFragmentData fragment)
    {
        var stack = effectFragments.Find(s => s.fragment == fragment);
        if (stack != null) stack.count = Mathf.Max(0, stack.count - 1);
    }

    public void ConsumeModifier(ModifierFragmentData fragment)
    {
        var stack = modifierFragments.Find(s => s.fragment == fragment);
        if (stack != null) stack.count = Mathf.Max(0, stack.count - 1);
    }
}

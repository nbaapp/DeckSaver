using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Defines the pool of basic fragments used to auto-fill empty deck slots.
///
/// Each basic effect and modifier is a real fragment asset, so auto-filled cards
/// work seamlessly with fragment swapping during runs.
///
/// Distribution: effects and modifiers are each distributed evenly across the
/// slots to fill, then randomly paired (excluding any banned combinations).
/// </summary>
[CreateAssetMenu(fileName = "BasicFragmentPool", menuName = "DeckSaver/Basic Fragment Pool")]
public class BasicFragmentPool : ScriptableObject
{
    [Tooltip("Basic effect fragments (e.g. Strike, Block). Distributed evenly across auto-fill slots.")]
    public List<EffectFragmentData> basicEffects = new();

    [Tooltip("Basic modifier fragments (e.g. Forward, Self). Distributed evenly across auto-fill slots.")]
    public List<ModifierFragmentData> basicModifiers = new();

    [Tooltip("Effect + modifier pairs that should never be generated as basic cards.")]
    public List<ExcludedCombination> excludedCombinations = new();

    public bool IsCombinationExcluded(EffectFragmentData effect, ModifierFragmentData modifier)
    {
        foreach (var ex in excludedCombinations)
            if (ex.effect == effect && ex.modifier == modifier)
                return true;
        return false;
    }

    [Serializable]
    public struct ExcludedCombination
    {
        public EffectFragmentData effect;
        public ModifierFragmentData modifier;
    }
}

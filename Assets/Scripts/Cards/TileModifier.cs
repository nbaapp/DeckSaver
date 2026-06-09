using System;
using System.Collections.Generic;

[Serializable]
public class TileModifier
{
    public TileModifierType type;
    public float value;

    [UnityEngine.Tooltip("Only applies when the card has ALL of these keywords. Empty = always applies. Lets you author tiles like 'Powerful' (+damage if Strike) or 'Infected' (+stacks if Debuff).")]
    public List<Keyword> requiredKeywords = new();

    [UnityEngine.Tooltip("Only applies to these effect types. Empty = applies to every effect's value. Lets you target a single channel, e.g. {Push, Pull} to scale only knockback distance, or {Status} to scale only status stacks, leaving damage/heal/block untouched.")]
    public List<EffectType> targetEffectTypes = new();

    [UnityEngine.Tooltip("Narrows Status scaling to specific statuses, e.g. {Poison} for 'Infected'. Empty = every status. Only consulted for Status effects; ignored by damage/heal/block/knockback.")]
    public List<StatusType> targetStatusTypes = new();

    // Keyword gate: the card must have ALL requiredKeywords (or the list is empty).
    public bool AppliesTo(HashSet<Keyword> cardKeywords)
    {
        if (requiredKeywords == null || requiredKeywords.Count == 0) return true;
        if (cardKeywords == null) return false;
        foreach (var k in requiredKeywords)
            if (!cardKeywords.Contains(k)) return false;
        return true;
    }

    // Effect-type gate: this modifier scales only the listed effect types (or every type when empty),
    // and for Status effects can be narrowed further to specific statuses via targetStatusTypes.
    // Evaluated per-effect, so on a card that both Strikes and Pushes a {Push, Pull} modifier
    // touches only the knockback distance, not the damage; a {Status}+{Poison} modifier touches
    // only Poison stacks, not Burn applied by the same card.
    public bool AppliesToEffect(EffectType effectType, StatusType statusType)
    {
        if (targetEffectTypes != null && targetEffectTypes.Count > 0 && !targetEffectTypes.Contains(effectType))
            return false;
        if (effectType == EffectType.Status
            && targetStatusTypes != null && targetStatusTypes.Count > 0
            && !targetStatusTypes.Contains(statusType))
            return false;
        return true;
    }
}

public enum TileModifierType
{
    Multiply,   // e.g. 2.0 = double effect, 0.5 = half effect
    FlatAdd     // e.g. 5 = +5 to effect value, -5 = -5
}

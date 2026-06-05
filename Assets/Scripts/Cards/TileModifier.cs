using System;
using System.Collections.Generic;

[Serializable]
public class TileModifier
{
    public TileModifierType type;
    public float value;

    [UnityEngine.Tooltip("Only applies when the card has ALL of these keywords. Empty = always applies. Lets you author tiles like 'Powerful' (+damage if Strike) or 'Infected' (+stacks if Debuff).")]
    public List<Keyword> requiredKeywords = new();

    public bool AppliesTo(HashSet<Keyword> cardKeywords)
    {
        if (requiredKeywords == null || requiredKeywords.Count == 0) return true;
        if (cardKeywords == null) return false;
        foreach (var k in requiredKeywords)
            if (!cardKeywords.Contains(k)) return false;
        return true;
    }
}

public enum TileModifierType
{
    Multiply,   // e.g. 2.0 = double effect, 0.5 = half effect
    FlatAdd     // e.g. 5 = +5 to effect value, -5 = -5
}

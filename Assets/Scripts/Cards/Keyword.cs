using System;
using System.Collections.Generic;

// Keywords are a shared vocabulary that Orders, Formations, Boons, and passives can all reference.
// A card's effective keyword set = manual keywords on its fragments + keywords auto-derived from its effects.
// Some keywords are auto-derived (Strike, Heal, Shield, ForcedMovement, Debuff, Buff, Burn, Poison, Bleed);
// others must be set manually by the designer (Projectile, Blast, Mobility, Combo, Mark, Detonate, Support).
public enum Keyword
{
    // Mechanical — auto-derivable from EffectType
    Strike,
    Heal,
    Shield,
    ForcedMovement,

    // Status categories — auto-derivable from StatusType
    Debuff,
    Buff,

    // Specific statuses — auto-derivable from StatusType
    Burn,
    Poison,
    Bleed,

    // Author-applied — no automatic derivation
    Projectile,
    Blast,
    Mobility,
    Combo,
    Mark,
    Detonate,
    Support,
}

public static class KeywordHelpers
{
    private static readonly Dictionary<StatusType, Keyword[]> StatusKeywords = new()
    {
        { StatusType.Poison,     new[] { Keyword.Debuff, Keyword.Poison } },
        { StatusType.Burn,       new[] { Keyword.Debuff, Keyword.Burn   } },
        { StatusType.Bleed,      new[] { Keyword.Debuff, Keyword.Bleed  } },
        { StatusType.Shatter,    new[] { Keyword.Debuff } },
        { StatusType.Weak,       new[] { Keyword.Debuff } },
        { StatusType.Stunned,    new[] { Keyword.Debuff } },
        { StatusType.OffBalance, new[] { Keyword.Debuff } },
        { StatusType.Rooted,     new[] { Keyword.Debuff } },
        { StatusType.Slow,       new[] { Keyword.Debuff } },
        { StatusType.Targeted,   new[] { Keyword.Debuff } },
        { StatusType.Strong,     new[] { Keyword.Buff   } },
        { StatusType.Hard,       new[] { Keyword.Buff   } },
        { StatusType.Warded,     new[] { Keyword.Buff   } },
        { StatusType.Spikes,     new[] { Keyword.Buff   } },
        { StatusType.Haste,      new[] { Keyword.Buff   } },
        { StatusType.Focused,    new[] { Keyword.Buff   } },
    };

    // Yields the keywords implied by a list of CardEffects. Status effects contribute both a category
    // keyword (Debuff/Buff) and, when applicable, a specific keyword (Burn/Poison/Bleed).
    public static IEnumerable<Keyword> DeriveFromEffects(IEnumerable<CardEffect> effects)
    {
        if (effects == null) yield break;
        foreach (var e in effects)
        {
            switch (e.type)
            {
                case EffectType.Strike:    yield return Keyword.Strike;         break;
                case EffectType.Heal:      yield return Keyword.Heal;           break;
                case EffectType.Block:     yield return Keyword.Shield;         break;
                case EffectType.Push:      yield return Keyword.ForcedMovement; break;
                case EffectType.Pull:      yield return Keyword.ForcedMovement; break;
                case EffectType.Status:
                    if (StatusKeywords.TryGetValue(e.statusType, out var ks))
                        foreach (var k in ks) yield return k;
                    break;
            }
        }
    }
}

// A single overlay rule applied to a card's effective keyword set.
// Filter is AND-semantic: the card must have ALL filterKeywords (or filter is empty) for grants/strips to apply.
// Filter matching uses the *base* keyword set (intrinsic + derived), not the post-overlay set,
// so overlay rules don't chain into each other.
[Serializable]
public class KeywordOverlayRule
{
    [UnityEngine.Tooltip("Card must have ALL of these keywords for the rule to apply. Empty = applies to every card.")]
    public List<Keyword> filterKeywords = new();
    [UnityEngine.Tooltip("Keywords to add to the card's effective set when the rule applies.")]
    public List<Keyword> grantKeywords  = new();
    [UnityEngine.Tooltip("Keywords to remove from the card's effective set when the rule applies. Strips win over grants.")]
    public List<Keyword> stripKeywords  = new();

    public bool Matches(HashSet<Keyword> baseKeywords)
    {
        if (filterKeywords == null || filterKeywords.Count == 0) return true;
        if (baseKeywords == null) return false;
        foreach (var k in filterKeywords) if (!baseKeywords.Contains(k)) return false;
        return true;
    }
}

// Ambient registry of active keyword overlay rules.
// Owners (BoonManager, CommanderController) register their rule sets at battle start;
// CardData.GetKeywords consults this when computing a card's effective keyword set.
public static class KeywordOverlay
{
    private static readonly Dictionary<object, List<KeywordOverlayRule>> _byOwner = new();

    public static void SetOwnerRules(object owner, IEnumerable<KeywordOverlayRule> rules)
    {
        if (owner == null) return;
        var list = new List<KeywordOverlayRule>();
        if (rules != null) foreach (var r in rules) if (r != null) list.Add(r);
        _byOwner[owner] = list;
    }

    public static void ClearOwner(object owner)
    {
        if (owner == null) return;
        _byOwner.Remove(owner);
    }

    public static void ClearAll() => _byOwner.Clear();

    public static void Apply(HashSet<Keyword> keywords)
    {
        if (_byOwner.Count == 0 || keywords == null) return;
        // Snapshot the base set so grants don't satisfy other rules' filters.
        var baseSet = new HashSet<Keyword>(keywords);
        foreach (var ruleList in _byOwner.Values)
            foreach (var rule in ruleList)
            {
                if (!rule.Matches(baseSet)) continue;
                if (rule.grantKeywords != null)
                    foreach (var k in rule.grantKeywords) keywords.Add(k);
                if (rule.stripKeywords != null)
                    foreach (var k in rule.stripKeywords) keywords.Remove(k);
            }
    }
}

using System.Text;

/// <summary>
/// Generates condensed and full descriptions for a composite card from its fragments.
/// Condensed: short summary for the card face (e.g. "Deal 5 dmg. Around you.")
/// Full: detailed description shown on hover tooltip.
/// </summary>
public static class CardDescriptionGenerator
{
    public static string Condensed(CardData card)
    {
        if (card.effectFragment == null || card.modifierFragment == null)
            return string.Empty;

        var sb = new StringBuilder();
        var effects = card.effectFragment.effects;
        for (int i = 0; i < effects.Count; i++)
        {
            if (i > 0) sb.Append(" | ");
            sb.Append(EffectShort(effects[i]));
        }
        if (sb.Length > 0) sb.Append(". ");
        sb.Append(PlacementShort(card.modifierFragment.placementType));
        sb.Append('.');
        return sb.ToString();
    }

    public static string Full(CardData card)
    {
        if (card.effectFragment == null || card.modifierFragment == null)
            return string.Empty;

        var sb = new StringBuilder();
        foreach (var e in card.effectFragment.effects)
        {
            var line = EffectFull(e);
            if (line.Length > 0) sb.AppendLine(line);
        }
        sb.AppendLine();
        sb.Append(PlacementFull(card.modifierFragment.placementType, card.modifierFragment.tiles.Count));
        return sb.ToString().TrimEnd();
    }

    // -------------------------------------------------------------------------

    static string EffectShort(CardEffect e)
    {
        string hits = e.hits > 1 ? $" ×{e.hits}" : string.Empty;
        return e.type switch
        {
            EffectType.Strike    => $"Deal {e.baseValue} dmg{hits}",
            EffectType.Block     => $"Gain {e.baseValue} block{hits}",
            EffectType.Heal      => $"Heal {e.baseValue}{hits}",
            EffectType.Draw      => $"Draw {e.baseValue}",
            EffectType.Discard   => $"Discard {e.baseValue}",
            EffectType.Status    => $"Apply {e.baseValue} {e.statusType}",
            EffectType.Knockback => $"Knockback {e.baseValue}",
            EffectType.Special   => "Special",
            _                    => string.Empty
        };
    }

    static string EffectFull(CardEffect e)
    {
        string hitsStr = e.hits > 1 ? $", <b>{e.hits}</b> times" : string.Empty;
        return e.type switch
        {
            EffectType.Strike    => $"Deal <b>{e.baseValue}</b> damage to affected tiles{hitsStr}.",
            EffectType.Block     => $"Gain <b>{e.baseValue}</b> block{hitsStr}.",
            EffectType.Heal      => $"Restore <b>{e.baseValue}</b> HP{hitsStr}.",
            EffectType.Draw      => $"Draw <b>{e.baseValue}</b> card{S(e.baseValue)}.",
            EffectType.Discard   => $"Discard <b>{e.baseValue}</b> card{S(e.baseValue)}.",
            EffectType.Status    => $"Apply <b>{e.baseValue}</b> stack{S(e.baseValue)} of <b>{e.statusType}</b>.",
            EffectType.Knockback => $"Knock back enemies <b>{e.baseValue}</b> tile{S(e.baseValue)}.",
            EffectType.Special   => "Triggers a special effect.",
            _                    => string.Empty
        };
    }

    static string PlacementShort(PlacementType pt) => pt switch
    {
        PlacementType.CenteredOnPlayer      => "Around you",
        PlacementType.DirectionalFromPlayer => "Choose direction",
        PlacementType.FreelyPlaceable       => "Place freely",
        _                                   => string.Empty
    };

    static string PlacementFull(PlacementType pt, int count) => pt switch
    {
        PlacementType.CenteredOnPlayer      => $"Affects <b>{count}</b> tile{S(count)} centered on your position.",
        PlacementType.DirectionalFromPlayer => $"Choose a direction. Affects <b>{count}</b> tile{S(count)} in that direction.",
        PlacementType.FreelyPlaceable       => $"Place anywhere on the board. Affects <b>{count}</b> tile{S(count)}.",
        _                                   => string.Empty
    };

    static string S(int n) => n == 1 ? string.Empty : "s";
}

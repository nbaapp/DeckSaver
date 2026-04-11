using System.Collections.Generic;

public enum StatusType
{
    None       = 0,
    Poison     = 1,
    Burn       = 2,
    Shatter    = 3,
    Bleed      = 4,
    Weak       = 5,
    Strong     = 6,
    // 7 was Broken — removed (redundant with Bleed)
    Hard       = 8,
    Stunned    = 9,
    Warded     = 10,
    Spikes     = 11,
    OffBalance = 12,
    Rooted     = 13,
    Slow       = 14,
    Haste      = 15,
    Focused    = 16,
    Targeted   = 17,
}

public static class StatusTypeData
{
    private static readonly Dictionary<StatusType, StatusDecayType> DecayTypes = new()
    {
        { StatusType.None,       StatusDecayType.Flat   },
        { StatusType.Poison,     StatusDecayType.Normal }, // –1 after dealing start-of-turn damage
        { StatusType.Burn,       StatusDecayType.Normal }, // halved after dealing start-of-turn damage
        { StatusType.Shatter,    StatusDecayType.Normal }, // –1 per turn end
        { StatusType.Bleed,      StatusDecayType.Normal }, // –1 per turn end
        { StatusType.Weak,       StatusDecayType.Normal }, // –1 per turn end
        { StatusType.Strong,     StatusDecayType.Normal }, // –1 per turn end
        { StatusType.Hard,       StatusDecayType.Normal }, // –1 per turn end
        { StatusType.Stunned,    StatusDecayType.Flat   }, // –1 after each skipped turn
        { StatusType.Warded,     StatusDecayType.Flat   }, // –1 per hit (in ApplyStrike)
        { StatusType.Spikes,     StatusDecayType.Normal }, // –1 per hit (in ApplyStrike)
        { StatusType.OffBalance, StatusDecayType.Flat   }, // –1 per turn end
        { StatusType.Rooted,     StatusDecayType.Normal }, // –1 per turn end
        { StatusType.Slow,       StatusDecayType.Flat   }, // –1 per turn end
        { StatusType.Haste,      StatusDecayType.Flat   }, // –1 per turn end
        { StatusType.Focused,    StatusDecayType.Normal }, // halved per turn end
        { StatusType.Targeted,   StatusDecayType.Normal }, // –1 per hit (in ApplyStrike)
    };

    public static StatusDecayType GetDecayType(StatusType status) => DecayTypes[status];
}

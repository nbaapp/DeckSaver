/// <summary>
/// The type of a node on the run map. Determines what happens when the player visits it.
/// </summary>
public enum NodeType
{
    /// <summary>The run's entry point. No encounter; player chooses their first destination from here.</summary>
    Start,

    /// <summary>A normal battle. Rewards: fragment swap + money.</summary>
    StandardConflict,

    /// <summary>A tougher battle. Rewards: boon + money (no fragment swap).</summary>
    HardConflict,

    /// <summary>The segment boss. Rewards: boon + fragment swap + money.</summary>
    Boss,

    /// <summary>A rest site. Options: heal, add a unit (for money), upgrade a fragment.</summary>
    Camp,

    /// <summary>A merchant. Options: buy fragments or boons.</summary>
    Shop,

    /// <summary>A random event with variable outcomes.</summary>
    Event,
}

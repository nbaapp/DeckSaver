public enum PassiveValueSource
{
    Fixed,              // Use effect.baseValue as-is
    HalfTriggerAmount,  // Half of the contextual trigger amount (rounded down)
    FullTriggerAmount,  // Full contextual trigger amount
    TriggerStatus       // Apply effect.baseValue stacks of whichever status triggered this passive
}

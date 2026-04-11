using System;

/// <summary>
/// A single active status effect on an entity, tracking its current value.
///
/// What "value" means depends on the StatusDecayType of the status:
///   Normal      — rounds remaining AND current magnitude (shared integer)
///   Flat        — rounds remaining; effect is binary (on/off)
///   Eternal     — magnitude only; never decays
///   EternalFlat — effect is permanently on; value is ignored
///
/// Stacking applies by adding to value. Each turn end TickStatuses() decrements
/// Normal/Flat values by 1 and removes entries that reach 0.
/// </summary>
[Serializable]
public class StatusEffect
{
    public StatusType type;

    [UnityEngine.Tooltip("Rounds remaining (Flat/Normal) or magnitude (Eternal). See StatusDecayType for details.")]
    public int value;
}

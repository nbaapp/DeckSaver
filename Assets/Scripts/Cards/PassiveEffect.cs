using System;
using System.Collections.Generic;

/// <summary>
/// A single passive rule on a CommanderData.
///
/// Each passive has a trigger (when it fires), a target (who is affected),
/// and a list of CardEffects to apply — reusing the same effect format as cards.
///
/// Special triggers:
///   StatModifier   — adjusts a player stat at battle start; no target/effects needed.
///   StatusImmunity — blocks a specific status from being applied to the player.
/// </summary>
[Serializable]
public class PassiveEffect
{
    public PassiveTrigger trigger;

    // ── StatModifier only ──────────────────────────────────────────────────────
    [UnityEngine.Tooltip("Only used when trigger == StatModifier")]
    public StatModifierType statType;
    [UnityEngine.Tooltip("Only used when trigger == StatModifier")]
    public int statValue;

    // ── Triggered effects ──────────────────────────────────────────────────────
    [UnityEngine.Tooltip("Who receives the effects when this passive fires")]
    public PassiveTarget target;

    [UnityEngine.Tooltip("Fixed = use effect.baseValue; Trigger variants override it with the event amount")]
    public PassiveValueSource valueSource;

    [UnityEngine.Tooltip("Effects to apply. Uses the same CardEffect format as regular cards.")]
    public List<CardEffect> effects = new();

    // ── OnStatusApplied / StatusImmunity condition ────────────────────────────
    [UnityEngine.Tooltip("Used by OnStatusApplied: which statuses trigger this passive")]
    public StatusConditionType statusCondition;

    [UnityEngine.Tooltip("Used when statusCondition == Specific, or for StatusImmunity")]
    public StatusType specificStatus;

    [UnityEngine.Tooltip("Used when statusCondition == AnyOf")]
    public List<StatusType> statusSet = new();
}

public enum StatusConditionType
{
    Any,        // Fires on any status
    Specific,   // Fires only when specificStatus matches
    AnyOf       // Fires when the status is in statusSet
}

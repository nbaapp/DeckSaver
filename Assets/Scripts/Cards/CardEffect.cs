using System;

// A single effect on a card or enemy attack. Both players and enemies share this structure.
// baseValue meaning depends on type:
//   Strike/Block/Heal  — amount per hit (scaled by tile modifiers on player cards; raw on enemies)
//   Draw/Discard       — number of cards
//   Knockback          — number of tiles
//   Status             — stack count applied (duration/magnitude per StatusDecayType)
//   Special            — defined by custom game logic; baseValue is a freeform parameter
//
// hits — how many times this effect fires per target (default 1).
//   Each hit is a discrete application: per-hit triggers, animations, and events fire separately.
[Serializable]
public class CardEffect
{
    public EffectType type;

    [UnityEngine.Tooltip("Amount per application: damage/block/heal per hit, or card count for Draw/Discard.")]
    public int baseValue;

    [UnityEngine.Tooltip("How many times this effect fires per target. Each hit triggers events independently (default 1).")]
    [UnityEngine.SerializeField] public int hits = 1;

    public StatusType statusType; // only used when type == Status
}

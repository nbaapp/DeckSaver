using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// Applies the player's active run boons during battle.
/// Lives in the Battle scene alongside CommanderController.
///
/// Boons use the same PassiveEffect system as Commander passives, so
/// any trigger/effect combination that works on a Commander works here.
/// The two systems fire independently — a boon can trigger an event
/// that fires a commander passive and vice versa.
/// </summary>
public class BoonManager : MonoBehaviour
{
    public static BoonManager Instance { get; private set; }

    private List<BoonData> _boons = new();
    private bool _firingPassive; // re-entrancy guard

    // Context carried between event handlers (mirrors CommanderController)
    private Entity _lastStrikeTarget;
    private Entity _lastAttacker;

    private void Awake() => Instance = this;

    private void OnEnable()
    {
        BattleEvents.OnBattleStart          += HandleBattleStart;
        BattleEvents.OnPlayerTurnStart      += HandleTurnStart;
        BattleEvents.OnCardPlayed           += HandleCardPlayed;
        BattleEvents.OnPlayerStrike         += HandlePlayerStrike;
        BattleEvents.OnPlayerBlockGain      += HandleBlockGain;
        BattleEvents.OnPlayerHit            += HandlePlayerHit;
        BattleEvents.OnPlayerDamaged        += HandlePlayerDamaged;
        BattleEvents.OnEnemyKilled          += HandleEnemyKilled;
        BattleEvents.OnPlayerStatusReceived += HandleStatusReceived;
        BattleEvents.OnCardDrawn            += HandleCardDrawn;
        BattleEvents.OnCardDiscarded        += HandleCardDiscarded;
        BattleEvents.OnForcedMovement       += HandleForcedMovement;
    }

    private void OnDisable()
    {
        BattleEvents.OnBattleStart          -= HandleBattleStart;
        BattleEvents.OnPlayerTurnStart      -= HandleTurnStart;
        BattleEvents.OnCardPlayed           -= HandleCardPlayed;
        BattleEvents.OnPlayerStrike         -= HandlePlayerStrike;
        BattleEvents.OnPlayerBlockGain      -= HandleBlockGain;
        BattleEvents.OnPlayerHit            -= HandlePlayerHit;
        BattleEvents.OnPlayerDamaged        -= HandlePlayerDamaged;
        BattleEvents.OnEnemyKilled          -= HandleEnemyKilled;
        BattleEvents.OnPlayerStatusReceived -= HandleStatusReceived;
        BattleEvents.OnCardDrawn            -= HandleCardDrawn;
        BattleEvents.OnCardDiscarded        -= HandleCardDiscarded;
        BattleEvents.OnForcedMovement       -= HandleForcedMovement;
        KeywordOverlay.ClearOwner(this);
    }

    // ── Battle start ──────────────────────────────────────────────────────────

    private void HandleBattleStart()
    {
        _boons = RunCarrier.CurrentRun?.ActiveBoons ?? new List<BoonData>();

        // Apply stat modifiers first, then OnBattleStart effects
        foreach (var boon in _boons)
            foreach (var passive in boon.effects)
                if (passive.trigger == PassiveTrigger.StatModifier)
                    PlayerParty.Instance?.ApplyStatBonus(passive.statType, passive.statValue);

        // Aggregate keyword overlay rules from all active boons and register them.
        var rules = new List<KeywordOverlayRule>();
        foreach (var boon in _boons)
            if (boon != null && boon.keywordOverlays != null)
                rules.AddRange(boon.keywordOverlays);
        KeywordOverlay.SetOwnerRules(this, rules);

        FirePassives(PassiveTrigger.OnBattleStart, null, 0);
    }

    // ── Status immunity ───────────────────────────────────────────────────────

    /// <summary>Returns true if any active boon makes the player immune to this status.</summary>
    public bool IsImmuneToStatus(StatusType type)
    {
        foreach (var boon in _boons)
            foreach (var passive in boon.effects)
                if (passive.trigger == PassiveTrigger.StatusImmunity &&
                    passive.specificStatus == type)
                    return true;
        return false;
    }

    // ── Forced-movement config (consumed by KnockbackRules) ───────────────────

    private bool HasTrigger(PassiveTrigger trigger)
    {
        foreach (var boon in _boons)
            foreach (var passive in boon.effects)
                if (passive.trigger == trigger) return true;
        return false;
    }

    public bool KnockbackIgnoresDistanceFalloff() => HasTrigger(PassiveTrigger.KnockbackIgnoreDistanceFalloff);
    public bool KnockbackIgnoresRooted()          => HasTrigger(PassiveTrigger.KnockbackIgnoresRooted);
    public bool PlayerImmuneToKnockbackDamage()   => HasTrigger(PassiveTrigger.KnockbackDamageImmunity);

    /// <summary>Total per-tile knockback damage vs enemies, summed across all active boons.</summary>
    public int KnockbackDamagePerTile()
    {
        int total = 0;
        foreach (var boon in _boons)
            foreach (var passive in boon.effects)
                if (passive.trigger == PassiveTrigger.KnockbackDamagePerTile)
                    total += passive.statValue;
        return total;
    }

    // ── Event handlers ────────────────────────────────────────────────────────

    private void HandleTurnStart()                            => FirePassives(PassiveTrigger.OnTurnStart,  null, 0);
    private void HandleCardPlayed(CardData card)              => FirePassives(PassiveTrigger.OnCardPlay,   null, 0, card);
    private void HandleBlockGain(int amount)                  => FirePassives(PassiveTrigger.OnBlockGain,  null, amount);
    private void HandlePlayerDamaged(int net)                 => FirePassives(PassiveTrigger.OnDamage,     null, net);
    private void HandleCardDrawn(CardData _)                  => FirePassives(PassiveTrigger.OnDraw,       null, 0);
    private void HandleCardDiscarded(CardData _)              => FirePassives(PassiveTrigger.OnDiscard,    null, 0);
    private void HandleForcedMovement(Entity moved, int tiles)=> FirePassives(PassiveTrigger.OnForcedMovement, moved, tiles);

    private void HandlePlayerStrike(Entity target, int damage)
    {
        _lastStrikeTarget = target;
        FirePassives(PassiveTrigger.OnStrike, target, damage);
    }

    private void HandlePlayerHit(Entity attacker, int damage)
    {
        _lastAttacker = attacker;
        FirePassives(PassiveTrigger.OnHit, attacker, damage);
    }

    private void HandleEnemyKilled(EnemyEntity enemy) =>
        FirePassives(PassiveTrigger.OnKill, enemy, 0);

    private void HandleStatusReceived(StatusType type, int stacks)
    {
        if (_firingPassive) return;
        _firingPassive = true;
        try
        {
            foreach (var boon in _boons)
                foreach (var passive in boon.effects)
                {
                    if (passive.trigger != PassiveTrigger.OnStatusApplied) continue;
                    if (!MatchesStatusCondition(passive, type)) continue;
                    ResolvePassive(passive, null, stacks, type);
                }
        }
        finally { _firingPassive = false; }
    }

    // ── Passive resolution ────────────────────────────────────────────────────

    private void FirePassives(PassiveTrigger trigger, Entity contextEntity, int contextAmount,
                              CardData contextCard = null)
    {
        if (_firingPassive) return;
        _firingPassive = true;
        try
        {
            foreach (var boon in _boons)
                foreach (var passive in boon.effects)
                {
                    if (passive.trigger != trigger) continue;
                    if (!passive.PassesKeywordFilter(contextCard)) continue;
                    ResolvePassive(passive, contextEntity, contextAmount);
                }
        }
        finally { _firingPassive = false; }
    }

    private void ResolvePassive(PassiveEffect passive, Entity contextEntity, int contextAmount,
                                StatusType triggerStatus = StatusType.None)
    {
        bool useOverride   = passive.valueSource != PassiveValueSource.Fixed &&
                             passive.valueSource != PassiveValueSource.TriggerStatus;
        int  overrideValue = passive.valueSource switch
        {
            PassiveValueSource.HalfTriggerAmount => Mathf.FloorToInt(contextAmount / 2f),
            PassiveValueSource.FullTriggerAmount => contextAmount,
            _                                    => 0
        };

        var targets = ResolveTargets(passive.target, contextEntity);
        foreach (var target in targets)
            foreach (var effect in passive.effects)
                ApplyEffectToEntity(effect, target,
                    useOverride ? overrideValue : -1,
                    passive.valueSource == PassiveValueSource.TriggerStatus ? triggerStatus : StatusType.None);
    }

    private static bool MatchesStatusCondition(PassiveEffect passive, StatusType type) =>
        passive.statusCondition switch
        {
            StatusConditionType.Any      => true,
            StatusConditionType.Specific => type == passive.specificStatus,
            StatusConditionType.AnyOf    => passive.statusSet.Contains(type),
            _                            => false
        };

    private static List<Entity> ResolveTargets(PassiveTarget targetType, Entity contextEntity)
    {
        var result  = new List<Entity>();
        var party   = PlayerParty.Instance;
        var player  = PlayerEntity.Instance; // selected unit — used for proximity checks
        var enemies = EntityManager.Instance?.Enemies ?? (IReadOnlyList<EnemyEntity>)new List<EnemyEntity>();

        switch (targetType)
        {
            case PassiveTarget.Self:
                // Self boon effects (heal, block, buffs) apply to all living units.
                if (party != null)
                    result.AddRange(party.Units);
                break;
            case PassiveTarget.AllEnemies:
                result.AddRange(enemies.Where(e => e != null));
                break;
            case PassiveTarget.StrongestEnemy:
            {
                var best = enemies.Where(e => e != null)
                                  .OrderByDescending(e => e.currentHealth)
                                  .FirstOrDefault();
                if (best != null) result.Add(best);
                break;
            }
            case PassiveTarget.WeakestEnemy:
            {
                var worst = enemies.Where(e => e != null)
                                   .OrderBy(e => e.currentHealth)
                                   .FirstOrDefault();
                if (worst != null) result.Add(worst);
                break;
            }
            case PassiveTarget.NearestEnemy:
            {
                if (player == null) break;
                var nearest = enemies.Where(e => e != null)
                    .OrderBy(e => Mathf.Abs(e.GridPosition.x - player.GridPosition.x)
                                + Mathf.Abs(e.GridPosition.y - player.GridPosition.y))
                    .FirstOrDefault();
                if (nearest != null) result.Add(nearest);
                break;
            }
            case PassiveTarget.StrikeTarget:
            case PassiveTarget.Attacker:
                if (contextEntity != null) result.Add(contextEntity);
                break;
        }

        return result;
    }

    // ── Effect application ────────────────────────────────────────────────────

    private static void ApplyEffectToEntity(CardEffect effect, Entity target, int valueOverride,
                                            StatusType statusTypeOverride = StatusType.None)
    {
        if (target == null) return;
        int value      = valueOverride >= 0 ? valueOverride : effect.baseValue;
        int count      = Mathf.Max(1, effect.hits);
        var statusType = statusTypeOverride != StatusType.None ? statusTypeOverride : effect.statusType;
        var attacker = PlayerEntity.Instance; // selected unit is the attacker

        switch (effect.type)
        {
            case EffectType.Strike:
                for (int i = 0; i < count; i++)
                    StatusResolver.ApplyStrike(attacker, target,
                        attacker?.GridPosition ?? Vector2Int.zero, value, out _);
                break;
            case EffectType.Block:
                for (int i = 0; i < count; i++)
                    target.GainBlock(value);
                break;
            case EffectType.Heal:
                for (int i = 0; i < count; i++)
                    target.Heal(value);
                break;
            case EffectType.Status:
                for (int i = 0; i < count; i++)
                    target.ApplyStatus(statusType, value);
                break;
            case EffectType.Push:
            case EffectType.Pull:
            {
                Vector2Int anchorPos = attacker?.GridPosition ?? Vector2Int.zero;
                for (int i = 0; i < count; i++)
                    KnockbackResolver.Resolve(target, anchorPos, value, isPull: effect.type == EffectType.Pull);
                break;
            }
            case EffectType.Draw:
                for (int i = 0; i < value; i++)
                    BattleDeck.Instance?.DrawCard();
                break;
            case EffectType.Discard:
            {
                var hand = BattleDeck.Instance?.Hand;
                for (int i = 0; i < value && hand != null && hand.Count > 0; i++)
                    BattleDeck.Instance.DiscardCard(hand[Random.Range(0, hand.Count)]);
                break;
            }
            default:
                Debug.Log($"[BoonManager] Effect type {effect.type} not yet handled.");
                break;
        }
    }
}

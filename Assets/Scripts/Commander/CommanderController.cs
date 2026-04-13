using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// Manages the player's Commander during a battle:
///   • Applies stat modifiers at battle start.
///   • Fires passive effects in response to BattleEvents.
///   • Tracks uses and resolves the active ability (with optional tile targeting).
/// </summary>
public class CommanderController : MonoBehaviour
{
    public static CommanderController Instance { get; private set; }

    private CommanderData _commander;
    private bool          _awaitingActiveTarget;
    private bool          _firingPassive; // re-entrancy guard — prevents passive-triggered effects from re-triggering passives

    public int  ActiveUsesRemaining { get; private set; }
    public bool IsAwaitingTarget    => _awaitingActiveTarget;

    /// <summary>Fired whenever ActiveUsesRemaining or ability availability changes.</summary>
    public event Action OnActiveChanged;

    // Context entities passed through multi-step event handlers
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
        GridInputHandler.OnTileClicked      += HandleTileClickedForActive;
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
        GridInputHandler.OnTileClicked      -= HandleTileClickedForActive;
    }

    // ── Battle start ──────────────────────────────────────────────────────────

    private void HandleBattleStart()
    {
        _commander = PlayerParty.Instance?.Commander;
        if (_commander == null) return;

        ActiveUsesRemaining = _commander.activesPerBattle;
        ApplyStatModifiers();
        FirePassives(PassiveTrigger.OnBattleStart, null, 0);
        OnActiveChanged?.Invoke();
    }

    private void ApplyStatModifiers()
    {
        if (_commander == null) return;
        foreach (var p in _commander.passiveEffects)
        {
            if (p.trigger != PassiveTrigger.StatModifier) continue;
            PlayerParty.Instance?.ApplyStatBonus(p.statType, p.statValue);
        }
    }

    // ── Status immunity ───────────────────────────────────────────────────────

    /// <summary>Returns true if the active Commander makes the player immune to this status.</summary>
    public bool IsImmuneToStatus(StatusType type)
    {
        if (_commander == null) return false;
        return _commander.passiveEffects.Any(p =>
            p.trigger == PassiveTrigger.StatusImmunity &&
            p.specificStatus == type);
    }

    // ── Active ability ────────────────────────────────────────────────────────

    public bool CanUseActive =>
        ActiveUsesRemaining > 0 &&
        TurnManager.Instance?.CurrentPhase == TurnPhase.PlayerTurn;

    /// <summary>
    /// Called when the player clicks the Commander card in the UI.
    /// Fires immediately if the active has no area (hits all enemies),
    /// or enters targeting mode if an activeArea is defined.
    /// Clicking again while targeting cancels it.
    /// </summary>
    public void InitiateActiveAbility()
    {
        if (_commander == null) return;

        if (_awaitingActiveTarget)
        {
            CancelActiveTargeting();
            return;
        }

        if (!CanUseActive) return;

        if (_commander.activeArea == null)
        {
            // No area required — fire immediately on all enemies
            ExecuteActive(null);
        }
        else
        {
            _awaitingActiveTarget = true;
            GridInputHandler.Instance?.SetPendingCommanderActive(_commander);
            OnActiveChanged?.Invoke();
        }
    }

    private void CancelActiveTargeting()
    {
        _awaitingActiveTarget = false;
        GridInputHandler.Instance?.ClearPendingCommanderActive();
        OnActiveChanged?.Invoke();
    }

    private void HandleTileClickedForActive(GridTile tile)
    {
        if (!_awaitingActiveTarget) return;

        var affected = GridInputHandler.Instance?.GetAffectedTilesForCommander(tile.GridPosition);
        _awaitingActiveTarget = false;
        GridInputHandler.Instance?.ClearPendingCommanderActive();

        ExecuteActive(affected);
    }

    private void ExecuteActive(List<(GridTile tile, TileData data)> affected)
    {
        if (_commander == null) return;
        ActiveUsesRemaining--;
        OnActiveChanged?.Invoke();

        if (affected == null)
        {
            // Fire on all enemies
            var enemies = EntityManager.Instance.Enemies.ToList();
            foreach (var effect in _commander.activeEffects)
                foreach (var enemy in enemies)
                    if (enemy != null)
                        ApplyEffectToEntity(effect, enemy, -1);
        }
        else
        {
            // Fire on targeted tiles, applying tile modifiers
            var globalMods = _commander.activeArea?.globalModifiers ?? new List<TileModifier>();
            foreach (var effect in _commander.activeEffects)
                foreach (var (tile, data) in affected)
                {
                    var entity = EntityManager.Instance.GetEntityAt(tile.GridPosition);
                    if (entity == null) continue;
                    // Status stacks are not scaled by tile modifiers — use baseValue directly.
                    int value = effect.type == EffectType.Status
                        ? effect.baseValue
                        : ComputeValue(effect.baseValue, globalMods, data.modifiers);
                    ApplyEffectToEntity(effect, entity, value);
                }
        }
    }

    // ── Passive event handlers ────────────────────────────────────────────────

    private void HandleTurnStart()                             => FirePassives(PassiveTrigger.OnTurnStart,  null,   0);
    private void HandleCardPlayed(CardData _)                  => FirePassives(PassiveTrigger.OnCardPlay,   null,   0);
    private void HandleBlockGain(int amount)                   => FirePassives(PassiveTrigger.OnBlockGain,  null,   amount);
    private void HandlePlayerDamaged(int net)                  => FirePassives(PassiveTrigger.OnDamage,     null,   net);
    private void HandleCardDrawn(CardData _)                   => FirePassives(PassiveTrigger.OnDraw,       null,   0);
    private void HandleCardDiscarded(CardData _)               => FirePassives(PassiveTrigger.OnDiscard,    null,   0);

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
        if (_commander == null || _firingPassive) return;
        _firingPassive = true;
        try
        {
            foreach (var passive in _commander.passiveEffects)
            {
                if (passive.trigger != PassiveTrigger.OnStatusApplied) continue;
                if (!MatchesStatusCondition(passive, type)) continue;
                ResolvePassive(passive, null, stacks, type);
            }
        }
        finally { _firingPassive = false; }
    }

    // ── Passive resolution ────────────────────────────────────────────────────

    private void FirePassives(PassiveTrigger trigger, Entity contextEntity, int contextAmount)
    {
        if (_commander == null || _firingPassive) return;
        _firingPassive = true;
        try
        {
            foreach (var passive in _commander.passiveEffects)
            {
                if (passive.trigger != trigger) continue;
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

    private List<Entity> ResolveTargets(PassiveTarget targetType, Entity contextEntity)
    {
        var result  = new List<Entity>();
        var party   = PlayerParty.Instance;
        var player  = PlayerEntity.Instance; // selected unit — used for proximity checks
        var enemies = EntityManager.Instance?.Enemies ?? new List<EnemyEntity>();

        switch (targetType)
        {
            case PassiveTarget.Self:
                // Self-targeting passives (block, heal, buffs) apply to all living units.
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
                var nearest = enemies
                    .Where(e => e != null)
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

    /// <summary>
    /// Apply a single CardEffect to target.
    /// Pass valueOverride >= 0 to use it instead of effect.baseValue.
    /// Pass statusTypeOverride != None to replace the effect's statusType (used by TriggerStatus).
    /// </summary>
    private static void ApplyEffectToEntity(CardEffect effect, Entity target, int valueOverride,
                                            StatusType statusTypeOverride = StatusType.None)
    {
        if (target == null) return;
        int value      = valueOverride >= 0 ? valueOverride : effect.baseValue;
        int count      = Mathf.Max(1, effect.hits);
        var statusType = statusTypeOverride != StatusType.None ? statusTypeOverride : effect.statusType;
        var attacker   = PlayerEntity.Instance; // selected unit is the attacker

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

            case EffectType.Draw:
                for (int i = 0; i < value; i++)
                    BattleDeck.Instance?.DrawCard();
                break;

            case EffectType.Discard:
            {
                var hand = BattleDeck.Instance?.Hand;
                for (int i = 0; i < value && hand != null && hand.Count > 0; i++)
                    BattleDeck.Instance.DiscardCard(hand[UnityEngine.Random.Range(0, hand.Count)]);
                break;
            }

            default:
                Debug.Log($"[CommanderController] Effect type {effect.type} not yet handled.");
                break;
        }
    }

    // ── Value computation (mirrors CardPlayManager) ───────────────────────────

    private static int ComputeValue(int baseValue, List<TileModifier> globalMods, List<TileModifier> tileMods)
    {
        float v = baseValue;
        foreach (var m in globalMods) ApplyMod(ref v, m);
        foreach (var m in tileMods)   ApplyMod(ref v, m);
        return Mathf.RoundToInt(v);
    }

    private static void ApplyMod(ref float v, TileModifier mod)
    {
        switch (mod.type)
        {
            case TileModifierType.Multiply: v *= mod.value; break;
            case TileModifierType.FlatAdd:  v += mod.value; break;
        }
    }
}

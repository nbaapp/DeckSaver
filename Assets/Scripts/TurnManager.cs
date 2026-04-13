using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

public enum TurnPhase { None, EnemySelect, PlayerTurn, EnemyExecute }

/// <summary>
/// Drives the battle turn loop.
///
/// Flow per round:
///   1. EnemySelect  — each enemy picks and telegraphs its attack
///   2. PlayerTurn   — player plays cards / moves (ends by pressing End Turn)
///   3. EnemyExecute — enemies move and resolve their attacks
///   → repeat until win or loss
///
/// Resources (mana/stamina) are restored at the END of the player turn
/// so that end-of-turn effects can modify next-turn values before they take effect.
///
/// With multiple player units, all units participate in start/end-of-turn
/// processing.  The battle is lost when ALL player units have been killed.
/// </summary>
public class TurnManager : MonoBehaviour
{
    public static TurnManager Instance { get; private set; }

    public TurnPhase CurrentPhase { get; private set; } = TurnPhase.None;

    /// <summary>Fired whenever the phase changes.</summary>
    public event Action<TurnPhase> OnPhaseChanged;

    [Tooltip("Seconds to wait between each enemy's action during EnemyExecute.")]
    [SerializeField] private float _enemyActionDelay = 0.5f;

    [Tooltip("Scene to load after a battle ends. Must match the scene name in Build Settings.")]
    [SerializeField] private string _runSceneName = "Run";

    private void Awake() => Instance = this;

    // ── Public API ────────────────────────────────────────────────────────────

    public void StartBattle()
    {
        // Fire battle start first so stat bonuses are applied before resource refresh.
        BattleEvents.FireBattleStart();
        PlayerParty.Instance?.RefreshResources();
        StartEnemySelectPhase();
    }

    /// <summary>Called by the End Turn button.</summary>
    public void EndPlayerTurn()
    {
        if (CurrentPhase != TurnPhase.PlayerTurn) return;

        // Clear any pending card preview
        GridInputHandler.Instance?.SetPendingCard(null);

        // Restore shared resources
        PlayerParty.Instance?.RefreshResources();

        // Tick end-of-turn statuses on all living units
        foreach (var unit in EntityManager.Instance.Players.ToList())
            StatusResolver.TickEndOfTurn(unit);

        // Draw / discard to hand size
        BattleDeck.Instance?.OnTurnEnd();

        StartCoroutine(EnemyExecuteRoutine());
    }

    // ── Phase transitions ─────────────────────────────────────────────────────

    private void StartEnemySelectPhase()
    {
        SetPhase(TurnPhase.EnemySelect);

        foreach (var enemy in EntityManager.Instance.Enemies.ToList())
            enemy.SelectAttack();

        StartPlayerTurn();
    }

    private void StartPlayerTurn()
    {
        SetPhase(TurnPhase.PlayerTurn);

        // Apply start-of-turn effects to all living units
        var players = EntityManager.Instance.Players.ToList();
        foreach (var unit in players)
        {
            unit.ClearBlock();
            StatusResolver.ResolveStartOfTurn(unit); // Poison / Burn ticks
        }

        BattleEvents.FirePlayerTurnStart();

        // Check if every unit is stunned — if so, auto-skip the turn.
        // Units that are stunned but not in an all-stunned state have their
        // stun decremented here so they recover on the next turn.
        bool allStunned = players.Count > 0 && players.All(u => u.HasStatus(StatusType.Stunned));
        foreach (var unit in players)
            if (unit.HasStatus(StatusType.Stunned))
                unit.DecrementStatus(StatusType.Stunned);

        if (allStunned)
        {
            Debug.Log("[TurnManager] All units stunned — skipping turn.");
            EndPlayerTurn();
            return;
        }

        HandDisplay.Instance?.RefreshAffordability();
    }

    private IEnumerator EnemyExecuteRoutine()
    {
        SetPhase(TurnPhase.EnemyExecute);

        var enemies = EntityManager.Instance.Enemies.ToList();
        foreach (var enemy in enemies)
        {
            if (enemy == null) continue;
            enemy.ClearBlock();
            StatusResolver.ResolveStartOfTurn(enemy); // Poison / Burn

            if (enemy.HasStatus(StatusType.Stunned))
            {
                Debug.Log($"[TurnManager] {enemy.name} is stunned — skipping turn.");
                enemy.DecrementStatus(StatusType.Stunned);
            }
            else
            {
                enemy.ExecuteTurn();
            }

            StatusResolver.TickEndOfTurn(enemy);
            yield return new WaitForSeconds(_enemyActionDelay);

            if (CheckLoss()) yield break;
        }

        if (CheckWin()) yield break;

        StartEnemySelectPhase();
    }

    // ── Win / loss ────────────────────────────────────────────────────────────

    private bool CheckWin()
    {
        if (EntityManager.Instance.Enemies.Count > 0) return false;

        // Persist surviving unit HPs to RunState so health carries into the next battle.
        var run = RunCarrier.CurrentRun;
        if (run != null)
        {
            var healths = EntityManager.Instance.Players
                .Select(p => p.currentHealth)
                .ToList();
            run.RecordBattleUnitHealth(healths);
        }

        Debug.Log("[TurnManager] All enemies defeated — Battle Won!");
        SetPhase(TurnPhase.None);
        BattleDeck.Instance?.OnBattleEnd();
        EndBattle(BattleResultCarrier.Result.Win);
        return true;
    }

    private bool CheckLoss()
    {
        if (EntityManager.Instance.Players.Count > 0) return false;
        Debug.Log("[TurnManager] All units defeated — Battle Lost!");
        SetPhase(TurnPhase.None);
        BattleDeck.Instance?.OnBattleEnd();
        EndBattle(BattleResultCarrier.Result.Loss);
        return true;
    }

    private void EndBattle(BattleResultCarrier.Result result)
    {
        BattleResultCarrier.Set(result);

        if (RunCarrier.CurrentRun != null)
            SceneManager.LoadScene(_runSceneName);
    }

    private void SetPhase(TurnPhase phase)
    {
        CurrentPhase = phase;
        Debug.Log($"[TurnManager] Phase → {phase}");
        OnPhaseChanged?.Invoke(phase);
    }
}

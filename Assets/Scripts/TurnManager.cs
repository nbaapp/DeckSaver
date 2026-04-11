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
        PlayerEntity.Instance?.RefreshResources();
        StartEnemySelectPhase();
    }

    /// <summary>Called by the End Turn button.</summary>
    public void EndPlayerTurn()
    {
        if (CurrentPhase != TurnPhase.PlayerTurn) return;

        // Clear any pending card preview
        GridInputHandler.Instance?.SetPendingCard(null);

        // Restore resources immediately so end-of-turn effects can read/modify them
        PlayerEntity.Instance?.RefreshResources();

        // Tick player statuses after resources are set (end-of-turn decay)
        StatusResolver.TickEndOfTurn(PlayerEntity.Instance);

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

        // Telegraph phase is instant for now; immediately hand off to player
        StartPlayerTurn();
    }

    private void StartPlayerTurn()
    {
        SetPhase(TurnPhase.PlayerTurn);
        var player = PlayerEntity.Instance;
        player?.ClearBlock();
        StatusResolver.ResolveStartOfTurn(player);  // Poison / Burn
        BattleEvents.FirePlayerTurnStart();

        if (player != null && player.HasStatus(StatusType.Stunned))
        {
            Debug.Log("[TurnManager] Player is stunned — skipping turn.");
            player.DecrementStatus(StatusType.Stunned);
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
        Debug.Log("[TurnManager] All enemies defeated — Battle Won!");
        SetPhase(TurnPhase.None);
        BattleDeck.Instance?.OnBattleEnd();
        EndBattle(BattleResultCarrier.Result.Win);
        return true;
    }

    private bool CheckLoss()
    {
        var p = PlayerEntity.Instance;
        if (p == null || p.currentHealth > 0) return false;
        Debug.Log("[TurnManager] Player defeated — Battle Lost!");
        SetPhase(TurnPhase.None);
        BattleDeck.Instance?.OnBattleEnd();
        EndBattle(BattleResultCarrier.Result.Loss);
        return true;
    }

    private void EndBattle(BattleResultCarrier.Result result)
    {
        BattleResultCarrier.Set(result);

        // If we're in a run, return to the Run scene.
        // If there's no active run (e.g. standalone test), just stay in the scene.
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

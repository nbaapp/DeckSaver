using System;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;

/// <summary>
/// Base class for anything that occupies a tile on the grid (player, enemy).
/// maxHealth is intentionally not defined here — each subclass owns its source of truth.
/// </summary>
[RequireComponent(typeof(SpriteRenderer))]
public abstract class Entity : MonoBehaviour
{
    public Vector2Int GridPosition { get; private set; }

    protected int maxHealth;
    public int currentHealth;
    public int CurrentBlock { get; private set; }

    /// <summary>
    /// Fired once per discrete hit with the net damage dealt (after block absorption).
    /// Value is 0 when the hit was fully absorbed by block.
    /// Subscribe here to drive per-hit animations, sounds, and triggers.
    /// </summary>
    public event Action<int> OnHitReceived;

    /// <summary>Fired once when currentHealth reaches 0.</summary>
    public event Action OnDeath;

    [Header("Active Status Effects (runtime — edit to test)")]
    [SerializeField] private List<StatusEffect> _statusEffects = new();

    private bool _isPlaced;
    private bool _dead;
    private TextMeshPro _statsLabel;

    protected virtual void Awake()
    {
        var sr = GetComponent<SpriteRenderer>();
        sr.sortingOrder = 2; // above grid lines (1) and tile fills (0)
        BuildStatsLabel();
    }

    // ── Combat API ────────────────────────────────────────────────────────────

    public void TakeDamage(int amount)
    {
        if (amount <= 0 || _dead) return;
        int absorbed  = Mathf.Min(CurrentBlock, amount);
        CurrentBlock -= absorbed;
        int net       = amount - absorbed;
        currentHealth = Mathf.Max(0, currentHealth - net);
        OnHitReceived?.Invoke(net);
        RefreshStatsLabel();
        if (this is PlayerEntity && net > 0) BattleEvents.FirePlayerDamaged(net);
        if (currentHealth <= 0 && !_dead)
        {
            _dead = true;
            OnDeath?.Invoke();
        }
    }

    public void Heal(int amount)
    {
        if (amount <= 0) return;
        currentHealth = Mathf.Min(maxHealth, currentHealth + amount);
        RefreshStatsLabel();
    }

    public void GainBlock(int amount)
    {
        if (amount <= 0) return;
        CurrentBlock += amount;
        RefreshStatsLabel();
        if (this is PlayerEntity) BattleEvents.FirePlayerBlockGain(amount);
    }

    /// <summary>Call at the start of each turn to clear leftover block.</summary>
    public void ClearBlock()
    {
        CurrentBlock = 0;
        RefreshStatsLabel();
    }

    /// <summary>
    /// Damages block only; excess does NOT carry over to health.
    /// Used by Shattered to deal extra block damage per hit.
    /// </summary>
    public void TakeBlockDamageOnly(int amount)
    {
        if (amount <= 0 || _dead) return;
        CurrentBlock = Mathf.Max(0, CurrentBlock - amount);
        RefreshStatsLabel();
    }

    /// <summary>Returns the effective move speed after applying Slow and Haste.</summary>
    public int GetEffectiveMoveSpeed(int baseSpeed) =>
        Mathf.Max(0, baseSpeed
            - GetStatusValue(StatusType.Slow)
            + GetStatusValue(StatusType.Haste));

    // ── Status API ────────────────────────────────────────────────────────────

    public IReadOnlyList<StatusEffect> StatusEffects => _statusEffects;

    /// <summary>Apply stacks/duration to a status. Stacks add to existing value.</summary>
    public void ApplyStatus(StatusType type, int amount)
    {
        if (amount <= 0 || type == StatusType.None) return;
        if (this is PlayerEntity && CommanderController.Instance?.IsImmuneToStatus(type) == true) return;
        var existing = _statusEffects.Find(s => s.type == type);
        if (existing != null)
            existing.value += amount;
        else
            _statusEffects.Add(new StatusEffect { type = type, value = amount });
        RefreshStatsLabel();
        if (this is PlayerEntity) BattleEvents.FirePlayerStatusReceived(type, amount);
    }

    /// <summary>Returns true if this entity has at least 1 stack of the status.</summary>
    public bool HasStatus(StatusType type) =>
        _statusEffects.Exists(s => s.type == type && s.value > 0);

    /// <summary>Returns the current value (stacks/rounds) of a status, or 0 if absent.</summary>
    public int GetStatusValue(StatusType type) =>
        _statusEffects.Find(s => s.type == type)?.value ?? 0;

    /// <summary>Decrements a status by 1 and removes it if it reaches 0. No-op if absent.</summary>
    public void DecrementStatus(StatusType type)
    {
        var existing = _statusEffects.Find(s => s.type == type);
        if (existing == null) return;
        existing.value--;
        if (existing.value <= 0)
            _statusEffects.Remove(existing);
        RefreshStatsLabel();
    }

    /// <summary>Floors a status value to half and removes it if it reaches 0. No-op if absent.</summary>
    public void HalveStatus(StatusType type)
    {
        var existing = _statusEffects.Find(s => s.type == type);
        if (existing == null) return;
        existing.value = Mathf.FloorToInt(existing.value / 2f);
        if (existing.value <= 0)
            _statusEffects.Remove(existing);
        RefreshStatsLabel();
    }

    // ── Stats display ─────────────────────────────────────────────────────────

    private void BuildStatsLabel()
    {
        var go = new GameObject("StatsLabel");
        go.transform.SetParent(transform, false);
        go.transform.localPosition = new Vector3(0f, 0.6f, 0f);

        _statsLabel               = go.AddComponent<TextMeshPro>();
        _statsLabel.fontSize      = 2f;
        _statsLabel.alignment     = TextAlignmentOptions.Center;
        _statsLabel.sortingOrder  = 3; // above entity sprite
        RefreshStatsLabel();
    }

    protected void RefreshStatsLabel()
    {
        if (_statsLabel == null) return;
        var sb = new StringBuilder();
        sb.Append($"<color=#ffffff>{currentHealth}/{maxHealth} HP</color>");
        if (CurrentBlock > 0)
            sb.Append($"\n<color=#88ccff>{CurrentBlock} Block</color>");
        foreach (var s in _statusEffects)
        {
            var decay = StatusTypeData.GetDecayType(s.type);
            bool showValue = decay != StatusDecayType.EternalFlat;
            sb.Append(showValue
                ? $"\n<color=#ffcc44>{s.type}({s.value})</color>"
                : $"\n<color=#ffcc44>{s.type}</color>");
        }
        _statsLabel.text = sb.ToString();
    }

    // ── Placement ─────────────────────────────────────────────────────────────

    /// <summary>Move (or initially place) this entity onto a grid tile.</summary>
    public void PlaceAt(Vector2Int gridPos)
    {
        if (GridManager.Instance == null) return;

        if (_isPlaced)
            GridManager.Instance.GetTile(GridPosition)?.SetState(TileVisualState.Normal);

        GridPosition = gridPos;
        _isPlaced = true;

        transform.position = GridManager.Instance.GridToWorld(gridPos);
        GridManager.Instance.GetTile(gridPos)?.SetState(TileVisualState.Occupied);
        RefreshStatsLabel();
    }
}

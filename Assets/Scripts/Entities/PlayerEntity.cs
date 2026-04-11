using System;
using UnityEngine;

/// <summary>The player entity on the grid.</summary>
public class PlayerEntity : Entity
{
    public static PlayerEntity Instance { get; private set; }

    public CommanderData commander;

    // ── Inspector-editable values ──────────────────────────────────────────────

    [Header("Health")]
    [SerializeField] private int _maxHealth = 10;

    [Header("Base Resources (per-turn restore values)")]
    [SerializeField] private int _baseMana      = 3;
    [SerializeField] private int _baseStamina   = 2;
    [SerializeField] private int _baseMoveSpeed = 3;

    [Header("Current Resources (runtime — shows live values)")]
    [SerializeField] private int _currentMana;
    [SerializeField] private int _currentStamina;

    // ── Static accessors (used by other systems) ───────────────────────────────
    public static int BaseMana      => Instance != null ? Instance._baseMana      : 3;
    public static int BaseStamina   => Instance != null ? Instance._baseStamina   : 2;
    public static int BaseMoveSpeed => Instance != null ? Instance._baseMoveSpeed : 3;

    // ── Current resource properties ────────────────────────────────────────────
    public int CurrentMana    => _currentMana;
    public int CurrentStamina => _currentStamina;
    public int MoveSpeed      => _baseMoveSpeed;

    /// <summary>Fired whenever mana or stamina changes.</summary>
    public event Action OnResourcesChanged;

    // ── Status Effects (placeholder for future use) ────────────────────────────
    // StatusEffect list will go here when implemented.

    // ── Unity lifecycle ───────────────────────────────────────────────────────

    protected override void Awake()
    {
        Instance      = this;
        maxHealth     = _maxHealth;
        currentHealth = maxHealth;
        base.Awake();
    }

    // ── Resource API ──────────────────────────────────────────────────────────

    /// <summary>Restore full mana and stamina (call at end of player turn / battle start).</summary>
    public void RefreshResources()
    {
        _currentMana    = _baseMana;
        _currentStamina = _baseStamina;
        OnResourcesChanged?.Invoke();
    }

    /// <summary>Spends mana. Returns false (and spends nothing) if insufficient.</summary>
    public bool TrySpendMana(int amount)
    {
        if (_currentMana < amount) return false;
        _currentMana -= amount;
        OnResourcesChanged?.Invoke();
        return true;
    }

    /// <summary>Spends stamina. Returns false (and spends nothing) if insufficient.</summary>
    public bool TrySpendStamina(int amount)
    {
        if (_currentStamina < amount) return false;
        _currentStamina -= amount;
        OnResourcesChanged?.Invoke();
        return true;
    }

    /// <summary>
    /// Apply a stat bonus from the Commander's passive effects.
    /// Call at battle start before RefreshResources so base values are correct.
    /// </summary>
    public void ApplyStatBonus(StatModifierType stat, int value)
    {
        switch (stat)
        {
            case StatModifierType.MaxMana:
                _baseMana += value;
                break;
            case StatModifierType.MaxStamina:
                _baseStamina += value;
                break;
            case StatModifierType.MaxHealth:
                maxHealth     += value;
                currentHealth  = Mathf.Min(currentHealth + value, maxHealth);
                RefreshStatsLabel();
                break;
            case StatModifierType.MoveSpeed:
                _baseMoveSpeed += value;
                break;
        }
        OnResourcesChanged?.Invoke();
    }
}

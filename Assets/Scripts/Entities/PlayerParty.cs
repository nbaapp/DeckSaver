using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Singleton that owns:
///   • The shared mana/stamina resource pool (all units draw from the same pool).
///   • The list of living player units.
///   • The currently selected unit (reflected in PlayerEntity.Instance).
///   • The active Commander reference for this battle.
///
/// Automatically added to the same GameObject as EntityManager via
/// [RequireComponent] — do not place it separately.
/// </summary>
public class PlayerParty : MonoBehaviour
{
    public static PlayerParty Instance { get; private set; }

    // ── Resources ─────────────────────────────────────────────────────────────

    [Header("Shared Resources")]
    [SerializeField] private int _baseMana      = 3;
    [SerializeField] private int _baseStamina   = 2;
    [SerializeField] private int _baseMoveSpeed = 3;

    [Header("Current Resources (runtime)")]
    [SerializeField] private int _currentMana;
    [SerializeField] private int _currentStamina;

    public static int BaseMana      => Instance != null ? Instance._baseMana      : 3;
    public static int BaseStamina   => Instance != null ? Instance._baseStamina   : 2;
    public static int BaseMoveSpeed => Instance != null ? Instance._baseMoveSpeed : 3;

    public int CurrentMana    => _currentMana;
    public int CurrentStamina => _currentStamina;

    /// <summary>Fired whenever mana or stamina changes.</summary>
    public event Action OnResourcesChanged;

    // ── Units ─────────────────────────────────────────────────────────────────

    private readonly List<PlayerEntity> _units = new();
    public IReadOnlyList<PlayerEntity> Units => _units;

    /// <summary>The unit the player is currently acting with.</summary>
    public PlayerEntity SelectedUnit { get; private set; }

    // ── Commander ─────────────────────────────────────────────────────────────

    public CommanderData Commander { get; private set; }

    // ── Unity lifecycle ───────────────────────────────────────────────────────

    private void Awake()
    {
        Instance  = this;
        Commander = RunCarrier.CurrentRun?.Commander;
    }

    // ── Unit management ───────────────────────────────────────────────────────

    /// <summary>
    /// Register a newly spawned unit with the party.
    /// Subscribes to its death event and auto-selects it if nothing is selected yet.
    /// </summary>
    public void RegisterUnit(PlayerEntity unit)
    {
        _units.Add(unit);
        unit.OnDeath += () => HandleUnitDied(unit);
        if (SelectedUnit == null)
            SelectUnit(unit);
    }

    /// <summary>Switch the active unit. Updates PlayerEntity.Instance and refreshes visuals.</summary>
    public void SelectUnit(PlayerEntity unit)
    {
        if (SelectedUnit != null)
            SelectedUnit.SetSelected(false);

        SelectedUnit             = unit;
        PlayerEntity.Instance    = unit;

        if (SelectedUnit != null)
            SelectedUnit.SetSelected(true);
    }

    private void HandleUnitDied(PlayerEntity unit)
    {
        _units.Remove(unit);

        if (SelectedUnit == unit)
            SelectUnit(_units.Count > 0 ? _units[0] : null);

        BattleEvents.FireUnitDied(unit);
    }

    // ── Resource API (mirrors old PlayerEntity API) ───────────────────────────

    /// <summary>Restore mana and stamina to their base values.</summary>
    public void RefreshResources()
    {
        _currentMana    = _baseMana;
        _currentStamina = _baseStamina;
        OnResourcesChanged?.Invoke();
    }

    /// <summary>Spend mana. Returns false (and spends nothing) if insufficient.</summary>
    public bool TrySpendMana(int amount)
    {
        if (_currentMana < amount) return false;
        _currentMana -= amount;
        OnResourcesChanged?.Invoke();
        return true;
    }

    /// <summary>Spend stamina. Returns false (and spends nothing) if insufficient.</summary>
    public bool TrySpendStamina(int amount)
    {
        if (_currentStamina < amount) return false;
        _currentStamina -= amount;
        OnResourcesChanged?.Invoke();
        return true;
    }

    /// <summary>
    /// Apply a stat bonus (from the Commander's passive effects).
    /// MaxHealth applies to every living unit; the rest adjust the shared resource base.
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
            case StatModifierType.MoveSpeed:
                _baseMoveSpeed += value;
                break;
            case StatModifierType.MaxHealth:
                foreach (var u in _units)
                    u.ApplyMaxHealthBonus(value);
                return; // HP label refreshed by each unit — no OnResourcesChanged needed
        }
        OnResourcesChanged?.Invoke();
    }
}

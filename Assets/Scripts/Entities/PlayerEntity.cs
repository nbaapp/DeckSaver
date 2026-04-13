using UnityEngine;

/// <summary>
/// One player unit on the grid.
///
/// Mana, stamina, and the commander reference live on <see cref="PlayerParty"/>,
/// which is the shared resource pool for all units.
///
/// Instance always points to the currently selected unit; it is set by
/// PlayerParty.SelectUnit() and should not be assigned anywhere else.
/// </summary>
public class PlayerEntity : Entity
{
    /// <summary>The currently selected player unit. Maintained by PlayerParty.</summary>
    public static PlayerEntity Instance { get; set; }

    // ── Inspector ─────────────────────────────────────────────────────────────

    [Header("Health (fallback when no RunConfig is present)")]
    [SerializeField] private int _defaultMaxHealth = 10;

    // ── Move speed convenience ────────────────────────────────────────────────

    /// <summary>Base move speed, read from PlayerParty (shared resource).</summary>
    public int MoveSpeed => PlayerParty.Instance != null
        ? PlayerParty.BaseMoveSpeed
        : 3;

    // ── Unity lifecycle ───────────────────────────────────────────────────────

    protected override void Awake()
    {
        // maxHealth / currentHealth are set by EntityManager via InitHealth()
        // before this entity is used in combat.  Apply a safe default so the
        // stats label isn't blank if the unit is inspected before InitHealth runs.
        maxHealth     = _defaultMaxHealth;
        currentHealth = maxHealth;
        base.Awake();
    }

    // ── Initialisation ────────────────────────────────────────────────────────

    /// <summary>
    /// Called by EntityManager right after spawning.
    /// Sets the unit's max HP and restores it to its persisted current HP.
    /// </summary>
    public void InitHealth(int currentHp, int maxHp)
    {
        maxHealth     = maxHp;
        currentHealth = Mathf.Clamp(currentHp, 0, maxHp);
        RefreshStatsLabel();
    }

    // ── Selection visual ──────────────────────────────────────────────────────

    /// <summary>Tint the sprite to indicate selection state.</summary>
    public void SetSelected(bool selected)
    {
        var sr = GetComponent<SpriteRenderer>();
        if (sr != null)
            sr.color = selected ? new Color(0.6f, 1f, 0.6f) : Color.white;
    }

    // ── Stat bonuses (called by PlayerParty on behalf of Commander) ────────────

    /// <summary>Apply a max-health bonus to this unit only.</summary>
    public void ApplyMaxHealthBonus(int value)
    {
        maxHealth     += value;
        currentHealth  = Mathf.Min(currentHealth + value, maxHealth);
        RefreshStatsLabel();
    }
}

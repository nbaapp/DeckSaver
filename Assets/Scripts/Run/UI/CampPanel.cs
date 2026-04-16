using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Camp node UI. Three independent options the player may use before leaving:
///   • Heal All  — restores HP to all units (free)
///   • Add Unit  — recruits one new unit at full health (costs campAddUnitCost gold)
///   • Upgrade a Fragment — opens FragmentUpgradePanel (free)
///
/// Each option can only be used once per camp visit. A "Leave Camp" button ends the visit.
/// Wire all references in the inspector; pass a FragmentUpgradePanel reference.
/// </summary>
public class CampPanel : MonoBehaviour
{
    [Header("Heal Option")]
    [SerializeField] private Button          _healButton;
    [SerializeField] private TextMeshProUGUI _healLabel;

    [Header("Add Unit Option")]
    [SerializeField] private Button          _addUnitButton;
    [SerializeField] private TextMeshProUGUI _addUnitLabel;

    [Header("Upgrade Option")]
    [SerializeField] private Button          _upgradeButton;
    [SerializeField] private TextMeshProUGUI _upgradeLabel;

    [Header("Shared")]
    [SerializeField] private TextMeshProUGUI _moneyText;
    [SerializeField] private Button          _leaveButton;

    [Header("Fragment Upgrade Sub-panel")]
    [SerializeField] private FragmentUpgradePanel _fragmentUpgradePanel;

    // ── State ─────────────────────────────────────────────────────────────────

    private Action _onLeave;
    private bool   _healUsed;
    private bool   _addUnitUsed;
    private bool   _upgradeUsed;

    // ── Public API ────────────────────────────────────────────────────────────

    public void Show(Action onLeave)
    {
        _onLeave     = onLeave;
        _healUsed    = false;
        _addUnitUsed = false;
        _upgradeUsed = false;

        gameObject.SetActive(true);
        RefreshUI();

        _leaveButton?.onClick.RemoveAllListeners();
        _leaveButton?.onClick.AddListener(Leave);
    }

    public void Hide() => gameObject.SetActive(false);

    // ── Option handlers ───────────────────────────────────────────────────────

    private void UseHeal()
    {
        var run = RunCarrier.CurrentRun;
        if (run == null) return;

        run.HealUnits(run.Config.campHealAmount);
        _healUsed = true;
        RefreshUI();
    }

    private void UseAddUnit()
    {
        var run = RunCarrier.CurrentRun;
        if (run == null) return;

        if (!run.SpendMoney(run.Config.campAddUnitCost)) return;

        run.AddUnit();
        _addUnitUsed = true;
        RefreshUI();
    }

    private void UseUpgrade()
    {
        if (_fragmentUpgradePanel == null)
        {
            _upgradeUsed = true;
            RefreshUI();
            return;
        }

        _fragmentUpgradePanel.Show(() =>
        {
            _upgradeUsed = true;
            RefreshUI();
        });
    }

    private void Leave()
    {
        Hide();
        _onLeave?.Invoke();
    }

    // ── UI refresh ────────────────────────────────────────────────────────────

    private void RefreshUI()
    {
        var run = RunCarrier.CurrentRun;
        if (run == null) return;

        // Money display
        if (_moneyText) _moneyText.text = $"Gold: {run.Money}";

        // Heal button
        if (_healButton != null)
        {
            _healButton.interactable = !_healUsed;
            _healButton.onClick.RemoveAllListeners();
            if (!_healUsed) _healButton.onClick.AddListener(UseHeal);
        }
        if (_healLabel)
            _healLabel.text = _healUsed
                ? "Healed"
                : $"Heal All (+{run.Config.campHealAmount} HP)";

        // Add unit button
        int unitCost = run.Config.campAddUnitCost;
        bool canAffordUnit = run.Money >= unitCost;
        if (_addUnitButton != null)
        {
            _addUnitButton.interactable = !_addUnitUsed && canAffordUnit;
            _addUnitButton.onClick.RemoveAllListeners();
            if (!_addUnitUsed) _addUnitButton.onClick.AddListener(UseAddUnit);
        }
        if (_addUnitLabel)
            _addUnitLabel.text = _addUnitUsed
                ? "Unit Recruited"
                : $"Add Unit ({unitCost}g)";

        // Upgrade button
        bool hasUpgradeable = run.HasUpgradeableFragment();
        if (_upgradeButton != null)
        {
            _upgradeButton.interactable = !_upgradeUsed && hasUpgradeable;
            _upgradeButton.onClick.RemoveAllListeners();
            if (!_upgradeUsed && hasUpgradeable) _upgradeButton.onClick.AddListener(UseUpgrade);
        }
        if (_upgradeLabel)
            _upgradeLabel.text = _upgradeUsed
                ? "Fragment Upgraded"
                : hasUpgradeable
                    ? "Upgrade Fragment"
                    : "Upgrade Fragment (none upgradeable)";
    }
}

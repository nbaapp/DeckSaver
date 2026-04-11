using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Shows the player a set of reward options after an encounter.
/// Each option is either a specific boon or a "Swap a Fragment" meta-option.
///
/// Flow:
///   RewardPanel.Show(options, onChosen)
///     → player clicks a boon  → boon applied immediately, onChosen invoked
///     → player clicks "Fragment Swap" → FragmentSwapPanel opened; on complete, onChosen invoked
/// </summary>
public class RewardPanel : MonoBehaviour
{
    [Header("Header")]
    [SerializeField] private TextMeshProUGUI _headerText;

    [Header("Offer slots — add as many as regularOfferCount in RunConfig")]
    [SerializeField] private List<BoonOfferView>  _boonSlots       = new();
    [SerializeField] private List<Button>         _fragmentSwapSlots = new(); // one button per swap offer
    [SerializeField] private List<TextMeshProUGUI> _swapSlotLabels  = new();

    [Header("Fragment swap sub-panel")]
    [SerializeField] private FragmentSwapPanel _fragmentSwapPanel;

    [Header("Fragment upgrade sub-panel")]
    [SerializeField] private FragmentUpgradePanel        _fragmentUpgradePanel;
    [SerializeField] private List<Button>                _fragmentUpgradeSlots  = new();
    [SerializeField] private List<TextMeshProUGUI>       _upgradeSlotLabels     = new();

    // ── State ─────────────────────────────────────────────────────────────────

    private List<RewardOption> _options;
    private Action             _onChosen;
    private RunState           _run;

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>
    /// Display the reward options. onChosen is called after the player has
    /// fully resolved their choice (including any fragment swap sub-flow).
    /// </summary>
    public void Show(string header, List<RewardOption> options, Action onChosen)
    {
        _options   = options;
        _onChosen  = onChosen;
        _run       = RunCarrier.CurrentRun;
        gameObject.SetActive(true);

        if (_headerText) _headerText.text = header;

        int boonSlotIndex = 0;
        int swapSlotIndex = 0;

        // Hide all slots first
        foreach (var s in _boonSlots)           s.gameObject.SetActive(false);
        foreach (var s in _fragmentSwapSlots)   s.gameObject.SetActive(false);
        foreach (var s in _fragmentUpgradeSlots) s.gameObject.SetActive(false);

        int upgradeSlotIndex = 0;

        foreach (var option in options)
        {
            if (option.type == RewardOptionType.Boon && boonSlotIndex < _boonSlots.Count)
            {
                var slot = _boonSlots[boonSlotIndex++];
                var boon = option.boon;
                slot.gameObject.SetActive(true);
                slot.Populate(boon, () => PickBoon(boon));
            }
            else if (option.type == RewardOptionType.FragmentSwap && swapSlotIndex < _fragmentSwapSlots.Count)
            {
                var btn = _fragmentSwapSlots[swapSlotIndex];
                btn.gameObject.SetActive(true);
                btn.onClick.RemoveAllListeners();
                btn.onClick.AddListener(OpenFragmentSwap);

                if (swapSlotIndex < _swapSlotLabels.Count && _swapSlotLabels[swapSlotIndex] != null)
                    _swapSlotLabels[swapSlotIndex].text = option.fragmentSwapLabel;

                swapSlotIndex++;
            }
            else if (option.type == RewardOptionType.FragmentUpgrade && upgradeSlotIndex < _fragmentUpgradeSlots.Count)
            {
                var btn = _fragmentUpgradeSlots[upgradeSlotIndex];
                btn.gameObject.SetActive(true);
                btn.onClick.RemoveAllListeners();
                btn.onClick.AddListener(OpenFragmentUpgrade);

                if (upgradeSlotIndex < _upgradeSlotLabels.Count && _upgradeSlotLabels[upgradeSlotIndex] != null)
                    _upgradeSlotLabels[upgradeSlotIndex].text = option.fragmentSwapLabel;

                upgradeSlotIndex++;
            }
        }
    }

    public void Hide() => gameObject.SetActive(false);

    // ── Choices ───────────────────────────────────────────────────────────────

    private void PickBoon(BoonData boon)
    {
        _run?.AddBoon(boon);
        Hide();
        _onChosen?.Invoke();
    }

    private void OpenFragmentSwap()
    {
        if (_fragmentSwapPanel == null)
        {
            // No panel wired — skip the swap and complete
            Hide();
            _onChosen?.Invoke();
            return;
        }

        var choices = _run?.GenerateFragmentChoices() ?? new System.Collections.Generic.List<FragmentChoice>();
        _fragmentSwapPanel.Show(choices, () =>
        {
            Hide();
            _onChosen?.Invoke();
        });
    }

    private void OpenFragmentUpgrade()
    {
        if (_fragmentUpgradePanel == null)
        {
            Hide();
            _onChosen?.Invoke();
            return;
        }

        _fragmentUpgradePanel.Show(() =>
        {
            Hide();
            _onChosen?.Invoke();
        });
    }
}

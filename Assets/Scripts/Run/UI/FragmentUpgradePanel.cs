using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Two-step fragment upgrade UI:
///   Step 1 — Player picks a card from their deck.
///             Cards with no upgradeable fragments are shown but non-interactable.
///   Step 2 — The card is "blown up": player picks which fragment half (Effect or Modifier) to upgrade.
///             Halves that are already at max tier are shown but non-interactable.
///
/// Upgrading replaces a fragment with its upgradeVersion SO (the original asset is never modified).
///
/// Call Show() with an onComplete callback; subscribe to events or just use the callback.
/// </summary>
public class FragmentUpgradePanel : MonoBehaviour
{
    [Header("Step 1 — Choose Card")]
    [SerializeField] private GameObject _cardListRoot;
    [SerializeField] private Transform  _cardListParent;
    [SerializeField] private GameObject _cardSlotPrefab;

    [Header("Step 2 — Choose Fragment")]
    [SerializeField] private GameObject      _fragmentDetailRoot;
    [SerializeField] private TextMeshProUGUI _cardNameText;
    [SerializeField] private Button          _effectButton;
    [SerializeField] private TextMeshProUGUI _effectLabel;
    [SerializeField] private Button          _modifierButton;
    [SerializeField] private TextMeshProUGUI _modifierLabel;
    [SerializeField] private Button          _backButton;

    [Header("Shared")]
    [SerializeField] private Button _cancelButton;

    // ── State ─────────────────────────────────────────────────────────────────

    private int    _selectedCardIndex = -1;
    private Action _onComplete;
    private readonly List<GameObject> _cardSlots = new();

    // ── Public API ────────────────────────────────────────────────────────────

    public void Show(Action onComplete)
    {
        _onComplete = onComplete;
        gameObject.SetActive(true);

        _cancelButton?.onClick.RemoveAllListeners();
        _cancelButton?.onClick.AddListener(Cancel);

        _backButton?.onClick.RemoveAllListeners();
        _backButton?.onClick.AddListener(ShowCardStep);

        ShowCardStep();
    }

    public void Hide() => gameObject.SetActive(false);

    // ── Step 1: Card selection ────────────────────────────────────────────────

    private void ShowCardStep()
    {
        _cardListRoot?.SetActive(true);
        _fragmentDetailRoot?.SetActive(false);

        foreach (var slot in _cardSlots) Destroy(slot);
        _cardSlots.Clear();

        var run = RunCarrier.CurrentRun;
        if (run == null || _cardListParent == null || _cardSlotPrefab == null) return;

        for (int i = 0; i < run.CurrentCards.Count; i++)
        {
            int  captured   = i;
            var  card       = run.CurrentCards[i];
            bool canUpgrade = (card.effectFragment?.CanUpgrade  == true) ||
                              (card.modifierFragment?.CanUpgrade == true);

            var slot = Instantiate(_cardSlotPrefab, _cardListParent);
            _cardSlots.Add(slot);

            var label = slot.GetComponentInChildren<TextMeshProUGUI>();
            if (label != null) label.text = card.CardName;

            var btn = slot.GetComponentInChildren<Button>();
            if (btn != null)
            {
                btn.interactable = canUpgrade;
                if (canUpgrade)
                    btn.onClick.AddListener(() => OnCardPicked(captured));
            }
        }
    }

    private void OnCardPicked(int cardIndex)
    {
        _selectedCardIndex = cardIndex;
        ShowFragmentStep();
    }

    // ── Step 2: Fragment half selection ──────────────────────────────────────

    private void ShowFragmentStep()
    {
        _cardListRoot?.SetActive(false);
        _fragmentDetailRoot?.SetActive(true);

        var run = RunCarrier.CurrentRun;
        if (run == null) { Cancel(); return; }

        var card = run.CurrentCards[_selectedCardIndex];

        if (_cardNameText != null)
            _cardNameText.text = card.CardName;

        // Effect half
        bool effectUpgradeable = card.effectFragment?.CanUpgrade == true;
        if (_effectLabel != null)
        {
            _effectLabel.text = effectUpgradeable
                ? card.effectFragment.fragmentName
                : $"{card.effectFragment?.fragmentName ?? "?"} (max)";
        }
        if (_effectButton != null)
        {
            _effectButton.interactable = effectUpgradeable;
            _effectButton.onClick.RemoveAllListeners();
            if (effectUpgradeable)
                _effectButton.onClick.AddListener(CommitUpgradeEffect);
        }

        // Modifier half
        bool modUpgradeable = card.modifierFragment?.CanUpgrade == true;
        if (_modifierLabel != null)
        {
            _modifierLabel.text = modUpgradeable
                ? card.modifierFragment.fragmentName
                : $"{card.modifierFragment?.fragmentName ?? "?"} (max)";
        }
        if (_modifierButton != null)
        {
            _modifierButton.interactable = modUpgradeable;
            _modifierButton.onClick.RemoveAllListeners();
            if (modUpgradeable)
                _modifierButton.onClick.AddListener(CommitUpgradeModifier);
        }
    }

    // ── Commit ────────────────────────────────────────────────────────────────

    private void CommitUpgradeEffect()
    {
        RunCarrier.CurrentRun?.UpgradeEffectFragment(_selectedCardIndex);
        Hide();
        _onComplete?.Invoke();
    }

    private void CommitUpgradeModifier()
    {
        RunCarrier.CurrentRun?.UpgradeModifierFragment(_selectedCardIndex);
        Hide();
        _onComplete?.Invoke();
    }

    private void Cancel()
    {
        Hide();
        _onComplete?.Invoke();
    }
}

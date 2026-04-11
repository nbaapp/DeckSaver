using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Two-step fragment swap UI:
///   Step 1 — Player picks one of three offered fragments (≥1 effect, ≥1 modifier, 1 random).
///   Step 2 — Player picks which card in their deck to apply it to.
///             Only cards with the matching fragment type (effect/modifier) are selectable.
///
/// Call Show() with the pre-generated fragment choices; Subscribe to OnSwapComplete
/// to be notified when the player finishes (or cancels).
/// </summary>
public class FragmentSwapPanel : MonoBehaviour
{
    [Header("Step 1 — Choose Fragment")]
    [SerializeField] private GameObject         _fragmentChoiceRoot;
    [SerializeField] private FragmentOfferView  _offerView1;
    [SerializeField] private FragmentOfferView  _offerView2;
    [SerializeField] private FragmentOfferView  _offerView3;

    [Header("Step 2 — Choose Card")]
    [SerializeField] private GameObject         _cardChoiceRoot;
    [SerializeField] private TextMeshProUGUI    _instructionText;
    [SerializeField] private Transform          _cardListParent;
    [SerializeField] private GameObject         _cardSlotPrefab; // has a Button + TextMeshProUGUI for card name

    [Header("Shared")]
    [SerializeField] private Button             _cancelButton;

    // ── State ─────────────────────────────────────────────────────────────────

    private List<FragmentChoice> _choices;
    private FragmentChoice       _pickedFragment;
    private Action               _onComplete;
    private readonly List<GameObject> _cardSlots = new();

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>
    /// Show the panel with the given fragment choices.
    /// onComplete is invoked when the swap is committed or cancelled.
    /// </summary>
    public void Show(List<FragmentChoice> choices, Action onComplete)
    {
        _choices    = choices;
        _onComplete = onComplete;
        gameObject.SetActive(true);

        _cancelButton?.onClick.RemoveAllListeners();
        _cancelButton?.onClick.AddListener(Cancel);

        ShowFragmentStep();
    }

    public void Hide() => gameObject.SetActive(false);

    // ── Step 1: Fragment selection ────────────────────────────────────────────

    private void ShowFragmentStep()
    {
        _fragmentChoiceRoot?.SetActive(true);
        _cardChoiceRoot?.SetActive(false);

        var views = new[] { _offerView1, _offerView2, _offerView3 };
        for (int i = 0; i < views.Length; i++)
        {
            if (views[i] == null) continue;
            if (i < _choices.Count)
            {
                int captured = i;
                views[i].gameObject.SetActive(true);
                views[i].Populate(_choices[i], () => OnFragmentPicked(_choices[captured]));
            }
            else
            {
                views[i].gameObject.SetActive(false);
            }
        }
    }

    private void OnFragmentPicked(FragmentChoice choice)
    {
        _pickedFragment = choice;
        ShowCardStep();
    }

    // ── Step 2: Card selection ────────────────────────────────────────────────

    private void ShowCardStep()
    {
        _fragmentChoiceRoot?.SetActive(false);
        _cardChoiceRoot?.SetActive(true);

        if (_instructionText != null)
            _instructionText.text = _pickedFragment.isEffect
                ? "Pick a card to replace its Effect fragment."
                : "Pick a card to replace its Modifier fragment.";

        // Clear existing slots
        foreach (var slot in _cardSlots) Destroy(slot);
        _cardSlots.Clear();

        var run   = RunCarrier.CurrentRun;
        if (run == null || _cardListParent == null || _cardSlotPrefab == null) return;

        for (int i = 0; i < run.CurrentCards.Count; i++)
        {
            int   captured = i;
            var   card     = run.CurrentCards[i];
            var   slot     = Instantiate(_cardSlotPrefab, _cardListParent);
            _cardSlots.Add(slot);

            // Set the label
            var label = slot.GetComponentInChildren<TextMeshProUGUI>();
            if (label != null) label.text = card.CardName;

            // Wire the button
            var btn = slot.GetComponentInChildren<Button>();
            if (btn != null)
                btn.onClick.AddListener(() => CommitSwap(captured));
        }
    }

    private void CommitSwap(int cardIndex)
    {
        var run = RunCarrier.CurrentRun;
        if (run == null) { Cancel(); return; }

        if (_pickedFragment.isEffect)
            run.SwapEffectFragment(cardIndex, _pickedFragment.effectFragment);
        else
            run.SwapModifierFragment(cardIndex, _pickedFragment.modifierFragment);

        Hide();
        _onComplete?.Invoke();
    }

    private void Cancel()
    {
        Hide();
        _onComplete?.Invoke();
    }
}

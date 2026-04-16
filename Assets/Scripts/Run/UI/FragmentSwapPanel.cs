using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Three-step fragment swap UI.
///
///   Step 1 — Player picks one of three offered fragments (≥1 effect, ≥1 modifier, 1 random).
///   Step 2 — Player picks which card in their deck to apply it to.
///   Step 3 — Preview: shows the card before and after the swap; player confirms or goes back.
///
/// Entry points:
///   Show(choices, onComplete)     — full flow starting from step 1 (reward swap)
///   ShowApplyStep(choice, onComplete) — start from step 2 with a pre-chosen fragment (shop purchase)
/// </summary>
public class FragmentSwapPanel : MonoBehaviour
{
    [Header("Step 1 — Choose Fragment")]
    [SerializeField] private GameObject        _fragmentChoiceRoot;
    [SerializeField] private FragmentOfferView _offerView1;
    [SerializeField] private FragmentOfferView _offerView2;
    [SerializeField] private FragmentOfferView _offerView3;

    [Header("Step 2 — Choose Card")]
    [SerializeField] private GameObject      _cardChoiceRoot;
    [SerializeField] private TextMeshProUGUI _instructionText;
    [SerializeField] private Transform       _cardListParent;
    [SerializeField] private GameObject      _cardSlotPrefab;

    [Header("Step 3 — Preview")]
    [SerializeField] private GameObject      _previewRoot;
    [SerializeField] private TextMeshProUGUI _beforeNameText;
    [SerializeField] private TextMeshProUGUI _beforeDescText;
    [SerializeField] private TextMeshProUGUI _afterNameText;
    [SerializeField] private TextMeshProUGUI _afterDescText;
    [SerializeField] private Button          _confirmButton;
    [SerializeField] private Button          _previewBackButton;

    [Header("Shared")]
    [SerializeField] private Button _cancelButton;

    // ── State ─────────────────────────────────────────────────────────────────

    private List<FragmentChoice>    _choices;
    private FragmentChoice          _pickedFragment;
    private int                     _selectedCardIndex = -1;
    private Action                  _onComplete;
    private readonly List<GameObject> _cardSlots = new();

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>Full flow: player picks a fragment, then a card, then confirms preview.</summary>
    public void Show(List<FragmentChoice> choices, Action onComplete)
    {
        _choices    = choices;
        _onComplete = onComplete;
        gameObject.SetActive(true);

        _cancelButton?.onClick.RemoveAllListeners();
        _cancelButton?.onClick.AddListener(Cancel);

        ShowFragmentStep();
    }

    /// <summary>
    /// Abbreviated flow: fragment is already chosen (e.g. shop purchase).
    /// Starts at step 2 (card selection) with the given fragment pre-selected.
    /// </summary>
    public void ShowApplyStep(FragmentChoice choice, Action onComplete)
    {
        _pickedFragment = choice;
        _onComplete     = onComplete;
        gameObject.SetActive(true);

        _cancelButton?.onClick.RemoveAllListeners();
        _cancelButton?.onClick.AddListener(Cancel);

        ShowCardStep();
    }

    public void Hide() => gameObject.SetActive(false);

    // ── Step 1: Fragment selection ────────────────────────────────────────────

    private void ShowFragmentStep()
    {
        _fragmentChoiceRoot?.SetActive(true);
        _cardChoiceRoot?.SetActive(false);
        _previewRoot?.SetActive(false);

        var views = new[] { _offerView1, _offerView2, _offerView3 };
        for (int i = 0; i < views.Length; i++)
        {
            if (views[i] == null) continue;
            if (_choices != null && i < _choices.Count)
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
        _previewRoot?.SetActive(false);

        if (_instructionText != null)
            _instructionText.text = _pickedFragment.isEffect
                ? "Pick a card to replace its Effect fragment."
                : "Pick a card to replace its Modifier fragment.";

        foreach (var slot in _cardSlots) Destroy(slot);
        _cardSlots.Clear();

        var run = RunCarrier.CurrentRun;
        if (run == null || _cardListParent == null || _cardSlotPrefab == null) return;

        for (int i = 0; i < run.CurrentCards.Count; i++)
        {
            int  captured = i;
            var  card     = run.CurrentCards[i];
            var  slot     = Instantiate(_cardSlotPrefab, _cardListParent);
            _cardSlots.Add(slot);

            var label = slot.GetComponentInChildren<TextMeshProUGUI>();
            if (label != null) label.text = card.CardName;

            var btn = slot.GetComponentInChildren<Button>();
            if (btn != null)
                btn.onClick.AddListener(() => OnCardPicked(captured));
        }
    }

    private void OnCardPicked(int cardIndex)
    {
        _selectedCardIndex = cardIndex;
        ShowPreviewStep();
    }

    // ── Step 3: Preview ───────────────────────────────────────────────────────

    private void ShowPreviewStep()
    {
        _fragmentChoiceRoot?.SetActive(false);
        _cardChoiceRoot?.SetActive(false);
        _previewRoot?.SetActive(true);

        var run = RunCarrier.CurrentRun;
        if (run == null) { Cancel(); return; }

        var card = run.CurrentCards[_selectedCardIndex];

        // Before
        if (_beforeNameText) _beforeNameText.text = card.CardName;
        if (_beforeDescText) _beforeDescText.text = card.FullDescription;

        // After — create a temporary card to generate the preview
        var tempCard = ScriptableObject.CreateInstance<CardData>();
        if (_pickedFragment.isEffect)
        {
            tempCard.effectFragment   = _pickedFragment.effectFragment;
            tempCard.modifierFragment = card.modifierFragment;
        }
        else
        {
            tempCard.effectFragment   = card.effectFragment;
            tempCard.modifierFragment = _pickedFragment.modifierFragment;
        }

        if (_afterNameText) _afterNameText.text = tempCard.CardName;
        if (_afterDescText) _afterDescText.text = tempCard.FullDescription;

        Destroy(tempCard);

        // Wire buttons
        _confirmButton?.onClick.RemoveAllListeners();
        _confirmButton?.onClick.AddListener(CommitSwap);

        _previewBackButton?.onClick.RemoveAllListeners();
        _previewBackButton?.onClick.AddListener(ShowCardStep);
    }

    // ── Commit ────────────────────────────────────────────────────────────────

    private void CommitSwap()
    {
        var run = RunCarrier.CurrentRun;
        if (run == null) { Cancel(); return; }

        if (_pickedFragment.isEffect)
            run.SwapEffectFragment(_selectedCardIndex, _pickedFragment.effectFragment);
        else
            run.SwapModifierFragment(_selectedCardIndex, _pickedFragment.modifierFragment);

        Hide();
        _onComplete?.Invoke();
    }

    private void Cancel()
    {
        Hide();
        _onComplete?.Invoke();
    }
}

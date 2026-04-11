using TMPro;
using UnityEngine;

/// <summary>
/// The right panel in the hub deckbuilder.
///
/// Spawns 20 CardSlotViews and manages single-selection among them.
/// The CommanderSlotView is placed in the scene separately (not spawned here).
/// Also owns the card preview panel, which it populates when a slot is selected.
/// </summary>
public class DeckPanel : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Transform        _slotsParent;
    [SerializeField] private GameObject       _cardSlotPrefab;
    [SerializeField] private TMP_Text         _slotCountLabel;  // e.g. "12 / 20"

    [Header("Preview panel")]
    [SerializeField] private GameObject _previewRoot;
    [SerializeField] private TMP_Text   _previewNameLabel;
    [SerializeField] private TMP_Text   _previewDescLabel;
    [SerializeField] private TMP_Text   _previewCostLabel;

    private readonly CardSlotView[] _slots = new CardSlotView[DeckData.MaxSize];
    private CardSlotView _selectedSlot;

    // -------------------------------------------------------------------------

    void Start()
    {
        SpawnSlots();
        HubDeckBuilderState.Instance.OnStateChanged += RefreshSlotCount;
        RefreshSlotCount();
        if (_previewRoot != null) _previewRoot.SetActive(false);
    }

    void OnDestroy()
    {
        if (HubDeckBuilderState.Instance != null)
            HubDeckBuilderState.Instance.OnStateChanged -= RefreshSlotCount;
    }

    // -------------------------------------------------------------------------

    void SpawnSlots()
    {
        for (int i = 0; i < DeckData.MaxSize; i++)
        {
            var go   = Instantiate(_cardSlotPrefab, _slotsParent);
            var slot = go.GetComponent<CardSlotView>();
            slot.Init(i);
            slot.OnSelected = OnSlotSelected;
            _slots[i] = slot;
        }
    }

    void OnSlotSelected(CardSlotView slot)
    {
        // Deselect previous
        if (_selectedSlot != null && _selectedSlot != slot)
            _selectedSlot.SetSelected(false);

        bool wasAlreadySelected = _selectedSlot == slot && slot.IsSelected;
        slot.SetSelected(!wasAlreadySelected);
        _selectedSlot = wasAlreadySelected ? null : slot;

        RefreshPreview();
    }

    void RefreshSlotCount()
    {
        if (_slotCountLabel == null) return;
        int filled = HubDeckBuilderState.Instance.FilledSlotCount();
        _slotCountLabel.text = $"{filled} / {DeckData.MaxSize}";
        RefreshPreview(); // slot content may have changed
    }

    void RefreshPreview()
    {
        if (_previewRoot == null) return;

        if (_selectedSlot == null)
        {
            _previewRoot.SetActive(false);
            return;
        }

        var state = HubDeckBuilderState.Instance;
        string name = state.PreviewSlotName(_selectedSlot.SlotIndex);

        if (name == null)
        {
            _previewRoot.SetActive(false);
            return;
        }

        _previewRoot.SetActive(true);
        if (_previewNameLabel != null) _previewNameLabel.text = name;
        if (_previewDescLabel != null) _previewDescLabel.text = state.PreviewSlotDescription(_selectedSlot.SlotIndex);
        if (_previewCostLabel != null) _previewCostLabel.text = $"Cost: {state.PreviewSlotManaCost(_selectedSlot.SlotIndex)}";
    }
}

using TMPro;
using UnityEngine;

/// <summary>
/// The right panel in the hub deckbuilder.
///
/// Spawns card slots: the first CustomSlotCount are editable (for player-built cards),
/// the rest up to TotalDeckSize are locked and display as auto-fill Strike/Block cards.
/// Also owns the card preview panel.
/// </summary>
public class DeckPanel : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Transform        _slotsParent;
    [SerializeField] private GameObject       _cardSlotPrefab;
    [SerializeField] private TMP_Text         _slotCountLabel;  // e.g. "3 / 5"

    [Header("Preview panel")]
    [SerializeField] private GameObject _previewRoot;
    [SerializeField] private TMP_Text   _previewNameLabel;
    [SerializeField] private TMP_Text   _previewDescLabel;
    [SerializeField] private TMP_Text   _previewCostLabel;

    private CardSlotView[] _slots;
    private CardSlotView _selectedSlot;

    private static readonly Color BasicColor = new(0.4f, 0.4f, 0.45f);

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
        var state = HubDeckBuilderState.Instance;
        int total  = state.TotalDeckSize;
        int custom = state.CustomSlotCount;

        _slots = new CardSlotView[total];

        for (int i = 0; i < total; i++)
        {
            var go   = Instantiate(_cardSlotPrefab, _slotsParent);
            var slot = go.GetComponent<CardSlotView>();
            slot.Init(i);
            slot.OnSelected = OnSlotSelected;
            _slots[i] = slot;

            if (i >= custom)
            {
                slot.SetLocked(true);
                slot.SetLockedDisplay("Basic", BasicColor);
            }
        }
    }

    void OnSlotSelected(CardSlotView slot)
    {
        if (slot.IsLocked) return;

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
        var state = HubDeckBuilderState.Instance;
        int filled = state.FilledSlotCount();
        _slotCountLabel.text = $"{filled} / {state.CustomSlotCount}";
        RefreshPreview();
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

using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// One of the 20 normal card slots in the deck builder.
///
/// Contains an effect FragmentDropZone and a modifier FragmentDropZone.
/// Clicking the slot selects it and shows the card preview panel.
/// </summary>
public class CardSlotView : MonoBehaviour, IPointerClickHandler
{
    [Header("References")]
    [SerializeField] private FragmentDropZone _effectZone;
    [SerializeField] private FragmentDropZone _modifierZone;
    [SerializeField] private Image            _background;
    [SerializeField] private Image            _colorBar;    // left accent strip
    [SerializeField] private TMP_Text         _slotNumber;
    [SerializeField] private TMP_Text         _cardNameLabel;
    [SerializeField] private TMP_Text         _costLabel;
    [SerializeField] private GameObject       _selectedIndicator;

    public int SlotIndex { get; private set; }

    public bool IsSelected { get; private set; }
    public bool IsLocked   { get; private set; }

    // Invoked when this slot is clicked so DeckPanel can manage single-selection
    public System.Action<CardSlotView> OnSelected;

    private static readonly Color NormalBg   = new(0.18f, 0.18f, 0.18f, 1f);
    private static readonly Color SelectedBg = new(0.25f, 0.30f, 0.35f, 1f);
    private static readonly Color LockedBg   = new(0.12f, 0.12f, 0.12f, 1f);

    // -------------------------------------------------------------------------

    public void Init(int slotIndex)
    {
        SlotIndex = slotIndex;

        _effectZone.zoneType   = FragmentDropZone.ZoneType.Effect;
        _effectZone.slotIndex  = slotIndex;
        _modifierZone.zoneType = FragmentDropZone.ZoneType.Modifier;
        _modifierZone.slotIndex = slotIndex;

        _effectZone.OnFragmentChanged   += RefreshPreview;
        _modifierZone.OnFragmentChanged += RefreshPreview;

        if (_slotNumber != null) _slotNumber.text = (slotIndex + 1).ToString();

        HubDeckBuilderState.Instance.OnStateChanged += RefreshPreview;
        RefreshPreview();
    }

    void OnDestroy()
    {
        if (HubDeckBuilderState.Instance != null)
            HubDeckBuilderState.Instance.OnStateChanged -= RefreshPreview;
    }

    // -------------------------------------------------------------------------

    /// <summary>
    /// Locks the slot so it cannot be interacted with (used for auto-fill slots).
    /// </summary>
    public void SetLocked(bool locked)
    {
        IsLocked = locked;
        if (_effectZone   != null) _effectZone.gameObject.SetActive(!locked);
        if (_modifierZone != null) _modifierZone.gameObject.SetActive(!locked);
        _background.color = locked ? LockedBg : NormalBg;
    }

    public void SetSelected(bool selected)
    {
        if (IsLocked) return;
        IsSelected = selected;
        _background.color = selected ? SelectedBg : NormalBg;
        if (_selectedIndicator != null) _selectedIndicator.SetActive(selected);
    }

    public void OnPointerClick(PointerEventData _)
    {
        if (IsLocked) return;
        OnSelected?.Invoke(this);
    }

    // -------------------------------------------------------------------------

    void RefreshPreview()
    {
        if (IsLocked) return; // locked slots don't update from draft state

        var state = HubDeckBuilderState.Instance;
        if (state == null) return;

        string name = state.PreviewSlotName(SlotIndex);
        bool   full = name != null;

        if (_cardNameLabel != null)
            _cardNameLabel.text = full ? name : string.Empty;

        if (_colorBar != null)
            _colorBar.color = full ? state.PreviewSlotColor(SlotIndex) : Color.gray;
    }

    /// <summary>Sets display for a locked auto-fill slot (Strike or Block).</summary>
    public void SetLockedDisplay(string cardName, Color color)
    {
        if (_cardNameLabel != null) _cardNameLabel.text = cardName;
        if (_colorBar      != null) _colorBar.color     = color;
        if (_costLabel     != null) _costLabel.text      = "1";
    }
}

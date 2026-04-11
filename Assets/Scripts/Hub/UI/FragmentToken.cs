using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Represents one fragment type in the collection panel.
/// Shows the fragment name and available count badge.
/// Dragging initiates a hub drag from the collection (source = -1).
///
/// Attach to a UI panel that has an Image, a name label, and a count badge label.
/// </summary>
public class FragmentToken : MonoBehaviour,
    IBeginDragHandler, IDragHandler, IEndDragHandler, IPointerEnterHandler, IPointerExitHandler
{
    [Header("References")]
    [SerializeField] private Image        _background;
    [SerializeField] private TMP_Text     _nameLabel;
    [SerializeField] private TMP_Text     _countLabel;
    [SerializeField] private GameObject   _countBadge;

    public EffectFragmentData   Effect   { get; private set; }
    public ModifierFragmentData Modifier { get; private set; }
    public bool IsEffect => Effect != null;

    // Visual highlight state set by CollectionPanel during commander filtering
    public enum HighlightState { Normal, CommanderMatch, Dimmed }
    private HighlightState _highlight = HighlightState.Normal;

    private static readonly Color NormalBg       = new(0.22f, 0.22f, 0.22f, 1f);
    private static readonly Color HoverBg        = new(0.30f, 0.30f, 0.30f, 1f);
    private static readonly Color MatchBg        = new(0.55f, 0.45f, 0.10f, 1f); // gold tint
    private static readonly Color MatchHoverBg   = new(0.70f, 0.58f, 0.12f, 1f);
    private static readonly Color DimmedColor    = new(1f, 1f, 1f, 0.35f);
    private static readonly Color NormalColor    = Color.white;

    // -------------------------------------------------------------------------

    public void InitEffect(EffectFragmentData effect)
    {
        Effect   = effect;
        Modifier = null;
        _nameLabel.text = effect.fragmentName;
        if (_background != null) _background.color = NormalBg;
        RefreshCount();
    }

    public void InitModifier(ModifierFragmentData modifier)
    {
        Modifier = modifier;
        Effect   = null;
        _nameLabel.text = modifier.fragmentName;
        if (_background != null) _background.color = NormalBg;
        RefreshCount();
    }

    public void RefreshCount()
    {
        var state = HubDeckBuilderState.Instance;
        if (state == null) return;

        int available = IsEffect
            ? state.AvailableEffectCount(Effect)
            : state.AvailableModifierCount(Modifier);

        if (_countLabel != null) _countLabel.text = available.ToString();
        if (_countBadge != null) _countBadge.SetActive(true);

        // Exhausted tokens are non-interactive but still visible
        var cg = GetComponent<CanvasGroup>();
        if (cg != null) cg.interactable = available > 0;
    }

    public void SetHighlight(HighlightState state)
    {
        _highlight = state;
        RefreshVisual(false);
    }

    // -------------------------------------------------------------------------
    // Drag handlers

    public void OnBeginDrag(PointerEventData eventData)
    {
        var drag = HubDragController.Instance;
        if (drag == null) return;

        var available = IsEffect
            ? HubDeckBuilderState.Instance.AvailableEffectCount(Effect)
            : HubDeckBuilderState.Instance.AvailableModifierCount(Modifier);
        if (available <= 0) return;

        if (IsEffect) drag.BeginEffectDrag(Effect,     -1);
        else          drag.BeginModifierDrag(Modifier, -1);
    }

    public void OnDrag(PointerEventData eventData) { /* ghost is moved by HubDragController.Update */ }

    public void OnEndDrag(PointerEventData eventData)
    {
        HubDragController.Instance?.EndDrag();
        // HubDeckBuilderState.OnStateChanged fires inside EndDrag if a restore occurred
    }

    // -------------------------------------------------------------------------
    // Hover

    public void OnPointerEnter(PointerEventData _) => RefreshVisual(true);
    public void OnPointerExit(PointerEventData _)  => RefreshVisual(false);

    void RefreshVisual(bool hovered)
    {
        if (_background == null) return;

        var cg = GetComponent<CanvasGroup>();
        if (_highlight == HighlightState.Dimmed)
        {
            if (cg != null) cg.alpha = 0.35f;
            _background.color = NormalBg;
            return;
        }

        if (cg != null) cg.alpha = 1f;

        _background.color = _highlight == HighlightState.CommanderMatch
            ? (hovered ? MatchHoverBg : MatchBg)
            : (hovered ? HoverBg     : NormalBg);
    }
}

using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// One half of a card slot — either the effect half or the modifier half.
///
/// Acts as both a drop target (IDropHandler) and a drag source for re-dragging
/// an already-placed fragment (IBeginDragHandler / IEndDragHandler).
///
/// The slot index matches its parent CardSlotView.
/// </summary>
[RequireComponent(typeof(Image))]
public class FragmentDropZone : MonoBehaviour,
    IDropHandler,
    IBeginDragHandler, IDragHandler, IEndDragHandler,
    IPointerEnterHandler, IPointerExitHandler
{
    public enum ZoneType { Effect, Modifier }

    [Header("Config")]
    public ZoneType zoneType;
    public int      slotIndex;

    [Header("References")]
    [SerializeField] private Image    _background;
    [SerializeField] private TMP_Text _label;

    // Callback so CardSlotView / CommanderSlotView can react to changes
    public System.Action OnFragmentChanged;

    private static readonly Color EmptyColor       = new(0.15f, 0.15f, 0.15f, 0.8f);
    private static readonly Color FilledColor      = new(0.25f, 0.25f, 0.30f, 1.0f);
    private static readonly Color HoverValidColor  = new(0.20f, 0.45f, 0.20f, 1.0f);
    private static readonly Color HoverInvalidColor= new(0.45f, 0.20f, 0.20f, 1.0f);

    // -------------------------------------------------------------------------

    void Start()
    {
        HubDeckBuilderState.Instance.OnStateChanged += Refresh;
        Refresh();
    }

    void OnDestroy()
    {
        if (HubDeckBuilderState.Instance != null)
            HubDeckBuilderState.Instance.OnStateChanged -= Refresh;
    }

    public void Refresh()
    {
        var state = HubDeckBuilderState.Instance;
        if (state == null) return;

        string fragmentName = null;
        bool   filled       = false;

        if (zoneType == ZoneType.Effect)
        {
            var e = state.GetSlotEffect(slotIndex);
            fragmentName = e?.fragmentName;
            filled       = e != null;
        }
        else
        {
            var m = state.GetSlotModifier(slotIndex);
            fragmentName = m?.fragmentName;
            filled       = m != null;
        }

        _label.text       = filled ? fragmentName : (zoneType == ZoneType.Effect ? "Effect" : "Modifier");
        _background.color = filled ? FilledColor : EmptyColor;
    }

    // -------------------------------------------------------------------------
    // Drop target

    public void OnDrop(PointerEventData eventData)
    {
        var drag = HubDragController.Instance;
        if (drag == null || !drag.IsDragging) return;

        if (zoneType == ZoneType.Effect   && !drag.IsEffectDrag)  return;
        if (zoneType == ZoneType.Modifier &&  drag.IsEffectDrag)  return;

        var state = HubDeckBuilderState.Instance;
        bool accepted = zoneType == ZoneType.Effect
            ? state.TryAssignEffect(slotIndex,   drag.DraggedEffect)
            : state.TryAssignModifier(slotIndex, drag.DraggedModifier);

        if (accepted)
        {
            drag.MarkDropped();
            OnFragmentChanged?.Invoke();
        }
    }

    // -------------------------------------------------------------------------
    // Drag source (re-drag from a filled zone)

    public void OnBeginDrag(PointerEventData eventData)
    {
        var state = HubDeckBuilderState.Instance;
        var drag  = HubDragController.Instance;
        if (state == null || drag == null) return;

        if (zoneType == ZoneType.Effect)
        {
            var e = state.GetSlotEffect(slotIndex);
            if (e == null) return;
            drag.BeginEffectDrag(e, slotIndex);
        }
        else
        {
            var m = state.GetSlotModifier(slotIndex);
            if (m == null) return;
            drag.BeginModifierDrag(m, slotIndex);
        }
    }

    public void OnDrag(PointerEventData eventData) { /* ghost moves in HubDragController.Update */ }

    public void OnEndDrag(PointerEventData eventData)
    {
        HubDragController.Instance?.EndDrag();
    }

    // -------------------------------------------------------------------------
    // Hover feedback

    public void OnPointerEnter(PointerEventData _)
    {
        var drag = HubDragController.Instance;
        if (drag == null || !drag.IsDragging) return;

        bool compatible = (zoneType == ZoneType.Effect && drag.IsEffectDrag)
                       || (zoneType == ZoneType.Modifier && !drag.IsEffectDrag);
        _background.color = compatible ? HoverValidColor : HoverInvalidColor;
    }

    public void OnPointerExit(PointerEventData _)
    {
        Refresh(); // restore normal color
    }
}

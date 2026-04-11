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
/// The slot index matches its parent CardSlotView; use slotIndex = -2 for the
/// commander slot (CommanderSlotView manages that separately via override hooks).
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
    public int      slotIndex; // 0-19 for normal slots, -2 for commander slot

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

        if (slotIndex >= 0)
        {
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
        }
        else // commander slot
        {
            if (zoneType == ZoneType.Effect)
            {
                var e = state.CmdDraftEffect;
                fragmentName = e?.fragmentName;
                filled       = e != null;
            }
            else
            {
                var m = state.CmdDraftModifier;
                fragmentName = m?.fragmentName;
                filled       = m != null;
            }
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

        // Type check
        if (zoneType == ZoneType.Effect   && !drag.IsEffectDrag)  return;
        if (zoneType == ZoneType.Modifier &&  drag.IsEffectDrag)  return;

        var state = HubDeckBuilderState.Instance;
        bool accepted;

        if (slotIndex >= 0)
        {
            accepted = zoneType == ZoneType.Effect
                ? state.TryAssignEffect(slotIndex,   drag.DraggedEffect)
                : state.TryAssignModifier(slotIndex, drag.DraggedModifier);
        }
        else // commander slot
        {
            accepted = zoneType == ZoneType.Effect
                ? state.TryAssignCommanderEffect(drag.DraggedEffect)
                : state.TryAssignCommanderModifier(drag.DraggedModifier);
        }

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

        if (slotIndex >= 0)
        {
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
        else // commander slot
        {
            if (zoneType == ZoneType.Effect)
            {
                var e = state.CmdDraftEffect;
                if (e == null) return;
                drag.BeginEffectDrag(e, -2);
            }
            else
            {
                var m = state.CmdDraftModifier;
                if (m == null) return;
                drag.BeginModifierDrag(m, -2);
            }
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

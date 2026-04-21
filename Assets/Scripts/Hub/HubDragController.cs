using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;

/// <summary>
/// Singleton that manages the active drag operation in the hub deckbuilder.
///
/// When a drag begins (from a FragmentToken in the collection or from a filled
/// FragmentDropZone), this controller:
///   1. Records what is being dragged and where it came from.
///   2. Creates a ghost UI element that follows the cursor.
///   3. If the source was a slot, immediately clears that slot in HubDeckBuilderState
///      so the available count reflects the in-flight fragment.
///   4. On a successful drop (IDropHandler fires MarkDropped), does nothing extra.
///   5. On a failed drop (EndDrag without a successful drop), restores the fragment
///      to its original slot.
/// </summary>
public class HubDragController : MonoBehaviour
{
    public static HubDragController Instance { get; private set; }

    // -------------------------------------------------------------------------
    // Active drag state

    public EffectFragmentData   DraggedEffect   { get; private set; }
    public ModifierFragmentData DraggedModifier { get; private set; }
    public bool IsDragging    => DraggedEffect != null || DraggedModifier != null;
    public bool IsEffectDrag  => DraggedEffect != null;

    // Where the drag originated: -1 = collection, 0+ = normal slot
    public int  SourceSlotIndex      { get; private set; } = -1;

    private bool _wasDropped;

    // Ghost visual
    private RectTransform _ghost;
    private Canvas        _rootCanvas;

    // -------------------------------------------------------------------------

    void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
    }

    void Update()
    {
        if (_ghost != null && Mouse.current != null)
            _ghost.position = Mouse.current.position.ReadValue();
    }

    // -------------------------------------------------------------------------
    // Begin drag

    /// <summary>
    /// Starts a drag of an effect fragment.
    /// <paramref name="sourceSlotIndex"/>: -1 = collection, -2 = commander slot, 0-19 = normal slot.
    /// If coming from a slot, that slot is cleared immediately.
    /// </summary>
    public void BeginEffectDrag(EffectFragmentData effect, int sourceSlotIndex)
    {
        DraggedEffect   = effect;
        DraggedModifier = null;
        SourceSlotIndex = sourceSlotIndex;
        _wasDropped     = false;

        ClearSource();
        CreateGhost(effect.fragmentName, effect.effectColor);
    }

    /// <summary>
    /// Starts a drag of a modifier fragment.
    /// </summary>
    public void BeginModifierDrag(ModifierFragmentData modifier, int sourceSlotIndex)
    {
        DraggedEffect   = null;
        DraggedModifier = modifier;
        SourceSlotIndex = sourceSlotIndex;
        _wasDropped     = false;

        ClearSource();
        CreateGhost(modifier.fragmentName, new Color(0.7f, 0.7f, 0.9f, 1f));
    }

    // -------------------------------------------------------------------------
    // Drop resolution

    /// <summary>Called by a FragmentDropZone when it successfully accepts the drop.</summary>
    public void MarkDropped() => _wasDropped = true;

    /// <summary>Called by the drag source's OnEndDrag to clean up.</summary>
    public void EndDrag()
    {
        if (!_wasDropped)
            RestoreSource();

        DestroyGhost();
        DraggedEffect   = null;
        DraggedModifier = null;
        SourceSlotIndex = -1;
        _wasDropped     = false;
    }

    // -------------------------------------------------------------------------
    // Internal helpers

    void ClearSource()
    {
        var state = HubDeckBuilderState.Instance;
        if (state == null) return;

        if (SourceSlotIndex >= 0)
        {
            if (IsEffectDrag) state.ClearSlotEffect(SourceSlotIndex);
            else              state.ClearSlotModifier(SourceSlotIndex);
        }
        // Source == collection (-1): nothing to clear
    }

    void RestoreSource()
    {
        var state = HubDeckBuilderState.Instance;
        if (state == null) return;

        if (SourceSlotIndex >= 0)
        {
            if (IsEffectDrag) state.TryAssignEffect(SourceSlotIndex,   DraggedEffect);
            else              state.TryAssignModifier(SourceSlotIndex, DraggedModifier);
        }
        // Source == collection (-1): nothing to restore; fragment was never removed
    }

    void CreateGhost(string label, Color color)
    {
        if (_rootCanvas == null)
            _rootCanvas = FindFirstObjectByType<Canvas>();
        if (_rootCanvas == null) return;

        var go = new GameObject("DragGhost");
        _ghost = go.AddComponent<RectTransform>();
        _ghost.SetParent(_rootCanvas.transform, false);
        _ghost.sizeDelta = new Vector2(110, 50);

        var img = go.AddComponent<Image>();
        img.color         = new Color(color.r, color.g, color.b, 0.9f);
        img.raycastTarget = false;

        var labelGo = new GameObject("Label");
        labelGo.transform.SetParent(_ghost, false);
        var labelRt = labelGo.AddComponent<RectTransform>();
        labelRt.anchorMin  = Vector2.zero;
        labelRt.anchorMax  = Vector2.one;
        labelRt.offsetMin  = new Vector2(4, 4);
        labelRt.offsetMax  = new Vector2(-4, -4);

        var tmp = labelGo.AddComponent<TextMeshProUGUI>();
        tmp.text              = label;
        tmp.alignment         = TextAlignmentOptions.Center;
        tmp.fontSize          = 13;
        tmp.raycastTarget     = false;

        // Ghost must not block drop raycasts
        var cg = go.AddComponent<CanvasGroup>();
        cg.blocksRaycasts = false;
        cg.alpha          = 0.85f;

        _ghost.SetAsLastSibling();
        if (Mouse.current != null) _ghost.position = Mouse.current.position.ReadValue();
    }

    void DestroyGhost()
    {
        if (_ghost != null)
        {
            Destroy(_ghost.gameObject);
            _ghost = null;
        }
    }
}

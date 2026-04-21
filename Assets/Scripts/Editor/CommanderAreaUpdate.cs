using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using TMPro;

using UImg  = UnityEngine.UI.Image;
using UBtn  = UnityEngine.UI.Button;
using UHoriz = UnityEngine.UI.HorizontalLayoutGroup;
using ULE   = UnityEngine.UI.LayoutElement;

/// <summary>
/// One-shot editor script to replace the old commander draft/forge UI
/// with a simple TMP_Dropdown selector.
///
/// Run via: DeckSaver → Update Commander UI
/// </summary>
public static class CommanderAreaUpdate
{
    [MenuItem("DeckSaver/Update Commander UI")]
    public static void Run()
    {
        var scene = EditorSceneManager.GetActiveScene();

        // ── Find the existing CommanderArea ───────────────────────────────────
        var oldCsv = Object.FindFirstObjectByType<CommanderSlotView>();
        if (oldCsv == null)
        {
            Debug.LogError("[CommanderAreaUpdate] No CommanderSlotView found in scene. Aborting.");
            return;
        }

        var area = oldCsv.gameObject;

        // ── Nuke all children (old draft zones, forge button, picker, etc.) ──
        for (int i = area.transform.childCount - 1; i >= 0; i--)
            Object.DestroyImmediate(area.transform.GetChild(i).gameObject);

        // Remove the old CommanderSlotView component — we'll add a fresh one
        Object.DestroyImmediate(oldCsv);

        // ── Header ───────────────────────────────────────────────────────────
        Tmp(Anchored(area.transform, "Header", 0f, 0.82f, 1f, 1f),
            "COMMANDER", 15, FontStyles.Bold, TextAlignmentOptions.Center,
            new Color(1f, 0.85f, 0.4f));

        // ── Dropdown row (top portion) ───────────────────────────────────────
        var dropdownGo = Anchored(area.transform, "CommanderDropdown", 0.04f, 0.50f, 0.96f, 0.80f);
        dropdownGo.AddComponent<UImg>().color = new Color(0.18f, 0.18f, 0.22f);

        // TMP_Dropdown needs specific child structure
        var dropdown = dropdownGo.AddComponent<TMP_Dropdown>();

        // Caption label (shows selected option)
        var captionGo = Anchored(dropdownGo.transform, "Label", 0.05f, 0f, 0.85f, 1f);
        var captionTmp = Tmp(captionGo, "-- Select Commander --", 13, FontStyles.Normal,
            TextAlignmentOptions.MidlineLeft, Color.white);
        dropdown.captionText = captionTmp;

        // Arrow indicator
        var arrowGo = Anchored(dropdownGo.transform, "Arrow", 0.88f, 0.25f, 0.96f, 0.75f);
        var arrowTmp = Tmp(arrowGo, "\u25BC", 12, FontStyles.Normal,
            TextAlignmentOptions.Center, new Color(0.7f, 0.7f, 0.7f));

        // Template (the popup list — starts inactive)
        var templateGo = Anchored(dropdownGo.transform, "Template", 0f, 0f, 1f, 0f);
        var templateRt = templateGo.GetComponent<RectTransform>();
        templateRt.pivot = new Vector2(0.5f, 1f);
        templateRt.anchorMin = new Vector2(0f, 0f);
        templateRt.anchorMax = new Vector2(1f, 0f);
        templateRt.offsetMin = Vector2.zero;
        templateRt.offsetMax = Vector2.zero;
        templateRt.sizeDelta = new Vector2(0, 150);
        templateGo.AddComponent<UImg>().color = new Color(0.14f, 0.14f, 0.18f);
        templateGo.AddComponent<UnityEngine.UI.ScrollRect>();
        templateGo.SetActive(false);

        // Viewport inside template
        var viewportGo = FullChild(templateGo.transform, "Viewport");
        viewportGo.AddComponent<UImg>().color = Color.white;
        viewportGo.AddComponent<UnityEngine.UI.Mask>().showMaskGraphic = false;

        // Content inside viewport
        var contentGo = FullChild(viewportGo.transform, "Content");
        var contentRt = contentGo.GetComponent<RectTransform>();
        contentRt.pivot = new Vector2(0.5f, 1f);
        contentRt.anchorMin = new Vector2(0f, 1f);
        contentRt.anchorMax = new Vector2(1f, 1f);
        contentRt.offsetMin = Vector2.zero;
        contentRt.offsetMax = Vector2.zero;
        contentRt.sizeDelta = new Vector2(0, 28);

        // Item template inside content
        var itemGo = Anchored(contentGo.transform, "Item", 0f, 0f, 1f, 0f);
        var itemRt = itemGo.GetComponent<RectTransform>();
        itemRt.sizeDelta = new Vector2(0, 28);
        itemGo.AddComponent<UImg>().color = new Color(0.18f, 0.18f, 0.22f);

        // Item background (for highlight)
        var itemBgGo = FullChild(itemGo.transform, "Item Background");
        var itemBgImg = itemBgGo.AddComponent<UImg>();
        itemBgImg.color = new Color(0.25f, 0.35f, 0.5f, 0.6f);

        // Item checkmark (optional, small indicator)
        var checkGo = Anchored(itemGo.transform, "Item Checkmark", 0.02f, 0.2f, 0.08f, 0.8f);
        var checkTmp = Tmp(checkGo, "\u2713", 12, FontStyles.Normal,
            TextAlignmentOptions.Center, new Color(1f, 0.85f, 0.4f));

        // Item label
        var itemLabelGo = Anchored(itemGo.transform, "Item Label", 0.10f, 0f, 0.95f, 1f);
        var itemLabelTmp = Tmp(itemLabelGo, "", 13, FontStyles.Normal,
            TextAlignmentOptions.MidlineLeft, Color.white);

        // Wire up the toggle on the item
        var itemToggle = itemGo.AddComponent<UnityEngine.UI.Toggle>();
        itemToggle.targetGraphic = itemBgImg;
        itemToggle.graphic = checkTmp;
        itemToggle.isOn = true;

        // Wire ScrollRect
        var scrollRect = templateGo.GetComponent<UnityEngine.UI.ScrollRect>();
        scrollRect.content  = contentRt;
        scrollRect.viewport = viewportGo.GetComponent<RectTransform>();
        scrollRect.horizontal = false;
        scrollRect.movementType = UnityEngine.UI.ScrollRect.MovementType.Clamped;

        // Wire TMP_Dropdown template references
        dropdown.template   = templateRt;
        dropdown.itemText   = itemLabelTmp;

        // ── Selected commander display (bottom strip) ────────────────────────
        var selPanel = Anchored(area.transform, "SelectedPanel", 0f, 0f, 1f, 0.48f);
        selPanel.AddComponent<UImg>().color = new Color(0.2f, 0.15f, 0.05f);
        selPanel.SetActive(false);
        var shl = selPanel.AddComponent<UHoriz>();
        shl.childForceExpandWidth = false; shl.childForceExpandHeight = true;
        shl.spacing = 8; shl.padding = new RectOffset(8, 4, 3, 3);

        var selNameGo = LayoutChild(selPanel.transform, "SelectedName", 0, 0);
        selNameGo.GetComponent<ULE>().preferredWidth = 160;
        var selNameTmp = Tmp(selNameGo, "", 13, FontStyles.Bold,
            TextAlignmentOptions.MidlineLeft, new Color(1f, 0.85f, 0.4f));

        var selPassGo = Child(selPanel.transform, "SelectedPassive");
        selPassGo.AddComponent<ULE>().flexibleWidth = 1;
        var selPassTmp = Tmp(selPassGo, "", 10, FontStyles.Normal,
            TextAlignmentOptions.MidlineLeft, new Color(0.7f, 1f, 0.7f));
        selPassTmp.textWrappingMode = TextWrappingModes.Normal;

        // ── Add and wire new CommanderSlotView ───────────────────────────────
        var csv = area.AddComponent<CommanderSlotView>();
        Wire(csv, "_commanderDropdown",    dropdown);
        Wire(csv, "_selectedPanel",        selPanel);
        Wire(csv, "_selectedNameLabel",    selNameTmp);
        Wire(csv, "_selectedPassiveLabel", selPassTmp);

        // ── Save ─────────────────────────────────────────────────────────────
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("[CommanderAreaUpdate] Commander UI updated to dropdown selector. Scene saved.");
    }

    // =========================================================================
    // Helpers (duplicated from HubSceneSetup to keep this self-contained)
    // =========================================================================

    static GameObject Anchored(Transform parent, string name,
        float x0, float y0, float x1, float y1)
    {
        var go = Child(parent, name);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(x0, y0);
        rt.anchorMax = new Vector2(x1, y1);
        rt.offsetMin = rt.offsetMax = Vector2.zero;
        return go;
    }

    static GameObject Child(Transform parent, string name)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.AddComponent<RectTransform>();
        return go;
    }

    static GameObject FullChild(Transform parent, string name)
    {
        var go = Child(parent, name);
        FullRect(go.GetComponent<RectTransform>());
        return go;
    }

    static GameObject LayoutChild(Transform parent, string name, float flexH, float prefH)
    {
        var go = Child(parent, name);
        var le = go.AddComponent<ULE>();
        le.flexibleHeight = flexH;
        if (prefH > 0) le.preferredHeight = prefH;
        return go;
    }

    static void FullRect(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
    }

    static TextMeshProUGUI Tmp(GameObject go, string text, float size,
        FontStyles style, TextAlignmentOptions align, Color col)
    {
        var t = go.AddComponent<TextMeshProUGUI>();
        t.text      = text;
        t.fontSize  = size;
        t.fontStyle = style;
        t.alignment = align;
        t.color     = col;
        return t;
    }

    static void Wire(Object target, string field, Object value)
    {
        var so   = new SerializedObject(target);
        var prop = so.FindProperty(field);
        if (prop == null)
        {
            Debug.LogWarning($"[CommanderAreaUpdate] Field '{field}' not found on {target.GetType().Name}");
            return;
        }
        prop.objectReferenceValue = value;
        so.ApplyModifiedProperties();
    }
}

// Avoid all ambiguous using statements — use fully qualified names throughout.
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using TMPro;

// Aliases for every Unity UI type so nothing collides with project namespaces.
using UCanvas      = UnityEngine.Canvas;
using UCanvasScale = UnityEngine.UI.CanvasScaler;
using UGraphicRay  = UnityEngine.UI.GraphicRaycaster;
using UImg         = UnityEngine.UI.Image;
using UBtn         = UnityEngine.UI.Button;
using UHoriz       = UnityEngine.UI.HorizontalLayoutGroup;
using UVert        = UnityEngine.UI.VerticalLayoutGroup;
using UGrid        = UnityEngine.UI.GridLayoutGroup;
using UCSF         = UnityEngine.UI.ContentSizeFitter;
using ULE          = UnityEngine.UI.LayoutElement;
using URectMask    = UnityEngine.UI.RectMask2D;
using UInputField  = TMPro.TMP_InputField;

/// <summary>
/// Builds the Hub scene from scratch.
/// Run via: DeckSaver → Setup Hub Scene
/// Re-runnable — clears previously created objects first.
/// </summary>
public static class HubSceneSetup
{
    [MenuItem("DeckSaver/Setup Hub Scene")]
    public static void Run()
    {
        var scene = EditorSceneManager.OpenScene("Assets/Scenes/Hub.unity", OpenSceneMode.Single);

        // ── Clear previous attempt ────────────────────────────────────────────
        foreach (var name in new[] { "Canvas", "HubManagers" })
        {
            var old = GameObject.Find(name);
            if (old != null) Object.DestroyImmediate(old);
        }

        // ── EventSystem ───────────────────────────────────────────────────────
        var existingES = Object.FindFirstObjectByType<UnityEngine.EventSystems.EventSystem>();
        if (existingES == null)
        {
            var es = new GameObject("EventSystem");
            es.AddComponent<UnityEngine.EventSystems.EventSystem>();
            es.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
        }
        else
        {
            // Swap old StandaloneInputModule for InputSystemUIInputModule if needed
            var oldModule = existingES.GetComponent<UnityEngine.EventSystems.StandaloneInputModule>();
            if (oldModule != null)
            {
                Object.DestroyImmediate(oldModule);
                existingES.gameObject.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
                Debug.Log("[HubSetup] Replaced StandaloneInputModule with InputSystemUIInputModule.");
            }
        }

        // ── Canvas ────────────────────────────────────────────────────────────
        var canvasGo    = new GameObject("Canvas");
        var canvas      = canvasGo.AddComponent<UCanvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler      = canvasGo.AddComponent<UCanvasScale>();
        scaler.uiScaleMode          = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution  = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight   = 0.5f;
        canvasGo.AddComponent<UGraphicRay>();

        // ── HubManagers ───────────────────────────────────────────────────────
        var managers = new GameObject("HubManagers");
        var state    = managers.AddComponent<HubDeckBuilderState>();
        managers.AddComponent<HubDragController>();

        // ── Prefabs ───────────────────────────────────────────────────────────
        var tokenPrefab = CreateFragmentTokenPrefab();
        var slotPrefab  = CreateCardSlotPrefab();

        // ── Top-level panels ──────────────────────────────────────────────────
        var collectionPanel = Panel(canvasGo.transform, "CollectionPanel", 0f,    0.08f, 0.38f, 1f,    new Color(0.13f, 0.13f, 0.16f));
        var deckPanel       = Panel(canvasGo.transform, "DeckPanel",       0.38f, 0.08f, 1f,    1f,    new Color(0.11f, 0.11f, 0.14f));
        var bottomBar       = Panel(canvasGo.transform, "BottomBar",       0f,    0f,    1f,    0.08f, new Color(0.08f, 0.08f, 0.10f));

        // ── Collection panel ──────────────────────────────────────────────────
        BuildCollectionPanel(collectionPanel, tokenPrefab,
            out var effectsGrid, out var modsGrid);

        // ── Deck panel ────────────────────────────────────────────────────────
        BuildDeckPanel(deckPanel, slotPrefab,
            out var slotsParent, out var slotCountTmp,
            out var previewRoot, out var previewName,
            out var previewDesc, out var previewCost);

        // ── Bottom bar ────────────────────────────────────────────────────────
        BuildBottomBar(bottomBar,
            out var inputField, out var confirmBtn, out var confirmLabel);

        // ── Wire CollectionPanel ──────────────────────────────────────────────
        var cp = collectionPanel.AddComponent<CollectionPanel>();
        Wire(cp, "_effectsGrid",         effectsGrid.transform);
        Wire(cp, "_modifiersGrid",       modsGrid.transform);
        Wire(cp, "_fragmentTokenPrefab", tokenPrefab);

        // ── Wire DeckPanel ────────────────────────────────────────────────────
        var dp = deckPanel.AddComponent<DeckPanel>();
        Wire(dp, "_slotsParent",      slotsParent.transform);
        Wire(dp, "_cardSlotPrefab",   slotPrefab);
        Wire(dp, "_slotCountLabel",   slotCountTmp);
        Wire(dp, "_previewRoot",      previewRoot);
        Wire(dp, "_previewNameLabel", previewName);
        Wire(dp, "_previewDescLabel", previewDesc);
        Wire(dp, "_previewCostLabel", previewCost);

        // ── Wire HubUI ────────────────────────────────────────────────────────
        var hubUI = canvasGo.AddComponent<HubUI>();
        Wire(hubUI, "_confirmButton",      confirmBtn);
        Wire(hubUI, "_confirmButtonLabel", confirmLabel);
        Wire(hubUI, "_deckNameInput",      inputField);

        // ── Wire HubDeckBuilderState ──────────────────────────────────────────
        var collGuids = AssetDatabase.FindAssets("t:PlayerCollection");
        if (collGuids.Length > 0)
        {
            var coll = AssetDatabase.LoadAssetAtPath<PlayerCollection>(
                AssetDatabase.GUIDToAssetPath(collGuids[0]));
            Wire(state, "collection", coll);
            Debug.Log($"[HubSetup] Wired PlayerCollection: {coll.name}");
        }
        else
            Debug.LogWarning("[HubSetup] No PlayerCollection asset found — assign manually.");


        // ── Create PlayerCollection if missing ────────────────────────────────
        if (collGuids.Length == 0)
        {
            var coll = ScriptableObject.CreateInstance<PlayerCollection>();
            AssetDatabase.CreateAsset(coll, "Assets/Cards/PlayerCollection.asset");
            AssetDatabase.SaveAssets();
            Wire(state, "collection", coll);
            Debug.Log("[HubSetup] Created default PlayerCollection asset.");
        }

        // ── Wire BasicFragmentPool ────────────────────────────────────────────
        var poolGuids = AssetDatabase.FindAssets("t:BasicFragmentPool");
        if (poolGuids.Length > 0)
        {
            var pool = AssetDatabase.LoadAssetAtPath<BasicFragmentPool>(
                AssetDatabase.GUIDToAssetPath(poolGuids[0]));
            Wire(state, "basicFragmentPool", pool);
            Debug.Log($"[HubSetup] Wired BasicFragmentPool: {pool.name}");
        }
        else
            Debug.LogWarning("[HubSetup] No BasicFragmentPool asset found — create one and assign manually.");

        // ── Save ──────────────────────────────────────────────────────────────
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("[HubSetup] Done — Hub scene saved.");
    }

    // =========================================================================
    // Collection panel
    // =========================================================================

    static void BuildCollectionPanel(GameObject panel, GameObject tokenPrefab,
        out GameObject effectsGrid, out GameObject modsGrid)
    {
        // Header
        var header    = Anchored(panel.transform, "Header", 0f, 0.94f, 1f, 1f);
        var headerTmp = header.AddComponent<TextMeshProUGUI>();
        headerTmp.text      = "COLLECTION";
        headerTmp.fontSize  = 22;
        headerTmp.fontStyle = FontStyles.Bold;
        headerTmp.alignment = TextAlignmentOptions.Center;
        headerTmp.color     = new Color(0.9f, 0.8f, 0.5f);

        // Thin divider between the two halves
        var div = Anchored(panel.transform, "Divider", 0.02f, 0.465f, 0.98f, 0.470f);
        div.AddComponent<UImg>().color = new Color(0.4f, 0.4f, 0.4f);

        // Effects section (upper half) — label anchored at top, grid fills the rest
        var effSec = Anchored(panel.transform, "EffectsSection", 0f, 0.47f, 1f, 0.94f);
        effSec.AddComponent<UImg>().color = Color.clear;
        effSec.AddComponent<URectMask>(); // clip tokens that overflow

        var effLabel = Anchored(effSec.transform, "Label", 0f, 0.92f, 1f, 1f);
        effLabel.GetComponent<RectTransform>().offsetMin = new Vector2(8f, 0f);
        Tmp(effLabel, "Effects", 15, FontStyles.Bold, TextAlignmentOptions.MidlineLeft,
            new Color(0.7f, 0.85f, 1f));

        effectsGrid = Anchored(effSec.transform, "EffectsGrid", 0f, 0f, 1f, 0.92f);
        effectsGrid.GetComponent<RectTransform>().offsetMin = new Vector2(6f, 4f);
        effectsGrid.GetComponent<RectTransform>().offsetMax = new Vector2(-6f, -2f);
        effectsGrid.AddComponent<UImg>().color = Color.clear;
        var glgE = effectsGrid.AddComponent<UGrid>();
        glgE.cellSize        = new Vector2(148, 50);
        glgE.spacing         = new Vector2(6, 6);
        glgE.constraint      = UGrid.Constraint.FixedColumnCount;
        glgE.constraintCount = 2;
        glgE.childAlignment  = TextAnchor.UpperLeft;
        glgE.padding         = new RectOffset(0, 0, 2, 2);

        // Modifiers section (lower half) — same approach
        var modSec = Anchored(panel.transform, "ModifiersSection", 0f, 0f, 1f, 0.465f);
        modSec.AddComponent<UImg>().color = Color.clear;
        modSec.AddComponent<URectMask>();

        var modLabel = Anchored(modSec.transform, "Label", 0f, 0.92f, 1f, 1f);
        modLabel.GetComponent<RectTransform>().offsetMin = new Vector2(8f, 0f);
        Tmp(modLabel, "Modifiers", 15, FontStyles.Bold, TextAlignmentOptions.MidlineLeft,
            new Color(0.85f, 0.7f, 1f));

        modsGrid = Anchored(modSec.transform, "ModifiersGrid", 0f, 0f, 1f, 0.92f);
        modsGrid.GetComponent<RectTransform>().offsetMin = new Vector2(6f, 4f);
        modsGrid.GetComponent<RectTransform>().offsetMax = new Vector2(-6f, -2f);
        modsGrid.AddComponent<UImg>().color = Color.clear;
        var glgM = modsGrid.AddComponent<UGrid>();
        glgM.cellSize        = new Vector2(148, 50);
        glgM.spacing         = new Vector2(6, 6);
        glgM.constraint      = UGrid.Constraint.FixedColumnCount;
        glgM.constraintCount = 2;
        glgM.childAlignment  = TextAnchor.UpperLeft;
        glgM.padding         = new RectOffset(0, 0, 2, 2);
    }

    // =========================================================================
    // Deck panel
    // =========================================================================

    static void BuildDeckPanel(GameObject panel, GameObject slotPrefab,
        out GameObject slotsParent,    out TextMeshProUGUI slotCount,
        out GameObject previewRoot,    out TextMeshProUGUI previewName,
        out TextMeshProUGUI previewDesc, out TextMeshProUGUI previewCost)
    {
        // Header
        var header = Anchored(panel.transform, "DeckHeader", 0f, 0.94f, 1f, 1f);
        header.AddComponent<UImg>().color = Color.clear;
        var hl = header.AddComponent<UHoriz>();
        hl.padding             = new RectOffset(12, 12, 6, 6);
        hl.childForceExpandWidth  = true;
        hl.childForceExpandHeight = true;
        hl.spacing             = 8;

        Tmp(Child(header.transform, "Title"), "DECK", 22, FontStyles.Bold,
            TextAlignmentOptions.Left, new Color(0.9f, 0.8f, 0.5f));
        slotCount = Tmp(Child(header.transform, "SlotCount"), "0 / 20", 16,
            FontStyles.Normal, TextAlignmentOptions.Right, new Color(0.65f, 0.65f, 0.65f));

        // Commander area
        var cmdArea = Anchored(panel.transform, "CommanderArea", 0f, 0.71f, 1f, 0.94f);
        cmdArea.AddComponent<UImg>().color = new Color(0.15f, 0.12f, 0.08f);
        BuildCommanderArea(cmdArea);

        // Slots grid
        slotsParent = Anchored(panel.transform, "SlotsParent", 0.005f, 0.005f, 0.995f, 0.71f);
        slotsParent.AddComponent<UImg>().color = Color.clear;
        slotsParent.AddComponent<URectMask>(); // clip slots that overflow the panel
        var grid = slotsParent.AddComponent<UGrid>();
        grid.cellSize        = new Vector2(218, 78);
        grid.spacing         = new Vector2(5, 5);
        grid.constraint      = UGrid.Constraint.FixedColumnCount;
        grid.constraintCount = 5;
        grid.childAlignment  = TextAnchor.UpperLeft;
        grid.padding         = new RectOffset(4, 4, 4, 4);

        // Preview panel (floating bottom-right)
        previewRoot = Anchored(panel.transform, "PreviewPanel", 0.5f, 0f, 1f, 0.28f);
        previewRoot.AddComponent<UImg>().color = new Color(0.08f, 0.12f, 0.18f, 0.96f);
        previewRoot.SetActive(false);
        VLayout(previewRoot, 12, 10, 5);

        previewName = Tmp(LayoutChild(previewRoot.transform, "PName", 0, 30),
            "", 18, FontStyles.Bold, TextAlignmentOptions.Left, Color.white);
        previewCost = Tmp(LayoutChild(previewRoot.transform, "PCost", 0, 22),
            "", 13, FontStyles.Normal, TextAlignmentOptions.Left, new Color(0.8f, 0.9f, 0.5f));
        previewDesc = Tmp(LayoutChild(previewRoot.transform, "PDesc", 0, 90),
            "", 12, FontStyles.Normal, TextAlignmentOptions.TopLeft, new Color(0.85f, 0.85f, 0.85f));
        previewDesc.textWrappingMode = TextWrappingModes.Normal;
    }

    static void BuildCommanderArea(GameObject area)
    {
        // Header label
        Tmp(Anchored(area.transform, "Header", 0f, 0.82f, 1f, 1f),
            "COMMANDER", 15, FontStyles.Bold, TextAlignmentOptions.Center,
            new Color(1f, 0.85f, 0.4f));

        // Dropdown
        var dropdownGo = Anchored(area.transform, "CommanderDropdown", 0.04f, 0.50f, 0.96f, 0.80f);
        dropdownGo.AddComponent<UImg>().color = new Color(0.18f, 0.18f, 0.22f);

        var dropdown = dropdownGo.AddComponent<TMP_Dropdown>();

        var captionGo = Anchored(dropdownGo.transform, "Label", 0.05f, 0f, 0.85f, 1f);
        var captionTmp = Tmp(captionGo, "-- Select Commander --", 13, FontStyles.Normal,
            TextAlignmentOptions.MidlineLeft, Color.white);
        dropdown.captionText = captionTmp;

        var arrowGo = Anchored(dropdownGo.transform, "Arrow", 0.88f, 0.25f, 0.96f, 0.75f);
        Tmp(arrowGo, "\u25BC", 12, FontStyles.Normal,
            TextAlignmentOptions.Center, new Color(0.7f, 0.7f, 0.7f));

        // Template popup
        var templateGo = Anchored(dropdownGo.transform, "Template", 0f, 0f, 1f, 0f);
        var templateRt = templateGo.GetComponent<RectTransform>();
        templateRt.pivot = new Vector2(0.5f, 1f);
        templateRt.anchorMin = new Vector2(0f, 0f);
        templateRt.anchorMax = new Vector2(1f, 0f);
        templateRt.offsetMin = templateRt.offsetMax = Vector2.zero;
        templateRt.sizeDelta = new Vector2(0, 150);
        templateGo.AddComponent<UImg>().color = new Color(0.14f, 0.14f, 0.18f);
        var scrollRect = templateGo.AddComponent<UnityEngine.UI.ScrollRect>();
        templateGo.SetActive(false);

        var viewportGo = FullChild(templateGo.transform, "Viewport");
        viewportGo.AddComponent<UImg>().color = Color.white;
        viewportGo.AddComponent<UnityEngine.UI.Mask>().showMaskGraphic = false;

        var contentGo = FullChild(viewportGo.transform, "Content");
        var contentRt = contentGo.GetComponent<RectTransform>();
        contentRt.pivot = new Vector2(0.5f, 1f);
        contentRt.anchorMin = new Vector2(0f, 1f);
        contentRt.anchorMax = new Vector2(1f, 1f);
        contentRt.offsetMin = contentRt.offsetMax = Vector2.zero;
        contentRt.sizeDelta = new Vector2(0, 28);

        var itemGo = Anchored(contentGo.transform, "Item", 0f, 0f, 1f, 0f);
        var itemRt = itemGo.GetComponent<RectTransform>();
        itemRt.sizeDelta = new Vector2(0, 28);
        itemGo.AddComponent<UImg>().color = new Color(0.18f, 0.18f, 0.22f);

        var itemBgGo = FullChild(itemGo.transform, "Item Background");
        var itemBgImg = itemBgGo.AddComponent<UImg>();
        itemBgImg.color = new Color(0.25f, 0.35f, 0.5f, 0.6f);

        var checkGo = Anchored(itemGo.transform, "Item Checkmark", 0.02f, 0.2f, 0.08f, 0.8f);
        var checkTmp = Tmp(checkGo, "\u2713", 12, FontStyles.Normal,
            TextAlignmentOptions.Center, new Color(1f, 0.85f, 0.4f));

        var itemLabelGo = Anchored(itemGo.transform, "Item Label", 0.10f, 0f, 0.95f, 1f);
        var itemLabelTmp = Tmp(itemLabelGo, "", 13, FontStyles.Normal,
            TextAlignmentOptions.MidlineLeft, Color.white);

        var itemToggle = itemGo.AddComponent<UnityEngine.UI.Toggle>();
        itemToggle.targetGraphic = itemBgImg;
        itemToggle.graphic = checkTmp;
        itemToggle.isOn = true;

        scrollRect.content    = contentRt;
        scrollRect.viewport   = viewportGo.GetComponent<RectTransform>();
        scrollRect.horizontal = false;
        scrollRect.movementType = UnityEngine.UI.ScrollRect.MovementType.Clamped;

        dropdown.template = templateRt;
        dropdown.itemText = itemLabelTmp;

        // Selected commander display (bottom strip)
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

        // Wire CommanderSlotView
        var csv = area.AddComponent<CommanderSlotView>();
        Wire(csv, "_commanderDropdown",    dropdown);
        Wire(csv, "_selectedPanel",        selPanel);
        Wire(csv, "_selectedNameLabel",    selNameTmp);
        Wire(csv, "_selectedPassiveLabel", selPassTmp);
    }

    // =========================================================================
    // Bottom bar
    // =========================================================================

    static void BuildBottomBar(GameObject bar,
        out TMP_InputField inputField, out UBtn confirmBtn, out TextMeshProUGUI confirmLabel)
    {
        // Deck name input
        var inputGo = Anchored(bar.transform, "DeckNameInput", 0.02f, 0.12f, 0.46f, 0.88f);
        inputGo.AddComponent<UImg>().color = new Color(0.2f, 0.2f, 0.22f);
        inputField = inputGo.AddComponent<TMP_InputField>();

        var textArea = Child(inputGo.transform, "Text Area");
        textArea.AddComponent<URectMask>();
        FullRect(textArea.GetComponent<RectTransform>());
        textArea.GetComponent<RectTransform>().offsetMin = new Vector2(8, 2);
        textArea.GetComponent<RectTransform>().offsetMax = new Vector2(-8, -2);

        var ph = Child(textArea.transform, "Placeholder");
        FullRect(ph.GetComponent<RectTransform>());
        var phTmp = Tmp(ph, "Deck name...", 16, FontStyles.Italic,
            TextAlignmentOptions.MidlineLeft, new Color(0.45f, 0.45f, 0.45f));

        var it = Child(textArea.transform, "Text");
        FullRect(it.GetComponent<RectTransform>());
        var itTmp = Tmp(it, "", 16, FontStyles.Normal,
            TextAlignmentOptions.MidlineLeft, Color.white);

        var iSo = new SerializedObject(inputField);
        iSo.FindProperty("m_TextComponent").objectReferenceValue = itTmp;
        iSo.FindProperty("m_Placeholder").objectReferenceValue   = phTmp;
        iSo.ApplyModifiedProperties();
        inputField.text = "My Deck";

        // Confirm button
        var confirmGo = Anchored(bar.transform, "ConfirmButton", 0.54f, 0.10f, 0.98f, 0.90f);
        confirmGo.AddComponent<UImg>().color = new Color(0.2f, 0.45f, 0.2f);
        confirmBtn   = confirmGo.AddComponent<UBtn>();
        confirmLabel = Tmp(FullChild(confirmGo.transform, "Label"), "Start Run (0/20)",
            18, FontStyles.Bold, TextAlignmentOptions.Center, Color.white);
    }

    // =========================================================================
    // Prefabs
    // =========================================================================

    static GameObject CreateFragmentTokenPrefab()
    {
        const string path = "Assets/Prefabs/FragmentToken.prefab";
        var existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (existing != null) return existing;

        var root = new GameObject("FragmentToken");
        root.AddComponent<RectTransform>().sizeDelta = new Vector2(148, 50);
        var bg = root.AddComponent<UImg>();
        bg.color = new Color(0.22f, 0.22f, 0.22f);
        root.AddComponent<CanvasGroup>();

        var nameLblGo = Child(root.transform, "NameLabel");
        var nrt = nameLblGo.GetComponent<RectTransform>();
        nrt.anchorMin = Vector2.zero; nrt.anchorMax = Vector2.one;
        nrt.offsetMin = new Vector2(8f, 0f); nrt.offsetMax = new Vector2(-34f, 0f);
        var nameTmp = Tmp(nameLblGo, "", 14, FontStyles.Normal,
            TextAlignmentOptions.MidlineLeft, Color.white);

        var badge = Child(root.transform, "CountBadge");
        var brt = badge.GetComponent<RectTransform>();
        brt.anchorMin = brt.anchorMax = new Vector2(1f, 0.5f);
        brt.pivot = new Vector2(1f, 0.5f);
        brt.anchoredPosition = new Vector2(-4f, 0f);
        brt.sizeDelta = new Vector2(28f, 28f);
        badge.AddComponent<UImg>().color = new Color(0.28f, 0.28f, 0.48f);
        var countTmp = Tmp(FullChild(badge.transform, "CountLabel"), "",
            13, FontStyles.Bold, TextAlignmentOptions.Center, Color.white);

        var token = root.AddComponent<FragmentToken>();
        Wire(token, "_background", bg);
        Wire(token, "_nameLabel",  nameTmp);
        Wire(token, "_countLabel", countTmp);
        Wire(token, "_countBadge", badge);

        var prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
        Object.DestroyImmediate(root);
        Debug.Log("[HubSetup] Created FragmentToken prefab.");
        return prefab;
    }

    static GameObject CreateCardSlotPrefab()
    {
        const string path = "Assets/Prefabs/CardSlot.prefab";
        var existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (existing != null) return existing;

        var root = new GameObject("CardSlot");
        root.AddComponent<RectTransform>().sizeDelta = new Vector2(218, 78);
        var rootBg = root.AddComponent<UImg>();
        rootBg.color = new Color(0.18f, 0.18f, 0.18f);
        root.AddComponent<CanvasGroup>();

        // Left color strip
        var bar = Child(root.transform, "ColorBar");
        var barRt = bar.GetComponent<RectTransform>();
        barRt.anchorMin = Vector2.zero; barRt.anchorMax = new Vector2(0f, 1f);
        barRt.offsetMin = Vector2.zero; barRt.offsetMax = new Vector2(5f, 0f);
        var barImg = bar.AddComponent<UImg>();
        barImg.color = Color.gray;

        // Selected highlight
        var sel = Child(root.transform, "SelectedIndicator");
        FullRect(sel.GetComponent<RectTransform>());
        sel.AddComponent<UImg>().color = new Color(0.3f, 0.6f, 1f, 0.15f);
        sel.SetActive(false);

        // Slot number (top-left)
        var numGo = Child(root.transform, "SlotNumber");
        var nrt = numGo.GetComponent<RectTransform>();
        nrt.anchorMin = new Vector2(0f, 0.58f); nrt.anchorMax = new Vector2(0.2f, 1f);
        nrt.offsetMin = new Vector2(8f, 0f);    nrt.offsetMax = Vector2.zero;
        var numTmp = Tmp(numGo, "#", 10, FontStyles.Normal,
            TextAlignmentOptions.TopLeft, new Color(0.45f, 0.45f, 0.45f));

        // Card name (top-center)
        var nameGo = Child(root.transform, "CardName");
        var nameRt = nameGo.GetComponent<RectTransform>();
        nameRt.anchorMin = new Vector2(0.2f, 0.58f); nameRt.anchorMax = new Vector2(0.82f, 1f);
        nameRt.offsetMin = nameRt.offsetMax = Vector2.zero;
        var nameTmp = Tmp(nameGo, "", 12, FontStyles.Normal,
            TextAlignmentOptions.Center, Color.white);

        // Cost (top-right)
        var costGo = Child(root.transform, "CostLabel");
        var costRt = costGo.GetComponent<RectTransform>();
        costRt.anchorMin = new Vector2(0.82f, 0.58f); costRt.anchorMax = Vector2.one;
        costRt.offsetMin = Vector2.zero; costRt.offsetMax = new Vector2(-4f, 0f);
        var costTmp = Tmp(costGo, "", 11, FontStyles.Normal,
            TextAlignmentOptions.TopRight, new Color(0.8f, 0.9f, 0.5f));

        // Effect drop zone (bottom-left)
        var (effGo, effBg, effLbl) = DropZoneGO(root.transform, "EffectZone", "Effect");
        var effRt = effGo.GetComponent<RectTransform>();
        effRt.anchorMin = new Vector2(0f, 0f);  effRt.anchorMax = new Vector2(0.5f, 0.58f);
        effRt.offsetMin = new Vector2(7f, 4f);  effRt.offsetMax = new Vector2(-2f, -2f);
        effLbl.fontSize = 10;
        var effDZ = effGo.AddComponent<FragmentDropZone>();
        WireDZ(effDZ, effBg, effLbl);

        // Modifier drop zone (bottom-right)
        var (modGo, modBg, modLbl) = DropZoneGO(root.transform, "ModifierZone", "Modifier");
        var modRt = modGo.GetComponent<RectTransform>();
        modRt.anchorMin = new Vector2(0.5f, 0f); modRt.anchorMax = new Vector2(1f, 0.58f);
        modRt.offsetMin = new Vector2(2f, 4f);   modRt.offsetMax = new Vector2(-7f, -2f);
        modLbl.fontSize = 10;
        var modDZ = modGo.AddComponent<FragmentDropZone>();
        WireDZ(modDZ, modBg, modLbl);

        // CardSlotView
        var sv = root.AddComponent<CardSlotView>();
        Wire(sv, "_effectZone",        effDZ);
        Wire(sv, "_modifierZone",      modDZ);
        Wire(sv, "_background",        rootBg);
        Wire(sv, "_colorBar",          barImg);
        Wire(sv, "_slotNumber",        numTmp);
        Wire(sv, "_cardNameLabel",     nameTmp);
        Wire(sv, "_costLabel",         costTmp);
        Wire(sv, "_selectedIndicator", sel);

        var prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
        Object.DestroyImmediate(root);
        Debug.Log("[HubSetup] Created CardSlot prefab.");
        return prefab;
    }

    // =========================================================================
    // Low-level helpers
    // =========================================================================

    static (GameObject go, UImg bg, TextMeshProUGUI lbl) DropZoneGO(
        Transform parent, string name, string labelText)
    {
        var go = Child(parent, name);
        go.AddComponent<CanvasGroup>();
        var bg = go.AddComponent<UImg>();
        bg.color = new Color(0.14f, 0.14f, 0.14f, 0.9f);
        var lbl = Tmp(FullChild(go.transform, "Label"), labelText,
            11, FontStyles.Normal, TextAlignmentOptions.Center, new Color(0.6f, 0.6f, 0.6f));
        return (go, bg, lbl);
    }

    static void WireDZ(FragmentDropZone dz, UImg bg, TextMeshProUGUI lbl)
    {
        Wire(dz, "_background", bg);
        Wire(dz, "_label",      lbl);
    }

    // Anchored child (no Image)
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

    // Anchored child with background Image (used for panels)
    static GameObject Panel(Transform parent, string name,
        float x0, float y0, float x1, float y1, Color col)
    {
        var go = Anchored(parent, name, x0, y0, x1, y1);
        go.AddComponent<UImg>().color = col;
        return go;
    }

    // Plain child with RectTransform
    static GameObject Child(Transform parent, string name)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.AddComponent<RectTransform>();
        return go;
    }

    // Child that fills parent fully
    static GameObject FullChild(Transform parent, string name)
    {
        var go = Child(parent, name);
        FullRect(go.GetComponent<RectTransform>());
        return go;
    }

    // Child with LayoutElement for use inside layout groups
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

    static void VLayout(GameObject go, int padH, int padV, float spacing)
    {
        var vl = go.AddComponent<UVert>();
        vl.padding              = new RectOffset(padH, padH, padV, padV);
        vl.spacing              = spacing;
        vl.childForceExpandWidth  = true;
        vl.childForceExpandHeight = false;
        vl.childControlHeight     = false;
    }

    // Add TMP_Text and return it
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
            Debug.LogWarning($"[HubSetup] Field '{field}' not found on {target.GetType().Name}");
            return;
        }
        prop.objectReferenceValue = value;
        so.ApplyModifiedProperties();
    }
}

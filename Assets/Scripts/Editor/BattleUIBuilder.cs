using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using TMPro;

/// <summary>
/// Builds the full Battle UI hierarchy in the active scene.
/// Run via: DeckSaver → Build Battle UI
///
/// Re-running replaces the existing BattleUI root (with a confirmation prompt).
/// All created objects are registered for undo.
/// </summary>
public static class BattleUIBuilder
{
    [MenuItem("DeckSaver/Build Battle UI")]
    public static void Build()
    {
        var existing = Object.FindFirstObjectByType<BattleUI>();
        if (existing != null)
        {
            bool replace = EditorUtility.DisplayDialog(
                "Build Battle UI",
                "A BattleUI already exists in the scene. Replace it?",
                "Replace", "Cancel");
            if (!replace) return;
            Undo.DestroyObjectImmediate(existing.gameObject);
        }

        Undo.IncrementCurrentGroup();
        Undo.SetCurrentGroupName("Build Battle UI");

        // ---- Root ----
        var root   = CreateGO("BattleUI");
        var battle = root.AddComponent<BattleUI>();

        // ---- Canvas ----
        var canvasGO = CreateChild("BattleCanvas", root.transform);
        var canvas   = canvasGO.AddComponent<Canvas>();
        canvas.renderMode    = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder  = 100;
        var scaler = canvasGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode         = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight  = 0.5f;
        canvasGO.AddComponent<GraphicRaycaster>();

        // ---- EventSystem ----
        if (Object.FindFirstObjectByType<EventSystem>() == null)
        {
            var es = CreateGO("EventSystem");
            es.AddComponent<EventSystem>();
            es.AddComponent<InputSystemUIInputModule>();
        }

        // ---- CardView prefab ----
        var cardViewPrefab = BuildCardViewPrefab();

        // ---- HandDisplay ----
        var handRT = UI("HandDisplay", canvas.transform);
        handRT.anchorMin        = new Vector2(0.5f, 0f);
        handRT.anchorMax        = new Vector2(0.5f, 0f);
        handRT.pivot            = new Vector2(0.5f, 0f);
        handRT.sizeDelta        = new Vector2(0f, 300f);
        handRT.anchoredPosition = Vector2.zero;
        var hand = handRT.gameObject.AddComponent<HandDisplay>();
        Set(hand, "_cardViewPrefab", cardViewPrefab);

        // ---- Draw pile (bottom-right) ----
        var drawRT = UI("DrawPile", canvas.transform);
        drawRT.anchorMin        = new Vector2(1f, 0f);
        drawRT.anchorMax        = new Vector2(1f, 0f);
        drawRT.pivot            = new Vector2(1f, 0f);
        drawRT.sizeDelta        = new Vector2(90f, 110f);
        drawRT.anchoredPosition = new Vector2(-14f, 14f);
        drawRT.gameObject.AddComponent<Image>().color = PileColor;
        var drawBtn = drawRT.gameObject.AddComponent<PileButton>();
        Set(drawBtn, "_type", (int)PileButton.PileType.Draw);

        var drawLabel = Label("Label", drawRT, "Draw");
        var drawCount = Counter("Count", drawRT);
        Set(drawBtn, "_countText", drawCount);

        // ---- Discard pile (bottom-left) ----
        var discardRT = UI("DiscardPile", canvas.transform);
        discardRT.anchorMin        = new Vector2(0f, 0f);
        discardRT.anchorMax        = new Vector2(0f, 0f);
        discardRT.pivot            = new Vector2(0f, 0f);
        discardRT.sizeDelta        = new Vector2(90f, 110f);
        discardRT.anchoredPosition = new Vector2(14f, 14f);
        discardRT.gameObject.AddComponent<Image>().color = PileColor;
        var discardBtn = discardRT.gameObject.AddComponent<PileButton>();
        Set(discardBtn, "_type", (int)PileButton.PileType.Discard);

        var discardLabel = Label("Label", discardRT, "Discard");
        var discardCount = Counter("Count", discardRT);
        Set(discardBtn, "_countText", discardCount);

        // ---- Pile overlay ----
        var overlayRT = UI("PileOverlay", canvas.transform);
        Stretch(overlayRT);
        overlayRT.gameObject.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.78f);
        var overlay = overlayRT.gameObject.AddComponent<PileOverlay>();

        var overlayTitle = UIText("Title", overlayRT,
            new Vector2(0.1f, 0.88f), new Vector2(0.9f, 0.98f),
            "Pile Name", 24f, bold: true, color: Color.white);

        UIText("Hint", overlayRT,
            new Vector2(0.1f, 0.83f), new Vector2(0.9f, 0.89f),
            "Click anywhere to close", 11f, bold: false,
            color: new Color(0.55f, 0.55f, 0.55f, 1f));

        var contentRT = BuildScrollView(overlayRT,
            new Vector2(0.05f, 0.06f), new Vector2(0.95f, 0.82f));

        Set(overlay, "_titleText",      overlayTitle);
        Set(overlay, "_cardContainer",  contentRT);
        Set(overlay, "_cardViewPrefab", cardViewPrefab);

        // ---- End Turn button (top-right) ----
        var endTurnRT = UI("EndTurnButton", canvas.transform);
        endTurnRT.anchorMin        = new Vector2(1f, 1f);
        endTurnRT.anchorMax        = new Vector2(1f, 1f);
        endTurnRT.pivot            = new Vector2(1f, 1f);
        endTurnRT.sizeDelta        = new Vector2(150f, 52f);
        endTurnRT.anchoredPosition = new Vector2(-16f, -16f);
        endTurnRT.gameObject.AddComponent<Image>().color = new Color(0.12f, 0.40f, 0.12f, 0.92f);
        var endTurnBtn = endTurnRT.gameObject.AddComponent<Button>();
        UIText("Label", endTurnRT, new Vector2(0f, 0f), Vector2.one,
            "End Turn", 15f, bold: true, color: Color.white);

        // ---- Resource display (top-left) ----
        var resourceRT = UI("ResourceDisplay", canvas.transform);
        resourceRT.anchorMin        = new Vector2(0f, 1f);
        resourceRT.anchorMax        = new Vector2(0f, 1f);
        resourceRT.pivot            = new Vector2(0f, 1f);
        resourceRT.sizeDelta        = new Vector2(190f, 64f);
        resourceRT.anchoredPosition = new Vector2(16f, -16f);
        resourceRT.gameObject.AddComponent<Image>().color = new Color(0.08f, 0.07f, 0.06f, 0.82f);
        var manaText = UIText("ManaText", resourceRT,
            new Vector2(0.05f, 0.52f), new Vector2(0.95f, 0.94f),
            "Mana  3 / 3", 12f, bold: false, color: new Color(0.45f, 0.65f, 1.00f, 1f));
        var staminaText = UIText("StaminaText", resourceRT,
            new Vector2(0.05f, 0.06f), new Vector2(0.95f, 0.48f),
            "Stamina  2 / 2", 12f, bold: false, color: new Color(1.00f, 0.80f, 0.30f, 1f));

        // ---- Card tooltip ----
        var tooltipRT = UI("CardTooltip", canvas.transform);
        tooltipRT.sizeDelta = new Vector2(220f, 150f);
        tooltipRT.pivot     = new Vector2(1f, 0.5f);
        tooltipRT.gameObject.AddComponent<Image>().color = new Color(0.08f, 0.07f, 0.06f, 0.95f);
        var tooltip = tooltipRT.gameObject.AddComponent<CardTooltip>();

        var ttTitle = UIText("Title", tooltipRT,
            new Vector2(0.05f, 0.78f), new Vector2(0.95f, 0.97f),
            "Card Name", 11f, bold: true, color: Color.white);

        UIImage("Divider", tooltipRT,
            new Vector2(0.05f, 0.76f), new Vector2(0.95f, 0.78f),
            new Color(0.4f, 0.4f, 0.4f, 1f));

        var ttBody = UIText("Body", tooltipRT,
            new Vector2(0.05f, 0.05f), new Vector2(0.95f, 0.75f),
            "Full description.", 9f, bold: false,
            color: new Color(0.85f, 0.85f, 0.85f, 1f));
        ttBody.textWrappingMode = TextWrappingModes.Normal;

        Set(tooltip, "_canvas",     canvas);
        Set(tooltip, "_titleText",  ttTitle);
        Set(tooltip, "_bodyText",   ttBody);
        // Leave tooltip ACTIVE so Awake() sets CardTooltip.Instance at scene start.
        // CardTooltip.Start() hides it before any input can fire.

        // ---- Render order: overlay and tooltip on top ----
        overlayRT.SetAsLastSibling();
        tooltipRT.SetAsLastSibling();

        // ---- Wire BattleUI references ----
        Set(battle, "_handDisplay",   hand);
        Set(battle, "_drawPile",      drawBtn);
        Set(battle, "_discardPile",   discardBtn);
        Set(battle, "_pileOverlay",   overlay);
        Set(battle, "_endTurnButton", endTurnBtn);
        Set(battle, "_manaText",      manaText);
        Set(battle, "_staminaText",   staminaText);

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        Selection.activeGameObject = root;
        Debug.Log("[DeckSaver] Battle UI built. Select the BattleUI object to inspect references.");
    }

    // =========================================================================
    // CardView prefab
    // =========================================================================

    [MenuItem("DeckSaver/Add Turn Manager")]
    public static void AddTurnManager()
    {
        if (Object.FindFirstObjectByType<TurnManager>() != null)
        {
            EditorUtility.DisplayDialog("Add Turn Manager",
                "A TurnManager already exists in the scene.", "OK");
            return;
        }

        var battleUI = Object.FindFirstObjectByType<BattleUI>();
        var parent   = battleUI != null ? battleUI.transform : null;

        var go = new GameObject("TurnManager");
        Undo.RegisterCreatedObjectUndo(go, "Add TurnManager");
        if (parent != null) go.transform.SetParent(parent, false);
        go.AddComponent<TurnManager>();

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        Selection.activeGameObject = go;
        Debug.Log("[DeckSaver] TurnManager added to scene.");
    }

    [MenuItem("DeckSaver/Add Card Play Manager")]
    public static void AddCardPlayManager()
    {
        if (Object.FindFirstObjectByType<CardPlayManager>() != null)
        {
            EditorUtility.DisplayDialog("Add Card Play Manager",
                "A CardPlayManager already exists in the scene.", "OK");
            return;
        }

        var battleUI = Object.FindFirstObjectByType<BattleUI>();
        var parent   = battleUI != null ? battleUI.transform : null;

        var go = new GameObject("CardPlayManager");
        Undo.RegisterCreatedObjectUndo(go, "Add CardPlayManager");
        if (parent != null) go.transform.SetParent(parent, false);
        go.AddComponent<CardPlayManager>();

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        Selection.activeGameObject = go;
        Debug.Log("[DeckSaver] CardPlayManager added to scene.");
    }

    [MenuItem("DeckSaver/Rebuild CardView Prefab")]
    public static void RebuildCardViewPrefab()
    {
        BuildCardViewPrefab();

        // Update any HandDisplay and PileOverlay in the scene to use the new prefab
        var prefabAsset = UnityEditor.AssetDatabase.LoadAssetAtPath<CardView>("Assets/Prefabs/CardView.prefab");
        if (prefabAsset == null) return;

        foreach (var hd in Object.FindFirstObjectByType<HandDisplay>() != null
            ? new[] { Object.FindFirstObjectByType<HandDisplay>() }
            : System.Array.Empty<HandDisplay>())
        {
            Set(hd, "_cardViewPrefab", prefabAsset);
        }
        foreach (var po in Object.FindFirstObjectByType<PileOverlay>() != null
            ? new[] { Object.FindFirstObjectByType<PileOverlay>() }
            : System.Array.Empty<PileOverlay>())
        {
            Set(po, "_cardViewPrefab", prefabAsset);
        }

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        Debug.Log("[DeckSaver] CardView prefab rebuilt and scene references updated.");
    }

    private static CardView BuildCardViewPrefab()
    {
        const string path = "Assets/Prefabs/CardView.prefab";

        if (!AssetDatabase.IsValidFolder("Assets/Prefabs"))
            AssetDatabase.CreateFolder("Assets", "Prefabs");

        // ---- Root: transparent hitbox — stays at fan position, never moves on hover ----
        var go = new GameObject("CardView", typeof(RectTransform));
        var rt = go.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(120f, 180f);

        // Invisible Image for raycasting only (pointer events need a Graphic target)
        var hitbox = go.AddComponent<Image>();
        hitbox.color         = Color.clear;
        hitbox.raycastTarget = true;

        var cv = go.AddComponent<CardView>();

        // LayoutElement so HorizontalLayoutGroup in PileOverlay sizes it correctly
        var le = go.AddComponent<LayoutElement>();
        le.preferredWidth  = 120f;
        le.preferredHeight = 180f;

        // ---- Inner Visual child: all artwork + animation (lift & scale) lives here ----
        var visualGO = new GameObject("Visual", typeof(RectTransform));
        visualGO.transform.SetParent(go.transform, false);
        var visualRT   = visualGO.GetComponent<RectTransform>();
        visualRT.anchorMin = Vector2.zero;
        visualRT.anchorMax = Vector2.one;
        visualRT.pivot     = new Vector2(0.5f, 0.5f);
        visualRT.offsetMin = visualRT.offsetMax = Vector2.zero;

        // Background (visible card face — does NOT raycast, hitbox does)
        var bg = visualGO.AddComponent<Image>();
        bg.color         = new Color(0.95f, 0.92f, 0.85f, 1f);
        bg.raycastTarget = false;

        // Art placeholder (top 40%)
        var artRT  = ChildRT("Art", visualGO.transform, new Vector2(0.05f, 0.55f), new Vector2(0.95f, 0.95f));
        var artImg = artRT.gameObject.AddComponent<Image>();
        artImg.color         = Color.gray;
        artImg.raycastTarget = false;

        // Card name
        var nameRT  = ChildRT("Name", visualGO.transform, new Vector2(0.05f, 0.41f), new Vector2(0.95f, 0.56f));
        var nameTMP = nameRT.gameObject.AddComponent<TextMeshProUGUI>();
        nameTMP.text          = "Card Name";
        nameTMP.fontSize      = 9f;
        nameTMP.fontStyle     = FontStyles.Bold;
        nameTMP.alignment     = TextAlignmentOptions.Center;
        nameTMP.color         = Color.black;
        nameTMP.raycastTarget = false;

        // Divider
        var divRT  = ChildRT("Divider", visualGO.transform, new Vector2(0.05f, 0.395f), new Vector2(0.95f, 0.41f));
        var divImg = divRT.gameObject.AddComponent<Image>();
        divImg.color         = new Color(0.5f, 0.45f, 0.35f, 1f);
        divImg.raycastTarget = false;

        // Condensed description
        var descRT  = ChildRT("Desc", visualGO.transform, new Vector2(0.05f, 0.20f), new Vector2(0.95f, 0.40f));
        var descTMP = descRT.gameObject.AddComponent<TextMeshProUGUI>();
        descTMP.text             = "Description.";
        descTMP.fontSize         = 7f;
        descTMP.alignment        = TextAlignmentOptions.TopLeft;
        descTMP.textWrappingMode = TextWrappingModes.Normal;
        descTMP.overflowMode     = TextOverflowModes.Truncate;
        descTMP.color            = new Color(0.2f, 0.2f, 0.2f, 1f);
        descTMP.raycastTarget    = false;

        // Mana cost badge (bottom-left)
        var costRT  = ChildRT("Cost", visualGO.transform, new Vector2(0.03f, 0.02f), new Vector2(0.30f, 0.20f));
        var costBg  = costRT.gameObject.AddComponent<Image>();
        costBg.color         = new Color(0.10f, 0.20f, 0.50f, 0.85f);
        costBg.raycastTarget = false;
        var costTMP = new GameObject("Text", typeof(RectTransform)).GetComponent<RectTransform>();
        costTMP.SetParent(costRT, false);
        costTMP.anchorMin = Vector2.zero;
        costTMP.anchorMax = Vector2.one;
        costTMP.offsetMin = costTMP.offsetMax = Vector2.zero;
        var costText = costTMP.gameObject.AddComponent<TextMeshProUGUI>();
        costText.text          = "1";
        costText.fontSize      = 11f;
        costText.fontStyle     = FontStyles.Bold;
        costText.alignment     = TextAlignmentOptions.Center;
        costText.color         = Color.white;
        costText.raycastTarget = false;

        // Wire CardView serialized refs
        Set(cv, "_visual",         visualRT);
        Set(cv, "_background",     bg);
        Set(cv, "_artPlaceholder", artImg);
        Set(cv, "_nameText",       nameTMP);
        Set(cv, "_descText",       descTMP);
        Set(cv, "_costText",       costText);

        // Save as prefab asset
        var prefab = PrefabUtility.SaveAsPrefabAsset(go, path);
        Object.DestroyImmediate(go);

        AssetDatabase.Refresh();
        Debug.Log($"[DeckSaver] CardView prefab saved to {path}");
        return prefab.GetComponent<CardView>();
    }

    // =========================================================================
    // Helpers
    // =========================================================================

    private static readonly Color PileColor = new(0.12f, 0.10f, 0.08f, 0.90f);

    private static GameObject CreateGO(string name)
    {
        var go = new GameObject(name);
        Undo.RegisterCreatedObjectUndo(go, "Build Battle UI");
        return go;
    }

    private static GameObject CreateChild(string name, Transform parent)
    {
        var go = CreateGO(name);
        go.transform.SetParent(parent, false);
        return go;
    }

    /// <summary>Create a RectTransform child, zero offsets.</summary>
    private static RectTransform UI(string name, Transform parent)
    {
        var go = CreateChild(name, parent);
        return go.AddComponent<RectTransform>();
    }

    private static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
    }

    private static RectTransform ChildRT(string name, Transform parent, Vector2 min, Vector2 max)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = min;
        rt.anchorMax = max;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
        return rt;
    }

    private static TMP_Text Label(string name, RectTransform parent, string text)
    {
        var rt  = UI(name, parent);
        rt.anchorMin = new Vector2(0f, 0.62f);
        rt.anchorMax = new Vector2(1f, 0.96f);
        rt.offsetMin = rt.offsetMax = Vector2.zero;
        var tmp = rt.gameObject.AddComponent<TextMeshProUGUI>();
        tmp.text      = text;
        tmp.fontSize  = 9f;
        tmp.color     = new Color(0.65f, 0.65f, 0.65f, 1f);
        tmp.alignment = TextAlignmentOptions.Top;
        return tmp;
    }

    private static TMP_Text Counter(string name, RectTransform parent)
    {
        var rt  = UI(name, parent);
        rt.anchorMin = new Vector2(0f, 0.08f);
        rt.anchorMax = new Vector2(1f, 0.65f);
        rt.offsetMin = rt.offsetMax = Vector2.zero;
        var tmp = rt.gameObject.AddComponent<TextMeshProUGUI>();
        tmp.text      = "0";
        tmp.fontSize  = 28f;
        tmp.fontStyle = FontStyles.Bold;
        tmp.color     = Color.white;
        tmp.alignment = TextAlignmentOptions.Center;
        return tmp;
    }

    private static TMP_Text UIText(string name, RectTransform parent,
        Vector2 min, Vector2 max, string text, float fontSize, bool bold, Color color)
    {
        var rt  = UI(name, parent);
        rt.anchorMin = min;
        rt.anchorMax = max;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
        var tmp = rt.gameObject.AddComponent<TextMeshProUGUI>();
        tmp.text      = text;
        tmp.fontSize  = fontSize;
        tmp.color     = color;
        if (bold) tmp.fontStyle = FontStyles.Bold;
        return tmp;
    }

    private static Image UIImage(string name, RectTransform parent,
        Vector2 min, Vector2 max, Color color)
    {
        var rt = UI(name, parent);
        rt.anchorMin = min;
        rt.anchorMax = max;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
        var img = rt.gameObject.AddComponent<Image>();
        img.color = color;
        return img;
    }

    private static RectTransform BuildScrollView(RectTransform parent, Vector2 min, Vector2 max)
    {
        var scrollRT = UI("Scroll", parent);
        scrollRT.anchorMin = min;
        scrollRT.anchorMax = max;
        scrollRT.offsetMin = scrollRT.offsetMax = Vector2.zero;
        var scroll = scrollRT.gameObject.AddComponent<ScrollRect>();
        scroll.horizontal = true;
        scroll.vertical   = false;

        var vpRT = UI("Viewport", scrollRT);
        Stretch(vpRT);
        vpRT.gameObject.AddComponent<Image>().color = Color.clear;
        vpRT.gameObject.AddComponent<Mask>().showMaskGraphic = false;
        scroll.viewport = vpRT;

        var contentRT = UI("Content", vpRT);
        contentRT.anchorMin = new Vector2(0f, 0f);
        contentRT.anchorMax = new Vector2(0f, 1f);
        contentRT.pivot     = new Vector2(0f, 0.5f);
        contentRT.offsetMin = contentRT.offsetMax = Vector2.zero;
        scroll.content = contentRT;

        var layout = contentRT.gameObject.AddComponent<HorizontalLayoutGroup>();
        layout.childAlignment      = TextAnchor.MiddleLeft;
        layout.spacing             = 16f;
        layout.padding             = new RectOffset(16, 16, 0, 0);
        layout.childForceExpandWidth  = false;
        layout.childForceExpandHeight = false;
        contentRT.gameObject.AddComponent<ContentSizeFitter>().horizontalFit =
            ContentSizeFitter.FitMode.PreferredSize;

        return contentRT;
    }

    /// <summary>Set a serialized field by name via SerializedObject.</summary>
    private static void Set(Object target, string fieldName, Object value)
    {
        var so = new SerializedObject(target);
        so.FindProperty(fieldName).objectReferenceValue = value;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    /// <summary>Set a serialized enum/int field by name.</summary>
    private static void Set(Object target, string fieldName, int value)
    {
        var so = new SerializedObject(target);
        so.FindProperty(fieldName).enumValueIndex = value;
        so.ApplyModifiedPropertiesWithoutUndo();
    }
}

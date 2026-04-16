using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using TMPro;

/// <summary>
/// Builds the full Run scene hierarchy.
/// Run via: DeckSaver → Build Run Scene
///
/// Must be run while the Run scene is open and active.
/// Re-running replaces the existing RunSceneController root (with a confirmation prompt).
/// All created objects are registered for undo.
///
/// What gets built:
///   Camera, EventSystem
///   RunScene root (RunSceneController)
///   Canvas
///     HUD strip          — gold display
///     MapView            — full-screen FTL-style map
///     NodeRewardPanel    — simultaneous reward list after a battle
///     BoonRewardPanel    — pick a boon (opened from NodeRewardPanel)
///     CampPanel          — heal, add unit, upgrade fragment
///     ShopPanel          — buy fragments and boons
///     FragmentUpgradePanel — opened from Camp; re-used from old design
///     FragmentSwapPanel  — pick fragment → pick card → preview → confirm
///     RunOverPanel       — win / loss end screen
///
/// Prefabs created (in Assets/Prefabs/):
///   FragmentSwapCardSlot   — used inside FragmentSwapPanel card list
///   BoonOfferView          — used inside BoonRewardPanel slot list
///   ShopSlot               — used inside ShopPanel category columns
///   MapNode                — spawned by MapView for each node
///   MapEdge                — spawned by MapView for each edge
/// </summary>
public static class RunSceneBuilder
{
    [MenuItem("DeckSaver/Build Run Scene")]
    public static void Build()
    {
        var existing = Object.FindFirstObjectByType<RunSceneController>();
        if (existing != null)
        {
            bool replace = EditorUtility.DisplayDialog(
                "Build Run Scene",
                "A RunSceneController already exists in this scene. Replace it?",
                "Replace", "Cancel");
            if (!replace) return;
            Undo.DestroyObjectImmediate(existing.gameObject);
        }

        Undo.IncrementCurrentGroup();
        Undo.SetCurrentGroupName("Build Run Scene");

        // ── Camera ────────────────────────────────────────────────────────────

        if (Object.FindFirstObjectByType<Camera>() == null)
        {
            var camGO = CreateGO("Main Camera");
            camGO.tag = "MainCamera";
            var cam = camGO.AddComponent<Camera>();
            cam.clearFlags      = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.08f, 0.07f, 0.10f, 1f);
            cam.orthographic    = true;
            cam.orthographicSize = 5f;
        }

        // ── EventSystem ───────────────────────────────────────────────────────

        if (Object.FindFirstObjectByType<EventSystem>() == null)
        {
            var es = CreateGO("EventSystem");
            es.AddComponent<EventSystem>();
            es.AddComponent<InputSystemUIInputModule>();
        }

        // ── Root ──────────────────────────────────────────────────────────────

        var root       = CreateGO("RunScene");
        var controller = root.AddComponent<RunSceneController>();

        // ── Canvas ────────────────────────────────────────────────────────────

        var canvasGO = CreateChild("Canvas", root.transform);
        var canvas   = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = canvasGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode         = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight  = 0.5f;
        canvasGO.AddComponent<GraphicRaycaster>();

        // ── Prefabs ───────────────────────────────────────────────────────────

        var cardSlotPrefab     = LoadOrBuildCardSlotPrefab();
        var boonOfferPrefab    = LoadOrBuildBoonOfferPrefab();
        var shopSlotPrefab     = LoadOrBuildShopSlotPrefab();
        var mapNodePrefab      = LoadOrBuildMapNodePrefab();
        var mapEdgePrefab      = LoadOrBuildMapEdgePrefab();

        // ── MapView ───────────────────────────────────────────────────────────

        var mapViewRT = Panel("MapView", canvas.transform,
            Vector2.zero, Vector2.one,
            new Color(0.06f, 0.05f, 0.08f, 1f));
        var mapView = mapViewRT.gameObject.AddComponent<MapView>();

        // MapArea — inner rect where nodes and edges are drawn
        var mapAreaRT = UI("MapArea", mapViewRT);
        mapAreaRT.anchorMin = new Vector2(0.01f, 0.01f);
        mapAreaRT.anchorMax = new Vector2(0.99f, 0.90f); // leaves room for HUD at top
        mapAreaRT.offsetMin = mapAreaRT.offsetMax = Vector2.zero;

        SetRef(mapView, "_mapArea",        mapAreaRT);
        SetRef(mapView, "_nodeViewPrefab", mapNodePrefab);
        SetRef(mapView, "_edgeViewPrefab", mapEdgePrefab);

        mapViewRT.gameObject.SetActive(false);

        // ── BoonRewardPanel ───────────────────────────────────────────────────

        var boonRewardRT = Panel("BoonRewardPanel", canvas.transform,
            new Vector2(0.15f, 0.10f), new Vector2(0.85f, 0.95f),
            new Color(0.06f, 0.05f, 0.10f, 0.97f));
        var boonRewardPanel = boonRewardRT.gameObject.AddComponent<BoonRewardPanel>();

        var boonHeaderTMP = UIText("Header", boonRewardRT,
            new Vector2(0.05f, 0.88f), new Vector2(0.95f, 0.97f),
            "Choose a Boon", 22f, bold: true, color: Color.white);

        // Slot parent: vertical list for spawned BoonOfferView instances
        var boonSlotParentRT = UI("SlotParent", boonRewardRT);
        boonSlotParentRT.anchorMin = new Vector2(0.05f, 0.12f);
        boonSlotParentRT.anchorMax = new Vector2(0.95f, 0.87f);
        boonSlotParentRT.offsetMin = boonSlotParentRT.offsetMax = Vector2.zero;
        var boonVLayout = boonSlotParentRT.gameObject.AddComponent<VerticalLayoutGroup>();
        boonVLayout.spacing                = 12f;
        boonVLayout.childForceExpandWidth  = true;
        boonVLayout.childForceExpandHeight = true;
        boonVLayout.childAlignment         = TextAnchor.UpperCenter;

        // Skip button
        var boonSkipRT = UI("SkipButton", boonRewardRT);
        boonSkipRT.anchorMin = new Vector2(0.35f, 0.02f);
        boonSkipRT.anchorMax = new Vector2(0.65f, 0.10f);
        boonSkipRT.offsetMin = boonSkipRT.offsetMax = Vector2.zero;
        boonSkipRT.gameObject.AddComponent<Image>().color = new Color(0.25f, 0.25f, 0.30f, 1f);
        var boonSkipBtn = boonSkipRT.gameObject.AddComponent<Button>();
        UIText("Label", boonSkipRT, Vector2.zero, Vector2.one,
            "Skip", 13f, bold: false, color: Color.white);

        SetRef(boonRewardPanel, "_headerText",    boonHeaderTMP);
        SetRef(boonRewardPanel, "_boonSlotPrefab", boonOfferPrefab);
        SetRef(boonRewardPanel, "_slotParent",    boonSlotParentRT);
        SetRef(boonRewardPanel, "_skipButton",    boonSkipBtn);

        boonRewardRT.gameObject.SetActive(false);

        // ── NodeRewardPanel ───────────────────────────────────────────────────

        var nodeRewardRT = Panel("NodeRewardPanel", canvas.transform,
            new Vector2(0.20f, 0.15f), new Vector2(0.80f, 0.90f),
            new Color(0.05f, 0.05f, 0.10f, 0.97f));
        var nodeRewardPanel = nodeRewardRT.gameObject.AddComponent<NodeRewardPanel>();

        var nodeRewardHeaderTMP = UIText("Header", nodeRewardRT,
            new Vector2(0.05f, 0.88f), new Vector2(0.95f, 0.97f),
            "Battle Complete", 22f, bold: true, color: Color.white);

        var nodeGoldTMP = UIText("GoldText", nodeRewardRT,
            new Vector2(0.05f, 0.80f), new Vector2(0.95f, 0.89f),
            "+0 Gold", 16f, bold: false, color: new Color(1f, 0.85f, 0.3f, 1f));

        // Fragment Swap row
        var swapRowGO = CreateChild("SwapRow", nodeRewardRT);
        var swapRowRT = swapRowGO.AddComponent<RectTransform>();
        swapRowRT.anchorMin = new Vector2(0.04f, 0.62f);
        swapRowRT.anchorMax = new Vector2(0.96f, 0.78f);
        swapRowRT.offsetMin = swapRowRT.offsetMax = Vector2.zero;
        swapRowGO.AddComponent<Image>().color = new Color(0.10f, 0.10f, 0.16f, 1f);

        UIText("RowLabel", swapRowRT,
            new Vector2(0.03f, 0.15f), new Vector2(0.35f, 0.85f),
            "Fragment Swap", 13f, bold: true, color: Color.white);

        var swapStatusTMP = UIText("StatusText", swapRowRT,
            new Vector2(0.37f, 0.15f), new Vector2(0.62f, 0.85f),
            "Available", 12f, bold: false, color: new Color(0.7f, 1f, 0.7f, 1f));

        var swapClaimRT = UI("ClaimButton", swapRowRT);
        swapClaimRT.anchorMin = new Vector2(0.65f, 0.10f);
        swapClaimRT.anchorMax = new Vector2(0.97f, 0.90f);
        swapClaimRT.offsetMin = swapClaimRT.offsetMax = Vector2.zero;
        swapClaimRT.gameObject.AddComponent<Image>().color = new Color(0.15f, 0.40f, 0.15f, 1f);
        var swapClaimBtn = swapClaimRT.gameObject.AddComponent<Button>();
        UIText("Label", swapClaimRT, Vector2.zero, Vector2.one,
            "Claim", 13f, bold: true, color: Color.white);

        // Boon row
        var boonRowGO = CreateChild("BoonRow", nodeRewardRT);
        var boonRowRT = boonRowGO.AddComponent<RectTransform>();
        boonRowRT.anchorMin = new Vector2(0.04f, 0.42f);
        boonRowRT.anchorMax = new Vector2(0.96f, 0.58f);
        boonRowRT.offsetMin = boonRowRT.offsetMax = Vector2.zero;
        boonRowGO.AddComponent<Image>().color = new Color(0.10f, 0.10f, 0.16f, 1f);

        UIText("RowLabel", boonRowRT,
            new Vector2(0.03f, 0.15f), new Vector2(0.35f, 0.85f),
            "Boon", 13f, bold: true, color: Color.white);

        var boonStatusTMP = UIText("StatusText", boonRowRT,
            new Vector2(0.37f, 0.15f), new Vector2(0.62f, 0.85f),
            "Available", 12f, bold: false, color: new Color(0.7f, 1f, 0.7f, 1f));

        var boonClaimRT = UI("ClaimButton", boonRowRT);
        boonClaimRT.anchorMin = new Vector2(0.65f, 0.10f);
        boonClaimRT.anchorMax = new Vector2(0.97f, 0.90f);
        boonClaimRT.offsetMin = boonClaimRT.offsetMax = Vector2.zero;
        boonClaimRT.gameObject.AddComponent<Image>().color = new Color(0.15f, 0.40f, 0.15f, 1f);
        var boonClaimBtn = boonClaimRT.gameObject.AddComponent<Button>();
        UIText("Label", boonClaimRT, Vector2.zero, Vector2.one,
            "Claim", 13f, bold: true, color: Color.white);

        // Continue button
        var nodeRewardContinueRT = UI("ContinueButton", nodeRewardRT);
        nodeRewardContinueRT.anchorMin = new Vector2(0.30f, 0.04f);
        nodeRewardContinueRT.anchorMax = new Vector2(0.70f, 0.14f);
        nodeRewardContinueRT.offsetMin = nodeRewardContinueRT.offsetMax = Vector2.zero;
        nodeRewardContinueRT.gameObject.AddComponent<Image>().color = new Color(0.15f, 0.15f, 0.20f, 1f);
        var nodeRewardContinueBtn = nodeRewardContinueRT.gameObject.AddComponent<Button>();
        UIText("Label", nodeRewardContinueRT, Vector2.zero, Vector2.one,
            "Continue", 14f, bold: true, color: Color.white);

        SetRef(nodeRewardPanel, "_headerText",        nodeRewardHeaderTMP);
        SetRef(nodeRewardPanel, "_goldText",          nodeGoldTMP);
        SetRef(nodeRewardPanel, "_swapRow",           swapRowGO);
        SetRef(nodeRewardPanel, "_swapStatusText",    swapStatusTMP);
        SetRef(nodeRewardPanel, "_swapClaimButton",   swapClaimBtn);
        SetRef(nodeRewardPanel, "_boonRow",           boonRowGO);
        SetRef(nodeRewardPanel, "_boonStatusText",    boonStatusTMP);
        SetRef(nodeRewardPanel, "_boonClaimButton",   boonClaimBtn);
        SetRef(nodeRewardPanel, "_continueButton",    nodeRewardContinueBtn);

        nodeRewardRT.gameObject.SetActive(false);

        // ── CampPanel ─────────────────────────────────────────────────────────

        var campRT = Panel("CampPanel", canvas.transform,
            new Vector2(0.20f, 0.10f), new Vector2(0.80f, 0.95f),
            new Color(0.06f, 0.08f, 0.06f, 0.97f));
        var campPanel = campRT.gameObject.AddComponent<CampPanel>();

        UIText("Title", campRT,
            new Vector2(0.05f, 0.87f), new Vector2(0.95f, 0.97f),
            "Camp", 24f, bold: true, color: Color.white);

        var campMoneyTMP = UIText("MoneyText", campRT,
            new Vector2(0.05f, 0.80f), new Vector2(0.95f, 0.88f),
            "Gold: 0", 14f, bold: false, color: new Color(1f, 0.85f, 0.3f, 1f));

        // Option rows
        var healRow    = BuildCampOptionRow("HealRow",    campRT, new Vector2(0.04f, 0.58f), new Vector2(0.96f, 0.75f));
        var addRow     = BuildCampOptionRow("AddUnitRow", campRT, new Vector2(0.04f, 0.38f), new Vector2(0.96f, 0.55f));
        var upgradeRow = BuildCampOptionRow("UpgradeRow", campRT, new Vector2(0.04f, 0.18f), new Vector2(0.96f, 0.35f));

        // Leave button
        var campLeaveRT = UI("LeaveButton", campRT);
        campLeaveRT.anchorMin = new Vector2(0.30f, 0.02f);
        campLeaveRT.anchorMax = new Vector2(0.70f, 0.12f);
        campLeaveRT.offsetMin = campLeaveRT.offsetMax = Vector2.zero;
        campLeaveRT.gameObject.AddComponent<Image>().color = new Color(0.15f, 0.15f, 0.20f, 1f);
        var campLeaveBtn = campLeaveRT.gameObject.AddComponent<Button>();
        UIText("Label", campLeaveRT, Vector2.zero, Vector2.one,
            "Leave Camp", 14f, bold: true, color: Color.white);

        SetRef(campPanel, "_healButton",    healRow.btn);
        SetRef(campPanel, "_healLabel",     healRow.label);
        SetRef(campPanel, "_addUnitButton", addRow.btn);
        SetRef(campPanel, "_addUnitLabel",  addRow.label);
        SetRef(campPanel, "_upgradeButton", upgradeRow.btn);
        SetRef(campPanel, "_upgradeLabel",  upgradeRow.label);
        SetRef(campPanel, "_moneyText",     campMoneyTMP);
        SetRef(campPanel, "_leaveButton",   campLeaveBtn);

        campRT.gameObject.SetActive(false);

        // ── ShopPanel ─────────────────────────────────────────────────────────

        var shopRT = Panel("ShopPanel", canvas.transform,
            new Vector2(0.05f, 0.05f), new Vector2(0.95f, 0.95f),
            new Color(0.06f, 0.05f, 0.10f, 0.97f));
        var shopPanel = shopRT.gameObject.AddComponent<ShopPanel>();

        UIText("Title", shopRT,
            new Vector2(0.05f, 0.90f), new Vector2(0.95f, 0.97f),
            "Shop", 24f, bold: true, color: Color.white);

        var shopMoneyTMP = UIText("MoneyText", shopRT,
            new Vector2(0.05f, 0.84f), new Vector2(0.40f, 0.91f),
            "Gold: 0", 14f, bold: false, color: new Color(1f, 0.85f, 0.3f, 1f));

        // Three category columns
        var (effectHeader, effectParent)   = BuildShopColumn("EffectColumn",   shopRT, 0.02f, 0.34f);
        var (modifierHeader, modifierParent) = BuildShopColumn("ModifierColumn", shopRT, 0.35f, 0.67f);
        var (boonHeader, boonParent)       = BuildShopColumn("BoonColumn",     shopRT, 0.68f, 0.99f);

        // Leave button
        var shopLeaveRT = UI("LeaveButton", shopRT);
        shopLeaveRT.anchorMin = new Vector2(0.35f, 0.01f);
        shopLeaveRT.anchorMax = new Vector2(0.65f, 0.07f);
        shopLeaveRT.offsetMin = shopLeaveRT.offsetMax = Vector2.zero;
        shopLeaveRT.gameObject.AddComponent<Image>().color = new Color(0.15f, 0.15f, 0.20f, 1f);
        var shopLeaveBtn = shopLeaveRT.gameObject.AddComponent<Button>();
        UIText("Label", shopLeaveRT, Vector2.zero, Vector2.one,
            "Leave Shop", 14f, bold: true, color: Color.white);

        SetRef(shopPanel, "_shopSlotPrefab",  shopSlotPrefab);
        SetRef(shopPanel, "_effectParent",    effectParent);
        SetRef(shopPanel, "_modifierParent",  modifierParent);
        SetRef(shopPanel, "_boonParent",      boonParent);
        SetRef(shopPanel, "_effectHeader",    effectHeader);
        SetRef(shopPanel, "_modifierHeader",  modifierHeader);
        SetRef(shopPanel, "_boonHeader",      boonHeader);
        SetRef(shopPanel, "_moneyText",       shopMoneyTMP);
        SetRef(shopPanel, "_leaveButton",     shopLeaveBtn);

        shopRT.gameObject.SetActive(false);

        // ── Fragment Upgrade Panel ────────────────────────────────────────────

        var upgradePanelRT = Panel("FragmentUpgradePanel", canvas.transform,
            new Vector2(0.10f, 0.05f), new Vector2(0.90f, 0.95f),
            new Color(0.05f, 0.05f, 0.10f, 0.97f));
        var fragmentUpgradePanel = upgradePanelRT.gameObject.AddComponent<FragmentUpgradePanel>();

        // Step 1: card list
        var upgradeCardListRoot = CreateChild("CardListRoot", upgradePanelRT);
        Stretch(upgradeCardListRoot.AddComponent<RectTransform>());

        UIText("Header", upgradeCardListRoot.GetComponent<RectTransform>(),
            new Vector2(0.05f, 0.88f), new Vector2(0.95f, 0.97f),
            "Choose a Card to Upgrade", 20f, bold: true, color: Color.white);

        var upgradeScrollContent = BuildScrollView(
            upgradeCardListRoot.GetComponent<RectTransform>(),
            new Vector2(0.02f, 0.10f), new Vector2(0.98f, 0.84f));

        // Step 2: fragment detail
        var upgradeFragDetailRoot = CreateChild("FragmentDetailRoot", upgradePanelRT);
        Stretch(upgradeFragDetailRoot.AddComponent<RectTransform>());

        var upgradeCardNameTMP = UIText("CardName", upgradeFragDetailRoot.GetComponent<RectTransform>(),
            new Vector2(0.05f, 0.82f), new Vector2(0.95f, 0.95f),
            "Card Name", 22f, bold: true, color: Color.white);

        var effectBtnRT = Panel("EffectButton", upgradeFragDetailRoot.GetComponent<RectTransform>(),
            new Vector2(0.04f, 0.20f), new Vector2(0.48f, 0.78f),
            new Color(0.10f, 0.14f, 0.20f, 1f));
        var effectUpgradeBtn = effectBtnRT.gameObject.AddComponent<Button>();
        UIText("TypeTag", effectBtnRT, new Vector2(0.05f, 0.68f), new Vector2(0.95f, 0.85f),
            "EFFECT", 10f, bold: true, color: new Color(0.6f, 0.85f, 1f, 1f));
        var effectUpgradeLabel = UIText("Label", effectBtnRT,
            new Vector2(0.05f, 0.40f), new Vector2(0.95f, 0.65f),
            "Effect Fragment", 14f, bold: false, color: Color.white);

        var modifierBtnRT = Panel("ModifierButton", upgradeFragDetailRoot.GetComponent<RectTransform>(),
            new Vector2(0.52f, 0.20f), new Vector2(0.96f, 0.78f),
            new Color(0.14f, 0.10f, 0.20f, 1f));
        var modifierUpgradeBtn = modifierBtnRT.gameObject.AddComponent<Button>();
        UIText("TypeTag", modifierBtnRT, new Vector2(0.05f, 0.68f), new Vector2(0.95f, 0.85f),
            "MODIFIER", 10f, bold: true, color: new Color(1f, 0.85f, 0.6f, 1f));
        var modifierUpgradeLabel = UIText("Label", modifierBtnRT,
            new Vector2(0.05f, 0.40f), new Vector2(0.95f, 0.65f),
            "Modifier Fragment", 14f, bold: false, color: Color.white);

        var upgBackBtnRT = UI("BackButton", upgradePanelRT);
        upgBackBtnRT.anchorMin = new Vector2(0.02f, 0.01f);
        upgBackBtnRT.anchorMax = new Vector2(0.25f, 0.07f);
        upgBackBtnRT.offsetMin = upgBackBtnRT.offsetMax = Vector2.zero;
        upgBackBtnRT.gameObject.AddComponent<Image>().color = new Color(0.20f, 0.20f, 0.25f, 1f);
        var upgBackBtn = upgBackBtnRT.gameObject.AddComponent<Button>();
        UIText("Label", upgBackBtnRT, Vector2.zero, Vector2.one,
            "Back", 13f, bold: false, color: Color.white);

        var upgCancelBtnRT = UI("CancelButton", upgradePanelRT);
        upgCancelBtnRT.anchorMin = new Vector2(0.75f, 0.01f);
        upgCancelBtnRT.anchorMax = new Vector2(0.98f, 0.07f);
        upgCancelBtnRT.offsetMin = upgCancelBtnRT.offsetMax = Vector2.zero;
        upgCancelBtnRT.gameObject.AddComponent<Image>().color = new Color(0.35f, 0.10f, 0.10f, 1f);
        var upgCancelBtn = upgCancelBtnRT.gameObject.AddComponent<Button>();
        UIText("Label", upgCancelBtnRT, Vector2.zero, Vector2.one,
            "Cancel", 13f, bold: false, color: Color.white);

        SetRef(fragmentUpgradePanel, "_cardListRoot",       upgradeCardListRoot);
        SetRef(fragmentUpgradePanel, "_cardListParent",     upgradeScrollContent);
        SetRef(fragmentUpgradePanel, "_cardSlotPrefab",     cardSlotPrefab);
        SetRef(fragmentUpgradePanel, "_fragmentDetailRoot", upgradeFragDetailRoot);
        SetRef(fragmentUpgradePanel, "_cardNameText",       upgradeCardNameTMP);
        SetRef(fragmentUpgradePanel, "_effectButton",       effectUpgradeBtn);
        SetRef(fragmentUpgradePanel, "_effectLabel",        effectUpgradeLabel);
        SetRef(fragmentUpgradePanel, "_modifierButton",     modifierUpgradeBtn);
        SetRef(fragmentUpgradePanel, "_modifierLabel",      modifierUpgradeLabel);
        SetRef(fragmentUpgradePanel, "_backButton",         upgBackBtn);
        SetRef(fragmentUpgradePanel, "_cancelButton",       upgCancelBtn);

        upgradePanelRT.gameObject.SetActive(false);

        // Wire camp → upgrade panel
        SetRef(campPanel, "_fragmentUpgradePanel", fragmentUpgradePanel);

        // ── Fragment Swap Panel ───────────────────────────────────────────────

        var swapPanelRT = Panel("FragmentSwapPanel", canvas.transform,
            new Vector2(0.10f, 0.05f), new Vector2(0.90f, 0.95f),
            new Color(0.05f, 0.05f, 0.10f, 0.97f));
        var fragmentSwapPanel = swapPanelRT.gameObject.AddComponent<FragmentSwapPanel>();

        // Step 1: three side-by-side fragment offer slots
        var fragChoiceRoot = CreateChild("FragmentChoiceRoot", swapPanelRT);
        Stretch(fragChoiceRoot.AddComponent<RectTransform>());

        UIText("Header", fragChoiceRoot.GetComponent<RectTransform>(),
            new Vector2(0.05f, 0.88f), new Vector2(0.95f, 0.97f),
            "Choose a Fragment", 20f, bold: true, color: Color.white);

        var offerPositions = new[]
        {
            (new Vector2(0.04f, 0.20f), new Vector2(0.33f, 0.85f)),
            (new Vector2(0.36f, 0.20f), new Vector2(0.65f, 0.85f)),
            (new Vector2(0.68f, 0.20f), new Vector2(0.97f, 0.85f)),
        };

        var fragViews = new FragmentOfferView[3];
        for (int i = 0; i < 3; i++)
        {
            var (min, max) = offerPositions[i];
            var slotRT = Panel($"FragmentOffer{i + 1}", fragChoiceRoot.GetComponent<RectTransform>(),
                min, max, new Color(0.10f, 0.09f, 0.14f, 1f));
            var fov = slotRT.gameObject.AddComponent<FragmentOfferView>();
            fragViews[i] = fov;

            var nameTMP = UIText("Name", slotRT,
                new Vector2(0.05f, 0.72f), new Vector2(0.95f, 0.95f),
                "Fragment Name", 15f, bold: true, color: Color.white);
            var typeTMP = UIText("Type", slotRT,
                new Vector2(0.05f, 0.58f), new Vector2(0.95f, 0.72f),
                "Effect", 11f, bold: false, color: new Color(0.6f, 0.85f, 1f, 1f));
            var flavorTMP = UIText("Flavor", slotRT,
                new Vector2(0.05f, 0.20f), new Vector2(0.95f, 0.58f),
                "Flavor text.", 10f, bold: false, color: new Color(0.75f, 0.75f, 0.75f, 1f));
            if (flavorTMP is TextMeshProUGUI ftmp)
                ftmp.textWrappingMode = TextWrappingModes.Normal;

            var selBtnRT = UI("SelectButton", slotRT);
            selBtnRT.anchorMin = new Vector2(0.1f, 0.03f);
            selBtnRT.anchorMax = new Vector2(0.9f, 0.17f);
            selBtnRT.offsetMin = selBtnRT.offsetMax = Vector2.zero;
            selBtnRT.gameObject.AddComponent<Image>().color = new Color(0.20f, 0.40f, 0.20f, 1f);
            var selBtn = selBtnRT.gameObject.AddComponent<Button>();
            UIText("Label", selBtnRT, Vector2.zero, Vector2.one,
                "Choose", 12f, bold: true, color: Color.white);

            SetRef(fov, "_nameText",     nameTMP);
            SetRef(fov, "_typeText",     typeTMP);
            SetRef(fov, "_flavorText",   flavorTMP);
            SetRef(fov, "_selectButton", selBtn);
        }

        // Step 2: card list
        var cardChoiceRoot = CreateChild("CardChoiceRoot", swapPanelRT);
        Stretch(cardChoiceRoot.AddComponent<RectTransform>());

        var instructionTMP = UIText("Instruction", cardChoiceRoot.GetComponent<RectTransform>(),
            new Vector2(0.05f, 0.86f), new Vector2(0.95f, 0.97f),
            "Pick a card to replace its fragment.", 16f, bold: false, color: Color.white);

        var cardScrollContent = BuildScrollView(
            cardChoiceRoot.GetComponent<RectTransform>(),
            new Vector2(0.02f, 0.06f), new Vector2(0.98f, 0.84f));

        // Step 3: preview (before / after)
        var previewRoot = CreateChild("PreviewRoot", swapPanelRT);
        Stretch(previewRoot.AddComponent<RectTransform>());

        UIText("Header", previewRoot.GetComponent<RectTransform>(),
            new Vector2(0.05f, 0.88f), new Vector2(0.95f, 0.97f),
            "Preview Change", 20f, bold: true, color: Color.white);

        // Before column (left half)
        var beforeRT = Panel("Before", previewRoot.GetComponent<RectTransform>(),
            new Vector2(0.03f, 0.22f), new Vector2(0.48f, 0.87f),
            new Color(0.10f, 0.09f, 0.14f, 1f));

        UIText("Tag", beforeRT, new Vector2(0.05f, 0.86f), new Vector2(0.95f, 0.97f),
            "BEFORE", 10f, bold: true, color: new Color(0.6f, 0.6f, 0.6f, 1f));

        var beforeNameTMP = UIText("Name", beforeRT,
            new Vector2(0.05f, 0.72f), new Vector2(0.95f, 0.87f),
            "Card Name", 14f, bold: true, color: Color.white);

        var beforeDescTMP = UIText("Desc", beforeRT,
            new Vector2(0.05f, 0.10f), new Vector2(0.95f, 0.72f),
            "Card description.", 10f, bold: false, color: new Color(0.80f, 0.80f, 0.80f, 1f));
        if (beforeDescTMP is TextMeshProUGUI bdTmp)
            bdTmp.textWrappingMode = TextWrappingModes.Normal;

        // After column (right half)
        var afterRT = Panel("After", previewRoot.GetComponent<RectTransform>(),
            new Vector2(0.52f, 0.22f), new Vector2(0.97f, 0.87f),
            new Color(0.08f, 0.14f, 0.10f, 1f));

        UIText("Tag", afterRT, new Vector2(0.05f, 0.86f), new Vector2(0.95f, 0.97f),
            "AFTER", 10f, bold: true, color: new Color(0.5f, 0.9f, 0.5f, 1f));

        var afterNameTMP = UIText("Name", afterRT,
            new Vector2(0.05f, 0.72f), new Vector2(0.95f, 0.87f),
            "Card Name", 14f, bold: true, color: Color.white);

        var afterDescTMP = UIText("Desc", afterRT,
            new Vector2(0.05f, 0.10f), new Vector2(0.95f, 0.72f),
            "Card description.", 10f, bold: false, color: new Color(0.80f, 0.80f, 0.80f, 1f));
        if (afterDescTMP is TextMeshProUGUI adTmp)
            adTmp.textWrappingMode = TextWrappingModes.Normal;

        // Confirm and back buttons
        var previewConfirmRT = UI("ConfirmButton", previewRoot.GetComponent<RectTransform>());
        previewConfirmRT.anchorMin = new Vector2(0.55f, 0.02f);
        previewConfirmRT.anchorMax = new Vector2(0.97f, 0.12f);
        previewConfirmRT.offsetMin = previewConfirmRT.offsetMax = Vector2.zero;
        previewConfirmRT.gameObject.AddComponent<Image>().color = new Color(0.15f, 0.45f, 0.15f, 1f);
        var confirmBtn = previewConfirmRT.gameObject.AddComponent<Button>();
        UIText("Label", previewConfirmRT, Vector2.zero, Vector2.one,
            "Confirm Swap", 14f, bold: true, color: Color.white);

        var previewBackRT = UI("PreviewBackButton", previewRoot.GetComponent<RectTransform>());
        previewBackRT.anchorMin = new Vector2(0.03f, 0.02f);
        previewBackRT.anchorMax = new Vector2(0.45f, 0.12f);
        previewBackRT.offsetMin = previewBackRT.offsetMax = Vector2.zero;
        previewBackRT.gameObject.AddComponent<Image>().color = new Color(0.20f, 0.20f, 0.25f, 1f);
        var previewBackBtn = previewBackRT.gameObject.AddComponent<Button>();
        UIText("Label", previewBackRT, Vector2.zero, Vector2.one,
            "Back", 13f, bold: false, color: Color.white);

        // Shared cancel button
        var swapCancelRT = UI("CancelButton", swapPanelRT);
        swapCancelRT.anchorMin = new Vector2(0.38f, 0.01f);
        swapCancelRT.anchorMax = new Vector2(0.62f, 0.07f);
        swapCancelRT.offsetMin = swapCancelRT.offsetMax = Vector2.zero;
        swapCancelRT.gameObject.AddComponent<Image>().color = new Color(0.35f, 0.10f, 0.10f, 1f);
        var swapCancelBtn = swapCancelRT.gameObject.AddComponent<Button>();
        UIText("Label", swapCancelRT, Vector2.zero, Vector2.one,
            "Cancel", 13f, bold: false, color: Color.white);

        SetRef(fragmentSwapPanel, "_fragmentChoiceRoot", fragChoiceRoot);
        SetRef(fragmentSwapPanel, "_offerView1",         fragViews[0]);
        SetRef(fragmentSwapPanel, "_offerView2",         fragViews[1]);
        SetRef(fragmentSwapPanel, "_offerView3",         fragViews[2]);
        SetRef(fragmentSwapPanel, "_cardChoiceRoot",     cardChoiceRoot);
        SetRef(fragmentSwapPanel, "_instructionText",    instructionTMP);
        SetRef(fragmentSwapPanel, "_cardListParent",     cardScrollContent);
        SetRef(fragmentSwapPanel, "_cardSlotPrefab",     cardSlotPrefab);
        SetRef(fragmentSwapPanel, "_previewRoot",        previewRoot);
        SetRef(fragmentSwapPanel, "_beforeNameText",     beforeNameTMP);
        SetRef(fragmentSwapPanel, "_beforeDescText",     beforeDescTMP);
        SetRef(fragmentSwapPanel, "_afterNameText",      afterNameTMP);
        SetRef(fragmentSwapPanel, "_afterDescText",      afterDescTMP);
        SetRef(fragmentSwapPanel, "_confirmButton",      confirmBtn);
        SetRef(fragmentSwapPanel, "_previewBackButton",  previewBackBtn);
        SetRef(fragmentSwapPanel, "_cancelButton",       swapCancelBtn);

        swapPanelRT.gameObject.SetActive(false);

        // Wire shop → swap panel (for fragment purchases)
        SetRef(shopPanel, "_fragmentSwapPanel", fragmentSwapPanel);

        // Wire nodeRewardPanel → sub-panels
        SetRef(nodeRewardPanel, "_fragmentSwapPanel", fragmentSwapPanel);
        SetRef(nodeRewardPanel, "_boonRewardPanel",   boonRewardPanel);

        // ── Run Over Panel ────────────────────────────────────────────────────

        var runOverRT = Panel("RunOverPanel", canvas.transform,
            new Vector2(0.25f, 0.25f), new Vector2(0.75f, 0.80f),
            new Color(0.04f, 0.03f, 0.06f, 0.97f));
        var runOverPanel = runOverRT.gameObject.AddComponent<RunOverPanel>();

        var runOverHeaderTMP = UIText("Header", runOverRT,
            new Vector2(0.05f, 0.75f), new Vector2(0.95f, 0.95f),
            "Defeated", 26f, bold: true, color: Color.white);
        var runOverSummaryTMP = UIText("Summary", runOverRT,
            new Vector2(0.05f, 0.35f), new Vector2(0.95f, 0.72f),
            "Summary text.", 13f, bold: false, color: new Color(0.80f, 0.80f, 0.80f, 1f));

        var returnBtnRT = UI("ReturnButton", runOverRT);
        returnBtnRT.anchorMin = new Vector2(0.2f, 0.06f);
        returnBtnRT.anchorMax = new Vector2(0.8f, 0.20f);
        returnBtnRT.offsetMin = returnBtnRT.offsetMax = Vector2.zero;
        returnBtnRT.gameObject.AddComponent<Image>().color = new Color(0.15f, 0.15f, 0.20f, 1f);
        var returnBtn = returnBtnRT.gameObject.AddComponent<Button>();
        UIText("Label", returnBtnRT, Vector2.zero, Vector2.one,
            "Return to Hub", 14f, bold: false, color: Color.white);

        SetRef(runOverPanel, "_headerText",  runOverHeaderTMP);
        SetRef(runOverPanel, "_summaryText", runOverSummaryTMP);
        SetRef(runOverPanel, "_returnButton", returnBtn);

        runOverRT.gameObject.SetActive(false);

        // ── HUD strip (always on top — added last to render above panels) ─────

        var hudRT = UI("HUD", canvas.transform);
        hudRT.anchorMin = new Vector2(0f, 0.92f);
        hudRT.anchorMax = Vector2.one;
        hudRT.offsetMin = hudRT.offsetMax = Vector2.zero;
        hudRT.gameObject.AddComponent<Image>().color = new Color(0.04f, 0.03f, 0.06f, 0.90f);

        var moneyTMP = UIText("MoneyText", hudRT,
            new Vector2(0.02f, 0.1f), new Vector2(0.30f, 0.9f),
            "Gold: 100", 16f, bold: false, color: new Color(1f, 0.85f, 0.3f, 1f));

        // ── Wire RunSceneController ───────────────────────────────────────────

        SetRef(controller, "_mapView",           mapView);
        SetRef(controller, "_nodeRewardPanel",   nodeRewardPanel);
        SetRef(controller, "_boonRewardPanel",   boonRewardPanel);
        SetRef(controller, "_fragmentSwapPanel", fragmentSwapPanel);
        SetRef(controller, "_campPanel",         campPanel);
        SetRef(controller, "_shopPanel",         shopPanel);
        SetRef(controller, "_runOverPanel",      runOverPanel);
        SetRef(controller, "_moneyText",         moneyTMP);

        // ── Render order: modal panels always above map ───────────────────────
        upgradePanelRT.SetAsLastSibling();
        swapPanelRT.SetAsLastSibling();
        boonRewardRT.SetAsLastSibling();
        nodeRewardRT.SetAsLastSibling();
        runOverRT.SetAsLastSibling();
        hudRT.SetAsLastSibling();

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        Selection.activeGameObject = root;
        Debug.Log("[DeckSaver] Run scene built successfully.");
    }

    // ── Configure Hub ─────────────────────────────────────────────────────────

    [MenuItem("DeckSaver/Configure Hub for Run Scene")]
    public static void ConfigureHub()
    {
        var hubUI = Object.FindFirstObjectByType<HubUI>();
        if (hubUI == null)
        {
            EditorUtility.DisplayDialog("Configure Hub",
                "No HubUI found. Open the Hub scene first.", "OK");
            return;
        }

        const string configPath = "Assets/Data/DefaultRunConfig.asset";
        if (!AssetDatabase.IsValidFolder("Assets/Data"))
            AssetDatabase.CreateFolder("Assets", "Data");

        var config = AssetDatabase.LoadAssetAtPath<RunConfig>(configPath);
        if (config == null)
        {
            config = ScriptableObject.CreateInstance<RunConfig>();
            AssetDatabase.CreateAsset(config, configPath);
            AssetDatabase.SaveAssets();
            Debug.Log($"[DeckSaver] Created starter RunConfig at {configPath}.");
        }

        var so = new SerializedObject(hubUI);
        so.FindProperty("_runConfig").objectReferenceValue = config;
        so.FindProperty("_runSceneName").stringValue       = "Run";
        so.ApplyModifiedProperties();

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        Selection.activeObject = config;
        Debug.Log("[DeckSaver] Hub configured. Assign encounter and reward pools in the RunConfig.");
    }

    // ── Add BoonManager to Battle scene ──────────────────────────────────────

    [MenuItem("DeckSaver/Add Boon Manager to Battle Scene")]
    public static void AddBoonManager()
    {
        if (Object.FindFirstObjectByType<BoonManager>() != null)
        {
            EditorUtility.DisplayDialog("Add Boon Manager",
                "A BoonManager already exists in this scene.", "OK");
            return;
        }

        var battleUI = Object.FindFirstObjectByType<BattleUI>();
        var parent   = battleUI != null ? battleUI.transform : null;

        var go = new GameObject("BoonManager");
        Undo.RegisterCreatedObjectUndo(go, "Add BoonManager");
        if (parent != null) go.transform.SetParent(parent, false);
        go.AddComponent<BoonManager>();

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        Selection.activeGameObject = go;
        Debug.Log("[DeckSaver] BoonManager added.");
    }

    // ── Layout helpers ────────────────────────────────────────────────────────

    /// <summary>Builds one camp option row: label on the left, button on the right.</summary>
    private static (Button btn, TMP_Text label) BuildCampOptionRow(
        string name, RectTransform parent, Vector2 anchorMin, Vector2 anchorMax)
    {
        var rowRT = Panel(name, parent, anchorMin, anchorMax,
            new Color(0.10f, 0.12f, 0.10f, 1f));

        var label = UIText("Label", rowRT,
            new Vector2(0.03f, 0.15f), new Vector2(0.68f, 0.85f),
            "Option", 13f, bold: false, color: Color.white);
        if (label is TextMeshProUGUI ltmp)
            ltmp.textWrappingMode = TextWrappingModes.Normal;

        var btnRT = UI("Button", rowRT);
        btnRT.anchorMin = new Vector2(0.70f, 0.15f);
        btnRT.anchorMax = new Vector2(0.97f, 0.85f);
        btnRT.offsetMin = btnRT.offsetMax = Vector2.zero;
        btnRT.gameObject.AddComponent<Image>().color = new Color(0.18f, 0.38f, 0.18f, 1f);
        var btn = btnRT.gameObject.AddComponent<Button>();
        UIText("Label", btnRT, Vector2.zero, Vector2.one,
            "Use", 13f, bold: true, color: Color.white);

        return (btn, label);
    }

    /// <summary>Builds one shop column: header TMP + content parent (VerticalLayoutGroup).</summary>
    private static (TMP_Text header, RectTransform content) BuildShopColumn(
        string name, RectTransform parent, float xMin, float xMax)
    {
        var colRT = UI(name, parent);
        colRT.anchorMin = new Vector2(xMin, 0.08f);
        colRT.anchorMax = new Vector2(xMax, 0.83f);
        colRT.offsetMin = colRT.offsetMax = Vector2.zero;

        var header = UIText("Header", colRT,
            new Vector2(0f, 0.88f), new Vector2(1f, 1.0f),
            "Category", 14f, bold: true, color: new Color(0.85f, 0.85f, 1f, 1f));

        var contentRT = UI("Content", colRT);
        contentRT.anchorMin = new Vector2(0f, 0f);
        contentRT.anchorMax = new Vector2(1f, 0.86f);
        contentRT.offsetMin = contentRT.offsetMax = Vector2.zero;
        var vGroup = contentRT.gameObject.AddComponent<VerticalLayoutGroup>();
        vGroup.spacing                = 8f;
        vGroup.childForceExpandWidth  = true;
        vGroup.childForceExpandHeight = false;
        vGroup.childAlignment         = TextAnchor.UpperCenter;
        vGroup.padding                = new RectOffset(4, 4, 4, 4);
        contentRT.gameObject.AddComponent<ContentSizeFitter>().verticalFit =
            ContentSizeFitter.FitMode.PreferredSize;

        return (header, contentRT);
    }

    // ── Prefab builders ───────────────────────────────────────────────────────

    private static GameObject LoadOrBuildCardSlotPrefab()
    {
        const string path = "Assets/Prefabs/FragmentSwapCardSlot.prefab";
        var existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (existing != null) return existing;

        EnsurePrefabFolder();

        var go = new GameObject("FragmentSwapCardSlot", typeof(RectTransform));
        go.AddComponent<Image>().color = new Color(0.12f, 0.10f, 0.16f, 1f);
        go.AddComponent<Button>();

        var nameRT = new GameObject("Name", typeof(RectTransform)).GetComponent<RectTransform>();
        nameRT.SetParent(go.transform, false);
        SetAnchors(nameRT, new Vector2(0f, 0.15f), Vector2.one);
        var nameTMP = nameRT.gameObject.AddComponent<TextMeshProUGUI>();
        nameTMP.text             = "Card Name";
        nameTMP.fontSize         = 10f;
        nameTMP.alignment        = TextAlignmentOptions.Center;
        nameTMP.textWrappingMode = TextWrappingModes.Normal;
        nameTMP.color            = Color.white;

        go.GetComponent<RectTransform>().sizeDelta = new Vector2(140f, 200f);

        return SavePrefab(go, path);
    }

    private static GameObject LoadOrBuildBoonOfferPrefab()
    {
        const string path = "Assets/Prefabs/BoonOfferView.prefab";
        var existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (existing != null) return existing;

        EnsurePrefabFolder();

        var go = new GameObject("BoonOfferView", typeof(RectTransform));
        go.GetComponent<RectTransform>().sizeDelta = new Vector2(400f, 120f);
        go.AddComponent<Image>().color = new Color(0.10f, 0.09f, 0.14f, 1f);

        var bov = go.AddComponent<BoonOfferView>();

        // Icon (small, left side)
        var iconRT = new GameObject("Icon", typeof(RectTransform)).GetComponent<RectTransform>();
        iconRT.SetParent(go.transform, false);
        SetAnchors(iconRT, new Vector2(0.02f, 0.15f), new Vector2(0.18f, 0.85f));
        var iconImg = iconRT.gameObject.AddComponent<Image>();
        iconImg.color = new Color(0.5f, 0.5f, 0.5f, 0.5f);

        // Name TMP
        var nameRT = new GameObject("Name", typeof(RectTransform)).GetComponent<RectTransform>();
        nameRT.SetParent(go.transform, false);
        SetAnchors(nameRT, new Vector2(0.20f, 0.55f), new Vector2(0.78f, 0.90f));
        var nameTMP = nameRT.gameObject.AddComponent<TextMeshProUGUI>();
        nameTMP.text      = "Boon Name";
        nameTMP.fontSize  = 13f;
        nameTMP.fontStyle = FontStyles.Bold;
        nameTMP.color     = Color.white;

        // Description TMP
        var descRT = new GameObject("Description", typeof(RectTransform)).GetComponent<RectTransform>();
        descRT.SetParent(go.transform, false);
        SetAnchors(descRT, new Vector2(0.20f, 0.15f), new Vector2(0.78f, 0.55f));
        var descTMP = descRT.gameObject.AddComponent<TextMeshProUGUI>();
        descTMP.text             = "Boon description.";
        descTMP.fontSize         = 9f;
        descTMP.color            = new Color(0.80f, 0.80f, 0.80f, 1f);
        descTMP.textWrappingMode = TextWrappingModes.Normal;

        // Select button
        var btnRT = new GameObject("SelectButton", typeof(RectTransform)).GetComponent<RectTransform>();
        btnRT.SetParent(go.transform, false);
        SetAnchors(btnRT, new Vector2(0.80f, 0.15f), new Vector2(0.97f, 0.85f));
        btnRT.gameObject.AddComponent<Image>().color = new Color(0.20f, 0.35f, 0.55f, 1f);
        var selBtn = btnRT.gameObject.AddComponent<Button>();

        var selLabelRT = new GameObject("Label", typeof(RectTransform)).GetComponent<RectTransform>();
        selLabelRT.SetParent(btnRT, false);
        SetAnchors(selLabelRT, Vector2.zero, Vector2.one);
        var selTMP = selLabelRT.gameObject.AddComponent<TextMeshProUGUI>();
        selTMP.text      = "Choose";
        selTMP.fontSize  = 10f;
        selTMP.fontStyle = FontStyles.Bold;
        selTMP.alignment = TextAlignmentOptions.Center;
        selTMP.color     = Color.white;

        // Wire BoonOfferView
        var so = new SerializedObject(bov);
        so.FindProperty("_nameText").objectReferenceValue        = nameTMP;
        so.FindProperty("_descriptionText").objectReferenceValue = descTMP;
        so.FindProperty("_icon").objectReferenceValue            = iconImg;
        so.FindProperty("_selectButton").objectReferenceValue    = selBtn;
        so.ApplyModifiedPropertiesWithoutUndo();

        return SavePrefab(go, path);
    }

    private static GameObject LoadOrBuildShopSlotPrefab()
    {
        const string path = "Assets/Prefabs/ShopSlot.prefab";
        var existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (existing != null) return existing;

        EnsurePrefabFolder();

        var go = new GameObject("ShopSlot", typeof(RectTransform));
        go.GetComponent<RectTransform>().sizeDelta = new Vector2(200f, 80f);
        go.AddComponent<Image>().color = new Color(0.10f, 0.09f, 0.14f, 1f);

        // Name TMP (child 0 — ShopPanel.CreateSlot reads tmps[0])
        var nameRT = new GameObject("Name", typeof(RectTransform)).GetComponent<RectTransform>();
        nameRT.SetParent(go.transform, false);
        SetAnchors(nameRT, new Vector2(0.04f, 0.52f), new Vector2(0.96f, 0.95f));
        var nameTMP = nameRT.gameObject.AddComponent<TextMeshProUGUI>();
        nameTMP.text             = "Item Name";
        nameTMP.fontSize         = 11f;
        nameTMP.fontStyle        = FontStyles.Bold;
        nameTMP.color            = Color.white;
        nameTMP.textWrappingMode = TextWrappingModes.Normal;

        // Price TMP (child 1 — ShopPanel.CreateSlot reads tmps[1])
        var priceRT = new GameObject("Price", typeof(RectTransform)).GetComponent<RectTransform>();
        priceRT.SetParent(go.transform, false);
        SetAnchors(priceRT, new Vector2(0.04f, 0.28f), new Vector2(0.60f, 0.52f));
        var priceTMP = priceRT.gameObject.AddComponent<TextMeshProUGUI>();
        priceTMP.text     = "50g";
        priceTMP.fontSize = 10f;
        priceTMP.color    = new Color(1f, 0.85f, 0.3f, 1f);

        // Buy Button (child 2)
        var btnRT = new GameObject("BuyButton", typeof(RectTransform)).GetComponent<RectTransform>();
        btnRT.SetParent(go.transform, false);
        SetAnchors(btnRT, new Vector2(0.62f, 0.10f), new Vector2(0.97f, 0.90f));
        btnRT.gameObject.AddComponent<Image>().color = new Color(0.18f, 0.38f, 0.18f, 1f);
        btnRT.gameObject.AddComponent<Button>();

        var buyLabelRT = new GameObject("Label", typeof(RectTransform)).GetComponent<RectTransform>();
        buyLabelRT.SetParent(btnRT, false);
        SetAnchors(buyLabelRT, Vector2.zero, Vector2.one);
        var buyTMP = buyLabelRT.gameObject.AddComponent<TextMeshProUGUI>();
        buyTMP.text      = "Buy";
        buyTMP.fontSize  = 10f;
        buyTMP.fontStyle = FontStyles.Bold;
        buyTMP.alignment = TextAlignmentOptions.Center;
        buyTMP.color     = Color.white;

        return SavePrefab(go, path);
    }

    private static GameObject LoadOrBuildMapNodePrefab()
    {
        const string path = "Assets/Prefabs/MapNode.prefab";
        var existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (existing != null) return existing;

        EnsurePrefabFolder();

        var go = new GameObject("MapNode", typeof(RectTransform));
        go.GetComponent<RectTransform>().sizeDelta = new Vector2(72f, 72f);

        var bg = go.AddComponent<Image>();
        bg.color = Color.white;

        var btn = go.AddComponent<Button>();
        var nav = btn.navigation;
        nav.mode         = Navigation.Mode.None;
        btn.navigation   = nav;

        var mnv = go.AddComponent<MapNodeView>();

        // Type label
        var labelRT = new GameObject("TypeLabel", typeof(RectTransform)).GetComponent<RectTransform>();
        labelRT.SetParent(go.transform, false);
        SetAnchors(labelRT, new Vector2(0.05f, 0.20f), new Vector2(0.95f, 0.80f));
        var labelTMP = labelRT.gameObject.AddComponent<TextMeshProUGUI>();
        labelTMP.text             = "Battle";
        labelTMP.fontSize         = 10f;
        labelTMP.fontStyle        = FontStyles.Bold;
        labelTMP.alignment        = TextAlignmentOptions.Center;
        labelTMP.color            = Color.black;
        labelTMP.textWrappingMode = TextWrappingModes.Normal;

        // Wire MapNodeView
        var so = new SerializedObject(mnv);
        so.FindProperty("_button").objectReferenceValue     = btn;
        so.FindProperty("_typeLabel").objectReferenceValue  = labelTMP;
        so.FindProperty("_background").objectReferenceValue = bg;
        so.ApplyModifiedPropertiesWithoutUndo();

        return SavePrefab(go, path);
    }

    private static GameObject LoadOrBuildMapEdgePrefab()
    {
        const string path = "Assets/Prefabs/MapEdge.prefab";
        var existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (existing != null) return existing;

        EnsurePrefabFolder();

        var go = new GameObject("MapEdge", typeof(RectTransform));
        go.GetComponent<RectTransform>().sizeDelta = new Vector2(100f, 4f);

        var img = go.AddComponent<Image>();
        img.color = new Color(0.6f, 0.6f, 0.6f, 0.8f);

        go.AddComponent<MapEdgeView>();

        return SavePrefab(go, path);
    }

    // ── Shared builder helpers ────────────────────────────────────────────────

    private static void EnsurePrefabFolder()
    {
        if (!AssetDatabase.IsValidFolder("Assets/Prefabs"))
            AssetDatabase.CreateFolder("Assets", "Prefabs");
    }

    private static GameObject SavePrefab(GameObject go, string path)
    {
        var prefab = PrefabUtility.SaveAsPrefabAsset(go, path);
        Object.DestroyImmediate(go);
        AssetDatabase.Refresh();
        Debug.Log($"[DeckSaver] Prefab saved: {path}");
        return prefab;
    }

    private static void SetAnchors(RectTransform rt, Vector2 min, Vector2 max)
    {
        rt.anchorMin = min;
        rt.anchorMax = max;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
    }

    private static GameObject CreateGO(string name)
    {
        var go = new GameObject(name);
        Undo.RegisterCreatedObjectUndo(go, "Build Run Scene");
        return go;
    }

    private static GameObject CreateChild(string name, Transform parent)
    {
        var go = CreateGO(name);
        go.transform.SetParent(parent, false);
        return go;
    }

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

    private static RectTransform Panel(string name, Transform parent,
        Vector2 anchorMin, Vector2 anchorMax, Color color)
    {
        var rt = UI(name, parent);
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
        rt.gameObject.AddComponent<Image>().color = color;
        return rt;
    }

    private static TMP_Text UIText(string name, RectTransform parent,
        Vector2 min, Vector2 max, string text, float fontSize, bool bold, Color color)
    {
        var rt = UI(name, parent);
        rt.anchorMin = min;
        rt.anchorMax = max;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
        var tmp = rt.gameObject.AddComponent<TextMeshProUGUI>();
        tmp.text     = text;
        tmp.fontSize = fontSize;
        tmp.color    = color;
        if (bold) tmp.fontStyle = FontStyles.Bold;
        return tmp;
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
        vpRT.anchorMin = Vector2.zero;
        vpRT.anchorMax = Vector2.one;
        vpRT.offsetMin = vpRT.offsetMax = Vector2.zero;
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
        layout.childAlignment         = TextAnchor.MiddleLeft;
        layout.spacing                = 12f;
        layout.padding                = new RectOffset(12, 12, 0, 0);
        layout.childForceExpandWidth  = false;
        layout.childForceExpandHeight = false;
        contentRT.gameObject.AddComponent<ContentSizeFitter>().horizontalFit =
            ContentSizeFitter.FitMode.PreferredSize;

        return contentRT;
    }

    private static void SetRef(Object target, string field, Object value)
    {
        var so = new SerializedObject(target);
        so.FindProperty(field).objectReferenceValue = value;
        so.ApplyModifiedPropertiesWithoutUndo();
    }
}

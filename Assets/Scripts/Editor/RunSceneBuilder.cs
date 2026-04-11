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
///   RunScene root
///     Camera
///     EventSystem
///     Canvas
///       EncounterInfo      — encounter name + enemy count + Begin Battle button
///       RewardPanel        — boon offer slots + fragment swap button
///         FragmentSwapPanel
///           FragmentChoiceRoot  (step 1: pick a fragment)
///           CardChoiceRoot      (step 2: pick a card in your deck)
///       SavePromptPanel    — shown after boss defeat
///       RunOverPanel       — win / loss end screen
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
            cam.clearFlags       = CameraClearFlags.SolidColor;
            cam.backgroundColor  = new Color(0.08f, 0.07f, 0.10f, 1f);
            cam.orthographic     = true;
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

        // ── Encounter Info ────────────────────────────────────────────────────

        var infoRT = Panel("EncounterInfo", canvas.transform,
            new Vector2(0.3f, 0.35f), new Vector2(0.7f, 0.75f),
            new Color(0.06f, 0.05f, 0.08f, 0.85f));

        var encounterNameTMP = UIText("EncounterName", infoRT,
            new Vector2(0.05f, 0.72f), new Vector2(0.95f, 0.95f),
            "Encounter", 22f, bold: true, color: Color.white);

        var enemyCountTMP = UIText("EnemyCount", infoRT,
            new Vector2(0.05f, 0.54f), new Vector2(0.95f, 0.72f),
            "3 enemies", 13f, bold: false, color: new Color(0.75f, 0.75f, 0.75f, 1f));

        // Begin Battle button (centred, lower third of the info panel)
        var beginBtnRT = UI("BeginBattleButton", infoRT);
        beginBtnRT.anchorMin        = new Vector2(0.2f, 0.08f);
        beginBtnRT.anchorMax        = new Vector2(0.8f, 0.38f);
        beginBtnRT.offsetMin        = beginBtnRT.offsetMax = Vector2.zero;
        beginBtnRT.gameObject.AddComponent<Image>().color = new Color(0.12f, 0.40f, 0.12f, 0.92f);
        var beginBtn = beginBtnRT.gameObject.AddComponent<Button>();
        UIText("Label", beginBtnRT, Vector2.zero, Vector2.one,
            "Begin Battle", 16f, bold: true, color: Color.white);

        // ── Fragment Upgrade Panel ────────────────────────────────────────────
        // Built before RewardPanel so it can be referenced.

        var upgradePanelRT = Panel("FragmentUpgradePanel", canvas.transform,
            new Vector2(0.1f, 0.05f), new Vector2(0.9f, 0.95f),
            new Color(0.05f, 0.05f, 0.10f, 0.97f));
        var fragmentUpgradePanel = upgradePanelRT.gameObject.AddComponent<FragmentUpgradePanel>();

        // Step 1: card list
        var upgradeCardListRoot = CreateChild("CardListRoot", upgradePanelRT);
        Stretch(upgradeCardListRoot.AddComponent<RectTransform>());

        UIText("Header", upgradeCardListRoot.GetComponent<RectTransform>(),
            new Vector2(0.05f, 0.88f), new Vector2(0.95f, 0.97f),
            "Choose a Card to Upgrade", 20f, bold: true, color: Color.white);

        var upgradeCardSlotPrefab = LoadOrBuildCardSlotPrefab();
        var upgradeCardScrollContent = BuildScrollView(
            upgradeCardListRoot.GetComponent<RectTransform>(),
            new Vector2(0.02f, 0.10f), new Vector2(0.98f, 0.84f));

        // Step 2: fragment detail ("blown up" card)
        var upgradeFragDetailRoot = CreateChild("FragmentDetailRoot", upgradePanelRT);
        Stretch(upgradeFragDetailRoot.AddComponent<RectTransform>());

        var upgradeCardNameTMP = UIText("CardName", upgradeFragDetailRoot.GetComponent<RectTransform>(),
            new Vector2(0.05f, 0.82f), new Vector2(0.95f, 0.95f),
            "Card Name", 22f, bold: true, color: Color.white);

        // Effect fragment button (left half)
        var effectBtnRT = Panel("EffectButton", upgradeFragDetailRoot.GetComponent<RectTransform>(),
            new Vector2(0.04f, 0.20f), new Vector2(0.48f, 0.78f),
            new Color(0.10f, 0.14f, 0.20f, 1f));
        var effectUpgradeBtn = effectBtnRT.gameObject.AddComponent<Button>();
        var effectUpgradeLabel = UIText("Label", effectBtnRT,
            new Vector2(0.05f, 0.40f), new Vector2(0.95f, 0.65f),
            "Effect Fragment", 14f, bold: false, color: Color.white);
        UIText("TypeTag", effectBtnRT,
            new Vector2(0.05f, 0.68f), new Vector2(0.95f, 0.85f),
            "EFFECT", 10f, bold: true, color: new Color(0.6f, 0.85f, 1f, 1f));

        // Modifier fragment button (right half)
        var modifierBtnRT = Panel("ModifierButton", upgradeFragDetailRoot.GetComponent<RectTransform>(),
            new Vector2(0.52f, 0.20f), new Vector2(0.96f, 0.78f),
            new Color(0.14f, 0.10f, 0.20f, 1f));
        var modifierUpgradeBtn = modifierBtnRT.gameObject.AddComponent<Button>();
        var modifierUpgradeLabel = UIText("Label", modifierBtnRT,
            new Vector2(0.05f, 0.40f), new Vector2(0.95f, 0.65f),
            "Modifier Fragment", 14f, bold: false, color: Color.white);
        UIText("TypeTag", modifierBtnRT,
            new Vector2(0.05f, 0.68f), new Vector2(0.95f, 0.85f),
            "MODIFIER", 10f, bold: true, color: new Color(1f, 0.85f, 0.6f, 1f));

        // Back button (returns to card list)
        var upgBackBtnRT = UI("BackButton", upgradePanelRT);
        upgBackBtnRT.anchorMin = new Vector2(0.02f, 0.01f);
        upgBackBtnRT.anchorMax = new Vector2(0.25f, 0.07f);
        upgBackBtnRT.offsetMin = upgBackBtnRT.offsetMax = Vector2.zero;
        upgBackBtnRT.gameObject.AddComponent<Image>().color = new Color(0.20f, 0.20f, 0.25f, 1f);
        var upgBackBtn = upgBackBtnRT.gameObject.AddComponent<Button>();
        UIText("Label", upgBackBtnRT, Vector2.zero, Vector2.one,
            "Back", 13f, bold: false, color: Color.white);

        // Cancel button (shared)
        var upgCancelBtnRT = UI("CancelButton", upgradePanelRT);
        upgCancelBtnRT.anchorMin = new Vector2(0.75f, 0.01f);
        upgCancelBtnRT.anchorMax = new Vector2(0.98f, 0.07f);
        upgCancelBtnRT.offsetMin = upgCancelBtnRT.offsetMax = Vector2.zero;
        upgCancelBtnRT.gameObject.AddComponent<Image>().color = new Color(0.35f, 0.10f, 0.10f, 1f);
        var upgCancelBtn = upgCancelBtnRT.gameObject.AddComponent<Button>();
        UIText("Label", upgCancelBtnRT, Vector2.zero, Vector2.one,
            "Cancel", 13f, bold: false, color: Color.white);

        // Wire FragmentUpgradePanel
        SetRef(fragmentUpgradePanel, "_cardListRoot",       upgradeCardListRoot);
        SetRef(fragmentUpgradePanel, "_cardListParent",     upgradeCardScrollContent);
        SetRef(fragmentUpgradePanel, "_cardSlotPrefab",     upgradeCardSlotPrefab);
        SetRef(fragmentUpgradePanel, "_fragmentDetailRoot", upgradeFragDetailRoot);
        SetRef(fragmentUpgradePanel, "_cardNameText",       upgradeCardNameTMP);
        SetRef(fragmentUpgradePanel, "_effectButton",       effectUpgradeBtn);
        SetRef(fragmentUpgradePanel, "_effectLabel",        effectUpgradeLabel);
        SetRef(fragmentUpgradePanel, "_modifierButton",     modifierUpgradeBtn);
        SetRef(fragmentUpgradePanel, "_modifierLabel",      modifierUpgradeLabel);
        SetRef(fragmentUpgradePanel, "_backButton",         upgBackBtn);
        SetRef(fragmentUpgradePanel, "_cancelButton",       upgCancelBtn);

        upgradePanelRT.gameObject.SetActive(false);

        // ── Fragment Swap Panel ───────────────────────────────────────────────
        // Built before RewardPanel so it can be referenced.

        var swapPanelRT = Panel("FragmentSwapPanel", canvas.transform,
            new Vector2(0.1f, 0.05f), new Vector2(0.9f, 0.95f),
            new Color(0.05f, 0.05f, 0.10f, 0.97f));
        var fragmentSwapPanel = swapPanelRT.gameObject.AddComponent<FragmentSwapPanel>();

        // Step 1 root: three fragment offer slots side by side
        var fragChoiceRoot = CreateChild("FragmentChoiceRoot", swapPanelRT);
        Stretch(fragChoiceRoot.AddComponent<RectTransform>());

        UIText("Header", fragChoiceRoot.GetComponent<RectTransform>(),
            new Vector2(0.05f, 0.88f), new Vector2(0.95f, 0.97f),
            "Choose a Fragment", 20f, bold: true, color: Color.white);

        // Three side-by-side fragment offer views
        var offerSlotPositions = new[]
        {
            (new Vector2(0.04f, 0.20f), new Vector2(0.33f, 0.85f)),
            (new Vector2(0.36f, 0.20f), new Vector2(0.65f, 0.85f)),
            (new Vector2(0.68f, 0.20f), new Vector2(0.97f, 0.85f)),
        };

        var fragViews = new FragmentOfferView[3];
        for (int i = 0; i < 3; i++)
        {
            var (min, max) = offerSlotPositions[i];
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
            selBtnRT.anchorMin        = new Vector2(0.1f, 0.03f);
            selBtnRT.anchorMax        = new Vector2(0.9f, 0.17f);
            selBtnRT.offsetMin        = selBtnRT.offsetMax = Vector2.zero;
            selBtnRT.gameObject.AddComponent<Image>().color = new Color(0.20f, 0.40f, 0.20f, 1f);
            var selBtn = selBtnRT.gameObject.AddComponent<Button>();
            UIText("Label", selBtnRT, Vector2.zero, Vector2.one,
                "Choose", 12f, bold: true, color: Color.white);

            SetRef(fov, "_nameText",    nameTMP);
            SetRef(fov, "_typeText",    typeTMP);
            SetRef(fov, "_flavorText",  flavorTMP);
            SetRef(fov, "_selectButton", selBtn);
        }

        // Step 2 root: card list scroll view
        var cardChoiceRoot = CreateChild("CardChoiceRoot", swapPanelRT);
        Stretch(cardChoiceRoot.AddComponent<RectTransform>());

        var instructionTMP = UIText("Instruction", cardChoiceRoot.GetComponent<RectTransform>(),
            new Vector2(0.05f, 0.86f), new Vector2(0.95f, 0.97f),
            "Pick a card to replace its fragment.", 16f, bold: false, color: Color.white);

        // Reuse the card slot prefab (already built for the upgrade panel)
        var cardSlotPrefab = upgradeCardSlotPrefab;

        // Scroll view for card list
        var cardScrollContent = BuildScrollView(
            cardChoiceRoot.GetComponent<RectTransform>(),
            new Vector2(0.02f, 0.06f), new Vector2(0.98f, 0.84f));

        // Cancel button (shared between both steps)
        var cancelBtnRT = UI("CancelButton", swapPanelRT);
        cancelBtnRT.anchorMin        = new Vector2(0.38f, 0.01f);
        cancelBtnRT.anchorMax        = new Vector2(0.62f, 0.07f);
        cancelBtnRT.offsetMin        = cancelBtnRT.offsetMax = Vector2.zero;
        cancelBtnRT.gameObject.AddComponent<Image>().color = new Color(0.35f, 0.10f, 0.10f, 1f);
        var cancelBtn = cancelBtnRT.gameObject.AddComponent<Button>();
        UIText("Label", cancelBtnRT, Vector2.zero, Vector2.one,
            "Cancel", 13f, bold: false, color: Color.white);

        // Wire FragmentSwapPanel
        SetRef(fragmentSwapPanel, "_fragmentChoiceRoot", fragChoiceRoot);
        SetRef(fragmentSwapPanel, "_offerView1",         fragViews[0]);
        SetRef(fragmentSwapPanel, "_offerView2",         fragViews[1]);
        SetRef(fragmentSwapPanel, "_offerView3",         fragViews[2]);
        SetRef(fragmentSwapPanel, "_cardChoiceRoot",     cardChoiceRoot);
        SetRef(fragmentSwapPanel, "_instructionText",    instructionTMP);
        SetRef(fragmentSwapPanel, "_cardListParent",     cardScrollContent);
        SetRef(fragmentSwapPanel, "_cardSlotPrefab",     cardSlotPrefab);
        SetRef(fragmentSwapPanel, "_cancelButton",       cancelBtn);

        swapPanelRT.gameObject.SetActive(false);

        // ── Reward Panel ──────────────────────────────────────────────────────

        var rewardPanelRT = Panel("RewardPanel", canvas.transform,
            new Vector2(0.15f, 0.10f), new Vector2(0.85f, 0.92f),
            new Color(0.06f, 0.05f, 0.10f, 0.95f));
        var rewardPanel = rewardPanelRT.gameObject.AddComponent<RewardPanel>();

        var rewardHeaderTMP = UIText("Header", rewardPanelRT,
            new Vector2(0.05f, 0.88f), new Vector2(0.95f, 0.97f),
            "Encounter Complete", 22f, bold: true, color: Color.white);

        // Two boon offer slots (matches regularOfferCount = 2 default)
        var boonSlotPositions = new[]
        {
            (new Vector2(0.04f, 0.10f), new Vector2(0.48f, 0.85f)),
            (new Vector2(0.52f, 0.10f), new Vector2(0.96f, 0.85f)),
        };

        var boonSlots = new BoonOfferView[2];
        for (int i = 0; i < 2; i++)
        {
            var (min, max) = boonSlotPositions[i];
            var slotRT = Panel($"BoonOffer{i + 1}", rewardPanelRT,
                min, max, new Color(0.10f, 0.09f, 0.14f, 1f));
            var bov = slotRT.gameObject.AddComponent<BoonOfferView>();
            boonSlots[i] = bov;

            var iconRT = UI("Icon", slotRT);
            iconRT.anchorMin = new Vector2(0.35f, 0.73f);
            iconRT.anchorMax = new Vector2(0.65f, 0.95f);
            iconRT.offsetMin = iconRT.offsetMax = Vector2.zero;
            var iconImg = iconRT.gameObject.AddComponent<Image>();
            iconImg.color = new Color(0.5f, 0.5f, 0.5f, 0.5f);

            var bNameTMP = UIText("Name", slotRT,
                new Vector2(0.05f, 0.62f), new Vector2(0.95f, 0.75f),
                "Boon Name", 14f, bold: true, color: Color.white);
            var bDescTMP = UIText("Description", slotRT,
                new Vector2(0.05f, 0.18f), new Vector2(0.95f, 0.62f),
                "Boon description.", 10f, bold: false,
                color: new Color(0.80f, 0.80f, 0.80f, 1f));
            if (bDescTMP is TextMeshProUGUI bdtmp)
                bdtmp.textWrappingMode = TextWrappingModes.Normal;

            var bSelRT = UI("SelectButton", slotRT);
            bSelRT.anchorMin = new Vector2(0.1f, 0.02f);
            bSelRT.anchorMax = new Vector2(0.9f, 0.15f);
            bSelRT.offsetMin = bSelRT.offsetMax = Vector2.zero;
            bSelRT.gameObject.AddComponent<Image>().color = new Color(0.20f, 0.35f, 0.55f, 1f);
            var bSelBtn = bSelRT.gameObject.AddComponent<Button>();
            UIText("Label", bSelRT, Vector2.zero, Vector2.one,
                "Choose", 12f, bold: true, color: Color.white);

            SetRef(bov, "_nameText",        bNameTMP);
            SetRef(bov, "_descriptionText", bDescTMP);
            SetRef(bov, "_icon",            iconImg);
            SetRef(bov, "_selectButton",    bSelBtn);
        }

        // Fragment swap button (left half of bottom strip)
        var swapBtnRT = UI("FragmentSwapButton", rewardPanelRT);
        swapBtnRT.anchorMin        = new Vector2(0.03f, 0.01f);
        swapBtnRT.anchorMax        = new Vector2(0.48f, 0.09f);
        swapBtnRT.offsetMin        = swapBtnRT.offsetMax = Vector2.zero;
        swapBtnRT.gameObject.AddComponent<Image>().color = new Color(0.35f, 0.20f, 0.55f, 1f);
        var swapBtn = swapBtnRT.gameObject.AddComponent<Button>();
        var swapBtnLabel = UIText("Label", swapBtnRT, Vector2.zero, Vector2.one,
            "Swap a Fragment", 13f, bold: false, color: Color.white);

        // Fragment upgrade button (right half of bottom strip)
        var upgradeBtnRT = UI("FragmentUpgradeButton", rewardPanelRT);
        upgradeBtnRT.anchorMin        = new Vector2(0.52f, 0.01f);
        upgradeBtnRT.anchorMax        = new Vector2(0.97f, 0.09f);
        upgradeBtnRT.offsetMin        = upgradeBtnRT.offsetMax = Vector2.zero;
        upgradeBtnRT.gameObject.AddComponent<Image>().color = new Color(0.20f, 0.40f, 0.30f, 1f);
        var upgradeBtn = upgradeBtnRT.gameObject.AddComponent<Button>();
        var upgradeBtnLabel = UIText("Label", upgradeBtnRT, Vector2.zero, Vector2.one,
            "Upgrade a Fragment", 13f, bold: false, color: Color.white);

        // Wire RewardPanel
        SetRef(rewardPanel, "_headerText",           rewardHeaderTMP);
        SetRef(rewardPanel, "_fragmentSwapPanel",    fragmentSwapPanel);
        SetRef(rewardPanel, "_fragmentUpgradePanel", fragmentUpgradePanel);

        // Boon slots list
        var rpSO = new SerializedObject(rewardPanel);
        var boonSlotsProp = rpSO.FindProperty("_boonSlots");
        boonSlotsProp.arraySize = 2;
        boonSlotsProp.GetArrayElementAtIndex(0).objectReferenceValue = boonSlots[0];
        boonSlotsProp.GetArrayElementAtIndex(1).objectReferenceValue = boonSlots[1];

        var swapSlotsProp = rpSO.FindProperty("_fragmentSwapSlots");
        swapSlotsProp.arraySize = 1;
        swapSlotsProp.GetArrayElementAtIndex(0).objectReferenceValue = swapBtn;

        var swapLabelsProp = rpSO.FindProperty("_swapSlotLabels");
        swapLabelsProp.arraySize = 1;
        swapLabelsProp.GetArrayElementAtIndex(0).objectReferenceValue = swapBtnLabel;

        var upgradeSlotsProp = rpSO.FindProperty("_fragmentUpgradeSlots");
        upgradeSlotsProp.arraySize = 1;
        upgradeSlotsProp.GetArrayElementAtIndex(0).objectReferenceValue = upgradeBtn;

        var upgradeLabelsProp = rpSO.FindProperty("_upgradeSlotLabels");
        upgradeLabelsProp.arraySize = 1;
        upgradeLabelsProp.GetArrayElementAtIndex(0).objectReferenceValue = upgradeBtnLabel;

        rpSO.ApplyModifiedPropertiesWithoutUndo();

        rewardPanelRT.gameObject.SetActive(false);

        // ── Save Prompt Panel ─────────────────────────────────────────────────

        var savePanelRT = Panel("SavePromptPanel", canvas.transform,
            new Vector2(0.25f, 0.25f), new Vector2(0.75f, 0.80f),
            new Color(0.06f, 0.05f, 0.08f, 0.97f));
        var savePromptPanel = savePanelRT.gameObject.AddComponent<SavePromptPanel>();

        var saveBodyTMP = UIText("Body", savePanelRT,
            new Vector2(0.06f, 0.35f), new Vector2(0.94f, 0.94f),
            "(save prompt body)", 11f, bold: false,
            color: new Color(0.85f, 0.85f, 0.85f, 1f));
        if (saveBodyTMP is TextMeshProUGUI sbtmp)
            sbtmp.textWrappingMode = TextWrappingModes.Normal;

        var saveBtnRT = UI("SaveButton", savePanelRT);
        saveBtnRT.anchorMin = new Vector2(0.05f, 0.04f);
        saveBtnRT.anchorMax = new Vector2(0.46f, 0.18f);
        saveBtnRT.offsetMin = saveBtnRT.offsetMax = Vector2.zero;
        saveBtnRT.gameObject.AddComponent<Image>().color = new Color(0.55f, 0.22f, 0.10f, 1f);
        var saveBtn = saveBtnRT.gameObject.AddComponent<Button>();
        UIText("Label", saveBtnRT, Vector2.zero, Vector2.one,
            "Save", 15f, bold: true, color: Color.white);

        var contBtnRT = UI("ContinueButton", savePanelRT);
        contBtnRT.anchorMin = new Vector2(0.54f, 0.04f);
        contBtnRT.anchorMax = new Vector2(0.95f, 0.18f);
        contBtnRT.offsetMin = contBtnRT.offsetMax = Vector2.zero;
        contBtnRT.gameObject.AddComponent<Image>().color = new Color(0.12f, 0.35f, 0.12f, 1f);
        var contBtn = contBtnRT.gameObject.AddComponent<Button>();
        UIText("Label", contBtnRT, Vector2.zero, Vector2.one,
            "Continue", 15f, bold: true, color: Color.white);

        SetRef(savePromptPanel, "_bodyText",       saveBodyTMP);
        SetRef(savePromptPanel, "_saveButton",     saveBtn);
        SetRef(savePromptPanel, "_continueButton", contBtn);

        savePanelRT.gameObject.SetActive(false);

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
            "Summary text.", 13f, bold: false,
            color: new Color(0.80f, 0.80f, 0.80f, 1f));

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

        // ── Wire RunSceneController ───────────────────────────────────────────

        SetRef(controller, "_rewardPanel",        rewardPanel);
        SetRef(controller, "_savePromptPanel",     savePromptPanel);
        SetRef(controller, "_runOverPanel",        runOverPanel);
        SetRef(controller, "_encounterNameText",   encounterNameTMP);
        SetRef(controller, "_enemyCountText",      enemyCountTMP);
        SetRef(controller, "_beginEncounterButton", beginBtn);

        // ── Render order: sub-panels always on top ────────────────────────────
        upgradePanelRT.SetAsLastSibling();
        swapPanelRT.SetAsLastSibling();

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        Selection.activeGameObject = root;
        Debug.Log("[DeckSaver] Run scene built. Open the Run scene and run this from the DeckSaver menu.");
    }

    // ── Wire Hub scene for run ────────────────────────────────────────────────

    /// <summary>
    /// Open the Hub scene, then run this.
    /// Creates a starter RunConfig asset (if one doesn't exist) and wires it
    /// into HubUI along with the Run scene name.
    /// </summary>
    [MenuItem("DeckSaver/Configure Hub for Run Scene")]
    public static void ConfigureHub()
    {
        var hubUI = Object.FindFirstObjectByType<HubUI>();
        if (hubUI == null)
        {
            EditorUtility.DisplayDialog("Configure Hub",
                "No HubUI found in the active scene. Open the Hub scene first.", "OK");
            return;
        }

        // Create a starter RunConfig asset if none exists
        const string configPath = "Assets/Data/DefaultRunConfig.asset";
        if (!AssetDatabase.IsValidFolder("Assets/Data"))
            AssetDatabase.CreateFolder("Assets", "Data");

        var config = AssetDatabase.LoadAssetAtPath<RunConfig>(configPath);
        if (config == null)
        {
            config = ScriptableObject.CreateInstance<RunConfig>();
            AssetDatabase.CreateAsset(config, configPath);
            AssetDatabase.SaveAssets();
            Debug.Log($"[DeckSaver] Created starter RunConfig at {configPath}. " +
                      "Open it to assign encounter pools and reward pools.");
        }

        // Wire HubUI
        var so = new SerializedObject(hubUI);
        so.FindProperty("_runConfig").objectReferenceValue   = config;
        so.FindProperty("_runSceneName").stringValue         = "Run";
        so.ApplyModifiedProperties();

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        Selection.activeObject = config;
        Debug.Log("[DeckSaver] Hub configured. RunConfig selected in Project window — assign your encounter and reward pools.");
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

        // Try to parent under an existing manager object for tidiness
        var battleUI = Object.FindFirstObjectByType<BattleUI>();
        var parent   = battleUI != null ? battleUI.transform : null;

        var go = new GameObject("BoonManager");
        Undo.RegisterCreatedObjectUndo(go, "Add BoonManager");
        if (parent != null) go.transform.SetParent(parent, false);
        go.AddComponent<BoonManager>();

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        Selection.activeGameObject = go;
        Debug.Log("[DeckSaver] BoonManager added. Make sure to save the Battle scene.");
    }

    // ── Card slot prefab ──────────────────────────────────────────────────────

    private static GameObject LoadOrBuildCardSlotPrefab()
    {
        const string path = "Assets/Prefabs/FragmentSwapCardSlot.prefab";

        var existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (existing != null) return existing;

        if (!AssetDatabase.IsValidFolder("Assets/Prefabs"))
            AssetDatabase.CreateFolder("Assets", "Prefabs");

        var go = new GameObject("FragmentSwapCardSlot", typeof(RectTransform));
        var rt = go.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(140f, 200f);

        go.AddComponent<Image>().color = new Color(0.12f, 0.10f, 0.16f, 1f);
        var btn = go.AddComponent<Button>();

        var nameRT = new GameObject("Name", typeof(RectTransform)).GetComponent<RectTransform>();
        nameRT.SetParent(go.transform, false);
        nameRT.anchorMin = new Vector2(0f, 0.15f);
        nameRT.anchorMax = Vector2.one;
        nameRT.offsetMin = nameRT.offsetMax = Vector2.zero;
        var nameTMP = nameRT.gameObject.AddComponent<TextMeshProUGUI>();
        nameTMP.text             = "Card Name";
        nameTMP.fontSize         = 10f;
        nameTMP.alignment        = TextAlignmentOptions.Center;
        nameTMP.textWrappingMode = TextWrappingModes.Normal;
        nameTMP.color            = Color.white;

        var prefab = PrefabUtility.SaveAsPrefabAsset(go, path);
        Object.DestroyImmediate(go);
        AssetDatabase.Refresh();
        Debug.Log($"[DeckSaver] FragmentSwapCardSlot prefab saved to {path}");
        return prefab;
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

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

    /// <summary>Create a full-background panel with an Image.</summary>
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
        tmp.text      = text;
        tmp.fontSize  = fontSize;
        tmp.color     = color;
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

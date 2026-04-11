using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using TMPro;

/// <summary>
/// One-shot editor utility to wire up the Commander system in the battle scene.
/// Run via: DeckSaver → Setup Commander UI
/// </summary>
public static class CommanderSceneSetup
{
    [MenuItem("DeckSaver/Setup Commander UI")]
    public static void Run()
    {
        // ── 1. CommanderController on BattleUI ───────────────────────────────
        var battleUI = GameObject.Find("BattleUI");
        if (battleUI == null) { Debug.LogError("[Setup] BattleUI not found."); return; }

        if (battleUI.GetComponent<CommanderController>() == null)
        {
            battleUI.AddComponent<CommanderController>();
            Debug.Log("[Setup] Added CommanderController to BattleUI.");
        }
        else
        {
            Debug.Log("[Setup] CommanderController already present.");
        }

        // ── 2. Find BattleCanvas ─────────────────────────────────────────────
        var canvasT = battleUI.transform.Find("BattleCanvas");
        if (canvasT == null) { Debug.LogError("[Setup] BattleCanvas not found inside BattleUI."); return; }

        // ── 3. Create CommanderCard if it doesn't exist ──────────────────────
        var existing = canvasT.Find("CommanderCard");
        if (existing != null)
        {
            Debug.Log("[Setup] CommanderCard already exists — skipping UI creation.");
        }
        else
        {
            CreateCommanderCard(canvasT.gameObject);
        }

        // ── 4. Remind about deck setup ────────────────────────────────────────
        Debug.Log("[Setup] Commander is set per-deck: open any DeckData asset and assign a CommanderData to its 'Commander' field.");

        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        Debug.Log("[Setup] Done. Save the scene to keep changes (Ctrl+S).");
    }

    private static void CreateCommanderCard(GameObject canvas)
    {
        // ── Root card panel ───────────────────────────────────────────────────
        var cardGO   = new GameObject("CommanderCard");
        cardGO.transform.SetParent(canvas.transform, false);

        var cardRect              = cardGO.AddComponent<RectTransform>();
        cardRect.anchorMin        = new Vector2(0f, 0.5f);
        cardRect.anchorMax        = new Vector2(0f, 0.5f);
        cardRect.pivot            = new Vector2(0f, 0.5f);
        cardRect.anchoredPosition = new Vector2(10f, 0f);
        cardRect.sizeDelta        = new Vector2(160f, 220f);

        var bg    = cardGO.AddComponent<Image>();
        bg.color  = new Color(0.12f, 0.12f, 0.18f, 0.95f);

        // Needed for IPointerClickHandler to fire
        cardGO.AddComponent<GraphicRaycaster>();

        // ── Artwork placeholder ───────────────────────────────────────────────
        var artGO        = MakeChild(cardGO, "Artwork");
        SetAnchors(artGO, 0.05f, 0.55f, 0.95f, 0.98f);
        var artImg       = artGO.AddComponent<Image>();
        artImg.color     = new Color(0.2f, 0.2f, 0.3f, 1f);

        // ── Name label ────────────────────────────────────────────────────────
        var nameGO  = MakeChild(cardGO, "NameLabel");
        SetAnchors(nameGO, 0.05f, 0.47f, 0.95f, 0.56f);
        var nameTMP = nameGO.AddComponent<TextMeshProUGUI>();
        nameTMP.text      = "Commander";
        nameTMP.fontSize  = 11f;
        nameTMP.fontStyle = FontStyles.Bold;
        nameTMP.alignment = TextAlignmentOptions.Center;
        nameTMP.color     = new Color(1f, 0.85f, 0.4f);

        // ── Active label ──────────────────────────────────────────────────────
        var activeGO  = MakeChild(cardGO, "ActiveLabel");
        SetAnchors(activeGO, 0.05f, 0.27f, 0.95f, 0.46f);
        var activeTMP = activeGO.AddComponent<TextMeshProUGUI>();
        activeTMP.text               = "Active: —";
        activeTMP.fontSize           = 8f;
        activeTMP.alignment          = TextAlignmentOptions.TopLeft;
        activeTMP.color              = Color.white;
        activeTMP.textWrappingMode = TextWrappingModes.Normal;

        // ── Passive label ─────────────────────────────────────────────────────
        var passiveGO  = MakeChild(cardGO, "PassiveLabel");
        SetAnchors(passiveGO, 0.05f, 0.07f, 0.95f, 0.26f);
        var passiveTMP = passiveGO.AddComponent<TextMeshProUGUI>();
        passiveTMP.text               = "Passive: —";
        passiveTMP.fontSize           = 8f;
        passiveTMP.alignment          = TextAlignmentOptions.TopLeft;
        passiveTMP.color              = new Color(0.7f, 1f, 0.7f);
        passiveTMP.textWrappingMode = TextWrappingModes.Normal;

        // ── Uses label ────────────────────────────────────────────────────────
        var usesGO  = MakeChild(cardGO, "UsesLabel");
        SetAnchors(usesGO, 0.05f, 0.0f, 0.95f, 0.07f);
        var usesTMP = usesGO.AddComponent<TextMeshProUGUI>();
        usesTMP.text      = "0/1";
        usesTMP.fontSize  = 9f;
        usesTMP.alignment = TextAlignmentOptions.Center;
        usesTMP.color     = new Color(1f, 0.85f, 0.4f);

        // ── CommanderView component ───────────────────────────────────────────
        var view = cardGO.AddComponent<CommanderView>();
        var so   = new SerializedObject(view);
        so.FindProperty("_nameLabel")     .objectReferenceValue = nameTMP;
        so.FindProperty("_activeLabel")   .objectReferenceValue = activeTMP;
        so.FindProperty("_passiveLabel")  .objectReferenceValue = passiveTMP;
        so.FindProperty("_usesLabel")     .objectReferenceValue = usesTMP;
        so.FindProperty("_artwork")       .objectReferenceValue = artImg;
        so.FindProperty("_cardBackground").objectReferenceValue = bg;
        so.ApplyModifiedProperties();

        Debug.Log("[Setup] CommanderCard created and wired.");
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static GameObject MakeChild(GameObject parent, string name)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent.transform, false);
        go.AddComponent<RectTransform>();
        return go;
    }

    private static void SetAnchors(GameObject go, float xMin, float yMin, float xMax, float yMax)
    {
        var rt        = go.GetComponent<RectTransform>();
        rt.anchorMin  = new Vector2(xMin, yMin);
        rt.anchorMax  = new Vector2(xMax, yMax);
        rt.offsetMin  = Vector2.zero;
        rt.offsetMax  = Vector2.zero;
    }
}

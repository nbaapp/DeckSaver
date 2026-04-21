using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Top-level coordinator for the Hub scene.
///
/// Wires together HubDeckBuilderState, CollectionPanel, DeckPanel, and
/// CommanderSlotView. Owns the Confirm button and the deck name input.
///
/// Scene setup requirements:
///   - A HubDeckBuilderState MonoBehaviour with PlayerCollection and CommanderRegistry assigned.
///   - A HubDragController MonoBehaviour.
///   - CollectionPanel, DeckPanel, and CommanderSlotView in the Canvas hierarchy.
///   - This HubUI MonoBehaviour wired in the inspector.
/// </summary>
public class HubUI : MonoBehaviour
{
    [Header("Confirm")]
    [SerializeField] private Button    _confirmButton;
    [SerializeField] private TMP_Text  _confirmButtonLabel;
    [SerializeField] private TMP_InputField _deckNameInput;

    [Header("Scene transition")]
    [Tooltip("Run configuration that governs the run structure (encounter pools, rewards, etc.).")]
    [SerializeField] private RunConfig _runConfig;

    [Tooltip("Name of the Run scene to load when the deck is confirmed.")]
    [SerializeField] private string _runSceneName = "Run";

    // -------------------------------------------------------------------------

    void Start()
    {
        _confirmButton.onClick.AddListener(OnConfirmClicked);
        HubDeckBuilderState.Instance.OnStateChanged += RefreshConfirmButton;
        if (_deckNameInput != null) _deckNameInput.text = "My Deck";
        RefreshConfirmButton();
    }

    void OnDestroy()
    {
        if (HubDeckBuilderState.Instance != null)
            HubDeckBuilderState.Instance.OnStateChanged -= RefreshConfirmButton;
    }

    // -------------------------------------------------------------------------

    void RefreshConfirmButton()
    {
        var state = HubDeckBuilderState.Instance;
        bool ready = state.IsReadyToConfirm();
        _confirmButton.interactable = ready;

        if (_confirmButtonLabel != null)
        {
            int filled = state.FilledSlotCount();
            int emptyCustom = state.CustomSlotCount - filled;
            int totalDefaults = emptyCustom + (state.TotalDeckSize - state.CustomSlotCount);
            if (!ready)
                _confirmButtonLabel.text = "Start Run (no commander)";
            else if (totalDefaults == 0)
                _confirmButtonLabel.text = "Start Run";
            else
                _confirmButtonLabel.text = $"Start Run ({filled}/{state.CustomSlotCount} custom)";
        }
    }

    void OnConfirmClicked()
    {
        string deckName = _deckNameInput != null ? _deckNameInput.text : "My Deck";
        if (string.IsNullOrWhiteSpace(deckName)) deckName = "My Deck";

        var deck = HubDeckBuilderState.Instance.ConfirmDeck(deckName);
        if (deck == null) return;

        if (_runConfig != null)
        {
            RunCarrier.StartRun(_runConfig, deck);
            SceneManager.LoadScene(_runSceneName);
        }
        else
        {
            // Fallback: no RunConfig assigned — go straight to Battle for testing
            Debug.LogWarning("[HubUI] No RunConfig assigned. Loading Battle scene directly (test mode).");
            RunCarrier.CurrentDeck = deck;
            SceneManager.LoadScene("Battle");
        }
    }
}

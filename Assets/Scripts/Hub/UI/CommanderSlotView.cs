using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The commander slot in the hub deckbuilder.
///
/// Two modes:
///   Draft mode  — player places effect + modifier fragments to attempt a forge.
///                 If the combo matches a CommanderData in the registry, the Forge button
///                 becomes active. Clicking it immediately consumes the fragments and
///                 permanently unlocks the Commander.
///   Select mode — if the player has owned Commanders, a dropdown/list lets them pick one
///                 without spending fragments.
///
/// Commander slot drop zones use slotIndex = -2.
/// </summary>
public class CommanderSlotView : MonoBehaviour
{
    [Header("Draft zones")]
    [SerializeField] private FragmentDropZone _effectZone;
    [SerializeField] private FragmentDropZone _modifierZone;

    [Header("Forge UI")]
    [SerializeField] private Button    _forgeButton;
    [SerializeField] private TMP_Text  _forgeButtonLabel;
    [SerializeField] private TMP_Text  _matchLabel;   // shows name of matchable commander

    [Header("Selected commander display")]
    [SerializeField] private GameObject _selectedPanel;
    [SerializeField] private TMP_Text   _selectedNameLabel;
    [SerializeField] private TMP_Text   _selectedPassiveLabel;
    [SerializeField] private Button     _clearSelectionButton;

    [Header("Owned commander picker")]
    [SerializeField] private GameObject        _ownedPickerRoot;
    [SerializeField] private Transform         _ownedListParent;
    [SerializeField] private GameObject        _ownedEntryPrefab; // prefab with Button + TMP_Text
    [SerializeField] private Button            _openPickerButton;

    // -------------------------------------------------------------------------

    void Start()
    {
        // Wire commander slot drop zones to index -2
        _effectZone.zoneType    = FragmentDropZone.ZoneType.Effect;
        _effectZone.slotIndex   = -2;
        _modifierZone.zoneType  = FragmentDropZone.ZoneType.Modifier;
        _modifierZone.slotIndex = -2;

        _effectZone.OnFragmentChanged   += Refresh;
        _modifierZone.OnFragmentChanged += Refresh;

        _forgeButton.onClick.AddListener(OnForgeClicked);
        _clearSelectionButton?.onClick.AddListener(OnClearSelection);
        _openPickerButton?.onClick.AddListener(OnOpenPicker);

        var state = HubDeckBuilderState.Instance;
        state.OnStateChanged   += Refresh;
        state.OnCommanderForged += OnCommanderForged;

        if (_ownedPickerRoot != null) _ownedPickerRoot.SetActive(false);
        Refresh();
    }

    void OnDestroy()
    {
        var state = HubDeckBuilderState.Instance;
        if (state == null) return;
        state.OnStateChanged    -= Refresh;
        state.OnCommanderForged -= OnCommanderForged;
    }

    // -------------------------------------------------------------------------

    void Refresh()
    {
        var state = HubDeckBuilderState.Instance;
        if (state == null) return;

        bool hasSelection = state.SelectedCommander != null;

        // Show/hide panels
        if (_selectedPanel != null) _selectedPanel.SetActive(hasSelection);
        bool draftVisible = !hasSelection;
        _effectZone.gameObject.SetActive(draftVisible);
        _modifierZone.gameObject.SetActive(draftVisible);

        if (hasSelection)
        {
            var cmd = state.SelectedCommander;
            if (_selectedNameLabel    != null) _selectedNameLabel.text    = cmd.commanderName;
            if (_selectedPassiveLabel != null) _selectedPassiveLabel.text = cmd.passiveFlavorText;
            _forgeButton.gameObject.SetActive(false);
            if (_matchLabel != null) _matchLabel.gameObject.SetActive(false);
            return;
        }

        // Draft mode
        var match = state.GetDraftCommanderMatch();
        bool canForge = match != null && !state.collection.ownedCommanders.Contains(match);
        bool alreadyOwned = match != null && state.collection.ownedCommanders.Contains(match);

        _forgeButton.gameObject.SetActive(match != null);
        _forgeButton.interactable = canForge;

        if (_forgeButtonLabel != null)
            _forgeButtonLabel.text = alreadyOwned ? "Already Owned" : "Forge Commander";

        if (_matchLabel != null)
        {
            _matchLabel.gameObject.SetActive(match != null);
            if (match != null) _matchLabel.text = match.commanderName;
        }

        // Open picker button: only show if player owns any commanders
        if (_openPickerButton != null)
            _openPickerButton.gameObject.SetActive(
                state.collection != null && state.collection.ownedCommanders.Count > 0);
    }

    // -------------------------------------------------------------------------

    void OnForgeClicked()
    {
        HubDeckBuilderState.Instance.TryForgeCommander();
        // OnStateChanged fires internally, Refresh will run
    }

    void OnCommanderForged(CommanderData cmd)
    {
        if (_ownedPickerRoot != null) _ownedPickerRoot.SetActive(false);
    }

    void OnClearSelection()
    {
        HubDeckBuilderState.Instance.ClearCommanderSelection();
    }

    void OnOpenPicker()
    {
        if (_ownedPickerRoot == null) return;
        bool nowOpen = !_ownedPickerRoot.activeSelf;
        _ownedPickerRoot.SetActive(nowOpen);
        if (nowOpen) PopulatePicker();
    }

    void PopulatePicker()
    {
        if (_ownedListParent == null || _ownedEntryPrefab == null) return;

        foreach (Transform child in _ownedListParent)
            Destroy(child.gameObject);

        var owned = HubDeckBuilderState.Instance.collection.ownedCommanders;
        foreach (var cmd in owned)
        {
            var entry = Instantiate(_ownedEntryPrefab, _ownedListParent);
            var label = entry.GetComponentInChildren<TMP_Text>();
            if (label != null) label.text = cmd.commanderName;

            var btn = entry.GetComponent<Button>();
            var capturedCmd = cmd;
            btn?.onClick.AddListener(() =>
            {
                HubDeckBuilderState.Instance.SelectOwnedCommander(capturedCmd);
                if (_ownedPickerRoot != null) _ownedPickerRoot.SetActive(false);
            });
        }
    }
}

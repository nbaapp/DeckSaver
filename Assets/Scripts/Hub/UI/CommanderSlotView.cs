using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The commander selection slot in the hub deckbuilder.
///
/// Shows a dropdown of the player's unlocked commanders. Commanders are unlocked
/// through progression milestones (not fragment forging).
/// </summary>
public class CommanderSlotView : MonoBehaviour
{
    [Header("Selection dropdown")]
    [SerializeField] private TMP_Dropdown _commanderDropdown;

    [Header("Selected commander display")]
    [SerializeField] private GameObject _selectedPanel;
    [SerializeField] private TMP_Text   _selectedNameLabel;
    [SerializeField] private TMP_Text   _selectedPassiveLabel;

    private readonly List<CommanderData> _dropdownCommanders = new();

    // -------------------------------------------------------------------------

    void Start()
    {
        if (_commanderDropdown == null)
        {
            Debug.LogWarning("[CommanderSlotView] No TMP_Dropdown assigned. Assign one in the inspector.");
            return;
        }

        _commanderDropdown.onValueChanged.AddListener(OnDropdownChanged);

        var state = HubDeckBuilderState.Instance;
        state.OnStateChanged += Refresh;

        PopulateDropdown();
        Refresh();
    }

    void OnDestroy()
    {
        var state = HubDeckBuilderState.Instance;
        if (state == null) return;
        state.OnStateChanged -= Refresh;
    }

    // -------------------------------------------------------------------------

    void PopulateDropdown()
    {
        _commanderDropdown.ClearOptions();
        _dropdownCommanders.Clear();

        var owned = HubDeckBuilderState.Instance.collection.ownedCommanders;

        var options = new List<TMP_Dropdown.OptionData>();
        options.Add(new TMP_Dropdown.OptionData("-- Select Commander --"));

        foreach (var cmd in owned)
        {
            options.Add(new TMP_Dropdown.OptionData(cmd.commanderName));
            _dropdownCommanders.Add(cmd);
        }

        _commanderDropdown.AddOptions(options);
        _commanderDropdown.interactable = _dropdownCommanders.Count > 0;
    }

    void OnDropdownChanged(int index)
    {
        var state = HubDeckBuilderState.Instance;

        if (index <= 0)
        {
            state.ClearCommanderSelection();
            return;
        }

        int cmdIndex = index - 1; // offset by the placeholder option
        if (cmdIndex >= 0 && cmdIndex < _dropdownCommanders.Count)
            state.SelectOwnedCommander(_dropdownCommanders[cmdIndex]);
    }

    void Refresh()
    {
        var state = HubDeckBuilderState.Instance;
        if (state == null) return;

        bool hasSelection = state.SelectedCommander != null;

        if (_selectedPanel != null) _selectedPanel.SetActive(hasSelection);

        if (hasSelection)
        {
            var cmd = state.SelectedCommander;
            if (_selectedNameLabel    != null) _selectedNameLabel.text    = cmd.commanderName;
            if (_selectedPassiveLabel != null) _selectedPassiveLabel.text = cmd.passiveFlavorText;
        }
    }
}

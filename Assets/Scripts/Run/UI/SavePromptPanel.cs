using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Shown after the player beats a boss. Presents the save choice:
///   • Save  — locks the run into the harder post-save linear section.
///   • Continue — starts the next segment normally (no save used).
///
/// The panel explains the consequences clearly so the player can make
/// an informed decision. Wire in descriptive text in the inspector.
/// </summary>
public class SavePromptPanel : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI _bodyText;
    [SerializeField] private Button          _saveButton;
    [SerializeField] private Button          _continueButton;

    private Action _onSave;
    private Action _onContinue;

    private void Awake()
    {
        _saveButton?.onClick.AddListener(OnSaveClicked);
        _continueButton?.onClick.AddListener(OnContinueClicked);
    }

    /// <summary>
    /// Show the save prompt. The run must not have been saved already;
    /// the caller is responsible for checking RunState.HasSaved.
    /// </summary>
    public void Show(Action onSave, Action onContinue)
    {
        _onSave     = onSave;
        _onContinue = onContinue;
        gameObject.SetActive(true);

        if (_bodyText != null)
            _bodyText.text =
                "You have defeated the boss.\n\n" +
                "<b>Save your progress?</b>\n\n" +
                "Saving locks in your current build and sends you into a harder linear section — " +
                "no more upgrades, but you keep everything you have now.\n\n" +
                "Saving is required to complete the story and claim the best rewards.\n" +
                "You can only save once per run.";
    }

    public void Hide() => gameObject.SetActive(false);

    private void OnSaveClicked()
    {
        Hide();
        _onSave?.Invoke();
    }

    private void OnContinueClicked()
    {
        Hide();
        _onContinue?.Invoke();
    }
}

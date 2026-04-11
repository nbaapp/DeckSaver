using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Shown when the run ends — either by the player dying or (future) completing the run.
/// Displays a summary and a button to return to the Hub.
/// </summary>
public class RunOverPanel : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI _headerText;
    [SerializeField] private TextMeshProUGUI _summaryText;
    [SerializeField] private Button          _returnButton;

    private Action _onReturn;

    private void Awake() =>
        _returnButton?.onClick.AddListener(() => _onReturn?.Invoke());

    public void Show(bool won, int segmentsCleared, int boonsEarned, Action onReturn)
    {
        _onReturn = onReturn;
        gameObject.SetActive(true);

        if (_headerText)
            _headerText.text = won ? "Run Complete!" : "Defeated";

        if (_summaryText)
            _summaryText.text =
                $"Segments cleared: {segmentsCleared}\n" +
                $"Boons earned: {boonsEarned}";
    }

    public void Hide() => gameObject.SetActive(false);
}

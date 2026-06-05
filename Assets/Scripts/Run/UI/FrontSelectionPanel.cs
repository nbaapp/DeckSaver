using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// UI panel that presents the player with three Front choices for the current act.
/// Shows a button for each front; clicking one fires the callback and hides the panel.
/// </summary>
public class FrontSelectionPanel : MonoBehaviour
{
    [Header("Buttons")]
    [SerializeField] private Button _frontButton1;
    [SerializeField] private Button _frontButton2;
    [SerializeField] private Button _frontButton3;

    [Header("Labels")]
    [SerializeField] private TMP_Text _frontLabel1;
    [SerializeField] private TMP_Text _frontLabel2;
    [SerializeField] private TMP_Text _frontLabel3;

    [Header("Header")]
    [SerializeField] private TMP_Text _headerText;

    private Action<FrontConfig> _onSelected;
    private ActConfig _actConfig;

    public void Show(ActConfig act, int actNumber, Action<FrontConfig> onSelected)
    {
        _actConfig  = act;
        _onSelected = onSelected;

        if (_headerText != null)
            _headerText.text = $"Act {actNumber} — Choose Your Front";

        SetupButton(_frontButton1, _frontLabel1, act.fronts.Length > 0 ? act.fronts[0] : null);
        SetupButton(_frontButton2, _frontLabel2, act.fronts.Length > 1 ? act.fronts[1] : null);
        SetupButton(_frontButton3, _frontLabel3, act.fronts.Length > 2 ? act.fronts[2] : null);

        gameObject.SetActive(true);
    }

    public void Hide() => gameObject.SetActive(false);

    private void SetupButton(Button button, TMP_Text label, FrontConfig front)
    {
        if (button == null) return;

        button.onClick.RemoveAllListeners();

        if (front == null)
        {
            button.gameObject.SetActive(false);
            return;
        }

        button.gameObject.SetActive(true);

        if (label != null)
            label.text = front.frontName;

        button.onClick.AddListener(() => OnFrontClicked(front));
    }

    private void OnFrontClicked(FrontConfig front)
    {
        Hide();
        _onSelected?.Invoke(front);
    }
}

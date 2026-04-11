using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Displays one fragment choice inside the FragmentSwapPanel.
/// Shows the fragment name, type (Effect / Modifier), and a select button.
/// </summary>
public class FragmentOfferView : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI _nameText;
    [SerializeField] private TextMeshProUGUI _typeText;   // "Effect" or "Modifier"
    [SerializeField] private TextMeshProUGUI _flavorText;
    [SerializeField] private Button          _selectButton;

    private System.Action _onSelected;

    public void Populate(FragmentChoice choice, System.Action onSelected)
    {
        _onSelected = onSelected;

        if (_nameText)   _nameText.text   = choice.FragmentName;
        if (_typeText)   _typeText.text   = choice.isEffect ? "Effect" : "Modifier";
        if (_flavorText)
        {
            _flavorText.text = choice.isEffect
                ? choice.effectFragment?.flavorText ?? ""
                : choice.modifierFragment?.flavorText ?? "";
        }

        _selectButton?.onClick.RemoveAllListeners();
        _selectButton?.onClick.AddListener(OnClicked);
    }

    private void OnClicked() => _onSelected?.Invoke();
}

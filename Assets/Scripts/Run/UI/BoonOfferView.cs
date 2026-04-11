using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Displays a single boon offer in the reward panel.
/// Wire up the UI references in the inspector; call Populate() at runtime.
/// </summary>
public class BoonOfferView : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI _nameText;
    [SerializeField] private TextMeshProUGUI _descriptionText;
    [SerializeField] private Image           _icon;
    [SerializeField] private Button          _selectButton;

    private System.Action _onSelected;

    public void Populate(BoonData boon, System.Action onSelected)
    {
        _onSelected = onSelected;

        if (_nameText)        _nameText.text        = boon.boonName;
        if (_descriptionText) _descriptionText.text = boon.description;
        if (_icon)
        {
            _icon.sprite  = boon.icon;
            _icon.enabled = boon.icon != null;
        }

        _selectButton?.onClick.RemoveAllListeners();
        _selectButton?.onClick.AddListener(OnClicked);
    }

    private void OnClicked() => _onSelected?.Invoke();
}

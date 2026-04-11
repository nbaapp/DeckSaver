using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// The Commander card displayed in the battle UI (mid-left).
/// Shows the Commander's name, passive description, active description,
/// and remaining active uses. Clicking it triggers the active ability.
/// </summary>
public class CommanderView : MonoBehaviour, IPointerClickHandler
{
    [Header("UI References")]
    [SerializeField] private TextMeshProUGUI _nameLabel;
    [SerializeField] private TextMeshProUGUI _activeLabel;
    [SerializeField] private TextMeshProUGUI _passiveLabel;
    [SerializeField] private TextMeshProUGUI _usesLabel;
    [SerializeField] private Image _artwork;
    [SerializeField] private Image _cardBackground;

    [Header("State Colors")]
    [SerializeField] private Color _availableColor  = Color.white;
    [SerializeField] private Color _unavailableColor = new Color(0.4f, 0.4f, 0.4f);
    [SerializeField] private Color _targetingColor   = new Color(1f, 0.85f, 0.2f);

    private CommanderData _commander;

    private void Start()
    {
        BattleEvents.OnBattleStart += Refresh;

        if (CommanderController.Instance != null)
            CommanderController.Instance.OnActiveChanged += RefreshState;

        TurnManager.Instance.OnPhaseChanged += _ => RefreshState();

        Refresh();
    }

    private void OnDestroy()
    {
        BattleEvents.OnBattleStart -= Refresh;

        if (CommanderController.Instance != null)
            CommanderController.Instance.OnActiveChanged -= RefreshState;

        if (TurnManager.Instance != null)
            TurnManager.Instance.OnPhaseChanged -= _ => RefreshState();
    }

    private void Refresh()
    {
        _commander = PlayerEntity.Instance?.commander;

        if (_commander == null)
        {
            gameObject.SetActive(false);
            return;
        }

        gameObject.SetActive(true);

        if (_nameLabel   != null) _nameLabel.text   = _commander.commanderName;
        if (_activeLabel  != null) _activeLabel.text  = _commander.activeFlavorText;
        if (_passiveLabel != null) _passiveLabel.text = _commander.passiveFlavorText;

        if (_artwork != null && _commander.artwork != null)
            _artwork.sprite = _commander.artwork;

        RefreshState();
    }

    private void RefreshState()
    {
        if (_commander == null || CommanderController.Instance == null) return;

        int  uses       = CommanderController.Instance.ActiveUsesRemaining;
        bool canUse     = CommanderController.Instance.CanUseActive;
        bool targeting  = CommanderController.Instance.IsAwaitingTarget;

        if (_usesLabel != null)
            _usesLabel.text = $"{uses}/{_commander.activesPerBattle}";

        if (_cardBackground != null)
            _cardBackground.color = targeting  ? _targetingColor  :
                                    canUse     ? _availableColor  :
                                                 _unavailableColor;
    }

    public void OnPointerClick(PointerEventData _)
    {
        CommanderController.Instance?.InitiateActiveAbility();
    }
}

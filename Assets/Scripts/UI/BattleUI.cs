using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;
using TMPro;

/// <summary>
/// Coordinates the battle UI at runtime.
/// The full scene hierarchy is built by DeckSaver → Build Battle UI in the menu bar.
/// All references are assigned by that builder and visible in the Inspector.
/// </summary>
public class BattleUI : MonoBehaviour
{
    [Header("References (set by builder)")]
    [SerializeField] private HandDisplay _handDisplay;
    [SerializeField] private PileButton  _drawPile;
    [SerializeField] private PileButton  _discardPile;
    [SerializeField] private PileOverlay _pileOverlay;
    [SerializeField] private Button      _endTurnButton;
    [SerializeField] private TMP_Text    _manaText;
    [SerializeField] private TMP_Text    _staminaText;

    private void Awake()
    {
        // 100 tweens is plenty for card animations (5 cards × 3 tweens + buffer)
        DOTween.SetTweensCapacity(tweenersCapacity: 100, sequencesCapacity: 10);
    }

    private void Start()
    {
        _handDisplay.Init();
        _drawPile.Init(_pileOverlay);
        _discardPile.Init(_pileOverlay);

        if (_endTurnButton != null)
            _endTurnButton.onClick.AddListener(OnEndTurnClicked);

        if (TurnManager.Instance != null)
            TurnManager.Instance.OnPhaseChanged += OnPhaseChanged;

        // Subscribe to player resources via coroutine — handles any init order.
        // Also kicks off StartBattle once the player is confirmed ready.
        StartCoroutine(InitWithPlayer());
    }

    private IEnumerator InitWithPlayer()
    {
        // Wait one frame first so all Start() methods (including EntityManager.Start
        // which spawns enemies) have run before we call StartBattle.
        yield return null;

        while (PlayerEntity.Instance == null)
            yield return null;

        PlayerEntity.Instance.OnResourcesChanged += RefreshResourceDisplay;

        // Now it's safe to start the battle (resources will fire correctly)
        TurnManager.Instance?.StartBattle();

        // Sync UI immediately after battle starts
        OnPhaseChanged(TurnManager.Instance?.CurrentPhase ?? TurnPhase.None);
        RefreshResourceDisplay();
    }

    private void OnDestroy()
    {
        if (TurnManager.Instance != null)
            TurnManager.Instance.OnPhaseChanged -= OnPhaseChanged;
        if (PlayerEntity.Instance != null)
            PlayerEntity.Instance.OnResourcesChanged -= RefreshResourceDisplay;
    }

    private void OnEndTurnClicked() => TurnManager.Instance?.EndPlayerTurn();

    private void OnPhaseChanged(TurnPhase phase)
    {
        bool isPlayerTurn = phase == TurnPhase.PlayerTurn;
        if (_endTurnButton != null)
            _endTurnButton.interactable = isPlayerTurn;
        RefreshResourceDisplay();
    }

    private void RefreshResourceDisplay()
    {
        var player = PlayerEntity.Instance;
        if (_manaText != null)
            _manaText.text = player != null
                ? $"Mana  {player.CurrentMana} / {PlayerEntity.BaseMana}"
                : "Mana  - / -";
        if (_staminaText != null)
            _staminaText.text = player != null
                ? $"Stamina  {player.CurrentStamina} / {PlayerEntity.BaseStamina}"
                : "Stamina  - / -";
    }
}

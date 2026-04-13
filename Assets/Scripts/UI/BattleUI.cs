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
    [SerializeField] private TMP_Text    _unitCountText; // optional

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
        // Wait one frame so EntityManager.Start() (which spawns units) has run.
        yield return null;

        while (PlayerParty.Instance == null || PlayerParty.Instance.SelectedUnit == null)
            yield return null;

        PlayerParty.Instance.OnResourcesChanged += RefreshResourceDisplay;
        BattleEvents.OnUnitDied                 += OnUnitDied;

        TurnManager.Instance?.StartBattle();

        OnPhaseChanged(TurnManager.Instance?.CurrentPhase ?? TurnPhase.None);
        RefreshResourceDisplay();
    }

    private void OnDestroy()
    {
        if (TurnManager.Instance != null)
            TurnManager.Instance.OnPhaseChanged -= OnPhaseChanged;
        if (PlayerParty.Instance != null)
            PlayerParty.Instance.OnResourcesChanged -= RefreshResourceDisplay;
        BattleEvents.OnUnitDied -= OnUnitDied;
    }

    private void OnUnitDied(PlayerEntity _) => RefreshResourceDisplay();

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
        var party = PlayerParty.Instance;
        if (_manaText != null)
            _manaText.text = party != null
                ? $"Mana  {party.CurrentMana} / {PlayerParty.BaseMana}"
                : "Mana  - / -";
        if (_staminaText != null)
            _staminaText.text = party != null
                ? $"Stamina  {party.CurrentStamina} / {PlayerParty.BaseStamina}"
                : "Stamina  - / -";
        if (_unitCountText != null)
            _unitCountText.text = party != null
                ? $"Units  {party.Units.Count}"
                : "Units  -";
    }
}

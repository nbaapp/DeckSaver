using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Manages the player's hand as a fan of cards at the bottom of the screen.
/// Instantiates CardView prefabs at runtime; layout is purely positional (no LayoutGroup).
/// Call Init() once, after BattleDeck.Instance is available.
/// </summary>
public class HandDisplay : MonoBehaviour
{
    public static HandDisplay Instance { get; private set; }

    [Header("References")]
    [SerializeField] private CardView _cardViewPrefab;

    // Fan geometry — tweak these in the Inspector if needed
    [Header("Fan Settings")]
    [SerializeField] private float fanRadius    = 900f;
    [SerializeField] private float maxHalfAngle = 18f;
    [SerializeField] private float anglePerCard = 6f;
    [SerializeField] private float fanBaseY     = 100f;
    [SerializeField] private float neighborPush = 22f;

    private readonly List<CardView> _views = new();
    private CardView _hoveredCard;
    private CardView _selectedCard;

    public CardView SelectedCard => _selectedCard;

    // -------------------------------------------------------------------------

    private void Awake() => Instance = this;

    public void Init()
    {
        BattleDeck.Instance.OnHandChanged += RefreshHand;
        RefreshHand();
    }

    private void OnDestroy()
    {
        if (BattleDeck.Instance != null)
            BattleDeck.Instance.OnHandChanged -= RefreshHand;
    }

    // -------------------------------------------------------------------------

    private void RefreshHand()
    {
        var hand = BattleDeck.Instance.Hand;

        // Remove excess views (destroy from the end)
        while (_views.Count > hand.Count)
        {
            int last = _views.Count - 1;
            Destroy(_views[last].gameObject);
            _views.RemoveAt(last);
        }

        // Add missing views
        while (_views.Count < hand.Count)
            _views.Add(Instantiate(_cardViewPrefab, transform));

        // Re-initialise every view in-place (resets hover/select state)
        _hoveredCard  = null;
        _selectedCard = null;
        CardTooltip.Instance?.Hide();
        GridInputHandler.Instance?.SetPendingCard(null);
        for (int i = 0; i < hand.Count; i++)
            _views[i].Init(hand[i], this);

        LayoutCards(animate: false);
    }

    private void LayoutCards(bool animate)
    {
        int n = _views.Count;
        if (n == 0) return;

        float halfAngle = Mathf.Min(maxHalfAngle, (n - 1) * anglePerCard * 0.5f);
        int   hovIdx    = _hoveredCard != null ? _views.IndexOf(_hoveredCard) : -1;

        // Restore natural sibling order (index in hand = render order, left behind right)
        for (int i = 0; i < n; i++)
            _views[i].transform.SetSiblingIndex(i);
        // Hovered card renders on top
        if (_hoveredCard != null)
            _hoveredCard.transform.SetAsLastSibling();

        for (int i = 0; i < n; i++)
        {
            float t   = n == 1 ? 0f : Mathf.Lerp(-halfAngle, halfAngle, (float)i / (n - 1));
            float rad = t * Mathf.Deg2Rad;

            float x = Mathf.Sin(rad) * fanRadius;
            float y = fanRadius * (Mathf.Cos(rad) - 1f) + fanBaseY;

            if (hovIdx >= 0 && i != hovIdx)
                x += i < hovIdx ? -neighborPush : neighborPush;

            _views[i].SetFanTransform(new Vector2(x, y), rotDeg: -t, animate);
        }
    }

    // -------------------------------------------------------------------------
    // Called by CardView

    public void OnCardHoverBegin(CardView card)
    {
        _hoveredCard = card;
        LayoutCards(animate: true);
    }

    public void OnCardHoverEnd(CardView card)
    {
        if (_hoveredCard == card) _hoveredCard = null;
        LayoutCards(animate: true);
    }

    public void OnCardClicked(CardView card)
    {
        if (TurnManager.Instance?.CurrentPhase != TurnPhase.PlayerTurn) return;

        bool wasSelected = card.IsSelected;
        _selectedCard?.SetSelected(false);
        _selectedCard = null;
        GridInputHandler.Instance?.SetPendingCard(null);

        if (!wasSelected)
        {
            PlayerMovementHandler.Instance?.ExitMoveMode(); // selecting a card exits move mode
            _selectedCard = card;
            card.SetSelected(true);
            GridInputHandler.Instance?.SetPendingCard(card.Data);
        }
    }

    /// <summary>Programmatically deselects the current card without toggling.</summary>
    public void DeselectCard()
    {
        if (_selectedCard == null) return;
        _selectedCard.SetSelected(false);
        _selectedCard = null;
        GridInputHandler.Instance?.SetPendingCard(null);
    }

    public void RefreshAffordability()
    {
        var player = PlayerEntity.Instance;
        foreach (var view in _views)
        {
            if (view == null || view.Data == null) continue;
            bool affordable = player == null || player.CurrentMana >= view.Data.ManaCost;
            view.SetAffordable(affordable);
        }
    }
}

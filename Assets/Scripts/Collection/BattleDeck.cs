using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Manages the runtime state of the player's deck during a battle:
/// draw pile, hand, discard pile, and destroyed pile.
///
/// Call InitForBattle(deck) at the start of each battle.
/// The source DeckData asset is never modified — all state is kept in private lists.
/// </summary>
public class BattleDeck : MonoBehaviour
{
    public static BattleDeck Instance { get; private set; }

    [Header("Settings")]
    [Tooltip("Target hand size — draw up to this at end of turn, or request discards if over.")]
    public int handSize = 5;

    [Header("Test")]
    [Tooltip("Optional: assign a deck here to auto-initialize on Start for testing.")]
    [SerializeField] private DeckData _testDeck;

    // --- Runtime piles ---
    private readonly List<CardData> _drawPile      = new();
    private readonly List<CardData> _hand          = new();
    private readonly List<CardData> _discardPile   = new();
    private readonly List<CardData> _destroyedPile = new();

    // --- Read-only views for UI ---
    public IReadOnlyList<CardData> Hand          => _hand;
    public IReadOnlyList<CardData> DrawPile      => _drawPile;
    public IReadOnlyList<CardData> DiscardPile   => _discardPile;
    public IReadOnlyList<CardData> DestroyedPile => _destroyedPile;

    // --- Events ---
    public event Action<CardData> OnCardDrawn;
    public event Action<CardData> OnCardDiscarded;
    public event Action<CardData> OnCardDestroyed;
    public event Action           OnDeckShuffled;
    /// <summary>Fired whenever the hand contents change (draw, discard, destroy, play).</summary>
    public event Action           OnHandChanged;
    /// <summary>
    /// Fired at end of turn when the player has more cards than handSize.
    /// Argument is the number of cards that must be discarded.
    /// The UI should call DiscardCard() that many times in response.
    /// </summary>
    public event Action<int> OnDiscardRequired;

    private void Awake() => Instance = this;

    private void Start()
    {
        // Prefer the active run's (possibly fragment-swapped) card list.
        var run = RunCarrier.CurrentRun;
        if (run != null)
            InitForBattle(run.CurrentCards, run.Commander);
        else if (_testDeck != null)
            InitForBattle(_testDeck);
    }

    // -------------------------------------------------------------------------
    // Initialisation
    // -------------------------------------------------------------------------

    /// <summary>
    /// Set up the deck at the start of a battle from a DeckData asset.
    /// Shuffles a copy of the deck and draws an opening hand.
    /// </summary>
    public void InitForBattle(DeckData deck)
    {
        InitForBattle(deck.cards, deck.commander);
    }

    /// <summary>
    /// Set up the deck at the start of a battle from a runtime card list.
    /// Used by the run system, which may have mutated cards via fragment swaps.
    /// </summary>
    public void InitForBattle(System.Collections.Generic.List<CardData> cards, CommanderData commander)
    {
        _drawPile.Clear();
        _hand.Clear();
        _discardPile.Clear();
        _destroyedPile.Clear();

        if (PlayerEntity.Instance != null)
            PlayerEntity.Instance.commander = commander;

        _drawPile.AddRange(cards);
        Shuffle(_drawPile);
        DrawToHandSize();
    }

    // -------------------------------------------------------------------------
    // Drawing
    // -------------------------------------------------------------------------

    /// <summary>
    /// Draw one card from the draw pile into hand.
    /// Automatically reshuffles the discard pile if the draw pile is empty.
    /// Returns false only if both piles are empty.
    /// </summary>
    public bool DrawCard()
    {
        if (_drawPile.Count == 0)
        {
            if (_discardPile.Count == 0) return false;
            ShuffleDiscardIntoDraw();
        }

        var card = _drawPile[0];
        _drawPile.RemoveAt(0);
        _hand.Add(card);

        OnCardDrawn?.Invoke(card);
        OnHandChanged?.Invoke();
        BattleEvents.FireCardDrawn(card);
        return true;
    }

    /// <summary>Keep drawing until the hand reaches handSize (or no cards remain).</summary>
    public void DrawToHandSize()
    {
        while (_hand.Count < handSize)
            if (!DrawCard()) break;
    }

    // -------------------------------------------------------------------------
    // Playing
    // -------------------------------------------------------------------------

    /// <summary>
    /// Play a card from hand — sends it to the discard pile.
    /// This is the normal way a card leaves the hand during gameplay.
    /// </summary>
    public void PlayCard(CardData card)
    {
        if (!_hand.Remove(card)) return;
        _discardPile.Add(card);
        OnCardDiscarded?.Invoke(card);
        OnHandChanged?.Invoke();
    }

    // -------------------------------------------------------------------------
    // Discarding
    // -------------------------------------------------------------------------

    /// <summary>Move a specific card from hand to the discard pile.</summary>
    public void DiscardCard(CardData card)
    {
        if (!_hand.Remove(card)) return;
        _discardPile.Add(card);
        OnCardDiscarded?.Invoke(card);
        OnHandChanged?.Invoke();
        BattleEvents.FireCardDiscarded(card);
    }

    // -------------------------------------------------------------------------
    // Destroying
    // -------------------------------------------------------------------------

    /// <summary>
    /// Destroy a card from hand — removes it from play for the rest of this battle.
    /// It is tracked in the destroyed pile for UI display and restored after the battle.
    /// </summary>
    public void DestroyCard(CardData card)
    {
        if (!_hand.Remove(card)) return;
        _destroyedPile.Add(card);
        OnCardDestroyed?.Invoke(card);
        OnHandChanged?.Invoke();
    }

    /// <summary>Destroy a card directly from the discard pile (e.g. enemy effect).</summary>
    public void DestroyFromDiscard(CardData card)
    {
        if (!_discardPile.Remove(card)) return;
        _destroyedPile.Add(card);
        OnCardDestroyed?.Invoke(card);
    }

    // -------------------------------------------------------------------------
    // Turn boundaries
    // -------------------------------------------------------------------------

    /// <summary>
    /// Call this at the end of the player's turn.
    /// Draws up to handSize if under, or fires OnDiscardRequired if over.
    /// </summary>
    public void OnTurnEnd()
    {
        if (_hand.Count < handSize)
            DrawToHandSize();
        else if (_hand.Count > handSize)
            OnDiscardRequired?.Invoke(_hand.Count - handSize);
    }

    // -------------------------------------------------------------------------
    // Battle end
    // -------------------------------------------------------------------------

    /// <summary>
    /// Call this when the battle ends.
    /// Clears the destroyed pile so destroyed cards return to the deck next battle.
    /// The source DeckData asset is untouched throughout.
    /// </summary>
    public void OnBattleEnd()
    {
        _destroyedPile.Clear();
    }

    // -------------------------------------------------------------------------
    // Utilities
    // -------------------------------------------------------------------------

    private void ShuffleDiscardIntoDraw()
    {
        _drawPile.AddRange(_discardPile);
        _discardPile.Clear();
        Shuffle(_drawPile);
        OnDeckShuffled?.Invoke();
    }

    private static void Shuffle<T>(List<T> list)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = UnityEngine.Random.Range(0, i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
    }
}

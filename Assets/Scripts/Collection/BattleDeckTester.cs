using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Keyboard tester for BattleDeck. Attach to any GameObject in the scene.
/// All output goes to the Console.
///
///   D — Draw one card
///   P — Play first card in hand (→ discard pile)
///   X — Discard first card in hand (→ discard pile)
///   K — Destroy first card in hand (→ destroyed pile)
///   T — End turn (draw/discard to hand size)
///   H — Print full hand state
/// </summary>
public class BattleDeckTester : MonoBehaviour
{
    private void OnCardDrawn(CardData c)      => Log($"Drew: <b>{c.CardName}</b>");
    private void OnCardDiscarded(CardData c)  => Log($"Discarded: <b>{c.CardName}</b>");
    private void OnCardDestroyed(CardData c)  => Log($"Destroyed: <b>{c.CardName}</b>");
    private void OnDeckShuffled()             => Log("Discard shuffled back into draw pile.");
    private void OnDiscardRequired(int n)     => Log($"Must discard {n} card(s) — press X.");

    private void Start()
    {
        BattleDeck.Instance.OnCardDrawn       += OnCardDrawn;
        BattleDeck.Instance.OnCardDiscarded   += OnCardDiscarded;
        BattleDeck.Instance.OnCardDestroyed   += OnCardDestroyed;
        BattleDeck.Instance.OnDeckShuffled    += OnDeckShuffled;
        BattleDeck.Instance.OnDiscardRequired += OnDiscardRequired;
    }

    private void OnDestroy()
    {
        if (BattleDeck.Instance == null) return;
        BattleDeck.Instance.OnCardDrawn       -= OnCardDrawn;
        BattleDeck.Instance.OnCardDiscarded   -= OnCardDiscarded;
        BattleDeck.Instance.OnCardDestroyed   -= OnCardDestroyed;
        BattleDeck.Instance.OnDeckShuffled    -= OnDeckShuffled;
        BattleDeck.Instance.OnDiscardRequired -= OnDiscardRequired;
    }

    private void Update()
    {
        var kb = Keyboard.current;
        if (kb == null) return;

        if (kb.dKey.wasPressedThisFrame) DrawOne();
        if (kb.pKey.wasPressedThisFrame) PlayFirst();
        if (kb.xKey.wasPressedThisFrame) DiscardFirst();
        if (kb.kKey.wasPressedThisFrame) DestroyFirst();
        if (kb.tKey.wasPressedThisFrame) EndTurn();
        if (kb.hKey.wasPressedThisFrame) PrintState();
    }

    private void DrawOne()
    {
        bool drew = BattleDeck.Instance.DrawCard();
        if (!drew) Log("Nothing left to draw.");
    }

    private void PlayFirst()
    {
        var hand = BattleDeck.Instance.Hand;
        if (hand.Count == 0) { Log("Hand is empty."); return; }
        BattleDeck.Instance.PlayCard(hand[0]);
    }

    private void DiscardFirst()
    {
        var hand = BattleDeck.Instance.Hand;
        if (hand.Count == 0) { Log("Hand is empty."); return; }
        BattleDeck.Instance.DiscardCard(hand[0]);
    }

    private void DestroyFirst()
    {
        var hand = BattleDeck.Instance.Hand;
        if (hand.Count == 0) { Log("Hand is empty."); return; }
        BattleDeck.Instance.DestroyCard(hand[0]);
    }

    private void EndTurn() => BattleDeck.Instance.OnTurnEnd();

    private void PrintState()
    {
        var bd = BattleDeck.Instance;
        System.Text.StringBuilder sb = new();
        sb.AppendLine($"=== Deck State ===  Draw: {bd.DrawPile.Count}  Discard: {bd.DiscardPile.Count}  Destroyed: {bd.DestroyedPile.Count}");
        sb.Append("Hand: ");
        if (bd.Hand.Count == 0) sb.Append("(empty)");
        else foreach (var c in bd.Hand) sb.Append($"[{c.CardName}] ");
        Log(sb.ToString());
    }

    private static void Log(string msg) => Debug.Log($"[BattleDeck] {msg}");
}

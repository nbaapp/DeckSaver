using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// A player-built deck: a fixed-size list of Cards assembled from fragments in the hub.
/// </summary>
[CreateAssetMenu(fileName = "NewDeck", menuName = "DeckSaver/Deck")]
public class DeckData : ScriptableObject
{
    public string       deckName  = "My Deck";
    public CommanderData commander;

    public const int MaxSize = 20;
    public List<CardData> cards = new();

    public bool IsFull  => cards.Count >= MaxSize;
    public bool IsValid => cards.Count == MaxSize;
}

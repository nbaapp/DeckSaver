using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// A player-built deck: a commander plus a list of cards assembled from fragments in the hub.
/// </summary>
[CreateAssetMenu(fileName = "NewDeck", menuName = "DeckSaver/Deck")]
public class DeckData : ScriptableObject
{
    public string       deckName  = "My Deck";
    public CommanderData commander;

    public List<CardData> cards = new();

    public bool IsValid => cards.Count > 0;
}

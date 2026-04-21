using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Designer-populated registry of every possible Commander in the game.
/// Commanders are unlocked through progression milestones, not fragment forging.
/// </summary>
[CreateAssetMenu(fileName = "CommanderRegistry", menuName = "DeckSaver/Commander Registry")]
public class CommanderRegistry : ScriptableObject
{
    public List<CommanderData> allCommanders = new();
}

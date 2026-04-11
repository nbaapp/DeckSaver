using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// A run-level passive upgrade the player can earn between encounters.
/// Uses the same PassiveEffect system as Commander passives, so any
/// trigger/effect combination that works on a Commander works here too.
/// </summary>
[CreateAssetMenu(fileName = "NewBoon", menuName = "DeckSaver/Run/Boon")]
public class BoonData : ScriptableObject
{
    public string boonName;
    [TextArea] public string description;
    public Sprite icon;

    [Tooltip("Passive effects applied for the remainder of the run.")]
    public List<PassiveEffect> effects = new();
}

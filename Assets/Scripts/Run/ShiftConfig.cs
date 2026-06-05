using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Configuration for the Shift alternate-ending path.
/// The shift map is a linear sequence of encounters followed by a camp and a unique boss.
/// </summary>
[CreateAssetMenu(fileName = "NewShiftConfig", menuName = "DeckSaver/Run/Shift Config")]
public class ShiftConfig : ScriptableObject
{
    [Header("Shift Path")]
    [Tooltip("Encounters randomly drawn for the shift path's battle nodes.")]
    public List<EncounterDefinition> shiftEncounterPool = new();

    [Tooltip("The unique boss encounter at the end of the shift path.")]
    public EncounterDefinition shiftBossEncounter;

    [Tooltip("Total number of content nodes on the shift path (encounters + camp + boss). Default: 7 (5 encounters, 1 camp, 1 boss).")]
    public int nodeCount = 7;
}

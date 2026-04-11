using UnityEngine;

/// <summary>
/// Stores permanent player progress that persists across runs.
/// Currently a shell — permanent progression elements will be added
/// here as the design solidifies.
///
/// Assign a single instance of this asset in a Resources folder or
/// via a GameManager so all systems can access the same object.
/// </summary>
[CreateAssetMenu(fileName = "PlayerProgress", menuName = "DeckSaver/Player Progress")]
public class PlayerProgress : ScriptableObject
{
    // Permanent progression fields go here as they are designed.
    // Examples of what may land here:
    //   public int totalRunsCompleted;
    //   public List<EffectFragmentData> unlockedEffects;
    //   public List<ModifierFragmentData> unlockedModifiers;
    //   public List<CommanderData> unlockedCommanders;
}

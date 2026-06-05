using UnityEngine;

/// <summary>
/// Configuration for one act of a run. Holds the three Front options the player can choose from.
/// </summary>
[CreateAssetMenu(fileName = "NewActConfig", menuName = "DeckSaver/Run/Act Config")]
public class ActConfig : ScriptableObject
{
    [Tooltip("The three front options available in this act.")]
    public FrontConfig[] fronts = new FrontConfig[3];
}

using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "NewCommander", menuName = "DeckSaver/Commander")]
public class CommanderData : ScriptableObject
{
    public string commanderName;
    public Sprite artwork;

    [Header("Active Ability")]
    [TextArea] public string activeFlavorText;
    public int activesPerBattle = 1;
    public List<CardEffect> activeEffects = new();
    // If null, the active ability affects all enemies (no area targeting).
    public ModifierFragmentData activeArea;

    [Header("Passive Ability")]
    [TextArea] public string passiveFlavorText;
    public List<PassiveEffect> passiveEffects = new();

    [Tooltip("Always-on keyword overlays granted by this Commander. Cards matching filterKeywords gain grantKeywords and lose stripKeywords.")]
    public List<KeywordOverlayRule> keywordOverlays = new();
}

using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "NewModifierFragment", menuName = "DeckSaver/Modifier Fragment")]
public class ModifierFragmentData : ScriptableObject
{
    // fragmentName should be a noun or verb (e.g. "Strike", "Sweep", "Volley")
    public string fragmentName;
    [TextArea] public string flavorText;

    // Artwork that represents this modifier visually on a composite card
    public Sprite artwork;

    public PlacementType placementType;

    // Tile positions relative to origin (0,0), which maps to the placement anchor at play time.
    public List<TileData> tiles = new();

    // Applied to the final effect value after all per-tile modifiers.
    public List<TileModifier> globalModifiers = new();

    // ── Mana cost (designer-facing; hidden from player) ───────────────────────
    [Header("Mana Cost (hidden from player)")]
    [Tooltip("Cost adjustment contributed by this modifier. Negative values make the composite card cheaper.")]
    public int baseCost = 0;

    [Header("Movement")]
    [Tooltip("If true, playing this card moves the player in the aimed direction.")]
    public bool movesPlayer = false;
    [Tooltip("Tiles to move when movesPlayer is true.")]
    public int moveDistance = 1;

    // ── Upgrade chain ─────────────────────────────────────────────────────────
    [Header("Upgrade Chain")]
    [Tooltip("The upgraded version of this fragment. Null if this is already the top tier.")]
    public ModifierFragmentData upgradeVersion;

    [Tooltip("The base version of this fragment. Null if this is the base tier.")]
    public ModifierFragmentData baseVersion;

    public bool CanUpgrade  => upgradeVersion != null;
    public bool IsUpgraded  => baseVersion    != null;
}

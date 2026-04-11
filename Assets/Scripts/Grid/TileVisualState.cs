public enum TileVisualState
{
    Normal,
    Highlighted,  // hovered or selected
    Targeted,     // will be hit by the current card
    Occupied,     // has an entity on it (player, enemy)
    Hazard        // lingering hazard tile
}

using System;
using System.Collections.Generic;
using UnityEngine;

// Represents a single tile in a right card's area pattern.
// Position is relative to the card's origin (0,0).
// For DirectionalFromPlayer cards, positions are defined facing "up" and rotated at play time.
[Serializable]
public class TileData
{
    public Vector2Int position;
    public List<TileModifier> modifiers = new();
}

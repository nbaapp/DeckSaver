using System;

[Serializable]
public class TileModifier
{
    public TileModifierType type;
    public float value;
}

public enum TileModifierType
{
    Multiply,   // e.g. 2.0 = double effect, 0.5 = half effect
    FlatAdd     // e.g. 5 = +5 to effect value, -5 = -5
}

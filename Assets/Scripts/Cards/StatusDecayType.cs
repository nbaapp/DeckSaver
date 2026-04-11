// Determines how a status effect's value is interpreted and how it decays.
//
//  Normal      — value = duration (rounds) AND magnitude. Decays each round.
//  Eternal     — never decays. value = magnitude only.
//  Flat        — value = duration only. Effect is binary (no magnitude scaling).
//  EternalFlat — never decays, binary effect. Value is ignored entirely.

public enum StatusDecayType
{
    Normal,
    Eternal,
    Flat,
    EternalFlat
}

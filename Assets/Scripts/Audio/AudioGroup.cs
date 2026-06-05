/// <summary>
/// Logical channel a sound routes through. Maps 1:1 to AudioMixerGroups
/// configured on the AudioManager prefab.
/// </summary>
public enum AudioGroup
{
    Master,
    Music,
    SFX,
    UI,
}

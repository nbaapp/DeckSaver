/// <summary>
/// How AudioManager.PlayMusic should swap from the current track.
/// </summary>
public enum MusicTransition
{
    /// <summary>Stop the current track instantly and start the new one at full volume.</summary>
    Stop,
    /// <summary>Crossfade: fade the old track out while the new one fades in.</summary>
    Fade,
}

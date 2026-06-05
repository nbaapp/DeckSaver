using UnityEngine;

/// <summary>
/// Designer-tunable sound asset. One SoundData represents one logical sound
/// (e.g. "card play"); it may carry multiple clip variations and per-play
/// volume / pitch jitter so the same event doesn't play identically twice.
///
/// SFX should route through <see cref="AudioGroup.SFX"/> or <see cref="AudioGroup.UI"/>;
/// looping tracks should use <see cref="AudioGroup.Music"/> with <see cref="loop"/> true.
/// </summary>
[CreateAssetMenu(fileName = "NewSound", menuName = "DeckSaver/Audio/Sound Data")]
public class SoundData : ScriptableObject
{
    [Tooltip("One or more clips. A random one is picked each time the sound plays.")]
    public AudioClip[] clips;

    [Tooltip("Mixer channel this sound routes through.")]
    public AudioGroup group = AudioGroup.SFX;

    [Tooltip("Base volume multiplier (0–1). Combined with mixer volume.")]
    [Range(0f, 1f)] public float volume = 1f;

    [Tooltip("Random volume jitter applied around the base. 0 = no jitter.")]
    [Range(0f, 1f)] public float volumeVariance = 0f;

    [Tooltip("Base pitch.")]
    [Range(0.1f, 3f)] public float pitch = 1f;

    [Tooltip("Random pitch jitter applied around the base. 0 = no jitter.")]
    [Range(0f, 1f)] public float pitchVariance = 0f;

    [Tooltip("Loop the clip — required for music tracks.")]
    public bool loop = false;

    /// <summary>Pick a random clip from the variations, or null if none assigned.</summary>
    public AudioClip PickClip()
    {
        if (clips == null || clips.Length == 0) return null;
        return clips[Random.Range(0, clips.Length)];
    }

    /// <summary>Resolve a final volume sample using the variance.</summary>
    public float SampleVolume()
    {
        float jitter = volumeVariance > 0f ? Random.Range(-volumeVariance, volumeVariance) : 0f;
        return Mathf.Clamp01(volume + jitter);
    }

    /// <summary>Resolve a final pitch sample using the variance.</summary>
    public float SamplePitch()
    {
        float jitter = pitchVariance > 0f ? Random.Range(-pitchVariance, pitchVariance) : 0f;
        return Mathf.Clamp(pitch + jitter, 0.1f, 3f);
    }
}

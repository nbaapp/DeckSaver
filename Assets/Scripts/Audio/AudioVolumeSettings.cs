using UnityEngine;

/// <summary>
/// PlayerPrefs-backed volume persistence. Stores one float per AudioGroup,
/// always in the 0–1 linear range (dB conversion happens in AudioManager).
///
/// Named to avoid the global UnityEngine.AudioSettings type.
/// </summary>
public static class AudioVolumeSettings
{
    private const string KeyPrefix = "DeckSaver.Audio.Volume.";
    private const float  DefaultVolume = 0.8f;

    public static float GetVolume(AudioGroup group) =>
        PlayerPrefs.GetFloat(KeyFor(group), DefaultVolume);

    public static void SetVolume(AudioGroup group, float linear01)
    {
        PlayerPrefs.SetFloat(KeyFor(group), Mathf.Clamp01(linear01));
        PlayerPrefs.Save();
    }

    /// <summary>Push every persisted value through the manager (called once on boot).</summary>
    public static void ApplyAllToManager(AudioManager manager)
    {
        if (manager == null) return;
        foreach (AudioGroup g in System.Enum.GetValues(typeof(AudioGroup)))
            manager.ApplyVolume(g, GetVolume(g));
    }

    private static string KeyFor(AudioGroup group) => KeyPrefix + group;
}

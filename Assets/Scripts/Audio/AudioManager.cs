using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;

/// <summary>
/// Single global audio runtime. Auto-bootstraps from a prefab in
/// Resources/AudioManager so any scene (including the Battle scene loaded
/// directly) gets sound without manual placement.
///
/// Architecture:
///   • One AudioMixer with four exposed parameters: MasterVol / MusicVol / SFXVol / UIVol
///   • Four mixer groups (Master / Music / SFX / UI) referenced from this manager
///   • A pool of AudioSources used for SFX one-shots, routed to the right group
///   • Two AudioSources reserved for music (current + next, used during crossfade)
///
/// Volumes are kept linear (0–1) in code and converted to dB for the mixer.
/// Persistence lives in <see cref="AudioVolumeSettings"/>.
/// </summary>
public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance { get; private set; }

    [System.Serializable]
    public struct GroupBinding
    {
        public AudioGroup group;
        public AudioMixerGroup mixerGroup;
        [Tooltip("Exposed parameter name on the mixer for this group's volume (linear→dB).")]
        public string exposedVolumeParam;
    }

    [Header("Mixer")]
    [SerializeField] private AudioMixer _mixer;
    [SerializeField] private GroupBinding[] _groups;

    [Header("Pooling")]
    [Tooltip("Pre-allocated SFX sources. Grows on demand if all are busy.")]
    [SerializeField] private int _initialPoolSize = 8;

    [Header("Music")]
    [Tooltip("Default crossfade duration when MusicTransition.Fade is requested without an override.")]
    [SerializeField] private float _defaultFadeSeconds = 1f;

    private readonly List<AudioSource> _sfxPool = new();
    private AudioSource _musicA;
    private AudioSource _musicB;
    private AudioSource _musicCurrent;        // the one currently considered "playing"
    private SoundData  _currentTrack;
    private Coroutine  _musicRoutine;

    // ── Bootstrap ─────────────────────────────────────────────────────────────

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap()
    {
        if (Instance != null) return;

        var prefab = Resources.Load<GameObject>("AudioManager");
        if (prefab == null)
        {
            Debug.LogWarning("[AudioManager] No Resources/AudioManager prefab found — audio is disabled. " +
                             "Create the prefab as described in Assets/Audio/SETUP.txt.");
            return;
        }

        var go = Instantiate(prefab);
        go.name = "AudioManager";
        DontDestroyOnLoad(go);
    }

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        BuildPool();
        BuildMusicSources();

        AudioVolumeSettings.ApplyAllToManager(this);
    }

    // ── Public API: SFX ───────────────────────────────────────────────────────

    /// <summary>Fire-and-forget one-shot. Safe to call with null SoundData (no-op).</summary>
    public void PlaySFX(SoundData sound)
    {
        if (sound == null) return;
        var clip = sound.PickClip();
        if (clip == null) return;

        var src = AcquireSfxSource();
        src.outputAudioMixerGroup = ResolveMixerGroup(sound.group);
        src.clip   = clip;
        src.volume = sound.SampleVolume();
        src.pitch  = sound.SamplePitch();
        src.loop   = false;
        src.Play();
    }

    // ── Public API: Music ─────────────────────────────────────────────────────

    /// <summary>
    /// Replace whatever is playing with <paramref name="track"/>. Pass null to stop music.
    /// Same-track replays are ignored unless <paramref name="force"/> is true.
    /// </summary>
    public void PlayMusic(SoundData track, MusicTransition transition = MusicTransition.Fade,
                          float fadeSeconds = -1f, bool force = false)
    {
        if (!force && track == _currentTrack) return;
        _currentTrack = track;

        if (_musicRoutine != null) StopCoroutine(_musicRoutine);

        if (track == null)
        {
            _musicRoutine = StartCoroutine(StopMusicRoutine(transition, fadeSeconds));
            return;
        }

        var clip = track.PickClip();
        if (clip == null)
        {
            Debug.LogWarning($"[AudioManager] Music track {track.name} has no clips assigned.");
            return;
        }

        _musicRoutine = StartCoroutine(SwapMusicRoutine(track, clip, transition, fadeSeconds));
    }

    /// <summary>Convenience for stopping all music without forcing a track null check at callsites.</summary>
    public void StopMusic(MusicTransition transition = MusicTransition.Fade, float fadeSeconds = -1f) =>
        PlayMusic(null, transition, fadeSeconds);

    // ── Public API: Volume ────────────────────────────────────────────────────

    /// <summary>Set a group's volume (0–1 linear). Persists via AudioVolumeSettings.</summary>
    public void SetVolume(AudioGroup group, float linear01)
    {
        linear01 = Mathf.Clamp01(linear01);
        ApplyVolume(group, linear01);
        AudioVolumeSettings.SetVolume(group, linear01);
    }

    /// <summary>Apply without persisting — used internally during boot.</summary>
    public void ApplyVolume(AudioGroup group, float linear01)
    {
        if (_mixer == null) return;
        var binding = FindBinding(group);
        if (string.IsNullOrEmpty(binding.exposedVolumeParam)) return;
        _mixer.SetFloat(binding.exposedVolumeParam, LinearToDecibel(linear01));
    }

    public float GetVolume(AudioGroup group) => AudioVolumeSettings.GetVolume(group);

    // ── Internal: pool management ─────────────────────────────────────────────

    private void BuildPool()
    {
        for (int i = 0; i < _initialPoolSize; i++)
            _sfxPool.Add(CreateSfxSource());
    }

    private AudioSource CreateSfxSource()
    {
        var go = new GameObject("SfxSource");
        go.transform.SetParent(transform, false);
        var src = go.AddComponent<AudioSource>();
        src.playOnAwake = false;
        src.spatialBlend = 0f; // 2D
        return src;
    }

    private AudioSource AcquireSfxSource()
    {
        foreach (var s in _sfxPool)
            if (!s.isPlaying) return s;
        // Pool exhausted — grow rather than steal mid-playback.
        var fresh = CreateSfxSource();
        _sfxPool.Add(fresh);
        return fresh;
    }

    private void BuildMusicSources()
    {
        _musicA = CreateMusicSource("MusicA");
        _musicB = CreateMusicSource("MusicB");
        _musicCurrent = _musicA;
    }

    private AudioSource CreateMusicSource(string name)
    {
        var go = new GameObject(name);
        go.transform.SetParent(transform, false);
        var src = go.AddComponent<AudioSource>();
        src.playOnAwake = false;
        src.spatialBlend = 0f;
        src.loop = true;
        src.outputAudioMixerGroup = ResolveMixerGroup(AudioGroup.Music);
        return src;
    }

    // ── Internal: music transitions ───────────────────────────────────────────

    private IEnumerator SwapMusicRoutine(SoundData track, AudioClip clip,
                                         MusicTransition transition, float fadeSeconds)
    {
        AudioSource outgoing = _musicCurrent;
        AudioSource incoming = (outgoing == _musicA) ? _musicB : _musicA;
        _musicCurrent = incoming;

        incoming.clip   = clip;
        incoming.loop   = track.loop;
        incoming.pitch  = track.SamplePitch();
        incoming.outputAudioMixerGroup = ResolveMixerGroup(track.group);

        float target = track.SampleVolume();

        if (transition == MusicTransition.Stop || fadeSeconds == 0f)
        {
            outgoing.Stop();
            outgoing.volume = 0f;
            incoming.volume = target;
            incoming.Play();
            yield break;
        }

        float dur = fadeSeconds > 0f ? fadeSeconds : _defaultFadeSeconds;
        float startOut = outgoing.volume;

        incoming.volume = 0f;
        incoming.Play();

        float t = 0f;
        while (t < dur)
        {
            t += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(t / dur);
            outgoing.volume = Mathf.Lerp(startOut, 0f, k);
            incoming.volume = Mathf.Lerp(0f, target, k);
            yield return null;
        }
        outgoing.Stop();
        outgoing.volume = 0f;
        incoming.volume = target;
    }

    private IEnumerator StopMusicRoutine(MusicTransition transition, float fadeSeconds)
    {
        if (_musicCurrent == null) yield break;

        if (transition == MusicTransition.Stop || fadeSeconds == 0f)
        {
            _musicA.Stop(); _musicB.Stop();
            yield break;
        }

        float dur = fadeSeconds > 0f ? fadeSeconds : _defaultFadeSeconds;
        float startA = _musicA.volume;
        float startB = _musicB.volume;
        float t = 0f;
        while (t < dur)
        {
            t += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(t / dur);
            _musicA.volume = Mathf.Lerp(startA, 0f, k);
            _musicB.volume = Mathf.Lerp(startB, 0f, k);
            yield return null;
        }
        _musicA.Stop(); _musicB.Stop();
        _musicA.volume = 0f; _musicB.volume = 0f;
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private AudioMixerGroup ResolveMixerGroup(AudioGroup group)
    {
        var binding = FindBinding(group);
        return binding.mixerGroup;
    }

    private GroupBinding FindBinding(AudioGroup group)
    {
        if (_groups != null)
            foreach (var b in _groups)
                if (b.group == group) return b;
        return default;
    }

    /// <summary>
    /// Convert a 0–1 linear "fader" value to mixer dB. 0 maps to -80 dB (effective mute);
    /// 1 maps to 0 dB. Uses log10 so the curve sounds perceptually linear.
    /// </summary>
    public static float LinearToDecibel(float linear01)
    {
        if (linear01 <= 0.0001f) return -80f;
        return Mathf.Log10(linear01) * 20f;
    }
}

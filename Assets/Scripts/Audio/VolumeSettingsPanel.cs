using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Stub settings UI: four sliders, one per AudioGroup. Drop this on a panel
/// once you have a settings/pause menu, wire the slider references, and the
/// component handles the rest.
///
/// Slider values are 0–1 linear. Reads/writes via AudioManager so changes
/// affect the running mix immediately and persist across sessions.
/// </summary>
public class VolumeSettingsPanel : MonoBehaviour
{
    [SerializeField] private Slider _masterSlider;
    [SerializeField] private Slider _musicSlider;
    [SerializeField] private Slider _sfxSlider;
    [SerializeField] private Slider _uiSlider;

    private void OnEnable()
    {
        Bind(_masterSlider, AudioGroup.Master);
        Bind(_musicSlider,  AudioGroup.Music);
        Bind(_sfxSlider,    AudioGroup.SFX);
        Bind(_uiSlider,     AudioGroup.UI);
    }

    private void OnDisable()
    {
        Unbind(_masterSlider);
        Unbind(_musicSlider);
        Unbind(_sfxSlider);
        Unbind(_uiSlider);
    }

    private static void Bind(Slider slider, AudioGroup group)
    {
        if (slider == null) return;
        slider.minValue = 0f;
        slider.maxValue = 1f;
        slider.SetValueWithoutNotify(AudioVolumeSettings.GetVolume(group));
        slider.onValueChanged.AddListener(v => AudioManager.Instance?.SetVolume(group, v));
    }

    private static void Unbind(Slider slider)
    {
        if (slider == null) return;
        slider.onValueChanged.RemoveAllListeners();
    }
}

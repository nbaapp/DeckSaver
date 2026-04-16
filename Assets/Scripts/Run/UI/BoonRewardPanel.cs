using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Displays a set of boon choices after a Hard Conflict or Boss battle.
/// The player picks one; it is added to their active boons for the rest of the run.
///
/// Wire up _boonSlotPrefab (a prefab with BoonOfferView) and _slotParent in the inspector.
/// Call Show() with the pre-generated choices and an onComplete callback.
/// </summary>
public class BoonRewardPanel : MonoBehaviour
{
    [Header("Header")]
    [SerializeField] private TextMeshProUGUI _headerText;

    [Header("Boon Slots")]
    [Tooltip("Prefab with a BoonOfferView component. Spawned once per choice.")]
    [SerializeField] private GameObject _boonSlotPrefab;
    [SerializeField] private Transform  _slotParent;

    [Header("Skip")]
    [Tooltip("Optional button to skip the boon reward entirely.")]
    [SerializeField] private Button _skipButton;

    // ── State ─────────────────────────────────────────────────────────────────

    private Action                 _onComplete;
    private readonly List<GameObject> _slots = new();

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>
    /// Show the panel with the given boon choices.
    /// header: e.g. "Choose a Boon"
    /// choices: boons to offer (already randomly drawn from the pool by RunSceneController)
    /// onComplete: called after the player picks or skips
    /// </summary>
    public void Show(string header, List<BoonData> choices, Action onComplete)
    {
        _onComplete = onComplete;
        gameObject.SetActive(true);

        if (_headerText) _headerText.text = header;

        // Clear previous slots
        foreach (var slot in _slots) Destroy(slot);
        _slots.Clear();

        if (_boonSlotPrefab != null && _slotParent != null)
        {
            foreach (var boon in choices)
            {
                var capturedBoon = boon;
                var slot         = Instantiate(_boonSlotPrefab, _slotParent);
                _slots.Add(slot);

                var view = slot.GetComponent<BoonOfferView>();
                view?.Populate(capturedBoon, () => Pick(capturedBoon));
            }
        }

        _skipButton?.onClick.RemoveAllListeners();
        _skipButton?.onClick.AddListener(Skip);
        _skipButton?.gameObject.SetActive(_skipButton != null);
    }

    public void Hide() => gameObject.SetActive(false);

    // ── Choices ───────────────────────────────────────────────────────────────

    private void Pick(BoonData boon)
    {
        RunCarrier.CurrentRun?.AddBoon(boon);
        Hide();
        _onComplete?.Invoke();
    }

    private void Skip()
    {
        Hide();
        _onComplete?.Invoke();
    }
}

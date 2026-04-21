using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The left panel in the hub deckbuilder.
///
/// Populates one FragmentToken per unique effect and modifier type in the player's
/// collection (tokens show available count badges).
/// </summary>
public class CollectionPanel : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Transform _effectsGrid;
    [SerializeField] private Transform _modifiersGrid;
    [SerializeField] private GameObject _fragmentTokenPrefab;

    private readonly List<FragmentToken> _effectTokens   = new();
    private readonly List<FragmentToken> _modifierTokens = new();

    // -------------------------------------------------------------------------

    void Start()
    {
        Populate();
        HubDeckBuilderState.Instance.OnStateChanged += OnStateChanged;
    }

    void OnDestroy()
    {
        if (HubDeckBuilderState.Instance != null)
            HubDeckBuilderState.Instance.OnStateChanged -= OnStateChanged;
    }

    // -------------------------------------------------------------------------

    void Populate()
    {
        var state = HubDeckBuilderState.Instance;
        var collection = state.collection;
        if (collection == null)
        {
            Debug.LogWarning("[CollectionPanel] PlayerCollection not assigned on HubDeckBuilderState.");
            return;
        }

        foreach (var stack in collection.effectFragments)
        {
            var go    = Instantiate(_fragmentTokenPrefab, _effectsGrid);
            var token = go.GetComponent<FragmentToken>();
            token.InitEffect(stack.fragment);
            _effectTokens.Add(token);
        }

        foreach (var stack in collection.modifierFragments)
        {
            var go    = Instantiate(_fragmentTokenPrefab, _modifiersGrid);
            var token = go.GetComponent<FragmentToken>();
            token.InitModifier(stack.fragment);
            _modifierTokens.Add(token);
        }
    }

    // -------------------------------------------------------------------------

    void OnStateChanged()
    {
        foreach (var t in _effectTokens)   t.RefreshCount();
        foreach (var t in _modifierTokens) t.RefreshCount();
    }
}

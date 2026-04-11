using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The left panel in the hub deckbuilder.
///
/// Populates one FragmentToken per unique effect and modifier type in the player's
/// collection (tokens show available count badges).
///
/// Reacts to commander slot changes to highlight or dim tokens:
///   - If one commander-draft half is filled, tokens that could pair with it to
///     form a Commander are highlighted gold; all others are dimmed.
///   - When neither half is filled, all tokens display normally.
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
        RefreshCounts();
        RefreshCommanderFilter();
    }

    void RefreshCounts()
    {
        foreach (var t in _effectTokens)   t.RefreshCount();
        foreach (var t in _modifierTokens) t.RefreshCount();
    }

    void RefreshCommanderFilter()
    {
        var state    = HubDeckBuilderState.Instance;
        var registry = state.commanderRegistry;

        var draftEffect   = state.CmdDraftEffect;
        var draftModifier = state.CmdDraftModifier;

        // No filtering needed when neither half is set or a commander is already chosen
        if ((draftEffect == null && draftModifier == null) || state.SelectedCommander != null)
        {
            SetAllHighlight(FragmentToken.HighlightState.Normal);
            return;
        }

        if (draftEffect != null && draftModifier == null)
        {
            // Effect placed → highlight modifier tokens that complete a commander
            var compatModifiers = new HashSet<ModifierFragmentData>(registry.CompatibleModifiers(draftEffect));
            foreach (var t in _effectTokens)
                t.SetHighlight(FragmentToken.HighlightState.Normal);
            foreach (var t in _modifierTokens)
                t.SetHighlight(compatModifiers.Contains(t.Modifier)
                    ? FragmentToken.HighlightState.CommanderMatch
                    : FragmentToken.HighlightState.Dimmed);
        }
        else if (draftModifier != null && draftEffect == null)
        {
            // Modifier placed → highlight effect tokens that complete a commander
            var compatEffects = new HashSet<EffectFragmentData>(registry.CompatibleEffects(draftModifier));
            foreach (var t in _modifierTokens)
                t.SetHighlight(FragmentToken.HighlightState.Normal);
            foreach (var t in _effectTokens)
                t.SetHighlight(compatEffects.Contains(t.Effect)
                    ? FragmentToken.HighlightState.CommanderMatch
                    : FragmentToken.HighlightState.Dimmed);
        }
    }

    void SetAllHighlight(FragmentToken.HighlightState hs)
    {
        foreach (var t in _effectTokens)   t.SetHighlight(hs);
        foreach (var t in _modifierTokens) t.SetHighlight(hs);
    }
}

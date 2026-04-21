using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Singleton managing the draft state for hub deck building.
///
/// The player builds a small number of custom cards (CustomSlotCount) from fragments.
/// The remaining slots up to TotalDeckSize are auto-filled with basic Strike/Block cards.
/// Commanders are selected from the player's unlocked collection — no forging.
///
/// Fragment assignments are tracked without consuming them from PlayerCollection
/// until the player confirms the deck (ConfirmDeck).
/// </summary>
public class HubDeckBuilderState : MonoBehaviour
{
    public static HubDeckBuilderState Instance { get; private set; }

    [Header("References")]
    public PlayerCollection collection;
    public BasicFragmentPool basicFragmentPool;

    [Header("Deck Configuration")]
    [Tooltip("Number of custom card slots the player can build from fragments.")]
    [SerializeField] private int _customSlotCount = 5;

    [Tooltip("Total number of cards in the deck. Remaining slots after custom cards are auto-filled with basic fragments.")]
    [SerializeField] private int _totalDeckSize = 20;

    public int CustomSlotCount => _customSlotCount;
    public int TotalDeckSize   => _totalDeckSize;

    // Custom card slots (only _customSlotCount are used)
    private EffectFragmentData[]   _slotEffects;
    private ModifierFragmentData[] _slotModifiers;

    // Selected commander for this run
    private CommanderData _selectedCommander;

    public CommanderData SelectedCommander => _selectedCommander;

    /// <summary>Fired whenever any draft assignment changes.</summary>
    public event Action OnStateChanged;

    void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
        _slotEffects   = new EffectFragmentData[_customSlotCount];
        _slotModifiers = new ModifierFragmentData[_customSlotCount];
    }

    // -------------------------------------------------------------------------
    // Available count

    /// <summary>How many of this effect fragment can still be assigned (collection total minus already-assigned).</summary>
    public int AvailableEffectCount(EffectFragmentData fragment)
    {
        int assigned = 0;
        for (int i = 0; i < _customSlotCount; i++)
            if (_slotEffects[i] == fragment) assigned++;
        return Mathf.Max(0, collection.CountEffect(fragment) - assigned);
    }

    /// <summary>How many of this modifier fragment can still be assigned.</summary>
    public int AvailableModifierCount(ModifierFragmentData fragment)
    {
        int assigned = 0;
        for (int i = 0; i < _customSlotCount; i++)
            if (_slotModifiers[i] == fragment) assigned++;
        return Mathf.Max(0, collection.CountModifier(fragment) - assigned);
    }

    // -------------------------------------------------------------------------
    // Slot accessors

    public EffectFragmentData   GetSlotEffect(int i)   => _slotEffects[i];
    public ModifierFragmentData GetSlotModifier(int i) => _slotModifiers[i];

    // -------------------------------------------------------------------------
    // Slot assignment

    /// <summary>
    /// Assigns an effect fragment to slot <paramref name="slotIndex"/>.
    /// Pass null to clear. Returns false if the fragment is unavailable.
    /// </summary>
    public bool TryAssignEffect(int slotIndex, EffectFragmentData fragment)
    {
        if (slotIndex < 0 || slotIndex >= _customSlotCount) return false;
        if (fragment != null && fragment != _slotEffects[slotIndex] && AvailableEffectCount(fragment) <= 0)
            return false;
        _slotEffects[slotIndex] = fragment;
        OnStateChanged?.Invoke();
        return true;
    }

    /// <summary>
    /// Assigns a modifier fragment to slot <paramref name="slotIndex"/>.
    /// Pass null to clear. Returns false if the fragment is unavailable.
    /// </summary>
    public bool TryAssignModifier(int slotIndex, ModifierFragmentData fragment)
    {
        if (slotIndex < 0 || slotIndex >= _customSlotCount) return false;
        if (fragment != null && fragment != _slotModifiers[slotIndex] && AvailableModifierCount(fragment) <= 0)
            return false;
        _slotModifiers[slotIndex] = fragment;
        OnStateChanged?.Invoke();
        return true;
    }

    public void ClearSlotEffect(int slotIndex)
    {
        if (slotIndex < 0 || slotIndex >= _customSlotCount) return;
        _slotEffects[slotIndex] = null;
        OnStateChanged?.Invoke();
    }

    public void ClearSlotModifier(int slotIndex)
    {
        if (slotIndex < 0 || slotIndex >= _customSlotCount) return;
        _slotModifiers[slotIndex] = null;
        OnStateChanged?.Invoke();
    }

    // -------------------------------------------------------------------------
    // Commander selection

    /// <summary>Selects an owned Commander for this run.</summary>
    public void SelectOwnedCommander(CommanderData commander)
    {
        if (commander == null || !collection.ownedCommanders.Contains(commander)) return;
        _selectedCommander = commander;
        OnStateChanged?.Invoke();
    }

    public void ClearCommanderSelection()
    {
        _selectedCommander = null;
        OnStateChanged?.Invoke();
    }

    // -------------------------------------------------------------------------
    // Preview helpers

    public string PreviewSlotName(int slotIndex)
    {
        if (slotIndex < 0 || slotIndex >= _customSlotCount) return null;
        var e = _slotEffects[slotIndex];
        var m = _slotModifiers[slotIndex];
        return e != null && m != null ? $"{e.fragmentName} {m.fragmentName}" : null;
    }

    public string PreviewSlotDescription(int slotIndex)
    {
        if (slotIndex < 0 || slotIndex >= _customSlotCount) return null;
        var e = _slotEffects[slotIndex];
        var m = _slotModifiers[slotIndex];
        if (e == null || m == null) return null;

        var temp = ScriptableObject.CreateInstance<CardData>();
        temp.effectFragment   = e;
        temp.modifierFragment = m;
        string desc = CardDescriptionGenerator.Full(temp);
        Destroy(temp);
        return desc;
    }

    public int PreviewSlotManaCost(int slotIndex)
    {
        if (slotIndex < 0 || slotIndex >= _customSlotCount) return 0;
        var e = _slotEffects[slotIndex];
        var m = _slotModifiers[slotIndex];
        if (e == null || m == null) return 0;
        int raw = e.baseCost + m.baseCost;
        return Mathf.Clamp(raw, e.minCost, e.maxCost);
    }

    public Color PreviewSlotColor(int slotIndex)
    {
        if (slotIndex < 0 || slotIndex >= _customSlotCount) return Color.gray;
        var e = _slotEffects[slotIndex];
        return e != null ? e.effectColor : Color.gray;
    }

    // -------------------------------------------------------------------------
    // Validation & confirmation

    public int FilledSlotCount()
    {
        int count = 0;
        for (int i = 0; i < _customSlotCount; i++)
            if (_slotEffects[i] != null && _slotModifiers[i] != null) count++;
        return count;
    }

    public bool IsReadyToConfirm() => _selectedCommander != null;

    /// <summary>
    /// Consumes all assigned fragments from the player's collection and returns a runtime DeckData.
    /// Custom slots that are unfilled, plus all remaining slots up to TotalDeckSize,
    /// are auto-filled with cards built from the BasicFragmentPool.
    /// Returns null if no commander is selected.
    /// </summary>
    public DeckData ConfirmDeck(string deckName = "My Deck")
    {
        if (!IsReadyToConfirm()) return null;

        var deck = ScriptableObject.CreateInstance<DeckData>();
        deck.deckName  = deckName;
        deck.commander = _selectedCommander;

        // Build custom cards and count how many slots need defaults
        int emptyCustomSlots = 0;
        for (int i = 0; i < _customSlotCount; i++)
        {
            if (_slotEffects[i] != null && _slotModifiers[i] != null)
            {
                var card = ScriptableObject.CreateInstance<CardData>();
                card.effectFragment   = _slotEffects[i];
                card.modifierFragment = _slotModifiers[i];
                card.name             = card.CardName;
                collection.ConsumeEffect(_slotEffects[i]);
                collection.ConsumeModifier(_slotModifiers[i]);
                deck.cards.Add(card);
            }
            else
            {
                emptyCustomSlots++;
            }
        }

        // Fill remaining slots with basic fragment cards
        int defaultCount = emptyCustomSlots + (_totalDeckSize - _customSlotCount);
        var defaults = BuildBasicCards(defaultCount);
        deck.cards.AddRange(defaults);

        return deck;
    }

    // -------------------------------------------------------------------------
    // Basic card construction from fragment pool

    /// <summary>
    /// Builds <paramref name="count"/> cards from the BasicFragmentPool.
    /// Effects and modifiers are each distributed evenly, then randomly paired
    /// while respecting excluded combinations.
    /// </summary>
    List<CardData> BuildBasicCards(int count)
    {
        if (basicFragmentPool == null
            || basicFragmentPool.basicEffects.Count == 0
            || basicFragmentPool.basicModifiers.Count == 0)
        {
            Debug.LogWarning("[HubDeckBuilderState] BasicFragmentPool is missing or empty. Cannot fill deck.");
            return new List<CardData>();
        }

        var effects   = DistributeEvenly(basicFragmentPool.basicEffects, count);
        var modifiers = DistributeEvenly(basicFragmentPool.basicModifiers, count);

        // Pair effects with modifiers, avoiding excluded combinations.
        // Shuffle modifiers first, then swap to resolve any exclusions.
        Shuffle(modifiers);

        for (int i = 0; i < count; i++)
        {
            if (!basicFragmentPool.IsCombinationExcluded(effects[i], modifiers[i]))
                continue;

            // Find a swap partner that resolves both positions
            bool resolved = false;
            for (int j = i + 1; j < count; j++)
            {
                bool swapFixesI = !basicFragmentPool.IsCombinationExcluded(effects[i], modifiers[j]);
                bool swapFixesJ = !basicFragmentPool.IsCombinationExcluded(effects[j], modifiers[i]);
                if (swapFixesI && swapFixesJ)
                {
                    (modifiers[i], modifiers[j]) = (modifiers[j], modifiers[i]);
                    resolved = true;
                    break;
                }
            }

            if (!resolved)
                Debug.LogWarning($"[HubDeckBuilderState] Could not avoid excluded combination: " +
                    $"{effects[i].fragmentName} + {modifiers[i].fragmentName}. " +
                    $"Check that exclusions don't make valid pairings impossible.");
        }

        var list = new List<CardData>(count);
        for (int i = 0; i < count; i++)
        {
            var card = ScriptableObject.CreateInstance<CardData>();
            card.effectFragment   = effects[i];
            card.modifierFragment = modifiers[i];
            card.name             = card.CardName;
            list.Add(card);
        }

        Shuffle(list);
        return list;
    }

    /// <summary>
    /// Distributes items from <paramref name="pool"/> evenly to fill <paramref name="count"/> slots.
    /// Each item appears floor(count/poolSize) times, with remainder distributed randomly.
    /// </summary>
    static List<T> DistributeEvenly<T>(List<T> pool, int count)
    {
        int poolSize = pool.Count;
        int each     = count / poolSize;
        int remainder = count % poolSize;

        var result = new List<T>(count);
        for (int i = 0; i < poolSize; i++)
            for (int j = 0; j < each; j++)
                result.Add(pool[i]);

        // Distribute remainder randomly
        var indices = new List<int>(poolSize);
        for (int i = 0; i < poolSize; i++) indices.Add(i);
        Shuffle(indices);
        for (int i = 0; i < remainder; i++)
            result.Add(pool[indices[i]]);

        return result;
    }

    static void Shuffle<T>(List<T> list)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = UnityEngine.Random.Range(0, i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
    }
}

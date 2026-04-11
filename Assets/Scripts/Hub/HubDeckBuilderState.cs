using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Singleton managing the draft state for hub deck building.
///
/// Fragment assignments to slots are tracked without consuming them from PlayerCollection
/// until the player confirms the deck (ConfirmDeck).
///
/// Exception: forging a Commander immediately consumes its two fragments and permanently
/// adds the Commander to the player's collection.
/// </summary>
public class HubDeckBuilderState : MonoBehaviour
{
    public static HubDeckBuilderState Instance { get; private set; }

    [Header("References")]
    public PlayerCollection collection;
    public CommanderRegistry commanderRegistry;

    // Normal card slots (20)
    private readonly EffectFragmentData[]   _slotEffects    = new EffectFragmentData[DeckData.MaxSize];
    private readonly ModifierFragmentData[] _slotModifiers  = new ModifierFragmentData[DeckData.MaxSize];

    // Commander slot (for forging a new commander)
    private EffectFragmentData   _cmdDraftEffect;
    private ModifierFragmentData _cmdDraftModifier;

    // Finalized commander for this run (set after forge or selecting an owned one)
    private CommanderData _selectedCommander;

    public CommanderData      SelectedCommander    => _selectedCommander;
    public EffectFragmentData CmdDraftEffect       => _cmdDraftEffect;
    public ModifierFragmentData CmdDraftModifier   => _cmdDraftModifier;

    /// <summary>Fired whenever any draft assignment changes.</summary>
    public event Action OnStateChanged;

    /// <summary>Fired after a Commander is successfully forged.</summary>
    public event Action<CommanderData> OnCommanderForged;

    void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
    }

    // -------------------------------------------------------------------------
    // Available count

    /// <summary>How many of this effect fragment can still be assigned (collection total minus already-assigned).</summary>
    public int AvailableEffectCount(EffectFragmentData fragment)
    {
        int assigned = 0;
        for (int i = 0; i < DeckData.MaxSize; i++)
            if (_slotEffects[i] == fragment) assigned++;
        if (_cmdDraftEffect == fragment) assigned++;
        return Mathf.Max(0, collection.CountEffect(fragment) - assigned);
    }

    /// <summary>How many of this modifier fragment can still be assigned.</summary>
    public int AvailableModifierCount(ModifierFragmentData fragment)
    {
        int assigned = 0;
        for (int i = 0; i < DeckData.MaxSize; i++)
            if (_slotModifiers[i] == fragment) assigned++;
        if (_cmdDraftModifier == fragment) assigned++;
        return Mathf.Max(0, collection.CountModifier(fragment) - assigned);
    }

    // -------------------------------------------------------------------------
    // Slot accessors

    public EffectFragmentData   GetSlotEffect(int i)   => _slotEffects[i];
    public ModifierFragmentData GetSlotModifier(int i) => _slotModifiers[i];

    // -------------------------------------------------------------------------
    // Normal slot assignment

    /// <summary>
    /// Assigns an effect fragment to slot <paramref name="slotIndex"/>.
    /// Pass null to clear. Returns false if the fragment is unavailable.
    /// </summary>
    public bool TryAssignEffect(int slotIndex, EffectFragmentData fragment)
    {
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
        if (fragment != null && fragment != _slotModifiers[slotIndex] && AvailableModifierCount(fragment) <= 0)
            return false;
        _slotModifiers[slotIndex] = fragment;
        OnStateChanged?.Invoke();
        return true;
    }

    public void ClearSlotEffect(int slotIndex)   { _slotEffects[slotIndex]   = null; OnStateChanged?.Invoke(); }
    public void ClearSlotModifier(int slotIndex) { _slotModifiers[slotIndex] = null; OnStateChanged?.Invoke(); }

    // -------------------------------------------------------------------------
    // Commander draft assignment

    public bool TryAssignCommanderEffect(EffectFragmentData fragment)
    {
        if (fragment != null && fragment != _cmdDraftEffect && AvailableEffectCount(fragment) <= 0)
            return false;
        _cmdDraftEffect = fragment;
        OnStateChanged?.Invoke();
        return true;
    }

    public bool TryAssignCommanderModifier(ModifierFragmentData fragment)
    {
        if (fragment != null && fragment != _cmdDraftModifier && AvailableModifierCount(fragment) <= 0)
            return false;
        _cmdDraftModifier = fragment;
        OnStateChanged?.Invoke();
        return true;
    }

    public void ClearCommanderDraftEffect()   { _cmdDraftEffect   = null; OnStateChanged?.Invoke(); }
    public void ClearCommanderDraftModifier() { _cmdDraftModifier = null; OnStateChanged?.Invoke(); }

    /// <summary>The CommanderData that the current draft fragments would forge, or null.</summary>
    public CommanderData GetDraftCommanderMatch()
        => commanderRegistry.FindMatch(_cmdDraftEffect, _cmdDraftModifier);

    // -------------------------------------------------------------------------
    // Commander forging (immediate, permanent)

    /// <summary>
    /// Immediately consumes the two commander draft fragments and permanently unlocks the commander.
    /// Sets it as the selected commander for this run.
    /// Returns false if the combo is invalid or already owned.
    /// </summary>
    public bool TryForgeCommander()
    {
        var commander = GetDraftCommanderMatch();
        if (commander == null) return false;

        if (collection.ownedCommanders.Contains(commander))
        {
            Debug.LogWarning($"Commander '{commander.commanderName}' is already owned.");
            return false;
        }

        if (collection.CountEffect(_cmdDraftEffect) <= 0 || collection.CountModifier(_cmdDraftModifier) <= 0)
            return false;

        collection.ConsumeEffect(_cmdDraftEffect);
        collection.ConsumeModifier(_cmdDraftModifier);
        collection.ownedCommanders.Add(commander);

        _cmdDraftEffect   = null;
        _cmdDraftModifier = null;
        _selectedCommander = commander;

        OnCommanderForged?.Invoke(commander);
        OnStateChanged?.Invoke();
        return true;
    }

    /// <summary>Selects an already-owned Commander for this run without forging.</summary>
    public void SelectOwnedCommander(CommanderData commander)
    {
        if (commander == null || !collection.ownedCommanders.Contains(commander)) return;
        _cmdDraftEffect    = null;
        _cmdDraftModifier  = null;
        _selectedCommander = commander;
        OnStateChanged?.Invoke();
    }

    public void ClearCommanderSelection()
    {
        _cmdDraftEffect    = null;
        _cmdDraftModifier  = null;
        _selectedCommander = null;
        OnStateChanged?.Invoke();
    }

    // -------------------------------------------------------------------------
    // Preview helpers (no consumption, no allocation)

    public string PreviewSlotName(int slotIndex)
    {
        var e = _slotEffects[slotIndex];
        var m = _slotModifiers[slotIndex];
        return e != null && m != null ? $"{e.fragmentName} {m.fragmentName}" : null;
    }

    public string PreviewSlotDescription(int slotIndex)
    {
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
        var e = _slotEffects[slotIndex];
        var m = _slotModifiers[slotIndex];
        if (e == null || m == null) return 0;
        int raw = e.baseCost + m.baseCost;
        return Mathf.Clamp(raw, e.minCost, e.maxCost);
    }

    public Color PreviewSlotColor(int slotIndex)
    {
        var e = _slotEffects[slotIndex];
        return e != null ? e.effectColor : Color.gray;
    }

    // -------------------------------------------------------------------------
    // Validation & confirmation

    public int FilledSlotCount()
    {
        int count = 0;
        for (int i = 0; i < DeckData.MaxSize; i++)
            if (_slotEffects[i] != null && _slotModifiers[i] != null) count++;
        return count;
    }

    public bool IsReadyToConfirm() => _selectedCommander != null;

    /// <summary>
    /// Consumes all assigned fragments from the player's collection and returns a runtime DeckData.
    /// Any unfilled slots are filled with default Strike and Block cards in a 50/50 split
    /// (ties broken randomly).
    /// Returns null if no commander is selected.
    /// </summary>
    public DeckData ConfirmDeck(string deckName = "My Deck")
    {
        if (!IsReadyToConfirm()) return null;

        // Determine which empty slots get defaults and in what ratio
        var emptyIndices = new List<int>();
        for (int i = 0; i < DeckData.MaxSize; i++)
            if (_slotEffects[i] == null || _slotModifiers[i] == null)
                emptyIndices.Add(i);

        var defaultAssignments = BuildDefaultAssignments(emptyIndices.Count);

        var deck = ScriptableObject.CreateInstance<DeckData>();
        deck.deckName  = deckName;
        deck.commander = _selectedCommander;

        int defaultIndex = 0;
        for (int i = 0; i < DeckData.MaxSize; i++)
        {
            CardData card;
            if (_slotEffects[i] != null && _slotModifiers[i] != null)
            {
                card = ScriptableObject.CreateInstance<CardData>();
                card.effectFragment   = _slotEffects[i];
                card.modifierFragment = _slotModifiers[i];
                card.name             = card.CardName;
                collection.ConsumeEffect(_slotEffects[i]);
                collection.ConsumeModifier(_slotModifiers[i]);
            }
            else
            {
                card = defaultAssignments[defaultIndex++];
            }
            deck.cards.Add(card);
        }

        return deck;
    }

    // -------------------------------------------------------------------------
    // Default card construction

    static List<CardData> BuildDefaultAssignments(int count)
    {
        int strikeCount = count / 2;
        int blockCount  = count - strikeCount;

        // Break ties randomly
        if (count % 2 == 1 && UnityEngine.Random.value < 0.5f)
        {
            strikeCount++;
            blockCount--;
        }

        var list = new List<CardData>(count);
        for (int i = 0; i < strikeCount; i++) list.Add(MakeDefaultCard(isStrike: true));
        for (int i = 0; i < blockCount;  i++) list.Add(MakeDefaultCard(isStrike: false));

        // Fisher-Yates shuffle so strikes/blocks are spread across slots
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = UnityEngine.Random.Range(0, i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }

        return list;
    }

    static CardData MakeDefaultCard(bool isStrike)
    {
        var effect = ScriptableObject.CreateInstance<EffectFragmentData>();
        effect.fragmentName = isStrike ? "Strike" : "Block";
        effect.baseCost     = 1;
        effect.minCost      = 1;
        effect.maxCost      = 1;
        effect.effectColor  = isStrike ? new Color(0.85f, 0.25f, 0.25f) : new Color(0.25f, 0.55f, 0.85f);
        effect.effects.Add(new CardEffect
        {
            type      = isStrike ? EffectType.Strike : EffectType.Block,
            baseValue = 3,
            hits      = 1
        });

        var modifier = ScriptableObject.CreateInstance<ModifierFragmentData>();
        // Leave fragmentName empty so CardName is just the effect name ("Strike" / "Block")
        modifier.fragmentName  = "";

        if (isStrike)
        {
            // One tile directly ahead in the aimed direction
            modifier.placementType = PlacementType.DirectionalFromPlayer;
            modifier.tiles.Add(new TileData { position = new Vector2Int(0, 1) });
        }
        else
        {
            // Centred on the player — hits the player's own tile (for self-targeting Block)
            modifier.placementType = PlacementType.CenteredOnPlayer;
            modifier.tiles.Add(new TileData { position = Vector2Int.zero });
        }

        var card = ScriptableObject.CreateInstance<CardData>();
        card.effectFragment   = effect;
        card.modifierFragment = modifier;
        card.name             = card.CardName;
        return card;
    }
}

using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// All mutable state for the current run. Created fresh by the Hub when
/// a run starts and carried between scenes via RunCarrier.
///
/// Segment layout (repeating until save):
///   [encounter 0 .. encounterCount-1] → [boss]
///
/// Post-save:
///   [postSave encounter 0 .. postSaveCount-1] → [final boss]
/// </summary>
public class RunState
{
    // ── Config ────────────────────────────────────────────────────────────────
    public RunConfig Config { get; }

    // ── Encounter position ────────────────────────────────────────────────────
    /// <summary>Which boss segment we are on (0-indexed). Increments after each boss.</summary>
    public int Segment { get; private set; }

    /// <summary>
    /// Index within the current segment (0 = first regular encounter).
    /// When this equals Config.encountersPerSegment, the current encounter is the boss.
    /// In post-save mode, indexes into the post-save encounter list.
    /// </summary>
    public int EncounterIndex { get; private set; }

    // ── Save state ────────────────────────────────────────────────────────────
    /// <summary>True once the player has chosen to save this run.</summary>
    public bool HasSaved { get; private set; }

    /// <summary>True after the player saves; no more rewards, encounters become linear.</summary>
    public bool IsPostSave { get; private set; }

    // ── Deck (mutable for fragment swaps) ────────────────────────────────────
    /// <summary>
    /// Working copy of the player's cards for this run.
    /// Fragment swaps create new runtime CardData instances here via
    /// ScriptableObject.CreateInstance so the original assets are never touched.
    /// </summary>
    public List<CardData> CurrentCards { get; } = new();
    public CommanderData Commander { get; }

    // ── Active boons ──────────────────────────────────────────────────────────
    public List<BoonData> ActiveBoons { get; } = new();

    // ── Unit health (persists across battles) ─────────────────────────────────
    /// <summary>
    /// Current HP of each surviving unit, in spawn order.
    /// Initialised to unitMaxHealth for each unit at run start.
    /// Updated after each won battle; units that die are removed permanently.
    /// Count == number of living units remaining.
    /// </summary>
    public List<int> UnitHealths { get; } = new();

    public int UnitCount => UnitHealths.Count;

    // ── Pre-computed encounter sequence ──────────────────────────────────────
    // Shuffled at segment start; rebuilt after each boss.
    private List<EncounterDefinition> _segmentSequence = new();

    // ── Constructor ───────────────────────────────────────────────────────────

    public RunState(RunConfig config, DeckData deck)
    {
        Config    = config;
        Commander = deck.commander;

        // Copy card references (originals untouched during swaps)
        CurrentCards.AddRange(deck.cards);

        // Initialise unit health for each starting unit
        for (int i = 0; i < config.startingUnitCount; i++)
            UnitHealths.Add(config.unitMaxHealth);

        BuildSegmentSequence();
    }

    // ── Encounter access ──────────────────────────────────────────────────────

    public bool IsAtBoss => !IsPostSave && EncounterIndex >= Config.encountersPerSegment;
    public bool IsAtFinalBoss => IsPostSave && EncounterIndex >= Config.postSaveEncounterCount;

    /// <summary>Returns the EncounterDefinition for the current position in the run.</summary>
    public EncounterDefinition CurrentEncounter()
    {
        if (IsPostSave)
        {
            if (IsAtFinalBoss)
                return Config.postSaveFinalBoss;

            if (EncounterIndex < _segmentSequence.Count)
                return _segmentSequence[EncounterIndex];

            Debug.LogWarning("[RunState] Post-save encounter index out of range.");
            return Config.postSaveFinalBoss;
        }

        if (IsAtBoss)
            return Config.bossEncounter;

        if (EncounterIndex < _segmentSequence.Count)
            return _segmentSequence[EncounterIndex];

        Debug.LogWarning("[RunState] Encounter index out of range.");
        return Config.bossEncounter;
    }

    /// <summary>
    /// Returns the pool to draw rewards from for the current encounter.
    /// Prefers the encounter's own override, then falls back to the run default.
    /// </summary>
    public RewardPoolData CurrentRewardPool()
    {
        var enc = CurrentEncounter();
        return (enc != null && enc.rewardPoolOverride != null)
            ? enc.rewardPoolOverride
            : Config.defaultRewardPool;
    }

    /// <summary>Advance to the next encounter. Call after the player claims their reward.</summary>
    public void AdvanceEncounter()
    {
        EncounterIndex++;

        // After the boss (non-post-save), stay at boss index until Save() or NextSegment() is called
    }

    /// <summary>Move to the next segment after a boss is defeated and save is declined.</summary>
    public void StartNextSegment()
    {
        Segment++;
        EncounterIndex = 0;
        BuildSegmentSequence();
    }

    /// <summary>Lock the run into post-save linear mode.</summary>
    public void Save()
    {
        HasSaved    = true;
        IsPostSave  = true;
        EncounterIndex = 0;
        BuildSegmentSequence(); // rebuild for post-save pool
    }

    // ── Reward generation ─────────────────────────────────────────────────────

    /// <summary>
    /// Generate the reward options to present after the current encounter.
    /// Count is determined by whether the encounter was a boss.
    /// </summary>
    public List<RewardOption> GenerateRewardOptions(bool wasBoss)
    {
        int count = wasBoss ? Config.bossOfferCount : Config.regularOfferCount;
        var pool  = CurrentRewardPool();
        return GenerateOptions(pool, count);
    }

    /// <summary>
    /// Generate the three specific fragment choices for a fragment-swap offer.
    /// Guarantees at least one effect fragment and at least one modifier fragment.
    /// </summary>
    public List<FragmentChoice> GenerateFragmentChoices()
    {
        var pool    = CurrentRewardPool();
        var choices = new List<FragmentChoice>();

        if (pool == null)
        {
            Debug.LogWarning("[RunState] No reward pool available for fragment choices.");
            return choices;
        }

        // Guarantee at least one of each type
        if (pool.effectFragmentPool.Count > 0)
            choices.Add(FragmentChoice.ForEffect(PickRandom(pool.effectFragmentPool)));

        if (pool.modifierFragmentPool.Count > 0)
            choices.Add(FragmentChoice.ForModifier(PickRandom(pool.modifierFragmentPool)));

        // Third slot: random between effect and modifier (avoid duplicates if possible)
        var remaining = new List<FragmentChoice>();
        foreach (var f in pool.effectFragmentPool)
        {
            var c = FragmentChoice.ForEffect(f);
            if (!choices.Exists(x => x.isEffect && x.effectFragment == f))
                remaining.Add(c);
        }
        foreach (var f in pool.modifierFragmentPool)
        {
            var c = FragmentChoice.ForModifier(f);
            if (!choices.Exists(x => !x.isEffect && x.modifierFragment == f))
                remaining.Add(c);
        }

        if (remaining.Count > 0)
            choices.Add(PickRandom(remaining));

        return choices;
    }

    // ── Fragment swapping ─────────────────────────────────────────────────────

    /// <summary>
    /// Replace the effect fragment of the card at cardIndex with newEffect.
    /// Creates a new runtime CardData instance so the original asset is untouched.
    /// </summary>
    public void SwapEffectFragment(int cardIndex, EffectFragmentData newEffect)
    {
        if (cardIndex < 0 || cardIndex >= CurrentCards.Count) return;

        var original = CurrentCards[cardIndex];
        var swapped  = ScriptableObject.CreateInstance<CardData>();
        swapped.effectFragment   = newEffect;
        swapped.modifierFragment = original.modifierFragment;
        CurrentCards[cardIndex]  = swapped;
    }

    /// <summary>
    /// Replace the modifier fragment of the card at cardIndex with newModifier.
    /// Creates a new runtime CardData instance so the original asset is untouched.
    /// </summary>
    public void SwapModifierFragment(int cardIndex, ModifierFragmentData newModifier)
    {
        if (cardIndex < 0 || cardIndex >= CurrentCards.Count) return;

        var original = CurrentCards[cardIndex];
        var swapped  = ScriptableObject.CreateInstance<CardData>();
        swapped.effectFragment   = original.effectFragment;
        swapped.modifierFragment = newModifier;
        CurrentCards[cardIndex]  = swapped;
    }

    // ── Fragment upgrading ────────────────────────────────────────────────────

    /// <summary>True if any card in the deck has at least one fragment with an upgrade available.</summary>
    public bool HasUpgradeableFragment()
    {
        foreach (var card in CurrentCards)
        {
            if (card.effectFragment?.CanUpgrade  == true) return true;
            if (card.modifierFragment?.CanUpgrade == true) return true;
        }
        return false;
    }

    /// <summary>Replace the effect fragment of the card at cardIndex with its upgraded version.</summary>
    public void UpgradeEffectFragment(int cardIndex)
    {
        if (cardIndex < 0 || cardIndex >= CurrentCards.Count) return;
        var frag = CurrentCards[cardIndex].effectFragment;
        if (frag?.upgradeVersion == null) return;
        SwapEffectFragment(cardIndex, frag.upgradeVersion);
    }

    /// <summary>Replace the modifier fragment of the card at cardIndex with its upgraded version.</summary>
    public void UpgradeModifierFragment(int cardIndex)
    {
        if (cardIndex < 0 || cardIndex >= CurrentCards.Count) return;
        var frag = CurrentCards[cardIndex].modifierFragment;
        if (frag?.upgradeVersion == null) return;
        SwapModifierFragment(cardIndex, frag.upgradeVersion);
    }

    // ── Unit health management ────────────────────────────────────────────────

    /// <summary>
    /// Overwrite UnitHealths with the values from the battle that just ended.
    /// Pass the currentHealth of each surviving unit in spawn order.
    /// Dead units (HP == 0) are excluded — they are gone forever.
    /// </summary>
    public void RecordBattleUnitHealth(List<int> healthValues)
    {
        UnitHealths.Clear();
        foreach (int hp in healthValues)
            if (hp > 0)
                UnitHealths.Add(hp);
    }

    /// <summary>Heal each surviving unit by <paramref name="amount"/>, capped at unitMaxHealth.</summary>
    public void HealUnits(int amount)
    {
        if (amount <= 0) return;
        for (int i = 0; i < UnitHealths.Count; i++)
            UnitHealths[i] = Mathf.Min(UnitHealths[i] + amount, Config.unitMaxHealth);
    }

    // ── Boon management ───────────────────────────────────────────────────────

    public void AddBoon(BoonData boon)
    {
        if (boon != null)
            ActiveBoons.Add(boon);
    }

    // ── Internals ─────────────────────────────────────────────────────────────

    private void BuildSegmentSequence()
    {
        _segmentSequence.Clear();

        if (IsPostSave)
        {
            // Post-save: randomly pick from postSaveEncounterPool
            var pool = new List<EncounterDefinition>(Config.postSaveEncounterPool);
            Shuffle(pool);
            int count = Mathf.Min(Config.postSaveEncounterCount, pool.Count);
            for (int i = 0; i < count; i++)
                _segmentSequence.Add(pool[i]);
        }
        else
        {
            // Regular segment: randomly pick encountersPerSegment from encounterPool
            var pool = new List<EncounterDefinition>(Config.encounterPool);
            Shuffle(pool);
            int count = Mathf.Min(Config.encountersPerSegment, pool.Count);
            for (int i = 0; i < count; i++)
                _segmentSequence.Add(pool[i]);
        }
    }

    private List<RewardOption> GenerateOptions(RewardPoolData pool, int count)
    {
        var options = new List<RewardOption>();
        if (pool == null) return options;

        // Build a shuffled candidate list: boons + meta-options (swap / upgrade)
        var candidates = new List<RewardOption>();
        foreach (var boon in pool.boonPool)
            candidates.Add(RewardOption.ForBoon(boon));
        if (pool.effectFragmentPool.Count > 0 || pool.modifierFragmentPool.Count > 0)
            candidates.Add(RewardOption.ForFragmentSwap());
        if (pool.includeFragmentUpgrade && HasUpgradeableFragment())
            candidates.Add(RewardOption.ForFragmentUpgrade());

        Shuffle(candidates);

        // Deduplicate: at most one of each meta-option type, no duplicate boons
        var seen = new System.Collections.Generic.HashSet<BoonData>();
        bool hasSwap    = false;
        bool hasUpgrade = false;
        foreach (var c in candidates)
        {
            if (options.Count >= count) break;
            if (c.type == RewardOptionType.FragmentSwap)
            {
                if (hasSwap) continue;
                hasSwap = true;
            }
            else if (c.type == RewardOptionType.FragmentUpgrade)
            {
                if (hasUpgrade) continue;
                hasUpgrade = true;
            }
            else if (c.boon != null)
            {
                if (!seen.Add(c.boon)) continue;
            }
            options.Add(c);
        }

        return options;
    }

    private static T PickRandom<T>(List<T> list) =>
        list[Random.Range(0, list.Count)];

    private static void Shuffle<T>(List<T> list)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
    }
}

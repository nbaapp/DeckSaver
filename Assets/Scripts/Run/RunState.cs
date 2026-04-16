using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// All mutable state for the current run.
/// Created fresh by the Hub when a run starts and carried between scenes via RunCarrier.
///
/// The run's structure is determined by the MapGraph, which is generated once at
/// construction time and mutated as the player visits nodes.
/// </summary>
public class RunState
{
    // ── Config ────────────────────────────────────────────────────────────────
    public RunConfig Config { get; }

    // ── Map ───────────────────────────────────────────────────────────────────
    /// <summary>The full run map. Generated at construction, mutated as nodes are visited.</summary>
    public MapGraph Map { get; }

    // ── Deck (mutable — fragment swaps create new runtime CardData instances) ─
    public List<CardData>  CurrentCards { get; } = new();
    public CommanderData   Commander    { get; }

    // ── Active boons ──────────────────────────────────────────────────────────
    public List<BoonData> ActiveBoons { get; } = new();

    // ── Economy ───────────────────────────────────────────────────────────────
    public int Money { get; private set; }

    // ── Unit health (persists across battles) ────────────────────────────────
    /// <summary>
    /// Current HP of each surviving unit, in spawn order.
    /// Initialised to unitMaxHealth for each unit at run start.
    /// Dead units (HP == 0) are removed permanently.
    /// </summary>
    public List<int> UnitHealths { get; } = new();
    public int UnitCount => UnitHealths.Count;

    // ── Constructor ───────────────────────────────────────────────────────────

    public RunState(RunConfig config, DeckData deck)
    {
        Config = config;
        Commander = deck.commander;

        CurrentCards.AddRange(deck.cards);

        for (int i = 0; i < config.startingUnitCount; i++)
            UnitHealths.Add(config.unitMaxHealth);

        Money = config.startingMoney;

        Map = MapGenerator.Generate(config);
    }

    // ── Economy ───────────────────────────────────────────────────────────────

    public void EarnMoney(int amount)
    {
        if (amount > 0) Money += amount;
    }

    /// <summary>Deduct money. Returns false and does nothing if the player can't afford it.</summary>
    public bool SpendMoney(int amount)
    {
        if (amount > Money) return false;
        Money -= amount;
        return true;
    }

    // ── Boon management ───────────────────────────────────────────────────────

    public void AddBoon(BoonData boon)
    {
        if (boon != null) ActiveBoons.Add(boon);
    }

    /// <summary>
    /// Returns a random selection of boon choices from the given pool.
    /// </summary>
    public List<BoonData> GenerateBoonChoices(List<BoonData> pool)
    {
        if (pool == null || pool.Count == 0) return new List<BoonData>();

        int count    = Mathf.Min(Config.boonOfferCount, pool.Count);
        var shuffled = new List<BoonData>(pool);
        Shuffle(shuffled);

        var result = new List<BoonData>(count);
        for (int i = 0; i < count; i++)
            result.Add(shuffled[i]);
        return result;
    }

    // ── Fragment swap ─────────────────────────────────────────────────────────

    /// <summary>
    /// Generate the three fragment choices for a swap reward.
    /// Guarantees at least one effect fragment and at least one modifier fragment.
    /// Uses Config.fragmentSwapPool.
    /// </summary>
    public List<FragmentChoice> GenerateFragmentChoices()
    {
        var pool    = Config.fragmentSwapPool;
        var choices = new List<FragmentChoice>();

        if (pool == null)
        {
            Debug.LogWarning("[RunState] fragmentSwapPool is not assigned in RunConfig.");
            return choices;
        }

        if (pool.effectFragmentPool.Count > 0)
            choices.Add(FragmentChoice.ForEffect(PickRandom(pool.effectFragmentPool)));

        if (pool.modifierFragmentPool.Count > 0)
            choices.Add(FragmentChoice.ForModifier(PickRandom(pool.modifierFragmentPool)));

        // Third slot: random from either pool, avoiding exact duplicates already chosen
        var extras = new List<FragmentChoice>();
        foreach (var f in pool.effectFragmentPool)
        {
            var c = FragmentChoice.ForEffect(f);
            if (!choices.Exists(x => x.isEffect && x.effectFragment == f))
                extras.Add(c);
        }
        foreach (var f in pool.modifierFragmentPool)
        {
            var c = FragmentChoice.ForModifier(f);
            if (!choices.Exists(x => !x.isEffect && x.modifierFragment == f))
                extras.Add(c);
        }

        if (extras.Count > 0)
            choices.Add(PickRandom(extras));

        return choices;
    }

    /// <summary>Replace the effect fragment of card at cardIndex with a new runtime instance.</summary>
    public void SwapEffectFragment(int cardIndex, EffectFragmentData newEffect)
    {
        if (cardIndex < 0 || cardIndex >= CurrentCards.Count) return;
        var original = CurrentCards[cardIndex];
        var swapped  = ScriptableObject.CreateInstance<CardData>();
        swapped.effectFragment   = newEffect;
        swapped.modifierFragment = original.modifierFragment;
        CurrentCards[cardIndex]  = swapped;
    }

    /// <summary>Replace the modifier fragment of card at cardIndex with a new runtime instance.</summary>
    public void SwapModifierFragment(int cardIndex, ModifierFragmentData newModifier)
    {
        if (cardIndex < 0 || cardIndex >= CurrentCards.Count) return;
        var original = CurrentCards[cardIndex];
        var swapped  = ScriptableObject.CreateInstance<CardData>();
        swapped.effectFragment   = original.effectFragment;
        swapped.modifierFragment = newModifier;
        CurrentCards[cardIndex]  = swapped;
    }

    // ── Fragment upgrade ──────────────────────────────────────────────────────

    public bool HasUpgradeableFragment()
    {
        foreach (var card in CurrentCards)
        {
            if (card.effectFragment?.CanUpgrade  == true) return true;
            if (card.modifierFragment?.CanUpgrade == true) return true;
        }
        return false;
    }

    public void UpgradeEffectFragment(int cardIndex)
    {
        if (cardIndex < 0 || cardIndex >= CurrentCards.Count) return;
        var frag = CurrentCards[cardIndex].effectFragment;
        if (frag?.upgradeVersion != null) SwapEffectFragment(cardIndex, frag.upgradeVersion);
    }

    public void UpgradeModifierFragment(int cardIndex)
    {
        if (cardIndex < 0 || cardIndex >= CurrentCards.Count) return;
        var frag = CurrentCards[cardIndex].modifierFragment;
        if (frag?.upgradeVersion != null) SwapModifierFragment(cardIndex, frag.upgradeVersion);
    }

    // ── Unit health ───────────────────────────────────────────────────────────

    /// <summary>
    /// Overwrite UnitHealths with the surviving unit HPs after a battle.
    /// Units with HP == 0 are excluded (dead permanently).
    /// </summary>
    public void RecordBattleUnitHealth(List<int> healthValues)
    {
        UnitHealths.Clear();
        foreach (int hp in healthValues)
            if (hp > 0) UnitHealths.Add(hp);
    }

    /// <summary>Heal each surviving unit by amount, capped at unitMaxHealth.</summary>
    public void HealUnits(int amount)
    {
        if (amount <= 0) return;
        for (int i = 0; i < UnitHealths.Count; i++)
            UnitHealths[i] = Mathf.Min(UnitHealths[i] + amount, Config.unitMaxHealth);
    }

    /// <summary>Add a new unit at full health.</summary>
    public void AddUnit()
    {
        UnitHealths.Add(Config.unitMaxHealth);
    }

    // ── Internals ─────────────────────────────────────────────────────────────

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

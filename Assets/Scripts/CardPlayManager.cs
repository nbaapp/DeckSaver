using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// Listens for tile-click events and resolves the currently selected card.
///
/// Add this component to any persistent scene object (e.g. the BattleUI root).
/// It finds HandDisplay automatically via the singleton-style references already
/// in the scene.
/// </summary>
public class CardPlayManager : MonoBehaviour
{
    public static CardPlayManager Instance { get; private set; }

    private HandDisplay _handDisplay;

    // ── Unity lifecycle ───────────────────────────────────────────────────────

    private void Awake() => Instance = this;

    private void Start()
    {
        _handDisplay = Object.FindFirstObjectByType<HandDisplay>();
        GridInputHandler.OnTileClicked += HandleTileClicked;
    }

    private void OnDestroy()
    {
        GridInputHandler.OnTileClicked -= HandleTileClicked;
    }

    // ── Core ──────────────────────────────────────────────────────────────────

    private void HandleTileClicked(GridTile tile)
    {
        if (TurnManager.Instance?.CurrentPhase != TurnPhase.PlayerTurn) return;

        CardView selected = _handDisplay?.SelectedCard;
        if (selected == null) return;

        // Still waiting for the player to pick which unit uses the card — don't resolve yet.
        if (GridInputHandler.Instance?.IsAwaitingUnit == true) return;

        // If the player clicked any player unit's tile, let PlayerMovementHandler
        // handle the unit switch — don't consume the click here.
        var entityOnTile = EntityManager.Instance.GetEntityAt(tile.GridPosition);
        if (entityOnTile is PlayerEntity)
            return;

        // A selected unit is required before a card can resolve.
        if (PlayerParty.Instance?.SelectedUnit == null) return;

        CardData card = selected.Data;
        if (card?.modifierFragment == null) return;

        var affected = GridInputHandler.Instance.GetAffectedTiles(tile.GridPosition);

        // For cards whose pattern is fixed (not freely placed), only confirm
        // the play when the click lands on one of the highlighted tiles.
        if (card.PlacementType != PlacementType.FreelyPlaceable &&
            !affected.Exists(a => a.tile == tile))
            return;

        if (!PlayerParty.Instance.TrySpendMana(card.ManaCost)) return;

        var globalMods = card.modifierFragment.globalModifiers;
        foreach (var effect in card.Effects ?? new List<CardEffect>())
            ApplyEffect(effect, card, affected, globalMods);

        if (card.modifierFragment.movesPlayer)
            MovePlayer(card.modifierFragment.moveDistance);

        // PlayCard moves the card to discard and fires OnHandChanged,
        // which clears the hand selection and grid highlight automatically.
        BattleDeck.Instance.PlayCard(card);
        BattleEvents.FireCardPlayed(card);
        HandDisplay.Instance?.RefreshAffordability();
    }

    private static void MovePlayer(int steps)
    {
        var player = PlayerEntity.Instance;
        if (player == null) return;
        steps = player.GetEffectiveMoveSpeed(steps);
        var dir = GridInputHandler.Instance.CurrentDirection;
        for (int i = 0; i < steps; i++)
        {
            var next = player.GridPosition + dir;
            if (!GridManager.Instance.IsInBounds(next)) break;
            if (EntityManager.Instance.GetEntityAt(next) != null) break;
            player.PlaceAt(next);
        }
    }

    // ── Effect resolution ─────────────────────────────────────────────────────

    private void ApplyEffect(
        CardEffect effect,
        CardData card,
        List<(GridTile tile, TileData data)> affected,
        List<TileModifier> globalMods)
    {
        switch (effect.type)
        {
            case EffectType.Strike:
                Entity strikeAttacker = PlayerEntity.Instance;
                Vector2Int atkPos     = strikeAttacker?.GridPosition ?? Vector2Int.zero;
                foreach (var (tile, data) in affected)
                {
                    var target = EntityManager.Instance.GetEntityAt(tile.GridPosition);
                    if (target == null) continue;
                    int dmg   = ComputeValue(effect.baseValue, globalMods, data.modifiers);
                    int count = Mathf.Max(1, effect.hits);
                    for (int h = 0; h < count; h++)
                        StatusResolver.ApplyStrike(strikeAttacker, target, atkPos, dmg, out _);
                }
                break;

            case EffectType.Block:
                foreach (var (tile, data) in affected)
                {
                    var entity = EntityManager.Instance.GetEntityAt(tile.GridPosition);
                    if (entity == null) continue;
                    int block = ComputeValue(effect.baseValue, globalMods, data.modifiers);
                    int count = Mathf.Max(1, effect.hits);
                    for (int h = 0; h < count; h++)
                        entity.GainBlock(block);
                }
                break;

            case EffectType.Heal:
                foreach (var (tile, data) in affected)
                {
                    var entity = EntityManager.Instance.GetEntityAt(tile.GridPosition);
                    if (entity == null) continue;
                    int heal  = ComputeValue(effect.baseValue, globalMods, data.modifiers);
                    int count = Mathf.Max(1, effect.hits);
                    for (int h = 0; h < count; h++)
                        entity.Heal(heal);
                }
                break;

            case EffectType.Status:
                foreach (var (tile, data) in affected)
                {
                    var entity = EntityManager.Instance.GetEntityAt(tile.GridPosition);
                    if (entity == null) continue;
                    int stacks = ComputeValue(effect.baseValue, globalMods, data.modifiers);
                    int count  = Mathf.Max(1, effect.hits);
                    for (int h = 0; h < count; h++)
                        entity.ApplyStatus(effect.statusType, stacks);
                }
                break;

            case EffectType.Draw:
                for (int i = 0; i < effect.baseValue; i++)
                    BattleDeck.Instance.DrawCard();
                break;

            case EffectType.Discard:
                for (int i = 0; i < effect.baseValue; i++)
                    DiscardRandom(excluding: card);
                break;

            default:
                Debug.Log($"[CardPlayManager] {effect.type} not yet implemented.");
                break;
        }
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Apply global then per-tile modifiers to baseValue.
    /// Multiply scales the running value; FlatAdd adds to it.
    /// </summary>
    private static int ComputeValue(
        int baseValue,
        List<TileModifier> globalMods,
        List<TileModifier> tileMods)
    {
        float v = baseValue;
        foreach (var m in globalMods) Apply(ref v, m);
        foreach (var m in tileMods)   Apply(ref v, m);
        return Mathf.RoundToInt(v);
    }

    private static void Apply(ref float v, TileModifier mod)
    {
        switch (mod.type)
        {
            case TileModifierType.Multiply: v *= mod.value; break;
            case TileModifierType.FlatAdd:  v += mod.value; break;
        }
    }

    /// <summary>Discard a random card from hand, skipping the card being played.</summary>
    private static void DiscardRandom(CardData excluding)
    {
        var candidates = BattleDeck.Instance.Hand
            .Where(c => c != excluding)
            .ToList();
        if (candidates.Count == 0) return;
        BattleDeck.Instance.DiscardCard(candidates[Random.Range(0, candidates.Count)]);
    }
}

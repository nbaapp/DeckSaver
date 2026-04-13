using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class EnemySpawnEntry
{
    public EnemyData data;
    public Vector2Int position;
}

/// <summary>
/// Spawns and tracks all entities (player units + enemies) on the grid.
///
/// Player unit count and starting HPs are read from RunState if a run is
/// active; otherwise all <see cref="playerStartPositions"/> slots are used
/// and each unit starts at full HP.
///
/// [RequireComponent] ensures a PlayerParty is always co-located on this GO.
/// </summary>
[RequireComponent(typeof(PlayerParty))]
public class EntityManager : MonoBehaviour
{
    public static EntityManager Instance { get; private set; }

    [Header("Player")]
    public GameObject playerPrefab;

    [Tooltip("Spawn position for each unit, indexed to match RunState.UnitHealths order.")]
    public List<Vector2Int> playerStartPositions = new()
    {
        new Vector2Int(1, 0),
        new Vector2Int(1, 1),
        new Vector2Int(1, 2),
    };

    [Header("Enemies")]
    public GameObject enemyPrefab;
    public List<EnemySpawnEntry> enemySpawns = new();

    private readonly List<PlayerEntity> _players = new();
    private readonly List<EnemyEntity>  _enemies = new();

    // ── Public API ────────────────────────────────────────────────────────────

    public IReadOnlyList<PlayerEntity> Players => _players;
    public IReadOnlyList<EnemyEntity>  Enemies => _enemies;

    /// <summary>Convenience: the currently selected unit (delegates to PlayerParty).</summary>
    public PlayerEntity Player => PlayerParty.Instance?.SelectedUnit;

    public void RemoveEnemy(EnemyEntity enemy)   => _enemies.Remove(enemy);
    public void RemovePlayer(PlayerEntity player) => _players.Remove(player);

    /// <summary>Returns the living player unit nearest to <paramref name="pos"/>, or null.</summary>
    public PlayerEntity NearestPlayerTo(Vector2Int pos)
    {
        PlayerEntity best     = null;
        int          bestDist = int.MaxValue;

        foreach (var p in _players)
        {
            if (p == null) continue;
            int d = Mathf.Abs(p.GridPosition.x - pos.x) + Mathf.Abs(p.GridPosition.y - pos.y);
            if (d < bestDist) { best = p; bestDist = d; }
        }
        return best;
    }

    /// <summary>Returns the entity (any player unit or enemy) occupying <paramref name="pos"/>, or null.</summary>
    public Entity GetEntityAt(Vector2Int pos)
    {
        foreach (var p in _players)
            if (p != null && p.GridPosition == pos) return p;
        foreach (var e in _enemies)
            if (e.GridPosition == pos) return e;
        return null;
    }

    // ── Unity lifecycle ───────────────────────────────────────────────────────

    private void Awake() => Instance = this;

    private void Start()
    {
        // Override inspector enemy list with the current encounter if inside a run.
        var run = RunCarrier.CurrentRun;
        if (run != null)
        {
            var encounter = run.CurrentEncounter();
            if (encounter != null)
                enemySpawns = encounter.enemySpawns;
        }

        SpawnAll();
    }

    // ── Spawning ──────────────────────────────────────────────────────────────

    private void SpawnAll()
    {
        SpawnPlayers();
        foreach (var entry in enemySpawns)
            SpawnEnemy(entry);
    }

    private void SpawnPlayers()
    {
        if (playerPrefab == null) return;

        var party = GetComponent<PlayerParty>();
        var run   = RunCarrier.CurrentRun;

        int   unitCount = run != null ? run.UnitCount
                        : Mathf.Max(1, playerStartPositions.Count);
        int   maxHp     = run?.Config.unitMaxHealth ?? 10;

        for (int i = 0; i < unitCount; i++)
        {
            int currentHp = (run != null && i < run.UnitHealths.Count)
                ? run.UnitHealths[i]
                : maxHp;

            var pos = i < playerStartPositions.Count
                ? playerStartPositions[i]
                : new Vector2Int(i, 0);

            var unit = Instantiate(playerPrefab).GetComponent<PlayerEntity>();
            unit.InitHealth(currentHp, maxHp);
            unit.PlaceAt(pos);
            _players.Add(unit);
            party.RegisterUnit(unit);
        }
    }

    private void SpawnEnemy(EnemySpawnEntry entry)
    {
        if (enemyPrefab == null || entry.data == null) return;
        var enemy = Instantiate(enemyPrefab).GetComponent<EnemyEntity>();
        enemy.Init(entry.data);
        enemy.PlaceAt(entry.position);
        _enemies.Add(enemy);
    }
}

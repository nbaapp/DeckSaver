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
/// Spawns and tracks the player and all enemies on the grid.
/// </summary>
public class EntityManager : MonoBehaviour
{
    public static EntityManager Instance { get; private set; }

    [Header("Player")]
    public GameObject playerPrefab;
    public Vector2Int playerStartPosition = new(2, 0);

    [Header("Enemies")]
    public GameObject enemyPrefab;
    public List<EnemySpawnEntry> enemySpawns = new();

    private PlayerEntity _player;
    private readonly List<EnemyEntity> _enemies = new();

    private void Awake()
    {
        Instance = this;
    }

    private void Start()
    {
        // If a run is active, use the current encounter's enemy spawns instead of
        // the inspector list. This lets encounters be fully data-driven.
        var run = RunCarrier.CurrentRun;
        if (run != null)
        {
            var encounter = run.CurrentEncounter();
            if (encounter != null)
                enemySpawns = encounter.enemySpawns;
        }

        SpawnAll();
    }

    // --- Public API ---

    public PlayerEntity Player => _player;
    public IReadOnlyList<EnemyEntity> Enemies => _enemies;

    /// <summary>Returns the entity (player or enemy) occupying the given grid position, or null.</summary>
    public Entity GetEntityAt(Vector2Int pos)
    {
        if (_player != null && _player.GridPosition == pos) return _player;
        foreach (var e in _enemies)
            if (e.GridPosition == pos) return e;
        return null;
    }

    public void RemoveEnemy(EnemyEntity enemy) => _enemies.Remove(enemy);

    // --- Internals ---

    private void SpawnAll()
    {
        SpawnPlayer();
        foreach (var entry in enemySpawns)
            SpawnEnemy(entry);
    }

    private void SpawnPlayer()
    {
        if (playerPrefab == null) return;
        _player = Instantiate(playerPrefab).GetComponent<PlayerEntity>();
        _player.PlaceAt(playerStartPosition);
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

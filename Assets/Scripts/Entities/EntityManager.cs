using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>
/// Spawns and tracks all entities (player units + enemies) on the grid.
///
/// At Start():
///   1. Resolve the encounter (from RunCarrier, or fallback to inspector data).
///   2. Instantiate the encounter's painted map prefab (or build a programmatic
///      default rectangle when none is assigned).
///   3. Register the map's Grid + Ground/Overlay tilemaps with GridManager.
///   4. Read SpawnMarker children to place player units and enemies on the
///      cells the designer painted them at.
///
/// Player unit count and starting HPs come from RunState if a run is active;
/// otherwise every PlayerSpawn marker is filled from a fresh full-HP unit.
///
/// [RequireComponent] ensures a PlayerParty is always co-located on this GO.
/// </summary>
[RequireComponent(typeof(PlayerParty))]
public class EntityManager : MonoBehaviour
{
    public static EntityManager Instance { get; private set; }

    [Header("Prefabs")]
    public GameObject playerPrefab;
    public GameObject enemyPrefab;

    [Header("Map fallback")]
    [Tooltip("Used as the playfield when an encounter has no mapPrefab assigned. " +
             "Drag any TileBase from the iso pack here.")]
    [SerializeField] private TileBase defaultGroundTile;

    [Header("Inspector-only fallback (no run active)")]
    [Tooltip("Encounter to use when there is no RunCarrier.CurrentRun (i.e. " +
             "you opened the Battle scene directly).")]
    [SerializeField] private EncounterDefinition fallbackEncounter;

    [Tooltip("Enemy roster used when neither a run nor a fallback encounter is set.")]
    [SerializeField] private List<EnemyData> fallbackEnemyRoster = new();

    private readonly List<PlayerEntity> _players = new();
    private readonly List<EnemyEntity>  _enemies = new();

    private GameObject _mapInstance;

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
        var encounter = RunCarrier.CurrentRun?.Map.CurrentNode?.Encounter ?? fallbackEncounter;
        var roster    = encounter != null ? encounter.enemyRoster : fallbackEnemyRoster;

        LoadMap(encounter);
        SpawnAll(roster);
        StartEncounterMusic(encounter);
    }

    private static void StartEncounterMusic(EncounterDefinition encounter)
    {
        if (AudioManager.Instance == null) return;
        if (encounter == null) { AudioManager.Instance.StopMusic(); return; }
        AudioManager.Instance.PlayMusic(
            encounter.musicTrack,
            encounter.musicTransition,
            encounter.musicFadeSeconds);
    }

    // ── Map loading ───────────────────────────────────────────────────────────

    private void LoadMap(EncounterDefinition encounter)
    {
        var prefab = encounter != null ? encounter.mapPrefab : null;

        _mapInstance = prefab != null
            ? Instantiate(prefab)
            : IsoMapBuilder.BuildDefault(defaultGroundTile);

        if (_mapInstance == null)
        {
            Debug.LogError("[EntityManager] Failed to load a playfield map. Assign defaultGroundTile " +
                           "or set encounter.mapPrefab.");
            return;
        }

        _mapInstance.name = "EncounterMap";

        var grid    = _mapInstance.GetComponentInChildren<Grid>();
        var ground  = FindTilemapByName(_mapInstance, "Ground");
        var overlay = FindTilemapByName(_mapInstance, "Overlay");

        if (grid == null || ground == null)
        {
            Debug.LogError("[EntityManager] Map prefab is missing Grid or Ground tilemap.");
            return;
        }

        GridManager.Instance?.RegisterMap(grid, ground, overlay);
    }

    private static Tilemap FindTilemapByName(GameObject root, string name)
    {
        foreach (var tm in root.GetComponentsInChildren<Tilemap>(true))
            if (tm.gameObject.name == name) return tm;
        return null;
    }

    // ── Spawning ──────────────────────────────────────────────────────────────

    private void SpawnAll(List<EnemyData> enemyRoster)
    {
        if (_mapInstance == null) return;

        var markers = _mapInstance.GetComponentsInChildren<SpawnMarker>(true);
        var playerSlots = markers.Where(m => m.kind == SpawnMarkerKind.Player)
                                 .OrderBy(m => m.slotIndex)
                                 .ToList();
        var enemySlots  = markers.Where(m => m.kind == SpawnMarkerKind.Enemy)
                                 .OrderBy(m => m.slotIndex)
                                 .ToList();

        SpawnPlayers(playerSlots);
        SpawnEnemies(enemySlots, enemyRoster);
    }

    private void SpawnPlayers(List<SpawnMarker> playerSlots)
    {
        if (playerPrefab == null || playerSlots.Count == 0) return;

        var party = GetComponent<PlayerParty>();
        var run   = RunCarrier.CurrentRun;

        int unitCount = run != null
            ? Mathf.Min(run.UnitCount, playerSlots.Count)
            : playerSlots.Count;
        int maxHp = run?.Config.unitMaxHealth ?? 10;

        for (int i = 0; i < unitCount; i++)
        {
            int currentHp = (run != null && i < run.UnitHealths.Count)
                ? run.UnitHealths[i]
                : maxHp;

            var pos = MarkerCell(playerSlots[i]);

            var unit = Instantiate(playerPrefab).GetComponent<PlayerEntity>();
            unit.InitHealth(currentHp, maxHp);
            unit.PlaceAt(pos);
            _players.Add(unit);
            party.RegisterUnit(unit);
        }
    }

    private void SpawnEnemies(List<SpawnMarker> enemySlots, List<EnemyData> enemyRoster)
    {
        if (enemyPrefab == null || enemyRoster == null) return;

        for (int i = 0; i < enemyRoster.Count; i++)
        {
            if (i >= enemySlots.Count)
            {
                Debug.LogWarning($"[EntityManager] Encounter has {enemyRoster.Count} enemies but " +
                                 $"map only has {enemySlots.Count} EnemySpawn markers. Ignoring extras.");
                break;
            }

            var data = enemyRoster[i];
            if (data == null) continue;

            var enemy = Instantiate(enemyPrefab).GetComponent<EnemyEntity>();
            enemy.Init(data);
            enemy.PlaceAt(MarkerCell(enemySlots[i]));
            _enemies.Add(enemy);
        }
    }

    private static Vector2Int MarkerCell(SpawnMarker marker)
    {
        var grid = GridManager.Instance?.Grid;
        if (grid == null) return Vector2Int.zero;
        var cell = grid.WorldToCell(marker.transform.position);
        return new Vector2Int(cell.x, cell.y);
    }
}

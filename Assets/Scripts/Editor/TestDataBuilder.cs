using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Creates test data assets for development.
///   DeckSaver → Create Test Enemies        — 5 EnemyData in Assets/Run Data/Enemies/
///   DeckSaver → Create Test Boss           — 1 EnemyData in Assets/Run Data/Enemies/Bosses/
///   DeckSaver → Create Test Encounters     — 5 EncounterDefinitions in Assets/Run Data/Encounters/
///   DeckSaver → Create Test Boss Encounter — 1 EncounterDefinition in Assets/Run Data/Encounters/Bosses/
///
/// Safe to re-run — existing assets are cleared and rebuilt from scratch.
/// </summary>
public static class TestDataBuilder
{
    // ── Enemy paths ───────────────────────────────────────────────────────────

    private const string EnemyRoot    = "Assets/Run Data/Enemies";
    private const string BossEnemyRoot = "Assets/Run Data/Enemies/Bosses";

    // ── Encounter paths ───────────────────────────────────────────────────────

    private const string EncounterRoot    = "Assets/Run Data/Encounters";
    private const string BossEncounterRoot = "Assets/Run Data/Encounters/Bosses";

    // =========================================================================
    // Enemies
    // =========================================================================

    [MenuItem("DeckSaver/Create Test Enemies")]
    public static void CreateTestEnemies()
    {
        EnsureFolder("Assets/Run Data");
        EnsureFolder(EnemyRoot);

        // Grunt — basic melee
        var grunt = Fresh<EnemyData>($"{EnemyRoot}/Grunt.asset");
        grunt.enemyName = "Grunt";
        grunt.maxHealth = 8;
        grunt.attacks.Add(Attack("Slash", EnemyAttackPatternType.RangedSingle,
            moveRange: 1, attackRange: 1, Strike(3)));

        // Archer — ranged, doesn't move
        var archer = Fresh<EnemyData>($"{EnemyRoot}/Archer.asset");
        archer.enemyName = "Archer";
        archer.maxHealth = 6;
        archer.attacks.Add(Attack("Arrow Shot", EnemyAttackPatternType.RangedSingle,
            moveRange: 0, attackRange: 3, Strike(2)));

        // Brute — slow, hits hard
        var brute = Fresh<EnemyData>($"{EnemyRoot}/Brute.asset");
        brute.enemyName = "Brute";
        brute.maxHealth = 14;
        brute.attacks.Add(Attack("Heavy Strike", EnemyAttackPatternType.RangedSingle,
            moveRange: 1, attackRange: 1, Strike(6)));

        // Hexer — applies Weak from range
        var hexer = Fresh<EnemyData>($"{EnemyRoot}/Hexer.asset");
        hexer.enemyName = "Hexer";
        hexer.maxHealth = 7;
        hexer.attacks.Add(Attack("Curse", EnemyAttackPatternType.RangedSingle,
            moveRange: 1, attackRange: 2, Stat(StatusType.Weak, 2)));

        // Spiker — braces with Spikes then jabs
        var spiker = Fresh<EnemyData>($"{EnemyRoot}/Spiker.asset");
        spiker.enemyName = "Spiker";
        spiker.maxHealth = 10;
        spiker.attacks.Add(Attack("Brace", EnemyAttackPatternType.RangedSingle,
            moveRange: 0, attackRange: 0, Stat(StatusType.Spikes, 3)));
        spiker.attacks.Add(Attack("Jab", EnemyAttackPatternType.RangedSingle,
            moveRange: 1, attackRange: 1, Strike(2)));

        SaveAll(grunt, archer, brute, hexer, spiker);
        Debug.Log("[DeckSaver] 5 test enemies created in Assets/Run Data/Enemies/");
    }

    // =========================================================================
    // Boss enemy
    // =========================================================================

    [MenuItem("DeckSaver/Create Test Boss")]
    public static void CreateTestBoss()
    {
        EnsureFolder("Assets/Run Data");
        EnsureFolder(EnemyRoot);
        EnsureFolder(BossEnemyRoot);

        var golem = Fresh<EnemyData>($"{BossEnemyRoot}/StoneGolem.asset");
        golem.enemyName = "Stone Golem";
        golem.maxHealth = 40;

        // Slam — directional 3-tile line forward
        var slam = Attack("Slam", EnemyAttackPatternType.DirectionalPattern,
            moveRange: 1, attackRange: 0, Strike(6));
        slam.attackPattern = new List<Vector2Int>
            { new(0, 0), new(0, 1), new(0, 2) };
        golem.attacks.Add(slam);

        // Tremor — fixed + shape centred on nearest target
        var tremor = Attack("Tremor", EnemyAttackPatternType.FixedPattern,
            moveRange: 0, attackRange: 2, Strike(4));
        tremor.attackPattern = new List<Vector2Int>
            { new(0, 0), new(1, 0), new(-1, 0), new(0, 1), new(0, -1) };
        golem.attacks.Add(tremor);

        // Fortify — gains Hard and Block
        var fortify = Attack("Fortify", EnemyAttackPatternType.RangedSingle,
            moveRange: 0, attackRange: 0);
        fortify.effects.Add(Stat(StatusType.Hard, 3));
        fortify.effects.Add(new CardEffect { type = EffectType.Block, baseValue = 8, hits = 1 });
        golem.attacks.Add(fortify);

        SaveAll(golem);
        Debug.Log("[DeckSaver] Stone Golem created in Assets/Run Data/Enemies/Bosses/");
    }

    // =========================================================================
    // Encounters
    // =========================================================================

    [MenuItem("DeckSaver/Create Test Encounters")]
    public static void CreateTestEncounters()
    {
        EnsureFolder("Assets/Run Data");
        EnsureFolder(EncounterRoot);

        var grunt  = Load<EnemyData>($"{EnemyRoot}/Grunt.asset");
        var archer = Load<EnemyData>($"{EnemyRoot}/Archer.asset");
        var brute  = Load<EnemyData>($"{EnemyRoot}/Brute.asset");
        var hexer  = Load<EnemyData>($"{EnemyRoot}/Hexer.asset");
        var spiker = Load<EnemyData>($"{EnemyRoot}/Spiker.asset");

        if (grunt == null || archer == null || brute == null || hexer == null || spiker == null)
        {
            Debug.LogError("[DeckSaver] Missing enemy assets — run 'Create Test Enemies' first.");
            return;
        }

        // 1. Patrol — two grunts spread across the middle
        var patrol = Fresh<EncounterDefinition>($"{EncounterRoot}/Patrol.asset");
        patrol.encounterName = "Patrol";
        patrol.type          = EncounterType.Battle;
        patrol.enemySpawns   = new List<EnemySpawnEntry>
        {
            Spawn(grunt, 1, 3),
            Spawn(grunt, 3, 3),
        };

        // 2. Ambush — grunt up close, archer hanging back
        var ambush = Fresh<EncounterDefinition>($"{EncounterRoot}/Ambush.asset");
        ambush.encounterName = "Ambush";
        ambush.type          = EncounterType.Battle;
        ambush.enemySpawns   = new List<EnemySpawnEntry>
        {
            Spawn(grunt,  2, 2),
            Spawn(archer, 2, 4),
        };

        // 3. Overwhelming — three grunts in a line
        var overwhelming = Fresh<EncounterDefinition>($"{EncounterRoot}/Overwhelming.asset");
        overwhelming.encounterName = "Overwhelming";
        overwhelming.type          = EncounterType.Battle;
        overwhelming.enemySpawns   = new List<EnemySpawnEntry>
        {
            Spawn(grunt, 0, 3),
            Spawn(grunt, 2, 3),
            Spawn(grunt, 4, 3),
        };

        // 4. The Bodyguard — brute up front, hexer cursing from behind
        var bodyguard = Fresh<EncounterDefinition>($"{EncounterRoot}/TheBodyguard.asset");
        bodyguard.encounterName = "The Bodyguard";
        bodyguard.type          = EncounterType.Battle;
        bodyguard.enemySpawns   = new List<EnemySpawnEntry>
        {
            Spawn(brute, 2, 2),
            Spawn(hexer, 2, 4),
        };

        // 5. Trap — spiker up close, archers on the flanks
        var trap = Fresh<EncounterDefinition>($"{EncounterRoot}/Trap.asset");
        trap.encounterName = "Trap";
        trap.type          = EncounterType.Battle;
        trap.enemySpawns   = new List<EnemySpawnEntry>
        {
            Spawn(spiker, 2, 2),
            Spawn(archer, 0, 4),
            Spawn(archer, 4, 4),
        };

        SaveAll(patrol, ambush, overwhelming, bodyguard, trap);
        Debug.Log("[DeckSaver] 5 test encounters created in Assets/Run Data/Encounters/");
    }

    // =========================================================================
    // Boss encounter
    // =========================================================================

    [MenuItem("DeckSaver/Create Test Boss Encounter")]
    public static void CreateTestBossEncounter()
    {
        EnsureFolder("Assets/Run Data");
        EnsureFolder(EncounterRoot);
        EnsureFolder(BossEncounterRoot);

        var golem = Load<EnemyData>($"{BossEnemyRoot}/StoneGolem.asset");
        if (golem == null)
        {
            Debug.LogError("[DeckSaver] Missing StoneGolem — run 'Create Test Boss' first.");
            return;
        }

        var boss = Fresh<EncounterDefinition>($"{BossEncounterRoot}/StoneGolemEncounter.asset");
        boss.encounterName = "The Stone Golem";
        boss.type          = EncounterType.Battle;
        boss.enemySpawns   = new List<EnemySpawnEntry>
        {
            Spawn(golem, 2, 3),
        };

        SaveAll(boss);
        Debug.Log("[DeckSaver] Stone Golem encounter created in Assets/Run Data/Encounters/Bosses/");
    }

    // =========================================================================
    // Helpers
    // =========================================================================

    /// <summary>
    /// Returns a clean ScriptableObject at path — deletes any existing asset first
    /// so properties set after this call are never mixed with stale serialized data.
    /// </summary>
    private static T Fresh<T>(string path) where T : ScriptableObject
    {
        if (AssetDatabase.LoadAssetAtPath<T>(path) != null)
            AssetDatabase.DeleteAsset(path);

        var asset = ScriptableObject.CreateInstance<T>();
        AssetDatabase.CreateAsset(asset, path);
        return asset;
    }

    private static T Load<T>(string path) where T : Object =>
        AssetDatabase.LoadAssetAtPath<T>(path);

    private static void SaveAll(params Object[] assets)
    {
        foreach (var a in assets) EditorUtility.SetDirty(a);
        AssetDatabase.SaveAssets();
    }

    private static EnemyAttack Attack(string name, EnemyAttackPatternType pattern,
        int moveRange, int attackRange, params CardEffect[] effects)
    {
        var a = new EnemyAttack
        {
            attackName  = name,
            patternType = pattern,
            moveRange   = moveRange,
            attackRange = attackRange,
        };
        a.effects.AddRange(effects);
        return a;
    }

    private static CardEffect Strike(int damage) =>
        new CardEffect { type = EffectType.Strike, baseValue = damage, hits = 1 };

    private static CardEffect Stat(StatusType status, int stacks) =>
        new CardEffect { type = EffectType.Status, baseValue = stacks, hits = 1, statusType = status };

    private static EnemySpawnEntry Spawn(EnemyData data, int x, int y) =>
        new EnemySpawnEntry { data = data, position = new Vector2Int(x, y) };

    private static void EnsureFolder(string path)
    {
        if (!AssetDatabase.IsValidFolder(path))
        {
            int slash = path.LastIndexOf('/');
            AssetDatabase.CreateFolder(path[..slash], path[(slash + 1)..]);
        }
    }
}

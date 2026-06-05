using UnityEngine;

/// <summary>
/// Subscribes to <see cref="BattleEvents"/> and forwards them to the
/// <see cref="AudioManager"/> as SFX one-shots. Drop one of these into the
/// Battle scene and assign SoundData assets in the inspector.
///
/// Lives in the battle scene because all of the events it cares about only
/// fire there. Music is handled separately (per-encounter) by EntityManager.
/// </summary>
public class BattleAudioBridge : MonoBehaviour
{
    [Header("Cards")]
    [SerializeField] private SoundData _cardPlay;

    [Header("Combat")]
    [SerializeField] private SoundData _enemyHit;
    [SerializeField] private SoundData _playerHit;

    [Header("Movement")]
    [SerializeField] private SoundData _unitMove;

    private void OnEnable()
    {
        BattleEvents.OnCardPlayed += HandleCardPlayed;
        BattleEvents.OnEnemyHit   += HandleEnemyHit;
        BattleEvents.OnPlayerHit  += HandlePlayerHit;
        BattleEvents.OnUnitMoved  += HandleUnitMoved;
    }

    private void OnDisable()
    {
        BattleEvents.OnCardPlayed -= HandleCardPlayed;
        BattleEvents.OnEnemyHit   -= HandleEnemyHit;
        BattleEvents.OnPlayerHit  -= HandlePlayerHit;
        BattleEvents.OnUnitMoved  -= HandleUnitMoved;
    }

    private void HandleCardPlayed(CardData _)        => Play(_cardPlay);
    private void HandleEnemyHit(EnemyEntity _, int __) => Play(_enemyHit);
    private void HandlePlayerHit(Entity _, int __)   => Play(_playerHit);
    private void HandleUnitMoved(Entity _)           => Play(_unitMove);

    private static void Play(SoundData s) => AudioManager.Instance?.PlaySFX(s);
}

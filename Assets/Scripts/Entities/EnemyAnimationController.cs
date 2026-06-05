using UnityEngine;

/// <summary>
/// Drives an enemy's Animator from gameplay events.
///
/// Added at runtime by EnemyEntity.Init when the EnemyData has an animatorController
/// assigned. Subscribes to the entity's OnHitReceived and OnDeath events and exposes
/// Play* methods that EnemyEntity calls during its turn coroutine.
///
/// The asset pack contracts on these trigger names: Hit, Run, Attack, Attack 2,
/// Attack 3, Ability, Death.
/// </summary>
[RequireComponent(typeof(Animator))]
[RequireComponent(typeof(SpriteRenderer))]
public class EnemyAnimationController : MonoBehaviour
{
    private static readonly int HashHit     = Animator.StringToHash("Hit");
    private static readonly int HashDeath   = Animator.StringToHash("Death");
    private static readonly int HashRun     = Animator.StringToHash("Run");
    private static readonly int HashAttack  = Animator.StringToHash("Attack");
    private static readonly int HashAttack2 = Animator.StringToHash("Attack 2");
    private static readonly int HashAttack3 = Animator.StringToHash("Attack 3");
    private static readonly int HashAbility = Animator.StringToHash("Ability");

    private EnemyEntity   _entity;
    private EnemyData     _data;
    private Animator      _animator;
    private SpriteRenderer _sr;

    public void Bind(EnemyEntity entity, EnemyData data)
    {
        _entity   = entity;
        _data     = data;
        _animator = GetComponent<Animator>();
        _sr       = GetComponent<SpriteRenderer>();

        _animator.runtimeAnimatorController = data.animatorController;
        // The placeholder prefab tints the renderer red — clear it so animated
        // sprites display with their authored colours.
        _sr.color = Color.white;

        _entity.OnHitReceived += HandleHit;
        _entity.OnDeath       += HandleDeath;
    }

    private void OnDestroy()
    {
        if (_entity == null) return;
        _entity.OnHitReceived -= HandleHit;
        _entity.OnDeath       -= HandleDeath;
    }

    // ── Event handlers ────────────────────────────────────────────────────────

    // Plays even on fully-blocked hits (net == 0) so block absorption reads visually.
    private void HandleHit(int net)   => _animator.SetTrigger(HashHit);
    private void HandleDeath()        => _animator.SetTrigger(HashDeath);

    // ── Action API (called from EnemyEntity coroutines) ───────────────────────

    public void PlayMove() => _animator.SetTrigger(HashRun);

    public void PlayAttack(EnemyAttackAnimation variant)
    {
        int hash = variant switch
        {
            EnemyAttackAnimation.Attack2 => HashAttack2,
            EnemyAttackAnimation.Attack3 => HashAttack3,
            EnemyAttackAnimation.Ability => HashAbility,
            _                            => HashAttack,
        };
        _animator.SetTrigger(hash);
    }

    /// <summary>Flip the sprite to face <paramref name="target"/> horizontally.</summary>
    public void FaceTowards(Vector2Int target)
    {
        int dx = target.x - _entity.GridPosition.x;
        if (dx == 0) return;
        bool faceRight = dx > 0;
        // If the source sprites face right by default, flipX==true means face left.
        _sr.flipX = faceRight ? !_data.spriteFacesRight : _data.spriteFacesRight;
    }
}

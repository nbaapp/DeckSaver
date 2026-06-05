using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "NewEnemy", menuName = "DeckSaver/Enemy")]
public class EnemyData : ScriptableObject
{
    public string enemyName;
    public Sprite artwork;
    public int maxHealth = 5;

    [Tooltip("Attacks this enemy can choose from at the start of each round.")]
    public List<EnemyAttack> attacks = new();

    [Header("Animation")]
    [Tooltip("Animator controller for this enemy. Leave null to fall back to the static sprite. " +
             "Expected trigger params: Hit, Run, Attack, Attack 2, Attack 3, Ability, Death.")]
    public RuntimeAnimatorController animatorController;

    [Tooltip("True if the source sprites face right by default (most pixel-art packs do). " +
             "Used to decide which way to flip when facing a target.")]
    public bool spriteFacesRight = true;

    [Tooltip("Seconds to hold the enemy alive after triggering Death before destroying the GameObject. " +
             "Ignored if no animator controller is set.")]
    public float deathAnimSeconds = 0.6f;

    [Tooltip("Seconds an attack animation occupies before the next enemy acts.")]
    public float attackAnimSeconds = 0.4f;
}

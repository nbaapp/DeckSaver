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
}

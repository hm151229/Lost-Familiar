using UnityEngine;

namespace LostFamiliar.Battle
{
    [CreateAssetMenu(menuName = "Lost Familiar/Stage/Special Stage", fileName = "SpecialStageData")]
    public sealed class SpecialStageData : ScriptableObject
    {
        [Min(1)] public int stageNumber = 10;
        public string displayName = "특수 스테이지";
        public EnemyData bossOverride;
        [Min(.1f)] public float enemyHealthMultiplier = 1f;
        [Min(.1f)] public float enemyAttackMultiplier = 1f;
        [Min(.1f)] public float rewardMultiplier = 1f;
        [Min(0)] public int bonusGems;
    }
}

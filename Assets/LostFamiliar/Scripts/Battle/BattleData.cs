using System;
using UnityEngine;

namespace LostFamiliar.Battle
{
    public enum SkillTargetType { NearestEnemy, AllEnemies, Self }
    public enum GrowthValueType { EnemyHealth, EnemyAttack, GoldReward }

    [CreateAssetMenu(menuName = "Lost Familiar/Battle/Enemy", fileName = "EnemyData")]
    public sealed class EnemyData : ScriptableObject
    {
        public string displayName = "몬스터";

        [Header("공통 프리팹")]
        public GameObject prefab;

        [Header("외형")]
        public Sprite visualSprite;
        public RuntimeAnimatorController animatorController;
        public Color visualColor = Color.white;
        public Vector3 visualScale = Vector3.one;
        public Vector3 visualOffset = Vector3.zero;
        public Vector3 healthBarOffset = new Vector3(0f, 1.2f, 0f);

        [Header("전투 능력치")]
        [Min(1f)] public float baseHealth = 25f;
        [Min(0f)] public float baseAttack = 2f;
        [Min(0.1f)] public float moveSpeed = 1.5f;
        [Min(0.1f)] public float attackInterval = 1.2f;
        [Min(0.1f)] public float attackRange = 0.8f;
        [Min(0)] public int stageExperience = 10;
        [Min(0)] public int playerExperience = 3;
        [Min(0)] public double goldReward = 5d;
    }

    [Serializable]
    public sealed class EnemySpawnEntry
    {
        public EnemyData enemy;
        [Min(1)] public int weight = 1;
        [Min(1)] public int unlockStageInRegion = 1;
    }

    [Serializable]
    public sealed class StageGrowthSection
    {
        [Min(2)] public int startStage = 2;
        [Min(2)] public int endStage = 50;
        [Range(0f, 1f)] public float healthGrowth = .16f;
        [Range(0f, 1f)] public float attackGrowth = .08f;
        [Range(0f, 1f)] public float goldGrowth = .12f;

        public float GetRate(GrowthValueType type)
        {
            return type switch
            {
                GrowthValueType.EnemyHealth => healthGrowth,
                GrowthValueType.EnemyAttack => attackGrowth,
                GrowthValueType.GoldReward => goldGrowth,
                _ => 0f
            };
        }
    }
}

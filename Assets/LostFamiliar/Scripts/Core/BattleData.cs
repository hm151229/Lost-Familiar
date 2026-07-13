using System;
using UnityEngine;

namespace LostFamiliar.Battle
{
    public enum SkillTargetType { NearestEnemy, AllEnemies, Self }

    [CreateAssetMenu(menuName = "Lost Familiar/Enemy Data", fileName = "EnemyData")]
    public sealed class EnemyData : ScriptableObject
    {
        public string displayName = "버섯 몬스터";
        public GameObject prefab;
        [Min(1f)] public float baseHealth = 25f;
        [Min(0f)] public float baseAttack = 2f;
        [Min(0.1f)] public float moveSpeed = 1.5f;
        [Min(0.1f)] public float attackInterval = 1.2f;
        [Min(0.1f)] public float attackRange = 0.8f;
        [Min(0)] public int stageExperience = 10;
        [Min(0)] public int goldReward = 5;
    }

    [CreateAssetMenu(menuName = "Lost Familiar/Skill Data", fileName = "SkillData")]
    public sealed class SkillData : ScriptableObject
    {
        public string id = "magic_burst";
        public string displayName = "마력 폭발";
        public SkillTargetType targetType = SkillTargetType.NearestEnemy;
        [Min(0.1f)] public float cooldown = 5f;
        [Min(0f)] public float damageMultiplier = 3f;
        [Min(0f)] public float radius = 3f;
        public Color effectColor = new Color(.5f, .25f, 1f);
    }

    [Serializable]
    public sealed class EnemySpawnEntry
    {
        public EnemyData enemy;
        [Min(1)] public int weight = 1;
    }

    [CreateAssetMenu(menuName = "Lost Familiar/Stage Data", fileName = "StageData")]
    public sealed class StageData : ScriptableObject
    {
        public string displayName = "마법 숲";
        public Color backgroundColor = new Color(.08f, .18f, .12f);
        [Min(10)] public int experienceToBoss = 100;
        [Min(.2f)] public float spawnInterval = 1.5f;
        [Min(1)] public int maxAliveEnemies = 8;
        public EnemySpawnEntry[] normalEnemies;
        public EnemyData boss;

        public EnemyData PickEnemy()
        {
            if (normalEnemies == null || normalEnemies.Length == 0) return null;
            int total = 0;
            foreach (var entry in normalEnemies) if (entry.enemy != null) total += Mathf.Max(1, entry.weight);
            if (total == 0) return null;
            int roll = UnityEngine.Random.Range(0, total);
            foreach (var entry in normalEnemies)
            {
                if (entry.enemy == null) continue;
                roll -= Mathf.Max(1, entry.weight);
                if (roll < 0) return entry.enemy;
            }
            return normalEnemies[0].enemy;
        }
    }
}

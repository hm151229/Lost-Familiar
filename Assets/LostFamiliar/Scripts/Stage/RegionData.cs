using UnityEngine;

namespace LostFamiliar.Battle
{
    [CreateAssetMenu(menuName = "Lost Familiar/Stage/Region", fileName = "RegionData")]
    public sealed class RegionData : ScriptableObject
    {
        public string id = "magic_forest";
        public string displayName = "마법 숲";
        [Min(1)] public int startStage = 1;
        [Min(1)] public int endStage = 10;
        public Color backgroundColor = new Color(.08f, .18f, .12f);
        [Min(.2f)] public float spawnInterval = .65f;
        [Min(1)] public int spawnBatchSize = 3;
        [Min(1)] public int maxAliveEnemies = 15;
        public EnemySpawnEntry[] normalEnemies;
        public EnemyData[] stageBosses;
        public EnemyData boss;

        public bool Contains(int stageNumber) => stageNumber >= startStage && stageNumber <= endStage;

        public EnemyData PickEnemy(int stageNumber)
        {
            if (normalEnemies == null || normalEnemies.Length == 0)
                return null;

            int stageInRegion = Mathf.Max(1, stageNumber - startStage + 1);
            int totalWeight = 0;
            foreach (EnemySpawnEntry entry in normalEnemies)
            {
                if (entry != null && entry.enemy != null && entry.unlockStageInRegion <= stageInRegion)
                    totalWeight += Mathf.Max(1, entry.weight);
            }

            if (totalWeight <= 0)
                return null;

            int roll = Random.Range(0, totalWeight);
            foreach (EnemySpawnEntry entry in normalEnemies)
            {
                if (entry == null || entry.enemy == null || entry.unlockStageInRegion > stageInRegion)
                    continue;

                roll -= Mathf.Max(1, entry.weight);
                if (roll < 0)
                    return entry.enemy;
            }

            return null;
        }

        public EnemyData PickBoss(int stageNumber)
        {
            int stageIndex = Mathf.Clamp(stageNumber - startStage, 0, Mathf.Max(0, endStage - startStage));
            if (stageBosses != null && stageBosses.Length > 0)
            {
                int bossIndex = Mathf.Clamp(stageIndex, 0, stageBosses.Length - 1);
                if (stageBosses[bossIndex] != null)
                    return stageBosses[bossIndex];
            }

            return boss;
        }
    }
}

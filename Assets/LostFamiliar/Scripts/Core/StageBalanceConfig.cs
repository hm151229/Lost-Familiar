using System;
using UnityEngine;

namespace LostFamiliar.Battle
{
    [CreateAssetMenu(menuName = "Lost Familiar/Stage/Balance Config", fileName = "StageBalanceConfig")]
    public sealed class StageBalanceConfig : ScriptableObject
    {
        [Min(1)] public int designedMaxStage = 500;
        [Min(10)] public int baseExperienceToBoss = 500;
        [Min(0)] public int experienceIncreasePerStage = 10;
        [Min(1f)] public float baseEnemyHealthMultiplier = 1.5f;
        [Min(1f)] public float baseEnemyAttackMultiplier = 1.15f;
        [Min(1f)] public float bossHealthMultiplier = 8f;
        [Min(1f)] public float bossAttackMultiplier = 2f;
        [Min(1f)] public float regionBossHealthMultiplier = 14f;
        [Min(1f)] public float regionBossAttackMultiplier = 3f;
        [Min(1f)] public float bossTimeLimit = 45f;
        [Min(0)] public int bossGemReward = 2;
        [Min(0)] public int regionBossGemReward = 10;
        public StageGrowthSection[] growthSections =
        {
            new StageGrowthSection { startStage = 2, endStage = 50, healthGrowth = .14f, attackGrowth = .08f, goldGrowth = .10f },
            new StageGrowthSection { startStage = 51, endStage = 200, healthGrowth = .07f, attackGrowth = .055f, goldGrowth = .06f },
            new StageGrowthSection { startStage = 201, endStage = 500, healthGrowth = .04f, attackGrowth = .035f, goldGrowth = .04f }
        };

        public int GetExperienceToBoss(int stageNumber)
        {
            return baseExperienceToBoss + Mathf.Max(0, stageNumber - 1) * experienceIncreasePerStage;
        }

        public double GetMultiplier(GrowthValueType type, int stageNumber)
        {
            if (stageNumber <= 1 || growthSections == null)
                return 1d;

            double multiplier = 1d;
            foreach (StageGrowthSection section in growthSections)
            {
                if (section == null || section.endStage < section.startStage)
                    continue;

                int lastStage = Math.Min(stageNumber, section.endStage);
                int steps = Math.Max(0, lastStage - section.startStage + 1);
                if (steps > 0)
                    multiplier *= Math.Pow(1d + section.GetRate(type), steps);
            }

            return multiplier;
        }
    }
}

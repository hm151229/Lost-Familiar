using UnityEngine;

namespace LostFamiliar.Battle
{
    [CreateAssetMenu(menuName = "Lost Familiar/Stage/Database", fileName = "StageDatabase")]
    public sealed class StageDatabase : ScriptableObject
    {
        [Min(0)] public int contentVersion;
        public StageBalanceConfig balance;
        public Sprite guideFingerSprite;
        public RegionData[] regions;
        public SpecialStageData[] specialStages;

        public RegionData GetRegion(int stageNumber)
        {
            if (regions == null || regions.Length == 0)
                return null;

            RegionData nearest = null;
            foreach (RegionData region in regions)
            {
                if (region == null)
                    continue;
                if (region.Contains(stageNumber))
                    return region;
                if (nearest == null || region.startStage < stageNumber)
                    nearest = region;
            }

            return nearest;
        }

        public SpecialStageData GetSpecialStage(int stageNumber)
        {
            if (specialStages == null)
                return null;

            foreach (SpecialStageData specialStage in specialStages)
            {
                if (specialStage != null && specialStage.stageNumber == stageNumber)
                    return specialStage;
            }

            return null;
        }

        public StageRuntimeData BuildStage(int stageNumber)
        {
            if (balance == null)
                return null;

            RegionData region = GetRegion(stageNumber);
            if (region == null)
                return null;

            SpecialStageData special = GetSpecialStage(stageNumber);
            bool regionFinale = stageNumber == region.endStage;
            return new StageRuntimeData
            {
                stageNumber = stageNumber,
                region = region,
                special = special,
                experienceToBoss = balance.GetExperienceToBoss(stageNumber),
                healthMultiplier = balance.baseEnemyHealthMultiplier *
                                   balance.GetMultiplier(GrowthValueType.EnemyHealth, stageNumber) *
                                   (special?.enemyHealthMultiplier ?? 1f),
                attackMultiplier = balance.baseEnemyAttackMultiplier *
                                   balance.GetMultiplier(GrowthValueType.EnemyAttack, stageNumber) *
                                   (special?.enemyAttackMultiplier ?? 1f),
                rewardMultiplier = balance.GetMultiplier(GrowthValueType.GoldReward, stageNumber) * (special?.rewardMultiplier ?? 1f),
                bossHealthMultiplier = regionFinale ? balance.regionBossHealthMultiplier : balance.bossHealthMultiplier,
                bossAttackMultiplier = regionFinale ? balance.regionBossAttackMultiplier : balance.bossAttackMultiplier,
                bossTimeLimit = balance.bossTimeLimit,
                gemReward = (regionFinale ? balance.regionBossGemReward : balance.bossGemReward) + (special?.bonusGems ?? 0),
                isRegionFinale = regionFinale
            };
        }
    }

    public sealed class StageRuntimeData
    {
        public int stageNumber;
        public RegionData region;
        public SpecialStageData special;
        public int experienceToBoss;
        public double healthMultiplier;
        public double attackMultiplier;
        public double rewardMultiplier;
        public float bossHealthMultiplier;
        public float bossAttackMultiplier;
        public float bossTimeLimit;
        public int gemReward;
        public bool isRegionFinale;

        public string DisplayName => special != null ? special.displayName : region.displayName;
        public EnemyData Boss => special != null && special.bossOverride != null
            ? special.bossOverride
            : region.PickBoss(stageNumber);
    }
}

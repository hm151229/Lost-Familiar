using System;
using UnityEngine;

namespace LostFamiliar.Core
{
    public enum GuideMissionType
    {
        DefeatMonsters,
        Gacha,
        ClearStage,
        ReachStatLevel,
        ReachTotalUpgradeLevel,
        ClearGoldTower,
        ClearGemTower
    }

    public readonly struct GuideMissionDefinition
    {
        public readonly int index;
        public readonly GuideMissionType type;
        public readonly int target;
        public readonly int gemReward;
        public readonly StatType statType;
        public readonly int goldTowerTicketReward;
        public readonly int gemTowerTicketReward;

        public GuideMissionDefinition(
            int index,
            GuideMissionType type,
            int target,
            int gemReward,
            StatType statType = StatType.Attack,
            int goldTowerTicketReward = 0,
            int gemTowerTicketReward = 0)
        {
            this.index = index;
            this.type = type;
            this.target = Mathf.Max(1, target);
            this.gemReward = Mathf.Max(0, gemReward);
            this.statType = statType;
            this.goldTowerTicketReward = Mathf.Max(0, goldTowerTicketReward);
            this.gemTowerTicketReward = Mathf.Max(0, gemTowerTicketReward);
        }

        public string Title => type switch
        {
            GuideMissionType.DefeatMonsters => $"적 {target:N0}마리 처치",
            GuideMissionType.Gacha => $"장비 또는 스킬 {target:N0}회 뽑기",
            GuideMissionType.ClearStage => $"스테이지 {target:N0} 통과",
            GuideMissionType.ReachStatLevel => $"{GetStatName(statType)} 레벨 {target:N0} 달성",
            GuideMissionType.ReachTotalUpgradeLevel => $"총강화 레벨 {target:N0} 달성",
            GuideMissionType.ClearGoldTower => $"골드의 탑 {target:N0}회 클리어",
            GuideMissionType.ClearGemTower => $"보석의 탑 {target:N0}회 클리어",
            _ => "가이드 미션"
        };

        public string RewardText
        {
            get
            {
                if (goldTowerTicketReward > 0)
                    return goldTowerTicketReward.ToString("N0");
                if (gemTowerTicketReward > 0)
                    return gemTowerTicketReward.ToString("N0");
                return gemReward.ToString("N0");
            }
        }

        private static string GetStatName(StatType type) => type switch
        {
            StatType.Attack => "공격력",
            StatType.CriticalChance => "치명타 확률",
            StatType.CriticalDamage => "치명타 피해량",
            StatType.SkillDamage => "스킬 데미지",
            StatType.BossDamage => "보스 데미지",
            _ => "스탯"
        };

    }

    public static class GuideMissionCatalog
    {
        public const int TowerMissionInterval = 20;
        private const int TowerTicketRewardOffset = TowerMissionInterval - 2;
        private const int TowerClearOffset = TowerMissionInterval - 1;
        public const int MissionGroupsPerTier = 5;
        public const int MissionsPerGroup = 4;
        public const int MissionsPerTier = MissionGroupsPerTier * MissionsPerGroup + 1;
        public const int OnboardingTierCount = 2;
        public const int MissionsPerOnboardingTier = MissionGroupsPerTier * MissionsPerGroup;
        public const int MaximumMonsterTarget = 300;
        public const int GachaTarget = 10;

        private static readonly int[] MonsterTargets =
        {
            10, 20, 30, 50, 75, 100, 150, 200, 250, MaximumMonsterTarget
        };
        private static readonly int[] EarlyStageTargets = { 1, 2, 3, 5, 10, 15, 20, 30, 40 };
        private static readonly int[] OnboardingStatTargets = { 10, 50 };

        private static readonly StatType[] StatOrder =
        {
            StatType.Attack,
            StatType.CriticalChance,
            StatType.CriticalDamage,
            StatType.SkillDamage,
            StatType.BossDamage
        };

        public static GuideMissionDefinition Get(int missionIndex)
        {
            missionIndex = Mathf.Max(0, missionIndex);
            int towerMissionBlock = missionIndex / TowerMissionInterval;
            bool goldTowerBlock = towerMissionBlock % 2 == 0;
            if (missionIndex % TowerMissionInterval == TowerClearOffset)
            {
                return new GuideMissionDefinition(
                    missionIndex,
                    goldTowerBlock ? GuideMissionType.ClearGoldTower : GuideMissionType.ClearGemTower,
                    1,
                    300);
            }

            int onboardingMissionCount = OnboardingTierCount * MissionsPerOnboardingTier;
            int rewardTier;
            int step;
            int statTarget;
            int firstGlobalGroup;
            int totalUpgradeTarget = 0;

            if (missionIndex < onboardingMissionCount)
            {
                int onboardingTier = missionIndex / MissionsPerOnboardingTier;
                step = missionIndex % MissionsPerOnboardingTier;
                statTarget = OnboardingStatTargets[onboardingTier];
                firstGlobalGroup = onboardingTier * MissionGroupsPerTier;
                rewardTier = onboardingTier;
            }
            else
            {
                int progressionIndex = missionIndex - onboardingMissionCount;
                int progressionTier = progressionIndex / MissionsPerTier;
                step = progressionIndex % MissionsPerTier;
                statTarget = SafeMultiply(
                    progressionTier + 1,
                    GameBalance.StatLevelsPerTotalUpgradeLevel);
                firstGlobalGroup = SafeAdd(
                    OnboardingTierCount * MissionGroupsPerTier,
                    SafeMultiply(progressionTier, MissionGroupsPerTier));
                rewardTier = SafeAdd(OnboardingTierCount, progressionTier);
                totalUpgradeTarget = SafeAdd(progressionTier, 2);
            }

            if (step == MissionsPerTier - 1)
            {
                return new GuideMissionDefinition(
                    missionIndex,
                    GuideMissionType.ReachTotalUpgradeLevel,
                    totalUpgradeTarget,
                    ScaleReward(500, rewardTier, 100));
            }

            int group = step / MissionsPerGroup;
            int stepInGroup = step % MissionsPerGroup;
            int globalGroup = SafeAdd(firstGlobalGroup, group);

            GuideMissionDefinition mission = stepInGroup switch
            {
                // 바로 다음 미션이 10회 뽑기이므로 정확히 1,000 제스트를 지급한다.
                0 => new GuideMissionDefinition(
                    missionIndex,
                    GuideMissionType.DefeatMonsters,
                    MonsterTargets[Mathf.Min(globalGroup, MonsterTargets.Length - 1)],
                    1000),
                1 => new GuideMissionDefinition(missionIndex, GuideMissionType.Gacha, GachaTarget, 300),
                2 => new GuideMissionDefinition(
                    missionIndex,
                    GuideMissionType.ReachStatLevel,
                    statTarget,
                    ScaleReward(150, rewardTier, 25),
                    StatOrder[group]),
                _ => new GuideMissionDefinition(
                    missionIndex,
                    GuideMissionType.ClearStage,
                    GetStageTarget(globalGroup),
                    ScaleReward(300, rewardTier, 50))
            };

            if (missionIndex % TowerMissionInterval == TowerTicketRewardOffset && goldTowerBlock)
                return new GuideMissionDefinition(
                    mission.index, mission.type, mission.target, 0,
                    mission.statType, goldTowerTicketReward: 1);
            if (missionIndex % TowerMissionInterval == TowerTicketRewardOffset && !goldTowerBlock)
                return new GuideMissionDefinition(
                    mission.index, mission.type, mission.target, 0,
                    mission.statType, gemTowerTicketReward: 1);
            return mission;
        }

        private static int ScaleReward(int baseReward, int cycle, int perCycle) =>
            SafeAdd(baseReward, SafeMultiply(cycle, perCycle));

        private static int GetStageTarget(int cycle)
        {
            if (cycle < EarlyStageTargets.Length)
                return EarlyStageTargets[cycle];

            int cyclesAfterEarlyTargets = cycle - EarlyStageTargets.Length + 1;
            return SafeAdd(EarlyStageTargets[EarlyStageTargets.Length - 1],
                SafeMultiply(cyclesAfterEarlyTargets, 10));
        }

        private static int SafeAdd(int left, int right) =>
            (int)Math.Min(int.MaxValue, (long)Math.Max(0, left) + Math.Max(0, right));

        private static int SafeMultiply(int left, int right) =>
            (int)Math.Min(int.MaxValue, (long)Math.Max(0, left) * Math.Max(0, right));
    }
}

using System;
using System.Collections.Generic;
using UnityEngine;

namespace LostFamiliar.Core
{
    public enum StatType { Attack, CriticalChance, CriticalDamage, SkillDamage, BossDamage }

    public enum GuideMissionType
    {
        DefeatMonsters,
        Gacha,
        ClearStage,
        ReachStatLevel,
        ReachTotalUpgradeLevel
    }

    public readonly struct GuideMissionDefinition
    {
        public readonly int index;
        public readonly GuideMissionType type;
        public readonly int target;
        public readonly int gemReward;
        public readonly StatType statType;

        public GuideMissionDefinition(
            int index,
            GuideMissionType type,
            int target,
            int gemReward,
            StatType statType = StatType.Attack)
        {
            this.index = index;
            this.type = type;
            this.target = Mathf.Max(1, target);
            this.gemReward = Mathf.Max(0, gemReward);
            this.statType = statType;
        }

        public string Title => type switch
        {
            GuideMissionType.DefeatMonsters => $"적 {target:N0}마리 처치",
            GuideMissionType.Gacha => $"장비 또는 스킬 {target:N0}회 뽑기",
            GuideMissionType.ClearStage => $"스테이지 {target:N0} 통과",
            GuideMissionType.ReachStatLevel => $"{GetStatName(statType)} 레벨 {target:N0} 달성",
            GuideMissionType.ReachTotalUpgradeLevel => $"총강화 레벨 {target:N0} 달성",
            _ => "가이드 미션"
        };

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

            return stepInGroup switch
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

    [Serializable]
    public sealed class EquipmentSaveEntry
    {
        public string equipmentId;
        public int level;
        public int duplicates;
    }

    [Serializable]
    public sealed class SkillSaveEntry
    {
        public string skillId;
        public int level;
        public int duplicates;
    }

    [Serializable]
    public sealed class GameSaveData
    {
        public int version = 8;
        public double gold;
        public int gems;
        public int playerLevel = 1;
        public double playerExperience;
        public int stage = 1;
        public float stageProgress;
        public bool bossRetryRequired;
        public int attackLevel;
        public int healthLevel;
        public int attackSpeedLevel;
        public int criticalChanceLevel;
        public int criticalDamageLevel;
        public int skillDamageLevel;
        public int bossDamageLevel;
        public int totalUpgradeLevel = 1;
        public int guideMissionIndex;
        public int guideMissionProgress;
        public int guideMissionLayoutVersion = 2;
        public List<EquipmentSaveEntry> equipmentInventory = new List<EquipmentSaveEntry>();
        public string equippedHeadId;
        public string equippedBodyId;
        public string equippedShoesId;
        public string equippedAccessory1Id;
        public string equippedAccessory2Id;
        public string equippedWeaponId;
        public int armorGachaLevel = 1;
        public int armorGachaProgress;
        public int accessoryGachaLevel = 1;
        public int accessoryGachaProgress;
        public int skillGachaLevel = 1;
        public int skillGachaProgress;
        public int weaponGachaLevel = 1;
        public int weaponGachaProgress;
        public List<SkillSaveEntry> skillInventory = new List<SkillSaveEntry>();
        public List<string> equippedSkillIds = new List<string>();

        public int GetGachaLevel(GachaCategory category) => category switch
        {
            GachaCategory.Armor => Mathf.Clamp(armorGachaLevel, 1, GachaBalance.MaxLevel),
            GachaCategory.Accessory => Mathf.Clamp(accessoryGachaLevel, 1, GachaBalance.MaxLevel),
            GachaCategory.Skill => Mathf.Clamp(skillGachaLevel, 1, GachaBalance.MaxLevel),
            GachaCategory.Weapon => Mathf.Clamp(weaponGachaLevel, 1, GachaBalance.MaxLevel),
            _ => 1
        };

        public int GetGachaProgress(GachaCategory category) => category switch
        {
            GachaCategory.Armor => armorGachaProgress,
            GachaCategory.Accessory => accessoryGachaProgress,
            GachaCategory.Skill => skillGachaProgress,
            GachaCategory.Weapon => weaponGachaProgress,
            _ => 0
        };

        public void AddGachaProgress(GachaCategory category, int amount)
        {
            int level = GetGachaLevel(category);
            int progress = Mathf.Max(0, GetGachaProgress(category)) + Mathf.Max(0, amount);
            while (level < GachaBalance.MaxLevel)
            {
                int required = GachaBalance.RequiredDraws(level);
                if (progress < required)
                    break;
                progress -= required;
                level++;
            }
            if (level >= GachaBalance.MaxLevel)
                progress = 0;
            SetGachaState(category, level, progress);
        }

        public SkillSaveEntry GetOrCreateSkill(string skillId)
        {
            skillInventory ??= new List<SkillSaveEntry>();
            SkillSaveEntry entry = skillInventory.Find(value => value != null && value.skillId == skillId);
            if (entry != null)
                return entry;
            entry = new SkillSaveEntry { skillId = skillId };
            skillInventory.Add(entry);
            return entry;
        }

        private void SetGachaState(GachaCategory category, int level, int progress)
        {
            switch (category)
            {
                case GachaCategory.Armor: armorGachaLevel = level; armorGachaProgress = progress; break;
                case GachaCategory.Accessory: accessoryGachaLevel = level; accessoryGachaProgress = progress; break;
                case GachaCategory.Skill: skillGachaLevel = level; skillGachaProgress = progress; break;
                case GachaCategory.Weapon: weaponGachaLevel = level; weaponGachaProgress = progress; break;
            }
        }

        public EquipmentSaveEntry FindEquipment(string equipmentId)
        {
            if (string.IsNullOrWhiteSpace(equipmentId) || equipmentInventory == null)
                return null;

            return equipmentInventory.Find(entry => entry != null && entry.equipmentId == equipmentId);
        }

        public EquipmentSaveEntry GetOrCreateEquipment(string equipmentId)
        {
            equipmentInventory ??= new List<EquipmentSaveEntry>();
            EquipmentSaveEntry entry = FindEquipment(equipmentId);
            if (entry != null)
                return entry;

            entry = new EquipmentSaveEntry { equipmentId = equipmentId };
            equipmentInventory.Add(entry);
            return entry;
        }

        public int GetStatLevel(StatType type) => type switch
        {
            StatType.Attack => attackLevel,
            StatType.CriticalChance => criticalChanceLevel,
            StatType.CriticalDamage => criticalDamageLevel,
            StatType.SkillDamage => skillDamageLevel,
            StatType.BossDamage => bossDamageLevel,
            _ => 0
        };

        public void IncreaseStatLevel(StatType type)
        {
            IncreaseStatLevels(type, 1);
        }

        public void IncreaseStatLevels(StatType type, int amount)
        {
            amount = Math.Max(0, amount);
            switch (type)
            {
                case StatType.Attack: attackLevel = SafeAdd(attackLevel, amount); break;
                case StatType.CriticalChance: criticalChanceLevel = SafeAdd(criticalChanceLevel, amount); break;
                case StatType.CriticalDamage: criticalDamageLevel = SafeAdd(criticalDamageLevel, amount); break;
                case StatType.SkillDamage: skillDamageLevel = SafeAdd(skillDamageLevel, amount); break;
                case StatType.BossDamage: bossDamageLevel = SafeAdd(bossDamageLevel, amount); break;
            }
        }

        private static int SafeAdd(int value, int amount)
        {
            return (int)Math.Min(int.MaxValue, (long)Math.Max(0, value) + amount);
        }

        public int TotalUpgradeLevel => Mathf.Max(1, totalUpgradeLevel);

        public int StatLevelCap
        {
            get
            {
                long cap = (long)TotalUpgradeLevel * GameBalance.StatLevelsPerTotalUpgradeLevel;
                return cap >= int.MaxValue ? int.MaxValue : (int)cap;
            }
        }

        public int TotalUpgradeProgress
        {
            get
            {
                int previousCap = Mathf.Max(0, StatLevelCap - GameBalance.StatLevelsPerTotalUpgradeLevel);
                int currentCap = StatLevelCap;
                long progress = 0;
                progress += Mathf.Clamp(attackLevel, previousCap, currentCap) - previousCap;
                progress += Mathf.Clamp(criticalChanceLevel, previousCap, currentCap) - previousCap;
                progress += Mathf.Clamp(criticalDamageLevel, previousCap, currentCap) - previousCap;
                progress += Mathf.Clamp(skillDamageLevel, previousCap, currentCap) - previousCap;
                progress += Mathf.Clamp(bossDamageLevel, previousCap, currentCap) - previousCap;
                return (int)Math.Min(progress, TotalUpgradeProgressRequired);
            }
        }

        public int TotalUpgradeProgressRequired =>
            GameBalance.StatLevelsPerTotalUpgradeLevel * GameBalance.UpgradeableStatCount;

        public bool CanIncreaseTotalUpgradeLevel => TotalUpgradeProgress >= TotalUpgradeProgressRequired;

        public bool TryIncreaseTotalUpgradeLevel()
        {
            if (!CanIncreaseTotalUpgradeLevel || totalUpgradeLevel >= int.MaxValue / GameBalance.StatLevelsPerTotalUpgradeLevel)
                return false;

            totalUpgradeLevel = TotalUpgradeLevel + 1;
            return true;
        }

        public void Normalize()
        {
            int highestStatLevel = Math.Max(
                Math.Max(attackLevel, criticalChanceLevel),
                Math.Max(criticalDamageLevel, Math.Max(skillDamageLevel, bossDamageLevel)));
            long inferredLevelLong = Math.Max(1L,
                ((long)highestStatLevel + GameBalance.StatLevelsPerTotalUpgradeLevel - 1L) /
                GameBalance.StatLevelsPerTotalUpgradeLevel);
            int inferredLevel = (int)Math.Min(inferredLevelLong,
                int.MaxValue / GameBalance.StatLevelsPerTotalUpgradeLevel);
            totalUpgradeLevel = Math.Max(TotalUpgradeLevel, inferredLevel);
            equipmentInventory ??= new List<EquipmentSaveEntry>();
            equipmentInventory.RemoveAll(entry => entry == null || string.IsNullOrWhiteSpace(entry.equipmentId));
            foreach (EquipmentSaveEntry entry in equipmentInventory)
            {
                entry.level = Math.Max(0, entry.level);
                entry.duplicates = Math.Max(0, entry.duplicates);
            }
            armorGachaLevel = Mathf.Clamp(armorGachaLevel, 1, GachaBalance.MaxLevel);
            accessoryGachaLevel = Mathf.Clamp(accessoryGachaLevel, 1, GachaBalance.MaxLevel);
            skillGachaLevel = Mathf.Clamp(skillGachaLevel, 1, GachaBalance.MaxLevel);
            weaponGachaLevel = Mathf.Clamp(weaponGachaLevel, 1, GachaBalance.MaxLevel);
            armorGachaProgress = Math.Max(0, armorGachaProgress);
            accessoryGachaProgress = Math.Max(0, accessoryGachaProgress);
            skillGachaProgress = Math.Max(0, skillGachaProgress);
            weaponGachaProgress = Math.Max(0, weaponGachaProgress);
            skillInventory ??= new List<SkillSaveEntry>();
            skillInventory.RemoveAll(entry => entry == null || string.IsNullOrWhiteSpace(entry.skillId));
            foreach (SkillSaveEntry entry in skillInventory)
            {
                entry.level = Math.Max(0, entry.level);
                entry.duplicates = Math.Max(0, entry.duplicates);
            }
            equippedSkillIds ??= new List<string>();
            while (equippedSkillIds.Count < Battle.SkillBalance.MaxEquippedSkillCount)
                equippedSkillIds.Add(string.Empty);
            if (equippedSkillIds.Count > Battle.SkillBalance.MaxEquippedSkillCount)
                equippedSkillIds.RemoveRange(
                    Battle.SkillBalance.MaxEquippedSkillCount,
                    equippedSkillIds.Count - Battle.SkillBalance.MaxEquippedSkillCount);
            for (int i = 0; i < equippedSkillIds.Count; i++)
                equippedSkillIds[i] ??= string.Empty;
            if (guideMissionLayoutVersion < 2)
            {
                guideMissionIndex = 0;
                guideMissionProgress = 0;
                guideMissionLayoutVersion = 2;
            }
            guideMissionIndex = Math.Max(0, guideMissionIndex);
            guideMissionProgress = Math.Max(0, guideMissionProgress);
            version = 8;
        }
    }

    public static class SaveService
    {
        private const string Key = "LostFamiliar.Save.v1";
        public static GameSaveData Load()
        {
            if (!PlayerPrefs.HasKey(Key)) return new GameSaveData();
            try
            {
                GameSaveData data = JsonUtility.FromJson<GameSaveData>(PlayerPrefs.GetString(Key)) ?? new GameSaveData();
                data.Normalize();
                return data;
            }
            catch (Exception) { return new GameSaveData(); }
        }

        public static void Save(GameSaveData data)
        {
            PlayerPrefs.SetString(Key, JsonUtility.ToJson(data));
            PlayerPrefs.Save();
        }

        public static void Delete()
        {
            PlayerPrefs.DeleteKey(Key);
            PlayerPrefs.Save();
        }
    }
}

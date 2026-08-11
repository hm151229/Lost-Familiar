using System;
using System.Collections.Generic;
using UnityEngine;

namespace LostFamiliar.Core
{
    [Serializable]
    public sealed class GameSaveData
    {
        public int version = 12;
        public long lastSavedUtcTicks;
        public double pendingOfflineGold;
        public double pendingOfflineSeconds;
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
        public TowerProgressData goldTower = new TowerProgressData();
        public TowerProgressData gemTower = new TowerProgressData();

        public TowerProgressData GetTower(TowerType type) =>
            type == TowerType.Gold ? goldTower : gemTower;

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
            goldTower ??= new TowerProgressData();
            gemTower ??= new TowerProgressData();
            goldTower.Normalize();
            gemTower.Normalize();
            if (guideMissionLayoutVersion < 2)
            {
                guideMissionIndex = 0;
                guideMissionProgress = 0;
                guideMissionLayoutVersion = 2;
            }
            guideMissionIndex = Math.Max(0, guideMissionIndex);
            guideMissionProgress = Math.Max(0, guideMissionProgress);
            lastSavedUtcTicks = Math.Max(0L, lastSavedUtcTicks);
            pendingOfflineGold = Math.Max(0d, pendingOfflineGold);
            pendingOfflineSeconds = Math.Max(0d, Math.Min(12d * 60d * 60d, pendingOfflineSeconds));
            version = 12;
        }
    }
}

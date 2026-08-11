using System;
using System.Collections.Generic;
using LostFamiliar.Core;
using UnityEngine;

namespace LostFamiliar.Battle
{
    public sealed class SkillInventory
    {
        private const string ResourcePath = "StageData/Skills";

        private readonly GameSaveData _saveData;
        private readonly SkillData[] _skills;

        public SkillInventory(GameSaveData saveData)
        {
            _saveData = saveData;
            _skills = Resources.LoadAll<SkillData>(ResourcePath);

            EnsureEquippedSlots();
        }

        public SkillData Find(string skillId)
        {
            if (string.IsNullOrWhiteSpace(skillId))
                return null;

            foreach (SkillData skill in _skills)
            {
                if (skill != null && skill.id == skillId)
                    return skill;
            }

            return null;
        }

        public SkillSaveEntry GetState(string skillId)
        {
            if (_saveData?.skillInventory == null ||
                string.IsNullOrWhiteSpace(skillId))
                return null;

            return _saveData.skillInventory.Find(
                entry =>
                    entry != null &&
                    entry.skillId == skillId);
        }

        public SkillSaveEntry Grant(
            string skillId,
            int amount = 1)
        {
            SkillData skill = Find(skillId);

            if (skill == null || amount <= 0)
                return null;

            SkillSaveEntry entry =
                _saveData.GetOrCreateSkill(skill.id);

            for (int i = 0; i < amount; i++)
            {
                if (entry.level <= 0)
                {
                    entry.level = 1;
                    entry.duplicates = 0;
                }
                else
                {
                    entry.duplicates++;
                }
            }

            return entry;
        }

        public IReadOnlyList<SkillData> GetOwnedSkills()
        {
            List<SkillData> owned =
                new List<SkillData>();

            foreach (SkillData skill in _skills)
            {
                if (skill == null)
                    continue;

                SkillSaveEntry state =
                    GetState(skill.id);

                if (state != null &&
                    state.level > 0)
                {
                    owned.Add(skill);
                }
            }

            owned.Sort((left, right) =>
            {
                int rarity =
                    right.rarity.CompareTo(left.rarity);

                return rarity != 0
                    ? rarity
                    : string.Compare(
                        left.displayName,
                        right.displayName,
                        StringComparison.Ordinal);
            });

            return owned;
        }

        public int GetUnlockedSlotCount(
            int playerLevel)
        {
            return SkillBalance.UnlockedSlotCount(
                playerLevel);
        }

        public bool IsSlotUnlocked(
            int slotIndex,
            int playerLevel)
        {
            return slotIndex >= 0 &&
                   slotIndex <
                   GetUnlockedSlotCount(playerLevel);
        }

        private void EnsureEquippedSlots()
        {
            if (_saveData == null)
                return;

            _saveData.equippedSkillIds ??=
                new List<string>();

            while (_saveData.equippedSkillIds.Count <
                   SkillBalance.MaxEquippedSkillCount)
            {
                _saveData.equippedSkillIds.Add(
                    string.Empty);
            }

            if (_saveData.equippedSkillIds.Count >
                SkillBalance.MaxEquippedSkillCount)
            {
                _saveData.equippedSkillIds.RemoveRange(
                    SkillBalance.MaxEquippedSkillCount,
                    _saveData.equippedSkillIds.Count -
                    SkillBalance.MaxEquippedSkillCount);
            }
        }

        public string GetEquippedSkillId(
            int slotIndex)
        {
            EnsureEquippedSlots();

            if (_saveData == null ||
                slotIndex < 0 ||
                slotIndex >=
                _saveData.equippedSkillIds.Count)
                return string.Empty;

            return _saveData.equippedSkillIds[
                slotIndex];
        }

        public SkillData GetEquippedSkill(
            int slotIndex)
        {
            return Find(
                GetEquippedSkillId(slotIndex));
        }

        public bool TryEquip(
            string skillId,
            int slotIndex,
            int playerLevel)
        {
            SkillSaveEntry state =
                GetState(skillId);

            if (!IsSlotUnlocked(
                    slotIndex,
                    playerLevel) ||
                state == null ||
                state.level <= 0)
                return false;

            EnsureEquippedSlots();

            for (int i = 0;
                 i < _saveData.equippedSkillIds.Count;
                 i++)
            {
                if (_saveData.equippedSkillIds[i] ==
                    skillId)
                {
                    _saveData.equippedSkillIds[i] =
                        string.Empty;
                }
            }

            _saveData.equippedSkillIds[slotIndex] =
                skillId;

            return true;
        }

        public bool Unequip(int slotIndex)
        {
            EnsureEquippedSlots();

            if (_saveData == null ||
                slotIndex < 0 ||
                slotIndex >=
                _saveData.equippedSkillIds.Count ||
                string.IsNullOrEmpty(
                    _saveData.equippedSkillIds[
                        slotIndex]))
                return false;

            _saveData.equippedSkillIds[
                slotIndex] = string.Empty;

            return true;
        }

        public bool CanUpgrade(string skillId)
        {
            SkillData skill = Find(skillId);
            SkillSaveEntry state =
                GetState(skillId);

            return
                skill != null &&
                state != null &&
                state.level > 0 &&
                state.level < skill.maxLevel &&
                state.duplicates >=
                SkillBalance.DuplicateRequirement(
                    state.level);
        }

        public bool TryUpgrade(string skillId)
        {
            if (!CanUpgrade(skillId))
                return false;

            SkillSaveEntry state =
                GetState(skillId);

            int required =
                SkillBalance.DuplicateRequirement(
                    state.level);

            state.duplicates -= required;
            state.level++;

            return true;
        }

        public int TryUpgradeAll()
        {
            if (_saveData?.skillInventory == null)
                return 0;

            int upgradedCount = 0;

            foreach (SkillData skill in _skills)
            {
                if (skill == null)
                    continue;

                SkillSaveEntry state =
                    GetState(skill.id);

                while (
                    state != null &&
                    state.level > 0 &&
                    state.level < skill.maxLevel)
                {
                    int required =
                        SkillBalance.DuplicateRequirement(
                            state.level);

                    if (state.duplicates < required)
                        break;

                    state.duplicates -= required;
                    state.level++;

                    upgradedCount++;
                }
            }

            return upgradedCount;
        }

        public void AddOwnedBonuses(
            ref EquipmentBonuses bonuses)
        {
            if (_saveData?.skillInventory == null)
                return;

            foreach (SkillSaveEntry entry
                     in _saveData.skillInventory)
            {
                if (entry == null ||
                    entry.level <= 0)
                    continue;

                SkillData skill =
                    Find(entry.skillId);

                if (skill == null)
                    continue;

                bonuses.Add(
                    skill.ownedEffectType,
                    SkillBalance.OwnedEffectValue(
                        skill,
                        entry.level));
            }
        }

        public void BuildEquippedSkills(
            int playerLevel,
            out SkillData[] equipped,
            out int[] levels)
        {
            EnsureEquippedSlots();

            equipped =
                new SkillData[
                    SkillBalance.MaxEquippedSkillCount];

            levels =
                new int[
                    SkillBalance.MaxEquippedSkillCount];

            int unlocked =
                GetUnlockedSlotCount(playerLevel);

            for (int i = 0;
                 i < equipped.Length;
                 i++)
            {
                if (i >= unlocked)
                {
                    _saveData.equippedSkillIds[i] =
                        string.Empty;

                    levels[i] = 1;
                    continue;
                }

                SkillData skill =
                    Find(
                        _saveData.equippedSkillIds[i]);

                SkillSaveEntry state =
                    GetState(
                        skill != null
                            ? skill.id
                            : null);

                bool valid =
                    state != null &&
                    state.level > 0;

                equipped[i] =
                    valid ? skill : null;

                levels[i] =
                    valid ? state.level : 1;

                if (!valid)
                {
                    _saveData.equippedSkillIds[i] =
                        string.Empty;
                }
            }
        }
    }
}

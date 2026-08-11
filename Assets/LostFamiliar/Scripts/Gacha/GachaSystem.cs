using System.Collections.Generic;
using LostFamiliar.Core;
using UnityEngine;

namespace LostFamiliar.Battle
{
    public sealed class GachaSystem
    {
        private readonly GameSaveData _saveData;
        private readonly EquipmentDatabase _equipmentDatabase;
        private readonly SkillInventory _skillInventory;

        public GachaSystem(
            GameSaveData saveData,
            EquipmentDatabase equipmentDatabase,
            SkillInventory skillInventory)
        {
            _saveData = saveData;
            _equipmentDatabase = equipmentDatabase;
            _skillInventory = skillInventory;
        }

        public int GetLevel(GachaCategory category)
        {
            return _saveData?.GetGachaLevel(category) ?? 1;
        }

        public int GetProgress(GachaCategory category)
        {
            return _saveData?.GetGachaProgress(category) ?? 0;
        }

        private List<EquipmentData> GetEquipmentPool(
            GachaCategory category)
        {
            List<EquipmentData> pool =
                new List<EquipmentData>();

            if (_equipmentDatabase?.items == null)
                return pool;

            foreach (EquipmentData item
                     in _equipmentDatabase.items)
            {
                if (item == null)
                    continue;

                bool matches = category switch
                {
                    GachaCategory.Armor =>
                        item.type == EquipmentType.Head ||
                        item.type == EquipmentType.Body ||
                        item.type == EquipmentType.Shoes,

                    GachaCategory.Accessory =>
                        item.type == EquipmentType.Accessory,

                    GachaCategory.Weapon =>
                        item.type == EquipmentType.Weapon,

                    _ => false
                };

                if (matches)
                    pool.Add(item);
            }

            return pool;
        }

        private static void RollEquipment(
            IReadOnlyList<EquipmentData> pool,
            int level,
            int count,
            List<GachaReward> output)
        {
            HashSet<EquipmentRarity> available =
                new HashSet<EquipmentRarity>();

            foreach (EquipmentData item in pool)
            {
                if (item != null)
                    available.Add(item.rarity);
            }

            if (available.Count == 0)
                return;

            for (int i = 0; i < count; i++)
            {
                EquipmentRarity rarity =
                    GachaBalance.RollRarity(
                        level,
                        available);

                List<EquipmentData> candidates =
                    new List<EquipmentData>();

                foreach (EquipmentData item in pool)
                {
                    if (item != null &&
                        item.rarity == rarity)
                    {
                        candidates.Add(item);
                    }
                }

                if (candidates.Count == 0)
                    return;

                EquipmentData selected =
                    candidates[
                        Random.Range(
                            0,
                            candidates.Count)];

                output.Add(
                    new GachaReward(selected));
            }
        }

        private static void RollSkills(
            IReadOnlyList<SkillData> pool,
            int level,
            int count,
            List<GachaReward> output)
        {
            HashSet<EquipmentRarity> available =
                new HashSet<EquipmentRarity>();

            foreach (SkillData skill in pool)
            {
                if (skill != null)
                    available.Add(skill.rarity);
            }

            if (available.Count == 0)
                return;

            for (int i = 0; i < count; i++)
            {
                EquipmentRarity rarity =
                    GachaBalance.RollRarity(
                        level,
                        available);

                List<SkillData> candidates =
                    new List<SkillData>();

                foreach (SkillData skill in pool)
                {
                    if (skill != null &&
                        skill.rarity == rarity)
                    {
                        candidates.Add(skill);
                    }
                }

                if (candidates.Count == 0)
                    return;

                SkillData selected =
                    candidates[
                        Random.Range(
                            0,
                            candidates.Count)];

                output.Add(
                    new GachaReward(selected));
            }
        }

        public bool TryDraw(
            GachaCategory category,
            int drawCount,
            out List<GachaReward> rewards)
        {
            rewards =
                new List<GachaReward>();

            if (_saveData == null)
                return false;

            if (drawCount != 10 &&
                drawCount != 30)
                return false;

            int cost =
                GachaBalance.Cost(drawCount);

            if (_saveData.gems < cost)
                return false;

            int level =
                GetLevel(category);

            if (category == GachaCategory.Skill)
            {
                IReadOnlyList<SkillData> skills =
                    _skillInventory?.AllSkills;

                if (skills == null ||
                    skills.Count == 0)
                    return false;

                RollSkills(
                    skills,
                    level,
                    drawCount,
                    rewards);
            }
            else
            {
                List<EquipmentData> pool =
                    GetEquipmentPool(category);

                if (pool.Count == 0)
                    return false;

                RollEquipment(
                    pool,
                    level,
                    drawCount,
                    rewards);
            }

            if (rewards.Count != drawCount)
            {
                rewards.Clear();
                return false;
            }

            _saveData.gems -= cost;

            _saveData.AddGachaProgress(
                category,
                drawCount);

            return true;
        }
    }
}

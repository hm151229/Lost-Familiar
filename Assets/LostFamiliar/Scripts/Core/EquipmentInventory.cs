using System;
using System.Collections.Generic;

namespace LostFamiliar.Core
{
    public struct EquipmentBonuses
    {
        public float attackPercent;
        public float maxHealthPercent;
        public float attackSpeedPercent;
        public float criticalChancePercentPoint;
        public float criticalDamagePercent;
        public float skillDamagePercent;
        public float bossDamagePercent;

        public void Add(EquipmentEffectType type, float value)
        {
            switch (type)
            {
                case EquipmentEffectType.AttackPercent: attackPercent += value; break;
                case EquipmentEffectType.MaxHealthPercent: maxHealthPercent += value; break;
                case EquipmentEffectType.AttackSpeedPercent: attackSpeedPercent += value; break;
                case EquipmentEffectType.CriticalChancePercentPoint: criticalChancePercentPoint += value; break;
                case EquipmentEffectType.CriticalDamagePercent: criticalDamagePercent += value; break;
                case EquipmentEffectType.SkillDamagePercent: skillDamagePercent += value; break;
                case EquipmentEffectType.BossDamagePercent: bossDamagePercent += value; break;
            }
        }
    }

    public sealed class EquipmentInventory
    {
        public event Action Changed;

        public EquipmentDatabase Database { get; }
        private readonly GameSaveData _saveData;

        public EquipmentInventory(GameSaveData saveData, EquipmentDatabase database)
        {
            _saveData = saveData;
            Database = database;
        }

        public EquipmentSaveEntry GetState(string equipmentId)
        {
            return _saveData?.FindEquipment(equipmentId);
        }

        // 장비 소환 결과는 이 메서드로 지급한다. 첫 획득은 Lv.1 해금, 이후 획득은 중복 수량이 된다.
        public EquipmentSaveEntry Grant(string equipmentId, int amount = 1)
        {
            EquipmentSaveEntry entry = GrantInternal(equipmentId, amount);
            if (entry != null)
                Changed?.Invoke();
            return entry;
        }

        public int GrantBatch(IReadOnlyList<string> equipmentIds)
        {
            if (equipmentIds == null)
                return 0;
            int granted = 0;
            foreach (string id in equipmentIds)
                if (GrantInternal(id, 1) != null) granted++;
            if (granted > 0)
                Changed?.Invoke();
            return granted;
        }

        private EquipmentSaveEntry GrantInternal(string equipmentId, int amount)
        {
            EquipmentData data = Database?.Get(equipmentId);
            if (data == null || amount <= 0)
                return null;

            EquipmentSaveEntry entry = _saveData.GetOrCreateEquipment(equipmentId);
            if (entry.level <= 0)
            {
                entry.level = 1;
                amount--;
            }
            entry.duplicates += Math.Max(0, amount);
            return entry;
        }

        public int GetDuplicateRequirement(string equipmentId)
        {
            EquipmentSaveEntry entry = _saveData.FindEquipment(equipmentId);
            return entry == null || entry.level <= 0
                ? 0
                : EquipmentBalance.DuplicateRequirement(entry.level);
        }

        public bool CanUpgrade(string equipmentId)
        {
            EquipmentData data = Database?.Get(equipmentId);
            EquipmentSaveEntry entry = _saveData.FindEquipment(equipmentId);
            return data != null && entry != null && entry.level > 0 && entry.level < data.maxLevel &&
                   entry.duplicates >= EquipmentBalance.DuplicateRequirement(entry.level);
        }

        public bool TryUpgrade(string equipmentId)
        {
            if (!CanUpgrade(equipmentId))
                return false;

            EquipmentSaveEntry entry = _saveData.FindEquipment(equipmentId);
            entry.duplicates -= EquipmentBalance.DuplicateRequirement(entry.level);
            entry.level++;
            Changed?.Invoke();
            return true;
        }

        public int TryUpgradeAll(EquipmentType equipmentType)
        {
            if (Database?.items == null)
                return 0;

            int upgradedCount = 0;
            foreach (EquipmentData data in Database.items)
            {
                if (data == null || data.type != equipmentType)
                    continue;

                EquipmentSaveEntry entry = _saveData.FindEquipment(data.Id);
                while (entry != null && entry.level > 0 && entry.level < data.maxLevel)
                {
                    int requirement = EquipmentBalance.DuplicateRequirement(entry.level);
                    if (entry.duplicates < requirement)
                        break;
                    entry.duplicates -= requirement;
                    entry.level++;
                    upgradedCount++;
                }
            }

            if (upgradedCount > 0)
                Changed?.Invoke();
            return upgradedCount;
        }

        public bool AutoEquipBest()
        {
            if (Database?.items == null)
                return false;

            List<EquipmentData> accessories = GetOwnedSortedByPower(EquipmentType.Accessory);
            bool changed = false;
            changed |= SetEquippedIfDifferent(EquipmentSlot.Head, FindBestOwned(EquipmentType.Head)?.Id);
            changed |= SetEquippedIfDifferent(EquipmentSlot.Body, FindBestOwned(EquipmentType.Body)?.Id);
            changed |= SetEquippedIfDifferent(EquipmentSlot.Shoes, FindBestOwned(EquipmentType.Shoes)?.Id);
            changed |= SetEquippedIfDifferent(EquipmentSlot.Weapon, FindBestOwned(EquipmentType.Weapon)?.Id);
            changed |= SetEquippedIfDifferent(EquipmentSlot.Accessory1,
                accessories.Count > 0 ? accessories[0].Id : null);
            changed |= SetEquippedIfDifferent(EquipmentSlot.Accessory2,
                accessories.Count > 1 ? accessories[1].Id : null);

            if (changed)
                Changed?.Invoke();
            return changed;
        }

        public bool AutoEquipBest(EquipmentType equipmentType)
        {
            if (Database?.items == null)
                return false;

            bool changed;
            if (equipmentType == EquipmentType.Accessory)
            {
                List<EquipmentData> accessories = GetOwnedSortedByPower(EquipmentType.Accessory);
                changed = SetEquippedIfDifferent(
                    EquipmentSlot.Accessory1,
                    accessories.Count > 0 ? accessories[0].Id : null);
                changed |= SetEquippedIfDifferent(
                    EquipmentSlot.Accessory2,
                    accessories.Count > 1 ? accessories[1].Id : null);
            }
            else
            {
                EquipmentSlot slot = equipmentType switch
                {
                    EquipmentType.Head => EquipmentSlot.Head,
                    EquipmentType.Body => EquipmentSlot.Body,
                    EquipmentType.Shoes => EquipmentSlot.Shoes,
                    EquipmentType.Weapon => EquipmentSlot.Weapon,
                    _ => EquipmentSlot.Weapon
                };
                changed = SetEquippedIfDifferent(slot, FindBestOwned(equipmentType)?.Id);
            }

            if (changed)
                Changed?.Invoke();
            return changed;
        }

        public bool HasUpgradeableEquipment(EquipmentType equipmentType)
        {
            if (Database?.items == null)
                return false;
            foreach (EquipmentData data in Database.items)
                if (data != null && data.type == equipmentType && CanUpgrade(data.Id)) return true;
            return false;
        }

        public bool CanAutoEquipBetter()
        {
            return CanAutoEquipBetter(EquipmentType.Head) ||
                   CanAutoEquipBetter(EquipmentType.Body) ||
                   CanAutoEquipBetter(EquipmentType.Shoes) ||
                   CanAutoEquipBetter(EquipmentType.Accessory) ||
                   CanAutoEquipBetter(EquipmentType.Weapon);
        }

        public bool CanAutoEquipBetter(EquipmentType equipmentType)
        {
            if (Database?.items == null)
                return false;

            if (equipmentType != EquipmentType.Accessory)
            {
                EquipmentSlot slot = equipmentType switch
                {
                    EquipmentType.Head => EquipmentSlot.Head,
                    EquipmentType.Body => EquipmentSlot.Body,
                    EquipmentType.Shoes => EquipmentSlot.Shoes,
                    EquipmentType.Weapon => EquipmentSlot.Weapon,
                    _ => EquipmentSlot.Weapon
                };
                return IsBetterThanEquipped(slot, FindBestOwned(equipmentType));
            }

            List<EquipmentData> accessories = GetOwnedSortedByPower(EquipmentType.Accessory);
            float bestAccessoryPower = 0f;
            if (accessories.Count > 0) bestAccessoryPower += GetPowerScore(accessories[0]);
            if (accessories.Count > 1) bestAccessoryPower += GetPowerScore(accessories[1]);
            float equippedAccessoryPower = GetEquippedPower(EquipmentSlot.Accessory1) +
                                           GetEquippedPower(EquipmentSlot.Accessory2);
            return bestAccessoryPower > equippedAccessoryPower + .0001f;
        }

        public bool TryEquip(string equipmentId, EquipmentSlot slot)
        {
            EquipmentData data = Database?.Get(equipmentId);
            EquipmentSaveEntry entry = _saveData.FindEquipment(equipmentId);
            if (data == null || entry == null || entry.level <= 0 || !CanUseSlot(data.type, slot))
                return false;

            // 같은 장비 하나를 두 슬롯에 동시에 장착하지 않고, 새 슬롯으로 이동시킨다.
            foreach (EquipmentSlot otherSlot in Enum.GetValues(typeof(EquipmentSlot)))
            {
                if (GetEquippedId(otherSlot) == equipmentId)
                    SetEquippedId(otherSlot, string.Empty);
            }

            SetEquippedId(slot, equipmentId);
            Changed?.Invoke();
            return true;
        }

        public void Unequip(EquipmentSlot slot)
        {
            if (string.IsNullOrEmpty(GetEquippedId(slot)))
                return;

            SetEquippedId(slot, string.Empty);
            Changed?.Invoke();
        }

        public string GetEquippedId(EquipmentSlot slot) => slot switch
        {
            EquipmentSlot.Head => _saveData.equippedHeadId,
            EquipmentSlot.Body => _saveData.equippedBodyId,
            EquipmentSlot.Shoes => _saveData.equippedShoesId,
            EquipmentSlot.Accessory1 => _saveData.equippedAccessory1Id,
            EquipmentSlot.Accessory2 => _saveData.equippedAccessory2Id,
            EquipmentSlot.Weapon => _saveData.equippedWeaponId,
            _ => string.Empty
        };

        public bool IsEquipped(string equipmentId)
        {
            if (string.IsNullOrEmpty(equipmentId))
                return false;
            foreach (EquipmentSlot slot in Enum.GetValues(typeof(EquipmentSlot)))
                if (GetEquippedId(slot) == equipmentId) return true;
            return false;
        }

        public EquipmentBonuses CalculateBonuses()
        {
            EquipmentBonuses bonuses = default;

            // 보유 효과는 장착 여부와 관계없이 획득한 모든 장비에서 적용한다.
            if (Database?.items != null)
            {
                foreach (EquipmentData data in Database.items)
                {
                    EquipmentSaveEntry entry = data != null ? _saveData.FindEquipment(data.Id) : null;
                    if (data == null || entry == null || entry.level <= 0)
                        continue;

                    if (data.ownedEffects != null && data.ownedEffects.Length > 0)
                    {
                        foreach (EquipmentEffectDefinition effect in data.ownedEffects)
                            bonuses.Add(effect.type,
                                EquipmentBalance.EffectValue(data, effect.baseValue, entry.level));
                    }
                    else if (data.effects != null)
                    {
                        foreach (EquipmentEffectDefinition effect in data.effects)
                        {
                            float ownedRatio = data.ownedEffectRatio > 0f
                                ? data.ownedEffectRatio
                                : EquipmentBalance.DefaultOwnedEffectRatio;
                            bonuses.Add(effect.type,
                                EquipmentBalance.EffectValue(data, effect.baseValue, entry.level) *
                                ownedRatio);
                        }
                    }
                }
            }

            // 장착 효과는 실제 장착 슬롯에 들어간 장비에서만 추가 적용한다.
            HashSet<string> appliedIds = new HashSet<string>();
            foreach (EquipmentSlot slot in Enum.GetValues(typeof(EquipmentSlot)))
            {
                string id = GetEquippedId(slot);
                if (string.IsNullOrEmpty(id) || !appliedIds.Add(id))
                    continue;

                EquipmentData data = Database?.Get(id);
                EquipmentSaveEntry entry = _saveData.FindEquipment(id);
                if (data?.effects == null || entry == null || entry.level <= 0)
                    continue;

                foreach (EquipmentEffectDefinition effect in data.effects)
                    bonuses.Add(effect.type, EquipmentBalance.EffectValue(data, effect.baseValue, entry.level));
            }
            return bonuses;
        }

        private static bool CanUseSlot(EquipmentType type, EquipmentSlot slot) => type switch
        {
            EquipmentType.Head => slot == EquipmentSlot.Head,
            EquipmentType.Body => slot == EquipmentSlot.Body,
            EquipmentType.Shoes => slot == EquipmentSlot.Shoes,
            EquipmentType.Accessory => slot == EquipmentSlot.Accessory1 || slot == EquipmentSlot.Accessory2,
            EquipmentType.Weapon => slot == EquipmentSlot.Weapon,
            _ => false
        };

        private EquipmentData FindBestOwned(EquipmentType type)
        {
            List<EquipmentData> sorted = GetOwnedSortedByPower(type);
            return sorted.Count > 0 ? sorted[0] : null;
        }

        private List<EquipmentData> GetOwnedSortedByPower(EquipmentType type)
        {
            List<EquipmentData> result = new List<EquipmentData>();
            foreach (EquipmentData data in Database.items)
            {
                EquipmentSaveEntry state = data != null ? _saveData.FindEquipment(data.Id) : null;
                if (data != null && data.type == type && state != null && state.level > 0)
                    result.Add(data);
            }

            result.Sort((left, right) =>
            {
                int power = GetPowerScore(right).CompareTo(GetPowerScore(left));
                if (power != 0)
                    return power;
                int rarity = right.rarity.CompareTo(left.rarity);
                return rarity != 0 ? rarity : string.Compare(right.displayName, left.displayName, StringComparison.Ordinal);
            });
            return result;
        }

        public float GetPowerScore(EquipmentData data)
        {
            EquipmentSaveEntry state = data != null ? _saveData.FindEquipment(data.Id) : null;
            if (data?.effects == null || state == null || state.level <= 0)
                return 0f;

            float score = 0f;
            foreach (EquipmentEffectDefinition effect in data.effects)
            {
                float value = EquipmentBalance.EffectValue(data, effect.baseValue, state.level);
                float weight = effect.type switch
                {
                    EquipmentEffectType.MaxHealthPercent => .5f,
                    EquipmentEffectType.AttackSpeedPercent => 1.2f,
                    EquipmentEffectType.CriticalChancePercentPoint => 5f,
                    EquipmentEffectType.CriticalDamagePercent => .7f,
                    _ => 1f
                };
                score += value * weight;
            }
            return score;
        }

        private bool SetEquippedIfDifferent(EquipmentSlot slot, string id)
        {
            id ??= string.Empty;
            if (GetEquippedId(slot) == id)
                return false;
            SetEquippedId(slot, id);
            return true;
        }

        private bool IsBetterThanEquipped(EquipmentSlot slot, EquipmentData candidate)
        {
            if (candidate == null)
                return false;
            return GetPowerScore(candidate) > GetEquippedPower(slot) + .0001f;
        }

        private float GetEquippedPower(EquipmentSlot slot)
        {
            string id = GetEquippedId(slot);
            return string.IsNullOrEmpty(id) ? 0f : GetPowerScore(Database?.Get(id));
        }

        private void SetEquippedId(EquipmentSlot slot, string id)
        {
            switch (slot)
            {
                case EquipmentSlot.Head: _saveData.equippedHeadId = id; break;
                case EquipmentSlot.Body: _saveData.equippedBodyId = id; break;
                case EquipmentSlot.Shoes: _saveData.equippedShoesId = id; break;
                case EquipmentSlot.Accessory1: _saveData.equippedAccessory1Id = id; break;
                case EquipmentSlot.Accessory2: _saveData.equippedAccessory2Id = id; break;
                case EquipmentSlot.Weapon: _saveData.equippedWeaponId = id; break;
            }
        }
    }
}

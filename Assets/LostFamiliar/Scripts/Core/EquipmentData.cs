using System;
using UnityEngine;

namespace LostFamiliar.Core
{
    public enum EquipmentRarity { Common, Rare, Epic, Legend, Mythic }
    public enum EquipmentType { Head, Body, Shoes, Accessory, Weapon }
    public enum EquipmentSlot { Head, Body, Shoes, Accessory1, Accessory2, Weapon }
    public enum EquipmentEffectType
    {
        AttackPercent,
        MaxHealthPercent,
        AttackSpeedPercent,
        CriticalChancePercentPoint,
        CriticalDamagePercent,
        SkillDamagePercent,
        BossDamagePercent
    }

    [Serializable]
    public struct EquipmentEffectDefinition
    {
        public EquipmentEffectType type;
        [Min(0f)] public float baseValue;
    }

    [CreateAssetMenu(menuName = "Lost Familiar/Equipment/Equipment Data", fileName = "Equipment_")]
    public sealed class EquipmentData : ScriptableObject
    {
        public string id;
        public string displayName;
        public EquipmentType type;
        public EquipmentRarity rarity;
        public Sprite icon;
        [Min(1)] public int maxLevel = 100;
        [InspectorName("장착 효과")]
        public EquipmentEffectDefinition[] effects;
        [InspectorName("보유 효과")]
        public EquipmentEffectDefinition[] ownedEffects;
        [InspectorName("보유 효과 자동 비율"), Range(0f, 1f)]
        public float ownedEffectRatio = .2f;

        public string Id => string.IsNullOrWhiteSpace(id) ? name : id;
    }

    public static class EquipmentBalance
    {
        public const float DefaultOwnedEffectRatio = .2f;

        public static Color RarityColor(EquipmentRarity rarity) => rarity switch
        {
            EquipmentRarity.Common => FromHex(0xB7, 0xBD, 0xC7),
            EquipmentRarity.Rare => FromHex(0x66, 0xC9, 0x4F),
            EquipmentRarity.Epic => FromHex(0x4D, 0xA8, 0xFF),
            EquipmentRarity.Legend => FromHex(0xA5, 0x66, 0xFF),
            EquipmentRarity.Mythic => FromHex(0xFF, 0xB3, 0x47),
            _ => Color.white
        };

        public static float RarityEffectMultiplier(EquipmentRarity rarity) => rarity switch
        {
            EquipmentRarity.Common => 1f,
            EquipmentRarity.Rare => 8f,
            EquipmentRarity.Epic => 25f,
            EquipmentRarity.Legend => 80f,
            EquipmentRarity.Mythic => 250f,
            _ => 1f
        };

        public static int DuplicateRequirement(int currentLevel)
        {
            return Mathf.Max(2, currentLevel + 1);
        }

        public static float EffectValue(EquipmentData equipment, float baseValue, int level)
        {
            if (equipment == null || level <= 0)
                return 0f;

            float levelMultiplier = 1f + Mathf.Max(0, level - 1) * .05f;
            return baseValue * RarityEffectMultiplier(equipment.rarity) * levelMultiplier;
        }

        private static Color FromHex(byte r, byte g, byte b) => new Color32(r, g, b, 0xFF);
    }
}

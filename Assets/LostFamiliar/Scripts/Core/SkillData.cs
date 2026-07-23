using LostFamiliar.Core;
using UnityEngine;

namespace LostFamiliar.Battle
{
    public enum SkillBehavior
    {
        MagicMissile,
        FireBall,
        IceSpear,
        LightningBolt,
        ArcaneOrb,
        WindCutter,
        Meteor,
        Blizzard,
        BlackHole,
        StarNova
    }

    [CreateAssetMenu(menuName = "Lost Familiar/Battle/Skill", fileName = "SkillData")]
    public sealed class SkillData : ScriptableObject
    {
        public string id = "magic_burst";
        public EquipmentRarity rarity = EquipmentRarity.Common;
        public Sprite icon;
        [Min(1)] public int maxLevel = 100;
        public string displayName = "마력 폭발";
        [TextArea(2, 5)] public string description;
        public SkillBehavior behavior = SkillBehavior.MagicMissile;
        public SkillTargetType targetType = SkillTargetType.NearestEnemy;
        [Min(0.1f)] public float cooldown = 5f;
        [Min(0f)] public float damageMultiplier = 3f;
        [Min(0f)] public float radius = 3f;
        [Min(1)] public int projectileCount = 1;
        [Min(0f)] public float duration;
        [Min(0.02f)] public float tickInterval = .5f;
        [Min(0f)] public float secondaryDamageMultiplier;
        [Range(0f, .95f)] public float slowPercent;
        [Min(0f)] public float pullStrength = 4f;
        public Color effectColor = new Color(.5f, .25f, 1f);
        [Header("보유 효과")]
        public EquipmentEffectType ownedEffectType = EquipmentEffectType.SkillDamagePercent;
        [Tooltip("0이면 희귀도별 기본 보유 효과 수치를 사용합니다.")]
        [Min(0f)] public float ownedEffectBaseValue;
    }

    public static class SkillBalance
    {
        public const int MaxEquippedSkillCount = 6;

        private static readonly int[] SlotUnlockLevels = { 1, 1, 10, 20, 30, 40 };

        public static int UnlockedSlotCount(int playerLevel)
        {
            int count = 0;
            for (int i = 0; i < SlotUnlockLevels.Length; i++)
                if (playerLevel >= SlotUnlockLevels[i]) count++;
            return count;
        }

        public static int SlotUnlockLevel(int slotIndex) =>
            slotIndex >= 0 && slotIndex < SlotUnlockLevels.Length
                ? SlotUnlockLevels[slotIndex]
                : int.MaxValue;

        public static int DuplicateRequirement(int currentLevel) => Mathf.Max(2, currentLevel + 1);

        public static float OwnedEffectValue(SkillData skill, int level)
        {
            if (skill == null || level <= 0)
                return 0f;

            float baseValue = skill.ownedEffectBaseValue > 0f
                ? skill.ownedEffectBaseValue
                : skill.rarity switch
                {
                    EquipmentRarity.Common => .5f,
                    EquipmentRarity.Rare => 1f,
                    EquipmentRarity.Epic => 2.5f,
                    EquipmentRarity.Legend => 5f,
                    EquipmentRarity.Mythic => 10f,
                    _ => .5f
                };
            return baseValue * (1f + Mathf.Max(0, level - 1) * .05f);
        }
    }
}

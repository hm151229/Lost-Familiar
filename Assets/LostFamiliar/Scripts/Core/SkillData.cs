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
        [Tooltip("블랙홀 중앙 구체에 닿았다고 판정할 피해 반경입니다. Radius는 흡입 범위로만 사용됩니다.")]
        [Min(0f)] public float blackHoleDamageRadius = .8f;
        public Color effectColor = new Color(.5f, .25f, 1f);

        [Header("스킬 이펙트 프리팹")]
        [Tooltip("적에게 날아가는 스킬 이펙트입니다. 비워두면 기존 임시 이펙트를 사용합니다.")]
        public GameObject projectileEffectPrefab;
        [Tooltip("FirePoint에서 표적까지 날아가는 시간입니다.")]
        [Min(.05f)] public float projectileTravelDuration = .25f;
        [Tooltip("Enemy 스프라이트 경계에 추가할 충돌 여유 거리입니다. 0이면 외형 경계에 닿는 순간 적중합니다.")]
        [Min(0f)] public float projectileImpactDistance = .6f;
        [Tooltip("표적 Enemy의 몸 중앙을 기준으로 한 상공 투사체 생성 위치입니다. X와 Y를 함께 주면 사선으로 낙하합니다.")]
        public Vector3 projectileSpawnOffset;
        [Tooltip("이펙트가 오른쪽을 바라보는 방향을 기준으로 한 추가 회전값입니다.")]
        public Vector3 projectileRotationOffset;
        [Tooltip("스킬이 적에게 적중할 때 생성되는 이펙트입니다.")]
        public GameObject hitEffectPrefab;
        [Tooltip("적 위치를 기준으로 한 피격 이펙트 생성 위치 보정값입니다.")]
        public Vector3 hitEffectOffset;
        [Min(.05f)] public float hitEffectLifetime = 1.5f;
        [Tooltip("스타노바처럼 스킬 중심에서 한 번 크게 발생하는 폭발 이펙트입니다.")]
        public GameObject explosionEffectPrefab;
        [Tooltip("폭발 중심을 기준으로 한 이펙트 생성 위치 보정값입니다.")]
        public Vector3 explosionEffectOffset;
        [Tooltip("폭발 이펙트의 추가 회전값입니다.")]
        public Vector3 explosionEffectRotation;
        [Tooltip("프리팹 원본 스케일에 곱할 값입니다. (1, 1, 1)이면 원본 크기를 그대로 사용합니다.")]
        public Vector3 explosionEffectScaleMultiplier = Vector3.one;
        [Min(.05f)] public float explosionEffectLifetime = 1.5f;
        [Tooltip("아케인 오브, 스타노바처럼 플레이어 주변에 생성되는 이펙트입니다.")]
        public GameObject playerAreaEffectPrefab;
        [Tooltip("플레이어 위치를 기준으로 한 주변 이펙트 생성 위치 보정값입니다.")]
        public Vector3 playerAreaEffectOffset;
        [Tooltip("0이면 스킬의 기본 지속시간을 사용합니다.")]
        [Min(0f)] public float playerAreaEffectLifetime;
        [Tooltip("블리자드, 블랙홀처럼 필드의 지정된 위치에 생성되는 범위 이펙트입니다.")]
        public GameObject worldAreaEffectPrefab;
        [Tooltip("스킬 범위 중심을 기준으로 한 이펙트 생성 위치 보정값입니다.")]
        public Vector3 worldAreaEffectOffset;
        [Tooltip("범위 이펙트의 추가 회전값입니다.")]
        public Vector3 worldAreaEffectRotation;
        [Tooltip("0이면 스킬의 기본 지속시간을 사용합니다.")]
        [Min(0f)] public float worldAreaEffectLifetime;

        [Header("보유 효과")]
        public EquipmentEffectType ownedEffectType = EquipmentEffectType.SkillDamagePercent;
        [Tooltip("0이면 희귀도별 기본 보유 효과 수치를 사용합니다.")]
        [Min(0f)] public float ownedEffectBaseValue;
    }

    public static class SkillBalance
    {
        public const int MaxEquippedSkillCount = 6;
        public const float EffectGrowthPerLevel = .1f;

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
            return baseValue * EquippedEffectMultiplier(level);
        }

        public static float EquippedEffectMultiplier(int level) =>
            level <= 0 ? 0f : 1f + Mathf.Max(0, level - 1) * EffectGrowthPerLevel;
    }
}

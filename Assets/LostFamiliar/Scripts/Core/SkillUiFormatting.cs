using LostFamiliar.Core;

namespace LostFamiliar.Battle
{
    public static class SkillUiFormatting
    {
        public static string Rarity(EquipmentRarity rarity) => rarity switch
        {
            EquipmentRarity.Common => "COMMON",
            EquipmentRarity.Rare => "RARE",
            EquipmentRarity.Epic => "EPIC",
            EquipmentRarity.Legend => "LEGEND",
            EquipmentRarity.Mythic => "MYTHIC",
            _ => rarity.ToString().ToUpperInvariant()
        };

        public static string EffectName(EquipmentEffectType type) => type switch
        {
            EquipmentEffectType.AttackPercent => "공격력",
            EquipmentEffectType.MaxHealthPercent => "체력",
            EquipmentEffectType.AttackSpeedPercent => "공격속도",
            EquipmentEffectType.CriticalChancePercentPoint => "치명타 확률",
            EquipmentEffectType.CriticalDamagePercent => "치명타 피해",
            EquipmentEffectType.SkillDamagePercent => "스킬 피해",
            EquipmentEffectType.BossDamagePercent => "보스 피해",
            _ => "보유 효과"
        };

        public static string Effect(SkillData skill, int level)
        {
            if (skill == null)
                return string.Empty;
            float value = SkillBalance.OwnedEffectValue(skill, level);
            return $"{EffectName(skill.ownedEffectType)} +{value:0.##}%";
        }
    }
}

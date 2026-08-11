using LostFamiliar.Core;

namespace LostFamiliar.Battle
{
    public readonly struct GachaReward
    {
        public readonly EquipmentData equipment;
        public readonly SkillData skill;
        public readonly EquipmentRarity rarity;

        public GachaReward(
            EquipmentData equipment)
        {
            this.equipment = equipment;
            skill = null;

            rarity = equipment != null
                ? equipment.rarity
                : EquipmentRarity.Common;
        }

        public GachaReward(
            SkillData skill)
        {
            equipment = null;
            this.skill = skill;

            rarity = skill != null
                ? skill.rarity
                : EquipmentRarity.Common;
        }

        public string DisplayName =>
            equipment != null
                ? equipment.displayName
                : skill?.displayName
                    ?? string.Empty;
    }
}

using System;
using System.Collections.Generic;
using UnityEngine;

namespace LostFamiliar.Core
{
    public enum GachaCategory { Armor, Accessory, Skill, Weapon }

    [Serializable]
    public struct GachaRateTable
    {
        public float common;
        public float rare;
        public float epic;
        public float legend;
        public float mythic;

        public float Get(EquipmentRarity rarity) => rarity switch
        {
            EquipmentRarity.Common => common,
            EquipmentRarity.Rare => rare,
            EquipmentRarity.Epic => epic,
            EquipmentRarity.Legend => legend,
            EquipmentRarity.Mythic => mythic,
            _ => 0f
        };
    }

    public static class GachaBalance
    {
        public const int GemCostPerDraw = 100;
        public const int MaxLevel = 5;

        private static readonly GachaRateTable[] Rates =
        {
            new GachaRateTable { common = 70f, rare = 30f },
            new GachaRateTable { common = 30f, rare = 60f, epic = 10f },
            new GachaRateTable { common = 10f, rare = 40f, epic = 45f, legend = 5f },
            new GachaRateTable { rare = 15f, epic = 45f, legend = 35f, mythic = 5f },
            new GachaRateTable { epic = 25f, legend = 55f, mythic = 20f }
        };

        public static int Cost(int drawCount) => Mathf.Max(0, drawCount) * GemCostPerDraw;

        public static int RequiredDraws(int level) => Mathf.Clamp(level, 1, MaxLevel) switch
        {
            1 => 100,
            2 => 200,
            3 => 300,
            4 => 500,
            _ => 0
        };

        public static GachaRateTable GetRates(int level) => Rates[Mathf.Clamp(level, 1, MaxLevel) - 1];

        public static EquipmentRarity RollRarity(int level, HashSet<EquipmentRarity> available)
        {
            GachaRateTable table = GetRates(level);
            float total = 0f;
            foreach (EquipmentRarity rarity in available)
                total += Mathf.Max(0f, table.Get(rarity));

            if (total <= 0f)
            {
                EquipmentRarity fallback = EquipmentRarity.Common;
                foreach (EquipmentRarity rarity in available)
                    if (rarity > fallback) fallback = rarity;
                return fallback;
            }

            float roll = UnityEngine.Random.value * total;
            foreach (EquipmentRarity rarity in Enum.GetValues(typeof(EquipmentRarity)))
            {
                if (!available.Contains(rarity))
                    continue;
                roll -= Mathf.Max(0f, table.Get(rarity));
                if (roll <= 0f)
                    return rarity;
            }
            return EquipmentRarity.Common;
        }
    }
}

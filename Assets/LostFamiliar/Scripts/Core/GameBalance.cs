using UnityEngine;

namespace LostFamiliar.Core
{
    public static class GameBalance
    {
        public static double UpgradeCost(StatType type, int level)
        {
            double baseCost = type switch
            {
                StatType.Attack => 10d,
                StatType.MaxHealth => 15d,
                StatType.AttackSpeed => 40d,
                StatType.CriticalChance => 60d,
                StatType.CriticalDamage => 75d,
                _ => 10d
            };
            return System.Math.Ceiling(baseCost * System.Math.Pow(1.16d, level));
        }

        public static double EnemyHealth(int stage, bool boss) =>
            System.Math.Ceiling(18d * System.Math.Pow(1.24d, stage - 1) * (boss ? 8d : 1d));

        public static double EnemyAttack(int stage, bool boss) =>
            System.Math.Ceiling(2d * System.Math.Pow(1.15d, stage - 1) * (boss ? 2d : 1d));

        public static double GoldReward(int stage, bool boss) =>
            System.Math.Ceiling(5d * System.Math.Pow(1.18d, stage - 1) * (boss ? 10d : 1d));

        public static double PlayerExperienceReward(int stage, bool boss) => stage * (boss ? 25d : 3d);
        public static double ExperienceToLevel(int level) => 20d + level * level * 8d;
        public static float StageProgressReward(bool boss) => boss ? 0f : 10f;
    }
}

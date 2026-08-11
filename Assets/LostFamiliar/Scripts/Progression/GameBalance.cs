using UnityEngine;

namespace LostFamiliar.Core
{
    public static class GameBalance
    {
        public const int StatLevelsPerTotalUpgradeLevel = 100;
        public const int UpgradeableStatCount = 5;

        public static double UpgradeCost(StatType type, int level)
        {
            double baseCost = type switch
            {
                StatType.Attack => 3d,
                StatType.CriticalChance => 6d,
                StatType.CriticalDamage => 8d,
                StatType.SkillDamage => 9d,
                StatType.BossDamage => 11d,
                _ => 3d
            };

            level = Mathf.Max(0, level);
            double earlyCurve = 1d + level * 0.025d + level * level * 0.00015d;
            // 1~100 레벨은 빠른 초반 성장을 유지하고, 100 이후부터 비용이 본격적으로 증가한다.
            double lateCurve = System.Math.Pow(1.018d, System.Math.Max(0, level - 100));
            double cost = baseCost * earlyCurve * lateCurve;
            return double.IsInfinity(cost) ? double.MaxValue : System.Math.Ceiling(cost);
        }

        public static double StatValue(StatType type, int level)
        {
            level = Mathf.Max(0, level);
            return type switch
            {
                StatType.Attack => level * 2d,
                StatType.CriticalChance => System.Math.Min(70d, 5d + level * .15d),
                StatType.CriticalDamage => 150d + level * 1.5d,
                StatType.SkillDamage => 100d + level * 2d,
                StatType.BossDamage => 100d + level * 2d,
                _ => 0d
            };
        }

        public static int MaxUpgradeLevel(StatType type)
        {
            return int.MaxValue;
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

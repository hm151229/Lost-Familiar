using System;
using UnityEngine;

namespace LostFamiliar.Core
{
    public readonly struct TowerRunSetup
    {
        public readonly TowerType type;
        public readonly int floor;
        public readonly float timeLimit;
        public readonly int normalEnemyCount;
        public readonly int bossCount;
        public readonly double normalEnemyHealth;
        public readonly double bossHealth;
        public readonly double enemyAttack;

        public TowerRunSetup(TowerType type, int floor)
        {
            this.type = type;
            this.floor = Math.Max(1, floor);
            timeLimit = TowerBalance.TimeLimit;
            normalEnemyCount = TowerBalance.NormalEnemyCount(this.floor);
            bossCount = TowerBalance.BossCount(this.floor);
            normalEnemyHealth = TowerBalance.EnemyHealth(this.floor);
            bossHealth = TowerBalance.BossHealth(this.floor);
            enemyAttack = TowerBalance.EnemyAttack(this.floor);
        }
    }

    public readonly struct TowerRunResult
    {
        public readonly TowerType type;
        public readonly int floor;
        public readonly TowerGrade grade;
        public readonly float remainingTime;
        public readonly double goldReward;
        public readonly int gemReward;
        public readonly bool nextFloorUnlocked;
        public readonly bool sweepUnlocked;

        public TowerRunResult(
            TowerType type, int floor, TowerGrade grade, float remainingTime,
            double goldReward, int gemReward, bool nextFloorUnlocked, bool sweepUnlocked)
        {
            this.type = type;
            this.floor = floor;
            this.grade = grade;
            this.remainingTime = remainingTime;
            this.goldReward = goldReward;
            this.gemReward = gemReward;
            this.nextFloorUnlocked = nextFloorUnlocked;
            this.sweepUnlocked = sweepUnlocked;
        }
    }

    public static class TowerBalance
    {
        public const int DailyTickets = 2;
        public const float TimeLimit = 30f;

        public static TowerGrade Grade(float remainingTime, bool cleared)
        {
            if (!cleared) return TowerGrade.F;
            if (remainingTime >= 20f) return TowerGrade.S;
            if (remainingTime >= 12f) return TowerGrade.A;
            if (remainingTime >= 5f) return TowerGrade.B;
            return TowerGrade.C;
        }

        public static int NormalEnemyCount(int floor) =>
            Mathf.Clamp(5 + Math.Max(0, floor - 1) * 3, 5, 60);

        public static int BossCount(int floor) =>
            Mathf.Clamp(1 + Math.Max(0, floor - 1) / 5, 1, 10);

        public static double EnemyHealth(int floor) =>
            Math.Ceiling(20d * Math.Pow(1.5d, Math.Max(0, floor - 1)));

        public static double BossHealth(int floor) =>
            Math.Ceiling(
                EnemyHealth(floor) *
                30d *
                Math.Pow(1.15d, Math.Max(0, floor - 1)));

        public static double EnemyAttack(int floor) =>
            Math.Ceiling(2d * Math.Pow(1.25d, Math.Max(0, floor - 1)));

        public static double BaseGoldReward(int floor) =>
            Math.Ceiling(5000d * Math.Pow(1.35d, Math.Max(0, floor - 1)));

        public static int BaseGemReward(int floor) =>
            (int)Math.Min(int.MaxValue, 500L + Math.Max(0, floor - 1) * 100L);

        public static double GoldReward(int floor, TowerGrade grade, bool grantFirstSBonus = false) =>
            grade == TowerGrade.F ? 0d : BaseGoldReward(floor) *
                (grantFirstSBonus && grade == TowerGrade.S ? 1.5d : 1d);

        public static int GemReward(int floor, TowerGrade grade, bool grantFirstSBonus = false) =>
            grade == TowerGrade.F ? 0 : Mathf.CeilToInt(BaseGemReward(floor) *
                (grantFirstSBonus && grade == TowerGrade.S ? 1.5f : 1f));
    }
}

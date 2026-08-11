using UnityEngine;

namespace LostFamiliar.Core
{
    public sealed class UpgradeSystem
    {
        private readonly GameSaveData _saveData;

        public UpgradeSystem(GameSaveData saveData)
        {
            _saveData = saveData;
        }

        public int GetStatLevel(StatType type)
        {
            return _saveData?.GetStatLevel(type) ?? 0;
        }

        public int TotalUpgradeLevel =>
            _saveData?.TotalUpgradeLevel ?? 1;

        public int TotalUpgradeProgress =>
            _saveData?.TotalUpgradeProgress ?? 0;

        public int TotalUpgradeProgressRequired =>
            _saveData?.TotalUpgradeProgressRequired ??
            GameBalance.StatLevelsPerTotalUpgradeLevel *
            GameBalance.UpgradeableStatCount;

        public bool CanIncreaseTotalUpgradeLevel =>
            _saveData?.CanIncreaseTotalUpgradeLevel ?? false;

        public int GetMaxStatLevel(StatType type)
        {
            return _saveData?.StatLevelCap ??
                   GameBalance.StatLevelsPerTotalUpgradeLevel;
        }

        public double GetStatValue(
            StatType type,
            int additionalLevels = 0)
        {
            int level = Mathf.Min(
                GetMaxStatLevel(type),
                GetStatLevel(type) +
                Mathf.Max(0, additionalLevels));

            return GameBalance.StatValue(type, level);
        }

        public double GetUpgradeCost(StatType type)
        {
            return GameBalance.UpgradeCost(
                type,
                GetStatLevel(type));
        }

        public double GetUpgradeCost(
            StatType type,
            int levelCount)
        {
            int currentLevel = GetStatLevel(type);
            int maxLevel = GetMaxStatLevel(type);

            int count = Mathf.Max(
                0,
                Mathf.Min(
                    levelCount,
                    maxLevel - currentLevel));

            double total = 0d;

            for (int i = 0; i < count; i++)
            {
                total += GameBalance.UpgradeCost(
                    type,
                    currentLevel + i);

                if (double.IsInfinity(total))
                    return double.MaxValue;
            }

            return total;
        }

        public int GetUpgradeLevelCount(
            StatType type,
            int requestedLevels)
        {
            int remaining = Mathf.Max(
                0,
                GetMaxStatLevel(type) -
                GetStatLevel(type));

            return Mathf.Min(
                Mathf.Max(0, requestedLevels),
                remaining);
        }

        public bool CanUpgrade(StatType type)
        {
            return CanUpgrade(type, 1);
        }

        public bool CanUpgrade(
            StatType type,
            int requestedLevels)
        {
            if (_saveData == null)
                return false;

            int count =
                GetUpgradeLevelCount(
                    type,
                    requestedLevels);

            return count > 0 &&
                   _saveData.gold >=
                   GetUpgradeCost(type, count);
        }

        public int TryUpgrade(
            StatType type,
            int requestedLevels)
        {
            if (_saveData == null ||
                requestedLevels <= 0)
                return 0;

            int upgradedLevels =
                GetUpgradeLevelCount(
                    type,
                    requestedLevels);

            if (upgradedLevels <= 0)
                return 0;

            double totalCost =
                GetUpgradeCost(
                    type,
                    upgradedLevels);

            if (_saveData.gold < totalCost)
                return 0;

            _saveData.gold -= totalCost;

            _saveData.IncreaseStatLevels(
                type,
                upgradedLevels);

            return upgradedLevels;
        }

        public bool TryIncreaseTotalUpgradeLevel()
        {
            return _saveData != null &&
                   _saveData.TryIncreaseTotalUpgradeLevel();
        }
    }
}

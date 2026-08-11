using System;
using UnityEngine;
using LostFamiliar.Battle;

namespace LostFamiliar.Core
{
    public sealed class OfflineRewardSystem
    {
        public const double MaximumOfflineSeconds = 12d * 60d * 60d;
        private const double RewardRate = .3d;

        private readonly GameSaveData _saveData;

        public OfflineRewardSystem(GameSaveData saveData)
        {
            _saveData = saveData;
        }

        public double PendingGold =>
            _saveData?.pendingOfflineGold ?? 0d;

        public double PendingSeconds =>
            _saveData?.pendingOfflineSeconds ?? 0d;

        public float Progress01 =>
            (float)Math.Min(
                1d,
                Math.Max(
                    0d,
                    PendingSeconds / MaximumOfflineSeconds));

        public double CaptureElapsedSeconds()
        {
            if (_saveData == null)
                return 0d;

            long nowTicks = DateTime.UtcNow.Ticks;
            long previousTicks = _saveData.lastSavedUtcTicks;

            _saveData.lastSavedUtcTicks = nowTicks;

            if (previousTicks <= 0L ||
                previousTicks > nowTicks)
                return 0d;

            double elapsed =
                TimeSpan.FromTicks(
                    nowTicks - previousTicks)
                .TotalSeconds;

            return Math.Min(
                MaximumOfflineSeconds,
                Math.Max(0d, elapsed));
        }

        private static double GetAverageEnemyGold(
            StageRuntimeData stage,
            int stageNumber)
        {
            if (stage?.region?.normalEnemies == null)
                return 0d;

            EnemySpawnEntry[] entries =
                stage.region.normalEnemies;

            if (entries.Length == 0)
                return 0d;

            int stageInRegion = Mathf.Max(
                1,
                stageNumber -
                stage.region.startStage + 1);

            long totalWeight = 0L;
            double weightedGold = 0d;

            foreach (EnemySpawnEntry entry in entries)
            {
                if (entry?.enemy == null ||
                    entry.unlockStageInRegion > stageInRegion)
                    continue;

                int weight = Mathf.Max(1, entry.weight);

                totalWeight += weight;
                weightedGold +=
                    entry.enemy.goldReward * weight;
            }

            return totalWeight <= 0L
                ? 0d
                : weightedGold /
                  totalWeight *
                  stage.rewardMultiplier;
        }

        private static double GetAverageEnemyHealth(
            StageRuntimeData stage,
            int stageNumber)
        {
            if (stage?.region?.normalEnemies == null)
                return 0d;

            EnemySpawnEntry[] entries =
                stage.region.normalEnemies;

            if (entries.Length == 0)
                return 0d;

            int stageInRegion = Mathf.Max(
                1,
                stageNumber -
                stage.region.startStage + 1);

            long totalWeight = 0L;
            double weightedHealth = 0d;

            foreach (EnemySpawnEntry entry in entries)
            {
                if (entry?.enemy == null ||
                    entry.unlockStageInRegion > stageInRegion)
                    continue;

                int weight = Mathf.Max(1, entry.weight);

                totalWeight += weight;
                weightedHealth +=
                    entry.enemy.baseHealth * weight;
            }

            return totalWeight <= 0L
                ? 0d
                : weightedHealth /
                  totalWeight *
                  stage.healthMultiplier;
        }

        public bool QueueReward(
            double elapsedSeconds,
            StageRuntimeData stage,
            int stageNumber,
            PlayerAutoCombat player,
            int spawnBatchSize,
            float spawnInterval)
        {
            if (_saveData == null ||
                stage?.region == null ||
                elapsedSeconds <= 0d)
                return false;

            double remainingSeconds =
                Math.Max(
                    0d,
                    MaximumOfflineSeconds -
                    _saveData.pendingOfflineSeconds);

            elapsedSeconds =
                Math.Min(
                    elapsedSeconds,
                    remainingSeconds);

            if (elapsedSeconds <= 0d)
                return false;

            double goldPerEnemy =
                GetAverageEnemyGold(
                    stage,
                    stageNumber);

            double averageEnemyHealth =
                GetAverageEnemyHealth(
                    stage,
                    stageNumber);

            if (goldPerEnemy > 0d &&
                averageEnemyHealth > 0d &&
                player != null)
            {
                double spawnLimitPerSecond =
                    spawnBatchSize /
                    (double)Math.Max(.01f, spawnInterval);

                double enemiesPerSecond =
                    player.EstimateOfflineKillsPerSecond(
                        averageEnemyHealth,
                        spawnLimitPerSecond);

                if (enemiesPerSecond > 0d)
                {
                    double reward = Math.Floor(
                        goldPerEnemy *
                        enemiesPerSecond *
                        elapsedSeconds *
                        RewardRate);

                    if (!double.IsNaN(reward) &&
                        reward > 0d)
                    {
                        reward = Math.Min(
                            reward,
                            double.MaxValue -
                            Math.Max(
                                0d,
                                _saveData.pendingOfflineGold));

                        _saveData.pendingOfflineGold +=
                            reward;
                    }
                }
            }

            _saveData.pendingOfflineSeconds +=
                elapsedSeconds;

            return true;
        }

        public bool TryReceive()
        {
            if (_saveData == null ||
                _saveData.pendingOfflineSeconds <= 0d)
                return false;

            double reward =
                Math.Max(
                    0d,
                    _saveData.pendingOfflineGold);

            _saveData.pendingOfflineGold = 0d;
            _saveData.pendingOfflineSeconds = 0d;

            reward = Math.Min(
                reward,
                double.MaxValue -
                Math.Max(0d, _saveData.gold));

            _saveData.gold += reward;

            return true;
        }
    }
}

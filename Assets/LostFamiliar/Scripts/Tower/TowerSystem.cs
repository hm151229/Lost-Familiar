using System;
using LostFamiliar.Core;
using UnityEngine;

namespace LostFamiliar.Battle
{
    public sealed class TowerSystem
    {
        private readonly GameSaveData _saveData;

        private bool _runActive;
        private TowerType _activeType;
        private int _activeFloor;

        public bool IsRunActive => _runActive;

        public TowerSystem(GameSaveData saveData)
        {
            _saveData = saveData;
        }

        public TowerProgressData GetProgress(
            TowerType type)
        {
            return _saveData?.GetTower(type);
        }

        public bool RefreshDailyTickets()
        {
            if (_saveData == null)
                return false;

            string today =
                DateTime.Now.ToString("yyyyMMdd");

            bool changed =
                _saveData.goldTower
                    .RefreshDailyTickets(today);

            changed |=
                _saveData.gemTower
                    .RefreshDailyTickets(today);

            return changed;
        }

        public bool TryBeginRun(
            TowerType type,
            int floor,
            out TowerRunSetup setup)
        {
            setup = default;

            if (_saveData == null ||
                _runActive)
                return false;

            RefreshDailyTickets();

            TowerProgressData progress =
                _saveData.GetTower(type);

            floor = Mathf.Max(1, floor);

            if (progress == null ||
                progress.tickets <= 0 ||
                floor >
                progress.highestUnlockedFloor)
                return false;

            progress.tickets--;

            _runActive = true;
            _activeType = type;
            _activeFloor = floor;

            setup =
                new TowerRunSetup(
                    type,
                    floor);

            return true;
        }

        public bool TryGetActiveRun(
            out TowerRunSetup setup)
        {
            setup = default;

            if (!_runActive)
                return false;

            setup =
                new TowerRunSetup(
                    _activeType,
                    _activeFloor);

            return true;
        }

        public bool TryCompleteRun(
            bool cleared,
            float remainingTime,
            out TowerRunResult result)
        {
            result = default;

            if (_saveData == null ||
                !_runActive)
                return false;

            TowerType type =
                _activeType;

            int floor =
                _activeFloor;

            _runActive = false;
            _activeFloor = 0;

            remainingTime =
                Mathf.Clamp(
                    remainingTime,
                    0f,
                    TowerBalance.TimeLimit);

            TowerGrade grade =
                TowerBalance.Grade(
                    remainingTime,
                    cleared);

            TowerProgressData progress =
                _saveData.GetTower(type);

            TowerGrade previousBestGrade =
                progress.GetBestGrade(floor);

            bool firstSGradeClear =
                grade == TowerGrade.S &&
                previousBestGrade <
                TowerGrade.S;

            int previousHighest =
                progress.highestUnlockedFloor;

            if (grade == TowerGrade.F)
            {
                progress.tickets++;
            }
            else
            {
                progress.RecordClear(
                    floor,
                    grade,
                    TowerBalance.TimeLimit -
                    remainingTime);
            }

            double goldReward =
                type == TowerType.Gold
                    ? TowerBalance.GoldReward(
                        floor,
                        grade,
                        firstSGradeClear)
                    : 0d;

            int gemReward =
                type == TowerType.Gem
                    ? TowerBalance.GemReward(
                        floor,
                        grade,
                        firstSGradeClear)
                    : 0;

            _saveData.gold += goldReward;
            _saveData.gems += gemReward;

            result =
                new TowerRunResult(
                    type,
                    floor,
                    grade,
                    remainingTime,
                    goldReward,
                    gemReward,
                    progress.highestUnlockedFloor >
                    previousHighest,
                    progress.GetBestGrade(floor) >=
                    TowerGrade.A);

            return true;
        }

        public bool TrySweep(
            TowerType type,
            int floor,
            out TowerRunResult result)
        {
            result = default;

            if (_saveData == null ||
                _runActive)
                return false;

            RefreshDailyTickets();

            TowerProgressData progress =
                _saveData.GetTower(type);

            floor = Mathf.Max(1, floor);

            TowerGrade bestGrade =
                progress?.GetBestGrade(floor)
                ?? TowerGrade.F;

            if (progress == null ||
                progress.tickets <= 0 ||
                floor >
                progress.highestUnlockedFloor ||
                bestGrade < TowerGrade.A)
                return false;

            progress.tickets--;

            double goldReward =
                type == TowerType.Gold
                    ? TowerBalance.GoldReward(
                        floor,
                        bestGrade,
                        false)
                    : 0d;

            int gemReward =
                type == TowerType.Gem
                    ? TowerBalance.GemReward(
                        floor,
                        bestGrade,
                        false)
                    : 0;

            _saveData.gold += goldReward;
            _saveData.gems += gemReward;

            result =
                new TowerRunResult(
                    type,
                    floor,
                    bestGrade,
                    TowerBalance.TimeLimit,
                    goldReward,
                    gemReward,
                    false,
                    true);

            return true;
        }

        public bool CancelRun()
        {
            if (_saveData == null ||
                !_runActive)
                return false;

            TowerProgressData progress =
                _saveData.GetTower(
                    _activeType);

            if (progress != null)
                progress.tickets++;

            _runActive = false;
            _activeFloor = 0;

            return true;
        }

        public bool GrantTickets(
            TowerType type,
            int amount)
        {
            if (_saveData == null ||
                amount <= 0)
                return false;

            TowerProgressData progress =
                _saveData.GetTower(type);

            if (progress == null)
                return false;

            progress.tickets =
                (int)Math.Min(
                    int.MaxValue,
                    (long)progress.tickets +
                    amount);

            return true;
        }
    }
}

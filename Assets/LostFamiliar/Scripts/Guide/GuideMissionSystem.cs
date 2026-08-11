using System;

namespace LostFamiliar.Core
{
    public sealed class GuideMissionSystem
    {
        private readonly GameSaveData _saveData;

        public GuideMissionSystem(GameSaveData saveData)
        {
            _saveData = saveData;
        }

        public GuideMissionDefinition CurrentMission =>
            GuideMissionCatalog.Get(
                _saveData?.guideMissionIndex ?? 0);

        public int GetProgress(
            GuideMissionDefinition mission,
            int clearedStage)
        {
            if (_saveData == null)
                return 0;

            int progress = mission.type switch
            {
                GuideMissionType.DefeatMonsters =>
                    _saveData.guideMissionProgress,

                GuideMissionType.Gacha =>
                    _saveData.guideMissionProgress,

                GuideMissionType.ClearStage =>
                    Math.Max(0, clearedStage),

                GuideMissionType.ReachStatLevel =>
                    _saveData.GetStatLevel(mission.statType),

                GuideMissionType.ReachTotalUpgradeLevel =>
                    _saveData.TotalUpgradeLevel,

                GuideMissionType.ClearGoldTower =>
                    _saveData.guideMissionProgress,

                GuideMissionType.ClearGemTower =>
                    _saveData.guideMissionProgress,

                _ => 0
            };

            return Math.Clamp(
                progress,
                0,
                mission.target);
        }

        public bool AddActionProgress(
            GuideMissionType type,
            int amount)
        {
            if (_saveData == null || amount <= 0)
                return false;

            GuideMissionDefinition mission =
                CurrentMission;

            if (mission.type != type)
                return false;

            long next =
                (long)_saveData.guideMissionProgress +
                amount;

            int newProgress =
                (int)Math.Min(
                    mission.target,
                    next);

            if (newProgress ==
                _saveData.guideMissionProgress)
                return false;

            _saveData.guideMissionProgress =
                newProgress;

            return true;
        }

        public bool TryClaim(
            int clearedStage,
            out GuideMissionDefinition claimedMission)
        {
            claimedMission = CurrentMission;

            if (_saveData == null)
                return false;

            int progress =
                GetProgress(
                    claimedMission,
                    clearedStage);

            if (progress <
                claimedMission.target)
                return false;

            if (claimedMission.gemReward > 0)
            {
                _saveData.gems =
                    SafeAdd(
                        _saveData.gems,
                        claimedMission.gemReward);
            }

            if (claimedMission.goldTowerTicketReward > 0)
            {
                TowerProgressData tower =
                    _saveData.GetTower(TowerType.Gold);

                tower.tickets =
                    SafeAdd(
                        tower.tickets,
                        claimedMission.goldTowerTicketReward);
            }

            if (claimedMission.gemTowerTicketReward > 0)
            {
                TowerProgressData tower =
                    _saveData.GetTower(TowerType.Gem);

                tower.tickets =
                    SafeAdd(
                        tower.tickets,
                        claimedMission.gemTowerTicketReward);
            }

            _saveData.guideMissionIndex =
                SafeAdd(
                    _saveData.guideMissionIndex,
                    1);

            _saveData.guideMissionProgress = 0;

            return true;
        }

        private static int SafeAdd(
            int value,
            int amount)
        {
            return (int)Math.Min(
                int.MaxValue,
                (long)Math.Max(0, value) +
                Math.Max(0, amount));
        }
    }
}

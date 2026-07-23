using System;
using System.Collections;
using System.Collections.Generic;
using LostFamiliar.Core;
using UnityEngine;

namespace LostFamiliar.Battle
{
    public enum BattlePhase { Normal, EnteringBoss, Boss, Returning, StageClear }

    public readonly struct GachaReward
    {
        public readonly EquipmentData equipment;
        public readonly SkillData skill;
        public readonly EquipmentRarity rarity;

        public GachaReward(EquipmentData equipment)
        {
            this.equipment = equipment;
            skill = null;
            rarity = equipment != null ? equipment.rarity : EquipmentRarity.Common;
        }

        public GachaReward(SkillData skill)
        {
            equipment = null;
            this.skill = skill;
            rarity = skill != null ? skill.rarity : EquipmentRarity.Common;
        }

        public string DisplayName => equipment != null ? equipment.displayName : skill?.displayName ?? string.Empty;
    }

    public sealed class MainBattleLoop : MonoBehaviour
    {
        private const int SpawnGrowthStageInterval = 10;
        private const int BatchGrowthStageInterval = 25;
        private const float SpawnIntervalReductionPerStep = .025f;
        private const float MinimumSpawnInterval = .45f;
        private const int MaxSpawnBatchSize = 5;
        private const int AliveEnemyIncreasePerStep = 2;
        private const int MaxAliveEnemyLimit = 25;

        [SerializeField] private StageDatabase stageDatabase;
        [SerializeField] private EquipmentDatabase equipmentDatabase;
        [SerializeField] private PlayerAutoCombat player;
        [SerializeField, Min(1f)] private float bossSpawnDistance = 2.8f;

        public StageDatabase Database => stageDatabase;
        public EquipmentDatabase EquipmentDatabase => equipmentDatabase;
        public EquipmentInventory EquipmentInventory { get; private set; }
        public PlayerAutoCombat Player => player;
        public BattlePhase Phase { get; private set; }
        public int StageNumber { get; private set; } = 1;
        public int StageExperience { get; private set; }
        public StageRuntimeData CurrentStage { get; private set; }
        public EnemyActor CurrentBoss { get; private set; }
        public float BossTimeRemaining { get; private set; }
        public float BossTimeLimit => CurrentStage?.bossTimeLimit ?? 0f;
        public double Gold => _saveData?.gold ?? 0d;
        public int Gems => _saveData?.gems ?? 0;
        public int PlayerLevel => _saveData?.playerLevel ?? 1;
        public double PlayerExperience => _saveData?.playerExperience ?? 0d;
        public double PlayerExperienceToLevel => GameBalance.ExperienceToLevel(PlayerLevel);
        public float PlayerExperience01 => PlayerExperienceToLevel <= 0d
            ? 0f
            : Mathf.Clamp01((float)(PlayerExperience / PlayerExperienceToLevel));
        public float StageExperience01 => CurrentStage == null || CurrentStage.experienceToBoss <= 0
            ? 0f
            : Mathf.Clamp01((float)StageExperience / CurrentStage.experienceToBoss);
        public bool CanChallengeBoss =>
            _initialized &&
            !_transitioning &&
            Phase == BattlePhase.Normal &&
            CurrentStage != null &&
            _saveData != null &&
            _saveData.bossRetryRequired &&
            StageExperience >= CurrentStage.experienceToBoss;
        public GuideMissionDefinition CurrentGuideMission =>
            GuideMissionCatalog.Get(_saveData?.guideMissionIndex ?? 0);
        public int GuideMissionProgress => GetGuideMissionProgress(CurrentGuideMission);
        public bool CanClaimGuideMission => GuideMissionProgress >= CurrentGuideMission.target;

        public event Action StateChanged;
        public event Action<RewardNotification> RewardGained;

        private GameSaveData _saveData;
        private float _spawnTimer;
        private float _saveTimer;
        private bool _transitioning;
        private bool _initialized;

        public void Initialize(StageDatabase database, PlayerAutoCombat playerActor)
        {
            if (database == null || playerActor == null)
            {
                Debug.LogError("전투 초기화에 StageDatabase와 PlayerAutoCombat이 필요합니다.", this);
                return;
            }

            if (player != null)
                player.Defeated -= OnPlayerDefeated;

            stageDatabase = database;
            player = playerActor;
            player.Defeated += OnPlayerDefeated;
            _saveData ??= SaveService.Load();
            _saveData.Normalize();
            equipmentDatabase ??= Resources.Load<EquipmentDatabase>("Equipment/DefaultEquipmentDatabase");
            InitializeEquipmentInventory();
            StageNumber = Mathf.Max(1, _saveData.stage);
            RebuildCurrentStage();

            if (CurrentStage == null)
            {
                Debug.LogError($"스테이지 {StageNumber}에 사용할 지역 데이터가 없습니다.", this);
                return;
            }

            StageExperience = Mathf.RoundToInt(CurrentStage.experienceToBoss * Mathf.Clamp01(_saveData.stageProgress / 100f));
            ApplyPlayerProgression();
            SyncEquippedSkills();
            player.Revive();
            Phase = BattlePhase.Normal;
            BossTimeRemaining = 0f;
            _transitioning = false;
            _initialized = true;
            BossChallengeButtonPresenter presenter = GetComponent<BossChallengeButtonPresenter>();
            if (presenter == null)
                presenter = gameObject.AddComponent<BossChallengeButtonPresenter>();
            presenter.Bind(this);

            MainHUDController hud = UnityEngine.Object.FindFirstObjectByType<MainHUDController>();
            if (hud == null)
                hud = gameObject.AddComponent<MainHUDController>();
            hud.Bind(this);

            RewardFeedController rewardFeed = UnityEngine.Object.FindFirstObjectByType<RewardFeedController>();
            if (rewardFeed == null)
            {
                GameObject rewardFeedObject = GameObject.Find("Canvas/SafeArea/RewardFeed");
                if (rewardFeedObject != null)
                    rewardFeed = rewardFeedObject.AddComponent<RewardFeedController>();
            }
            if (rewardFeed != null)
                rewardFeed.Bind(this);

            GameObject guideMissionPanel = GameObject.Find("Canvas/SafeArea/GuideMissionPanel");
            if (guideMissionPanel != null)
            {
                GuideMissionPanelController guideMissionController =
                    guideMissionPanel.GetComponent<GuideMissionPanelController>();
                if (guideMissionController == null)
                    guideMissionController = guideMissionPanel.AddComponent<GuideMissionPanelController>();
                guideMissionController.Bind(this);
            }
            ApplyBackground();
            NotifyStateChanged();

            if (StageExperience >= CurrentStage.experienceToBoss && !_saveData.bossRetryRequired)
                StartCoroutine(EnterBoss());
        }

        private void Update()
        {
            if (!_initialized || CurrentStage == null || player == null)
                return;

            _saveTimer += Time.unscaledDeltaTime;
            if (_saveTimer >= 10f)
            {
                _saveTimer = 0f;
                Save();
            }

            if (_transitioning)
                return;

            if (Phase == BattlePhase.Boss)
            {
                BossTimeRemaining = Mathf.Max(0f, BossTimeRemaining - Time.deltaTime);
                if (BossTimeRemaining <= 0f)
                {
                    Debug.Log("보스전 제한 시간이 종료되어 일반 전투로 돌아갑니다.", this);
                    StartCoroutine(ReturnToNormal());
                }
                return;
            }

            if (Phase != BattlePhase.Normal)
                return;

            _spawnTimer += Time.deltaTime;
            float spawnInterval = GetCurrentSpawnInterval();
            int maxAliveEnemies = GetCurrentMaxAliveEnemies();
            if (_spawnTimer >= spawnInterval && EnemyActor.Active.Count < maxAliveEnemies)
            {
                _spawnTimer = 0f;
                int availableSlots = maxAliveEnemies - EnemyActor.Active.Count;
                int spawnCount = Mathf.Min(
                    GetCurrentSpawnBatchSize(),
                    availableSlots);
                for (int i = 0; i < spawnCount; i++)
                    Spawn(CurrentStage.region.PickEnemy(StageNumber), false);
            }
        }

        private float GetCurrentSpawnInterval()
        {
            int growthStep = Mathf.Max(1, StageNumber) / SpawnGrowthStageInterval;
            return Mathf.Max(
                MinimumSpawnInterval,
                CurrentStage.region.spawnInterval - growthStep * SpawnIntervalReductionPerStep);
        }

        private int GetCurrentSpawnBatchSize()
        {
            int batchBonus = Mathf.Max(1, StageNumber) / BatchGrowthStageInterval;
            return Mathf.Clamp(
                Mathf.Max(1, CurrentStage.region.spawnBatchSize) + batchBonus,
                1,
                MaxSpawnBatchSize);
        }

        private int GetCurrentMaxAliveEnemies()
        {
            int growthStep = Mathf.Max(1, StageNumber) / SpawnGrowthStageInterval;
            return Mathf.Clamp(
                CurrentStage.region.maxAliveEnemies + growthStep * AliveEnemyIncreasePerStep,
                1,
                MaxAliveEnemyLimit);
        }

        private void Spawn(EnemyData data, bool boss, Vector3? fixedPosition = null)
        {
            if (data == null)
            {
                Debug.LogWarning(boss ? "보스 데이터가 없어 일반 전투로 돌아갑니다." : "지역에 생성 가능한 일반 몬스터가 없습니다.", this);
                if (boss)
                    StartCoroutine(ReturnToNormal());
                return;
            }

            GameObject enemyObject = data.prefab != null
                ? Instantiate(data.prefab)
                : GameObject.CreatePrimitive(boss ? PrimitiveType.Capsule : PrimitiveType.Sphere);

            if (fixedPosition.HasValue)
            {
                enemyObject.transform.position = fixedPosition.Value;
            }
            else
            {
                float side = UnityEngine.Random.value < .5f ? -1f : 1f;
                enemyObject.transform.position = player.transform.position +
                                                 new Vector3(side * UnityEngine.Random.Range(4.5f, 6f), UnityEngine.Random.Range(-2.5f, 2.5f), 0f);
            }

            EnemyActor enemy = enemyObject.GetComponent<EnemyActor>() ?? enemyObject.AddComponent<EnemyActor>();
            enemy.Initialize(
                data,
                player,
                CurrentStage.healthMultiplier,
                CurrentStage.attackMultiplier,
                boss,
                CurrentStage.bossHealthMultiplier,
                CurrentStage.bossAttackMultiplier);
            enemy.Died += OnEnemyDied;
            if (boss)
                CurrentBoss = enemy;
        }

        private void OnEnemyDied(EnemyActor enemy)
        {
            enemy.Died -= OnEnemyDied;
            AddGuideMissionActionProgress(GuideMissionType.DefeatMonsters, 1);
            double bossRewardMultiplier = enemy.IsBoss ? 10d : 1d;
            double goldReward = enemy.Data.goldReward * CurrentStage.rewardMultiplier * bossRewardMultiplier;
            double experienceReward = enemy.Data.playerExperience * bossRewardMultiplier;
            _saveData.gold += goldReward;
            AddPlayerExperience(experienceReward);
            PublishReward(RewardType.Gold, goldReward);
            PublishReward(RewardType.PlayerExperience, experienceReward);

            if (enemy.IsBoss)
            {
                CurrentBoss = null;
                StartCoroutine(CompleteStage());
                return;
            }

            if (Phase != BattlePhase.Normal)
                return;

            StageExperience = Mathf.Min(CurrentStage.experienceToBoss, StageExperience + enemy.Data.stageExperience);
            UpdateSavedStageProgress();
            NotifyStateChanged();

            if (StageExperience >= CurrentStage.experienceToBoss && !_saveData.bossRetryRequired)
                StartCoroutine(EnterBoss());
        }

        private void AddPlayerExperience(double amount)
        {
            _saveData.playerExperience += Math.Max(0d, amount);
            bool leveledUp = false;
            while (_saveData.playerExperience >= GameBalance.ExperienceToLevel(_saveData.playerLevel))
            {
                _saveData.playerExperience -= GameBalance.ExperienceToLevel(_saveData.playerLevel);
                _saveData.playerLevel++;
                leveledUp = true;
            }

            if (leveledUp)
            {
                ApplyPlayerProgression();
                player.Revive();
            }
        }

        public bool TryEnterBossBattle()
        {
            if (!CanChallengeBoss)
                return false;

            StartCoroutine(EnterBoss());
            return true;
        }

        private IEnumerator EnterBoss()
        {
            _transitioning = true;
            Phase = BattlePhase.EnteringBoss;
            CurrentBoss = null;
            ClearEnemies();
            player.ResetPosition();
            CameraFollow2D cameraFollow = Camera.main != null ? Camera.main.GetComponent<CameraFollow2D>() : null;
            if (cameraFollow != null)
                cameraFollow.SnapToTarget();
            NotifyStateChanged();
            yield return new WaitForSeconds(1f);
            player.Revive();
            Phase = BattlePhase.Boss;
            BossTimeRemaining = Mathf.Max(1f, CurrentStage.bossTimeLimit);
            Vector3 bossPosition = player.transform.position + Vector3.right * bossSpawnDistance;
            Spawn(CurrentStage.Boss, true, bossPosition);
            _transitioning = false;
            NotifyStateChanged();
        }

        private IEnumerator CompleteStage()
        {
            _transitioning = true;
            Phase = BattlePhase.StageClear;
            CurrentBoss = null;
            BossTimeRemaining = 0f;
            int gemReward = CurrentStage.gemReward;
            _saveData.gems += gemReward;
            PublishReward(RewardType.Gem, gemReward);
            NotifyStateChanged();
            yield return new WaitForSeconds(1.5f);

            StageNumber++;
            _saveData.stage = StageNumber;
            _saveData.stageProgress = 0f;
            _saveData.bossRetryRequired = false;
            StageExperience = 0;
            RebuildCurrentStage();
            player.Revive();
            Phase = BattlePhase.Normal;
            ApplyBackground();
            _transitioning = false;
            Save();
            NotifyStateChanged();
        }

        private void OnPlayerDefeated()
        {
            if (!_initialized || _transitioning)
                return;

            StartCoroutine(Phase == BattlePhase.Boss ? ReturnToNormal() : RespawnInNormal());
        }

        private IEnumerator ReturnToNormal()
        {
            _transitioning = true;
            Phase = BattlePhase.Returning;
            CurrentBoss = null;
            BossTimeRemaining = 0f;
            ClearEnemies();
            yield return new WaitForSeconds(1.5f);

            StageExperience = CurrentStage.experienceToBoss;
            _saveData.bossRetryRequired = true;
            UpdateSavedStageProgress();
            player.Revive();
            Phase = BattlePhase.Normal;
            _transitioning = false;
            Save();
            NotifyStateChanged();
        }

        private IEnumerator RespawnInNormal()
        {
            _transitioning = true;
            ClearEnemies();
            yield return new WaitForSeconds(1f);
            player.Revive();
            _transitioning = false;
            NotifyStateChanged();
        }

        public bool TryUpgrade(StatType type)
        {
            return TryUpgradeMany(type, 1) > 0;
        }

        public int TryUpgradeMany(StatType type, int requestedLevels)
        {
            if (_saveData == null || requestedLevels <= 0)
                return 0;

            int upgradedLevels = GetUpgradeLevelCount(type, requestedLevels);
            if (upgradedLevels <= 0)
                return 0;

            double totalCost = GetUpgradeCost(type, upgradedLevels);
            if (_saveData.gold < totalCost)
                return 0;

            _saveData.gold -= totalCost;
            _saveData.IncreaseStatLevels(type, upgradedLevels);

            ApplyPlayerProgression();
            Save();
            NotifyStateChanged();
            return upgradedLevels;
        }

        public int GetStatLevel(StatType type) => _saveData?.GetStatLevel(type) ?? 0;
        public int TotalUpgradeLevel => _saveData?.TotalUpgradeLevel ?? 1;
        public int TotalUpgradeProgress => _saveData?.TotalUpgradeProgress ?? 0;
        public int TotalUpgradeProgressRequired =>
            _saveData?.TotalUpgradeProgressRequired ??
            GameBalance.StatLevelsPerTotalUpgradeLevel * GameBalance.UpgradeableStatCount;
        public bool CanIncreaseTotalUpgradeLevel => _saveData?.CanIncreaseTotalUpgradeLevel ?? false;

        public int GetMaxStatLevel(StatType type) => _saveData?.StatLevelCap ?? GameBalance.StatLevelsPerTotalUpgradeLevel;

        public bool TryIncreaseTotalUpgradeLevel()
        {
            if (_saveData == null || !_saveData.TryIncreaseTotalUpgradeLevel())
                return false;

            Save();
            NotifyStateChanged();
            return true;
        }

        public bool CanUpgrade(StatType type)
        {
            return CanUpgrade(type, 1);
        }

        public bool CanUpgrade(StatType type, int requestedLevels)
        {
            int count = GetUpgradeLevelCount(type, requestedLevels);
            return count > 0 && Gold >= GetUpgradeCost(type, count);
        }

        public double GetStatValue(StatType type, int additionalLevels = 0)
        {
            int level = Mathf.Min(
                GetMaxStatLevel(type),
                GetStatLevel(type) + Mathf.Max(0, additionalLevels));
            return GameBalance.StatValue(type, level);
        }

        public void ResetProgress()
        {
            if (!_initialized || stageDatabase == null || player == null)
                return;

            StopAllCoroutines();
            foreach (EnemyActor enemy in UnityEngine.Object.FindObjectsByType<EnemyActor>(FindObjectsSortMode.None))
            {
                if (enemy != null)
                    Destroy(enemy.gameObject);
            }

            SaveService.Delete();
            _saveData = new GameSaveData();
            _saveData.Normalize();
            InitializeEquipmentInventory();
            StageNumber = 1;
            StageExperience = 0;
            CurrentBoss = null;
            BossTimeRemaining = 0f;
            _spawnTimer = 0f;
            _saveTimer = 0f;
            _transitioning = false;
            Phase = BattlePhase.Normal;

            RebuildCurrentStage();
            ApplyPlayerProgression();
            SyncEquippedSkills();
            player.ResetPosition();
            player.Revive();
            ApplyBackground();
            Save();
            NotifyStateChanged();
        }

        public double GetUpgradeCost(StatType type)
        {
            return GameBalance.UpgradeCost(type, GetStatLevel(type));
        }

        public double GetUpgradeCost(StatType type, int levelCount)
        {
            int currentLevel = GetStatLevel(type);
            int maxLevel = GetMaxStatLevel(type);
            int count = Mathf.Max(0, Mathf.Min(levelCount, maxLevel - currentLevel));
            double total = 0d;

            for (int i = 0; i < count; i++)
            {
                total += GameBalance.UpgradeCost(type, currentLevel + i);
                if (double.IsInfinity(total))
                    return double.MaxValue;
            }

            return total;
        }

        public int GetUpgradeLevelCount(StatType type, int requestedLevels)
        {
            int remaining = Mathf.Max(0, GetMaxStatLevel(type) - GetStatLevel(type));
            return Mathf.Min(Mathf.Max(0, requestedLevels), remaining);
        }

        public void PublishReward(
            RewardType type,
            double amount,
            string labelOverride = null,
            Sprite iconOverride = null)
        {
            if (amount <= 0d)
                return;

            RewardGained?.Invoke(new RewardNotification(type, amount, labelOverride, iconOverride));
        }

        public EquipmentSaveEntry GrantEquipment(string equipmentId, int amount = 1)
        {
            return EquipmentInventory?.Grant(equipmentId, amount);
        }

        public void AddCurrencies(double gold, int gems)
        {
            if (_saveData == null)
                return;
            _saveData.gold += Math.Max(0d, gold);
            _saveData.gems += Math.Max(0, gems);
            Save();
            NotifyStateChanged();
        }

        public int GetGachaLevel(GachaCategory category) => _saveData?.GetGachaLevel(category) ?? 1;
        public int GetGachaProgress(GachaCategory category) => _saveData?.GetGachaProgress(category) ?? 0;

        public bool TryGacha(GachaCategory category, int drawCount, out List<GachaReward> rewards)
        {
            rewards = new List<GachaReward>();
            if (_saveData == null || (drawCount != 10 && drawCount != 30))
                return false;

            int cost = GachaBalance.Cost(drawCount);
            if (_saveData.gems < cost)
                return false;

            int level = _saveData.GetGachaLevel(category);
            if (category == GachaCategory.Skill)
            {
                SkillData[] skills = Resources.LoadAll<SkillData>("StageData/Skills");
                if (skills == null || skills.Length == 0)
                    return false;
                RollSkills(skills, level, drawCount, rewards);
            }
            else
            {
                List<EquipmentData> pool = GetEquipmentGachaPool(category);
                if (pool.Count == 0)
                    return false;
                RollEquipment(pool, level, drawCount, rewards);
            }

            if (rewards.Count != drawCount)
            {
                rewards.Clear();
                return false;
            }

            _saveData.gems -= cost;
            _saveData.AddGachaProgress(category, drawCount);
            AddGuideMissionActionProgress(GuideMissionType.Gacha, drawCount);
            List<string> equipmentIds = new List<string>();
            foreach (GachaReward reward in rewards)
            {
                if (reward.equipment != null)
                    equipmentIds.Add(reward.equipment.Id);
                else if (reward.skill != null)
                    GrantSkill(reward.skill);
            }
            if (equipmentIds.Count > 0)
                EquipmentInventory?.GrantBatch(equipmentIds);
            if (category == GachaCategory.Skill)
                ApplyPlayerProgression();
            Save();
            NotifyStateChanged();
            return true;
        }

        public bool TryClaimGuideMission()
        {
            if (_saveData == null)
                return false;

            GuideMissionDefinition mission = CurrentGuideMission;
            if (GetGuideMissionProgress(mission) < mission.target)
                return false;

            int reward = mission.gemReward;
            _saveData.gems += reward;
            _saveData.guideMissionIndex = (int)Math.Min(
                int.MaxValue,
                (long)Math.Max(0, _saveData.guideMissionIndex) + 1L);
            _saveData.guideMissionProgress = 0;
            PublishReward(RewardType.Gem, reward);
            Save();
            NotifyStateChanged();
            return true;
        }

        private int GetGuideMissionProgress(GuideMissionDefinition mission)
        {
            if (_saveData == null)
                return 0;

            int progress = mission.type switch
            {
                GuideMissionType.DefeatMonsters => _saveData.guideMissionProgress,
                GuideMissionType.Gacha => _saveData.guideMissionProgress,
                GuideMissionType.ClearStage => Mathf.Max(0, StageNumber - 1),
                GuideMissionType.ReachStatLevel => _saveData.GetStatLevel(mission.statType),
                GuideMissionType.ReachTotalUpgradeLevel => _saveData.TotalUpgradeLevel,
                _ => 0
            };
            return Mathf.Clamp(progress, 0, mission.target);
        }

        private void AddGuideMissionActionProgress(GuideMissionType type, int amount)
        {
            if (_saveData == null || amount <= 0)
                return;

            GuideMissionDefinition mission = CurrentGuideMission;
            if (mission.type != type)
                return;

            long next = (long)_saveData.guideMissionProgress + amount;
            _saveData.guideMissionProgress = (int)Math.Min(mission.target, next);
        }

        private List<EquipmentData> GetEquipmentGachaPool(GachaCategory category)
        {
            List<EquipmentData> pool = new List<EquipmentData>();
            if (equipmentDatabase?.items == null)
                return pool;
            foreach (EquipmentData item in equipmentDatabase.items)
            {
                if (item == null)
                    continue;
                bool matches = category switch
                {
                    GachaCategory.Armor => item.type == EquipmentType.Head || item.type == EquipmentType.Body || item.type == EquipmentType.Shoes,
                    GachaCategory.Accessory => item.type == EquipmentType.Accessory,
                    GachaCategory.Weapon => item.type == EquipmentType.Weapon,
                    _ => false
                };
                if (matches)
                    pool.Add(item);
            }
            return pool;
        }

        private static void RollEquipment(List<EquipmentData> pool, int level, int count, List<GachaReward> output)
        {
            HashSet<EquipmentRarity> available = new HashSet<EquipmentRarity>();
            foreach (EquipmentData item in pool)
                available.Add(item.rarity);
            for (int i = 0; i < count; i++)
            {
                EquipmentRarity rarity = GachaBalance.RollRarity(level, available);
                List<EquipmentData> candidates = pool.FindAll(item => item.rarity == rarity);
                output.Add(new GachaReward(candidates[UnityEngine.Random.Range(0, candidates.Count)]));
            }
        }

        private static void RollSkills(SkillData[] pool, int level, int count, List<GachaReward> output)
        {
            HashSet<EquipmentRarity> available = new HashSet<EquipmentRarity>();
            foreach (SkillData item in pool)
                if (item != null) available.Add(item.rarity);
            for (int i = 0; i < count; i++)
            {
                EquipmentRarity rarity = GachaBalance.RollRarity(level, available);
                List<SkillData> candidates = new List<SkillData>();
                foreach (SkillData item in pool)
                    if (item != null && item.rarity == rarity) candidates.Add(item);
                output.Add(new GachaReward(candidates[UnityEngine.Random.Range(0, candidates.Count)]));
            }
        }

        private void GrantSkill(SkillData skill)
        {
            SkillSaveEntry entry = _saveData.GetOrCreateSkill(skill.id);
            if (entry.level <= 0)
                entry.level = 1;
            else
                entry.duplicates++;
        }

        public IReadOnlyList<SkillData> GetOwnedSkills()
        {
            List<SkillData> owned = new List<SkillData>();
            foreach (SkillData skill in Resources.LoadAll<SkillData>("StageData/Skills"))
            {
                SkillSaveEntry state = GetSkillState(skill != null ? skill.id : null);
                if (skill != null && state != null && state.level > 0)
                    owned.Add(skill);
            }
            owned.Sort((left, right) =>
            {
                int rarity = right.rarity.CompareTo(left.rarity);
                return rarity != 0 ? rarity : string.Compare(left.displayName, right.displayName, StringComparison.Ordinal);
            });
            return owned;
        }

        public SkillSaveEntry GetSkillState(string skillId)
        {
            if (_saveData?.skillInventory == null || string.IsNullOrWhiteSpace(skillId))
                return null;
            return _saveData.skillInventory.Find(entry => entry != null && entry.skillId == skillId);
        }

        public int UnlockedSkillSlotCount => SkillBalance.UnlockedSlotCount(PlayerLevel);

        public bool IsSkillSlotUnlocked(int slotIndex) =>
            slotIndex >= 0 && slotIndex < UnlockedSkillSlotCount;

        public string GetEquippedSkillId(int slotIndex)
        {
            EnsureEquippedSkillSlots();
            return slotIndex >= 0 && slotIndex < _saveData.equippedSkillIds.Count
                ? _saveData.equippedSkillIds[slotIndex]
                : string.Empty;
        }

        public SkillData GetEquippedSkill(int slotIndex) => FindSkill(GetEquippedSkillId(slotIndex));

        public bool TryEquipSkill(string skillId, int slotIndex)
        {
            SkillSaveEntry state = GetSkillState(skillId);
            if (!IsSkillSlotUnlocked(slotIndex) || state == null || state.level <= 0)
                return false;

            EnsureEquippedSkillSlots();
            for (int i = 0; i < _saveData.equippedSkillIds.Count; i++)
                if (_saveData.equippedSkillIds[i] == skillId) _saveData.equippedSkillIds[i] = string.Empty;
            _saveData.equippedSkillIds[slotIndex] = skillId;
            SyncEquippedSkills();
            Save();
            NotifyStateChanged();
            return true;
        }

        public void UnequipSkill(int slotIndex)
        {
            EnsureEquippedSkillSlots();
            if (slotIndex < 0 || slotIndex >= _saveData.equippedSkillIds.Count ||
                string.IsNullOrEmpty(_saveData.equippedSkillIds[slotIndex]))
                return;
            _saveData.equippedSkillIds[slotIndex] = string.Empty;
            SyncEquippedSkills();
            Save();
            NotifyStateChanged();
        }

        public bool CanUpgradeSkill(string skillId)
        {
            SkillData skill = FindSkill(skillId);
            SkillSaveEntry state = GetSkillState(skillId);
            return skill != null && state != null && state.level > 0 && state.level < skill.maxLevel &&
                   state.duplicates >= SkillBalance.DuplicateRequirement(state.level);
        }

        public bool TryUpgradeSkill(string skillId)
        {
            if (!CanUpgradeSkill(skillId))
                return false;
            SkillSaveEntry state = GetSkillState(skillId);
            state.duplicates -= SkillBalance.DuplicateRequirement(state.level);
            state.level++;
            ApplyPlayerProgression();
            Save();
            NotifyStateChanged();
            return true;
        }

        public bool TryUpgradeEquipment(string equipmentId)
        {
            return EquipmentInventory?.TryUpgrade(equipmentId) ?? false;
        }

        public bool TryEquip(string equipmentId, EquipmentSlot slot)
        {
            return EquipmentInventory?.TryEquip(equipmentId, slot) ?? false;
        }

        public void Unequip(EquipmentSlot slot)
        {
            EquipmentInventory?.Unequip(slot);
        }

        private void InitializeEquipmentInventory()
        {
            if (EquipmentInventory != null)
                EquipmentInventory.Changed -= OnEquipmentChanged;

            EquipmentInventory = new EquipmentInventory(_saveData, equipmentDatabase);
            EquipmentInventory.Changed += OnEquipmentChanged;
        }

        private void OnEquipmentChanged()
        {
            ApplyPlayerProgression();
            Save();
            NotifyStateChanged();
        }

        private void ApplyPlayerProgression()
        {
            if (player == null || _saveData == null)
                return;

            EquipmentBonuses bonuses = EquipmentInventory?.CalculateBonuses() ?? default;
            if (_saveData.skillInventory != null)
            {
                foreach (SkillSaveEntry entry in _saveData.skillInventory)
                {
                    SkillData skill = entry != null ? FindSkill(entry.skillId) : null;
                    if (skill != null && entry.level > 0)
                        bonuses.Add(skill.ownedEffectType, SkillBalance.OwnedEffectValue(skill, entry.level));
                }
            }
            player.ApplyProgression(_saveData, bonuses);
        }

        private void SyncEquippedSkills()
        {
            if (player == null || _saveData == null)
                return;
            EnsureEquippedSkillSlots();
            SkillData[] equipped = new SkillData[SkillBalance.MaxEquippedSkillCount];
            int unlocked = UnlockedSkillSlotCount;
            for (int i = 0; i < equipped.Length; i++)
            {
                if (i >= unlocked)
                {
                    _saveData.equippedSkillIds[i] = string.Empty;
                    continue;
                }
                SkillData skill = FindSkill(_saveData.equippedSkillIds[i]);
                SkillSaveEntry state = GetSkillState(skill != null ? skill.id : null);
                equipped[i] = state != null && state.level > 0 ? skill : null;
                if (equipped[i] == null) _saveData.equippedSkillIds[i] = string.Empty;
            }
            player.SetEquippedSkills(equipped);
        }

        private void EnsureEquippedSkillSlots()
        {
            _saveData.equippedSkillIds ??= new List<string>();
            while (_saveData.equippedSkillIds.Count < SkillBalance.MaxEquippedSkillCount)
                _saveData.equippedSkillIds.Add(string.Empty);
            if (_saveData.equippedSkillIds.Count > SkillBalance.MaxEquippedSkillCount)
                _saveData.equippedSkillIds.RemoveRange(
                    SkillBalance.MaxEquippedSkillCount,
                    _saveData.equippedSkillIds.Count - SkillBalance.MaxEquippedSkillCount);
        }

        private static SkillData FindSkill(string skillId)
        {
            if (string.IsNullOrWhiteSpace(skillId))
                return null;
            foreach (SkillData skill in Resources.LoadAll<SkillData>("StageData/Skills"))
                if (skill != null && skill.id == skillId) return skill;
            return null;
        }

        private void RebuildCurrentStage()
        {
            CurrentStage = stageDatabase.BuildStage(StageNumber);
        }

        private void UpdateSavedStageProgress()
        {
            _saveData.stageProgress = CurrentStage == null
                ? 0f
                : Mathf.Clamp01((float)StageExperience / CurrentStage.experienceToBoss) * 100f;
        }

        private void ClearEnemies()
        {
            foreach (EnemyActor enemy in EnemyActor.Active.ToArray())
            {
                if (enemy != null)
                    Destroy(enemy.gameObject);
            }
        }

        private void ApplyBackground()
        {
            if (Camera.main != null && CurrentStage != null)
                Camera.main.backgroundColor = CurrentStage.region.backgroundColor;
        }

        private void NotifyStateChanged() => StateChanged?.Invoke();

        private void Save()
        {
            if (_saveData != null)
                SaveService.Save(_saveData);
        }

        private void OnApplicationPause(bool paused)
        {
            if (paused)
                Save();
        }

        private void OnApplicationQuit() => Save();

        private void OnDestroy()
        {
            if (player != null)
                player.Defeated -= OnPlayerDefeated;
            if (EquipmentInventory != null)
                EquipmentInventory.Changed -= OnEquipmentChanged;
        }
    }
}

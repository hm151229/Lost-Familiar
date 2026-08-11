using System;
using System.Collections;
using System.Collections.Generic;
using LostFamiliar.Core;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

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
        [SerializeField] private Transform bossSpawnPoint;
        [SerializeField] private Vector3 bossPlayerPosition = new Vector3(-1.35f, -.8f, 0f);
        [SerializeField, Min(1f)] private float bossSpawnDistance = 2.8f;

        public StageDatabase Database => stageDatabase;
        public EquipmentDatabase EquipmentDatabase => equipmentDatabase;
        public EquipmentInventory EquipmentInventory { get; private set; }
        public UpgradeSystem UpgradeSystem { get; private set; }
        public OfflineRewardSystem OfflineRewardSystem { get; private set; }
        public PlayerAutoCombat Player => player;
        public BattlePhase Phase { get; private set; }
        public int StageNumber { get; private set; } = 1;
        public int StageExperience { get; private set; }
        public StageRuntimeData CurrentStage { get; private set; }
        public EnemyActor CurrentBoss { get; private set; }
        public float BossTimeRemaining { get; private set; }
        public float BossTimeLimit => CurrentStage?.bossTimeLimit ?? 0f;
        public double Gold => _saveData?.gold ?? 0d;
        public double PendingOfflineGold => OfflineRewardSystem?.PendingGold ?? 0d;
        public double PendingOfflineSeconds => OfflineRewardSystem?.PendingSeconds ?? 0d;
        public float OfflineRewardProgress01 => OfflineRewardSystem?.Progress01 ?? 0f;
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
        private bool _towerRunActive;
        private TowerType _activeTowerType;
        private int _activeTowerFloor;

        public void Initialize(StageDatabase database, PlayerAutoCombat playerActor)
        {
            if (database == null || playerActor == null)
            {
                Debug.LogError("전투 초기화에 StageDatabase와 PlayerAutoCombat이 필요합니다.", this);
                return;
            }

            stageDatabase = database;
            player = playerActor;
            _saveData ??= SaveService.Load();
            _saveData.Normalize();
            UpgradeSystem = new UpgradeSystem(_saveData);
            OfflineRewardSystem = new OfflineRewardSystem(_saveData);
            double offlineSeconds = OfflineRewardSystem?.CaptureElapsedSeconds() ?? 0d;
            RefreshDailyTowerTickets();
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
            QueueOfflineReward(offlineSeconds);
            BindOfflineRewardPopup();

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

            if (player != null && !player.IsAlive)
            {
                if (Phase == BattlePhase.Boss || Phase == BattlePhase.EnteringBoss)
                    StartCoroutine(ReturnToNormal());
                else
                    StartCoroutine(RespawnInNormal());
                return;
            }

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
            int aliveMainEnemies = CountEnemiesInGroup(player.CombatGroup);
            if (_spawnTimer >= spawnInterval && aliveMainEnemies < maxAliveEnemies)
            {
                _spawnTimer = 0f;
                int availableSlots = maxAliveEnemies - aliveMainEnemies;
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
            player.ClearActiveSkills();
            ClearEnemies();
            NotifyStateChanged();
            yield return PlayBossCutTransition(() =>
            {
                player.ResetPosition(bossPlayerPosition);
                CameraFollow2D cameraFollow = Camera.main != null
                    ? Camera.main.GetComponent<CameraFollow2D>()
                    : null;
                if (cameraFollow != null)
                    cameraFollow.SnapToTarget();

                player.Revive();
                Phase = BattlePhase.Boss;
                BossTimeRemaining = Mathf.Max(1f, CurrentStage.bossTimeLimit);
                Vector3 bossPosition = bossSpawnPoint != null
                    ? bossSpawnPoint.position
                    : player.transform.position + Vector3.right * bossSpawnDistance;
                Spawn(CurrentStage.Boss, true, bossPosition);
                NotifyStateChanged();
            });
            _transitioning = false;
            NotifyStateChanged();
        }

        private static int CountEnemiesInGroup(int group)
        {
            int count = 0;
            foreach (EnemyActor enemy in EnemyActor.Active)
                if (enemy != null && enemy.CombatGroup == group) count++;
            return count;
        }

        private IEnumerator PlayBossCutTransition(Action onScreenCovered)
        {
            if (IsAnyPopupOpen())
            {
                onScreenCovered?.Invoke();
                yield break;
            }

            Canvas canvas = FindMainUiCanvas();
            Sprite fadeSprite = Resources.Load<Sprite>("UI/Fade");
            if (canvas == null || fadeSprite == null)
            {
                onScreenCovered?.Invoke();
                yield return new WaitForSecondsRealtime(.5f);
                yield break;
            }

            GameObject overlay = new GameObject(
                "BossFadeCrossTransition",
                typeof(RectTransform),
                typeof(CanvasGroup));
            RectTransform overlayRect = overlay.GetComponent<RectTransform>();
            overlayRect.SetParent(canvas.transform, false);
            overlayRect.anchorMin = Vector2.zero;
            overlayRect.anchorMax = Vector2.one;
            overlayRect.offsetMin = Vector2.zero;
            overlayRect.offsetMax = Vector2.zero;
            overlay.transform.SetAsLastSibling();

            CanvasGroup group = overlay.GetComponent<CanvasGroup>();
            group.blocksRaycasts = false;
            group.interactable = false;
            group.alpha = 1f;

            Canvas.ForceUpdateCanvases();
            float viewWidth = Mathf.Max(1f, overlayRect.rect.width);
            float viewHeight = Mathf.Max(1f, overlayRect.rect.height);
            float spriteAspect = fadeSprite.rect.width / Mathf.Max(1f, fadeSprite.rect.height);
            Vector2 imageSize = new Vector2(viewHeight * spriteAspect, viewHeight);
            float travelDistance = (viewWidth + imageSize.x) * .5f + 80f;

            RectTransform fade = CreateFadeCrossImage(
                overlayRect, "Fade_Wipe", fadeSprite, imageSize, false);

            const float closeDuration = .34f;
            float elapsed = 0f;
            while (elapsed < closeDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float progress = Mathf.Clamp01(elapsed / closeDuration);
                float eased = 1f - Mathf.Pow(1f - progress, 3f);
                fade.anchoredPosition = new Vector2(
                    Mathf.Lerp(-travelDistance, 0f, eased), 0f);
                yield return null;
            }

            fade.anchoredPosition = Vector2.zero;
            yield return new WaitForSecondsRealtime(.28f);

            const float openDuration = .4f;
            elapsed = 0f;
            while (elapsed < openDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float progress = Mathf.Clamp01(elapsed / openDuration);
                float eased = progress * progress * (3f - 2f * progress);
                fade.anchoredPosition = new Vector2(
                    Mathf.Lerp(0f, travelDistance, eased), 0f);
                yield return null;
            }

            Destroy(overlay);
            onScreenCovered?.Invoke();
        }

        private static bool IsAnyPopupOpen()
        {
            foreach (Transform candidate in Resources.FindObjectsOfTypeAll<Transform>())
            {
                GameObject popup = candidate.gameObject;
                if (!popup.scene.IsValid() || !popup.scene.isLoaded || !popup.activeInHierarchy)
                    continue;
                if (!candidate.name.EndsWith("Popup", StringComparison.Ordinal))
                    continue;

                // These are always-active layout containers, not opened popup windows.
                if (candidate.name == "MainPopup" || candidate.name == "Popup")
                    continue;
                return true;
            }
            return false;
        }

        private static Canvas FindMainUiCanvas()
        {
            // Player HP bars also use a GameObject named "Canvas".  Looking it up by
            // name can therefore attach this full-screen transition to a tiny
            // World-Space canvas, making the fade appear extremely small.
            GameObject safeArea = GameObject.Find("Canvas/SafeArea");
            Canvas safeAreaCanvas = safeArea != null
                ? safeArea.GetComponentInParent<Canvas>()
                : null;

            if (safeAreaCanvas != null && safeAreaCanvas.renderMode != RenderMode.WorldSpace)
            {
                return safeAreaCanvas;
            }

            Canvas[] canvases = UnityEngine.Object.FindObjectsOfType<Canvas>(true);
            foreach (Canvas candidate in canvases)
            {
                if (candidate != null &&
                    candidate.renderMode != RenderMode.WorldSpace &&
                    candidate.gameObject.scene.IsValid() &&
                    candidate.gameObject.scene.isLoaded)
                {
                    return candidate;
                }
            }

            return null;
        }

        private static RectTransform CreateFadeCrossImage(
            RectTransform parent,
            string objectName,
            Sprite sprite,
            Vector2 size,
            bool mirrorHorizontally)
        {
            GameObject imageObject = new GameObject(
                objectName,
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image));
            RectTransform rect = imageObject.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = new Vector2(.5f, .5f);
            rect.pivot = new Vector2(.5f, .5f);
            rect.sizeDelta = size;
            rect.localScale = new Vector3(mirrorHorizontally ? -1f : 1f, 1f, 1f);

            Image image = imageObject.GetComponent<Image>();
            image.sprite = sprite;
            image.color = Color.white;
            image.preserveAspect = true;
            image.raycastTarget = false;
            return rect;
        }

        private IEnumerator CompleteStage()
        {
            _transitioning = true;
            Phase = BattlePhase.StageClear;
            CurrentBoss = null;
            BossTimeRemaining = 0f;
            player.ClearActiveSkills();
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

        private IEnumerator ReturnToNormal()
        {
            _transitioning = true;
            Phase = BattlePhase.Returning;
            CurrentBoss = null;
            BossTimeRemaining = 0f;
            player.ClearActiveSkills();
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
            if (UpgradeSystem == null)
                return 0;

            int upgradedLevels = UpgradeSystem.TryUpgrade(type, requestedLevels);
            if (upgradedLevels <= 0)
                return 0;

            ApplyPlayerProgression();
            Save();
            NotifyStateChanged();
            return upgradedLevels;
        }

        public int GetStatLevel(StatType type) => UpgradeSystem?.GetStatLevel(type) ?? 0;
        public int TotalUpgradeLevel => UpgradeSystem?.TotalUpgradeLevel ?? 1;
        public int TotalUpgradeProgress => UpgradeSystem?.TotalUpgradeProgress ?? 0;
        public int TotalUpgradeProgressRequired =>
            UpgradeSystem?.TotalUpgradeProgressRequired ??
            GameBalance.StatLevelsPerTotalUpgradeLevel * GameBalance.UpgradeableStatCount;
        public bool CanIncreaseTotalUpgradeLevel => UpgradeSystem?.CanIncreaseTotalUpgradeLevel ?? false;

        public int GetMaxStatLevel(StatType type) =>
            UpgradeSystem?.GetMaxStatLevel(type) ?? GameBalance.StatLevelsPerTotalUpgradeLevel;

        public bool TryIncreaseTotalUpgradeLevel()
        {
            if (UpgradeSystem == null || !UpgradeSystem.TryIncreaseTotalUpgradeLevel())
                return false;

            Save();
            NotifyStateChanged();
            return true;
        }

        public bool CanUpgrade(StatType type)
        {
            return UpgradeSystem?.CanUpgrade(type) ?? false;
        }

        public bool CanUpgrade(StatType type, int requestedLevels)
        {
            return UpgradeSystem?.CanUpgrade(type, requestedLevels) ?? false;
        }

        public double GetStatValue(StatType type, int additionalLevels = 0)
        {
            return UpgradeSystem?.GetStatValue(type, additionalLevels) ?? 0d;
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
            UpgradeSystem = new UpgradeSystem(_saveData);
            OfflineRewardSystem = new OfflineRewardSystem(_saveData);
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

        public bool CheatMoveToStage(int targetStage)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (!_initialized || stageDatabase == null || player == null)
                return false;

            int nextStage = Mathf.Max(1, targetStage);
            StageRuntimeData nextStageData = stageDatabase.BuildStage(nextStage);
            if (nextStageData == null)
                return false;

            StopAllCoroutines();
            player.ClearActiveSkills();
            ClearEnemies();

            StageNumber = nextStage;
            CurrentStage = nextStageData;
            StageExperience = 0;
            CurrentBoss = null;
            BossTimeRemaining = 0f;
            _spawnTimer = 0f;
            _transitioning = false;
            Phase = BattlePhase.Normal;

            _saveData.stage = StageNumber;
            _saveData.stageProgress = 0f;
            _saveData.bossRetryRequired = false;

            player.ResetPosition();
            player.Revive();
            ApplyBackground();
            Save();
            NotifyStateChanged();
            return true;
#else
            return false;
#endif
        }

        public double GetUpgradeCost(StatType type)
        {
            return UpgradeSystem?.GetUpgradeCost(type) ?? 0d;
        }

        public double GetUpgradeCost(StatType type, int levelCount)
        {
            return UpgradeSystem?.GetUpgradeCost(type, levelCount) ?? 0d;
        }

        public int GetUpgradeLevelCount(StatType type, int requestedLevels)
        {
            return UpgradeSystem?.GetUpgradeLevelCount(type, requestedLevels) ?? 0;
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

        public TowerProgressData GetTowerProgress(TowerType type)
        {
            if (_saveData == null)
                return null;
            RefreshDailyTowerTickets();
            return _saveData.GetTower(type);
        }

        public bool RefreshDailyTowerTickets()
        {
            if (_saveData == null)
                return false;

            string today = DateTime.Now.ToString("yyyyMMdd");
            bool changed = _saveData.goldTower.RefreshDailyTickets(today);
            changed |= _saveData.gemTower.RefreshDailyTickets(today);
            if (changed)
                Save();
            return changed;
        }

        public bool TryBeginTowerRun(TowerType type, int floor, out TowerRunSetup setup)
        {
            setup = default;
            if (_saveData == null || _towerRunActive)
                return false;

            RefreshDailyTowerTickets();
            TowerProgressData progress = _saveData.GetTower(type);
            floor = Mathf.Max(1, floor);
            if (progress == null || progress.tickets <= 0 || floor > progress.highestUnlockedFloor)
                return false;

            progress.tickets--;
            _towerRunActive = true;
            _activeTowerType = type;
            _activeTowerFloor = floor;
            setup = new TowerRunSetup(type, floor);
            Save();
            NotifyStateChanged();
            return true;
        }

        public bool TryGetActiveTowerRun(out TowerRunSetup setup)
        {
            setup = default;
            if (!_towerRunActive) return false;
            setup = new TowerRunSetup(_activeTowerType, _activeTowerFloor);
            return true;
        }

        public void ConfigureTowerPlayer(PlayerAutoCombat towerPlayer)
        {
            if (towerPlayer == null || _saveData == null) return;
            ApplyPlayerProgression(towerPlayer);
            SyncEquippedSkills(towerPlayer);
            towerPlayer.Revive();
        }

        public TowerRunResult CompleteTowerRun(bool cleared, float remainingTime)
        {
            if (_saveData == null || !_towerRunActive)
                return default;

            TowerType type = _activeTowerType;
            int floor = _activeTowerFloor;
            _towerRunActive = false;
            _activeTowerFloor = 0;
            remainingTime = Mathf.Clamp(remainingTime, 0f, TowerBalance.TimeLimit);
            TowerGrade grade = TowerBalance.Grade(remainingTime, cleared);
            TowerProgressData progress = _saveData.GetTower(type);
            TowerGrade previousBestGrade = progress.GetBestGrade(floor);
            bool firstSGradeClear = grade == TowerGrade.S && previousBestGrade < TowerGrade.S;
            int previousHighest = progress.highestUnlockedFloor;
            if (grade == TowerGrade.F)
                progress.tickets++;
            else
            {
                progress.RecordClear(floor, grade, TowerBalance.TimeLimit - remainingTime);
                AddGuideMissionActionProgress(
                    type == TowerType.Gold
                        ? GuideMissionType.ClearGoldTower
                        : GuideMissionType.ClearGemTower,
                    1);
            }

            double goldReward = type == TowerType.Gold
                ? TowerBalance.GoldReward(floor, grade, firstSGradeClear)
                : 0d;
            int gemReward = type == TowerType.Gem
                ? TowerBalance.GemReward(floor, grade, firstSGradeClear)
                : 0;
            _saveData.gold += goldReward;
            _saveData.gems += gemReward;
            if (goldReward > 0d) PublishReward(RewardType.Gold, goldReward, "골드의 탑");
            if (gemReward > 0) PublishReward(RewardType.Gem, gemReward, "보석의 탑");
            Save();
            NotifyStateChanged();
            return new TowerRunResult(
                type,
                floor,
                grade,
                remainingTime,
                goldReward,
                gemReward,
                progress.highestUnlockedFloor > previousHighest,
                progress.GetBestGrade(floor) >= TowerGrade.A);
        }

        public bool TrySweepTower(TowerType type, int floor, out TowerRunResult result)
        {
            result = default;
            if (_saveData == null || _towerRunActive)
                return false;

            RefreshDailyTowerTickets();
            TowerProgressData progress = _saveData.GetTower(type);
            floor = Mathf.Max(1, floor);
            TowerGrade bestGrade = progress?.GetBestGrade(floor) ?? TowerGrade.F;
            if (progress == null || progress.tickets <= 0 || floor > progress.highestUnlockedFloor ||
                bestGrade < TowerGrade.A)
                return false;

            progress.tickets--;
            AddGuideMissionActionProgress(
                type == TowerType.Gold
                    ? GuideMissionType.ClearGoldTower
                    : GuideMissionType.ClearGemTower,
                1);
            double goldReward = type == TowerType.Gold
                ? TowerBalance.GoldReward(floor, bestGrade, false)
                : 0d;
            int gemReward = type == TowerType.Gem
                ? TowerBalance.GemReward(floor, bestGrade, false)
                : 0;
            _saveData.gold += goldReward;
            _saveData.gems += gemReward;
            if (goldReward > 0d) PublishReward(RewardType.Gold, goldReward, "골드의 탑 자동 토벌");
            if (gemReward > 0) PublishReward(RewardType.Gem, gemReward, "보석의 탑 자동 토벌");
            result = new TowerRunResult(
                type, floor, bestGrade, TowerBalance.TimeLimit, goldReward, gemReward, false, true);
            Save();
            NotifyStateChanged();
            return true;
        }

        public void CancelTowerRun()
        {
            if (_saveData == null || !_towerRunActive)
                return;
            _saveData.GetTower(_activeTowerType).tickets++;
            _towerRunActive = false;
            _activeTowerFloor = 0;
            Save();
            NotifyStateChanged();
        }

        public void GrantTowerTickets(TowerType type, int amount)
        {
            if (_saveData == null || amount <= 0)
                return;
            TowerProgressData progress = _saveData.GetTower(type);
            progress.tickets = (int)Math.Min(int.MaxValue, (long)progress.tickets + amount);
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
            if (mission.goldTowerTicketReward > 0)
                _saveData.GetTower(TowerType.Gold).tickets = (int)Math.Min(
                    int.MaxValue,
                    (long)_saveData.GetTower(TowerType.Gold).tickets + mission.goldTowerTicketReward);
            if (mission.gemTowerTicketReward > 0)
                _saveData.GetTower(TowerType.Gem).tickets = (int)Math.Min(
                    int.MaxValue,
                    (long)_saveData.GetTower(TowerType.Gem).tickets + mission.gemTowerTicketReward);
            _saveData.guideMissionIndex = (int)Math.Min(
                int.MaxValue,
                (long)Math.Max(0, _saveData.guideMissionIndex) + 1L);
            _saveData.guideMissionProgress = 0;
            GameAudioManager.Instance.PlaySfx("SFX_Mission_Complete");
            if (reward > 0)
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
                GuideMissionType.ClearGoldTower => _saveData.guideMissionProgress,
                GuideMissionType.ClearGemTower => _saveData.guideMissionProgress,
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

        public int CheatGrantAllSkills()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (_saveData == null)
                return 0;

            int grantedCount = 0;
            foreach (SkillData skill in Resources.LoadAll<SkillData>("StageData/Skills"))
            {
                if (skill == null || string.IsNullOrWhiteSpace(skill.id))
                    continue;

                SkillSaveEntry entry = _saveData.GetOrCreateSkill(skill.id);
                if (entry.level > 0)
                    continue;

                entry.level = 1;
                entry.duplicates = 0;
                grantedCount++;
            }

            ApplyPlayerProgression();
            SyncEquippedSkills();
            Save();
            NotifyStateChanged();
            return grantedCount;
#else
            return 0;
#endif
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
            SyncEquippedSkills();
            Save();
            NotifyStateChanged();
            return true;
        }

        public int TryUpgradeAllSkills()
        {
            if (_saveData?.skillInventory == null)
                return 0;

            int upgradedCount = 0;
            foreach (SkillData skill in Resources.LoadAll<SkillData>("StageData/Skills"))
            {
                if (skill == null)
                    continue;

                SkillSaveEntry state = GetSkillState(skill.id);
                while (state != null && state.level > 0 && state.level < skill.maxLevel)
                {
                    int required = SkillBalance.DuplicateRequirement(state.level);
                    if (state.duplicates < required)
                        break;

                    state.duplicates -= required;
                    state.level++;
                    upgradedCount++;
                }
            }

            if (upgradedCount <= 0)
                return 0;

            ApplyPlayerProgression();
            SyncEquippedSkills();
            Save();
            NotifyStateChanged();
            return upgradedCount;
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
            ApplyPlayerProgression(player);
        }

        private void ApplyPlayerProgression(PlayerAutoCombat target)
        {
            if (target == null || _saveData == null)
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
            target.ApplyProgression(_saveData, bonuses);
        }

        private void SyncEquippedSkills()
        {
            SyncEquippedSkills(player);
        }

        private void SyncEquippedSkills(PlayerAutoCombat target)
        {
            if (target == null || _saveData == null)
                return;
            EnsureEquippedSkillSlots();
            SkillData[] equipped = new SkillData[SkillBalance.MaxEquippedSkillCount];
            int[] levels = new int[SkillBalance.MaxEquippedSkillCount];
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
                levels[i] = state != null && state.level > 0 ? state.level : 1;
                if (equipped[i] == null) _saveData.equippedSkillIds[i] = string.Empty;
            }
            target.SetEquippedSkills(equipped, levels);
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
                if (enemy != null && (player == null || enemy.CombatGroup == player.CombatGroup))
                    Destroy(enemy.gameObject);
            }
        }

        private void ApplyBackground()
        {
            if (Camera.main != null && CurrentStage != null)
                Camera.main.backgroundColor = CurrentStage.region.backgroundColor;
        }

        private void NotifyStateChanged() => StateChanged?.Invoke();

        private void QueueOfflineReward(double elapsedSeconds)
        {
            if (OfflineRewardSystem == null)
                return;

            bool changed = OfflineRewardSystem.QueueReward(
                elapsedSeconds,
                CurrentStage,
                StageNumber,
                player,
                GetCurrentSpawnBatchSize(),
                GetCurrentSpawnInterval());

            if (!changed)
                return;

            Save();
            NotifyStateChanged();
        }

        public bool TryReceiveOfflineReward()
        {
            if (OfflineRewardSystem == null || !OfflineRewardSystem.TryReceive())
                return false;

            Save();
            NotifyStateChanged();
            return true;
        }

        private void BindOfflineRewardPopup()
        {
            GameObject popup = null;
            foreach (Transform candidate in Resources.FindObjectsOfTypeAll<Transform>())
            {
                if (candidate != null && candidate.name == "OfflineRewardPopup" &&
                    candidate.gameObject.scene.IsValid())
                {
                    popup = candidate.gameObject;
                    break;
                }
            }

            if (popup == null)
                return;
            OfflineRewardPopupController controller =
                popup.GetComponent<OfflineRewardPopupController>() ??
                popup.AddComponent<OfflineRewardPopupController>();
            controller.Bind(this);
        }

        private void Save()
        {
            if (_saveData != null)
            {
                _saveData.lastSavedUtcTicks = DateTime.UtcNow.Ticks;
                SaveService.Save(_saveData);
            }
        }

        private void OnApplicationPause(bool paused)
        {
            if (paused)
            {
                Save();
                return;
            }

            if (!_initialized || OfflineRewardSystem == null)
                return;

            double elapsed = OfflineRewardSystem.CaptureElapsedSeconds();
            QueueOfflineReward(elapsed);
        }

        private void OnApplicationQuit() => Save();

        private void OnDestroy()
        {
            if (EquipmentInventory != null)
                EquipmentInventory.Changed -= OnEquipmentChanged;
        }
    }

    [DisallowMultipleComponent]
    public sealed class OfflineRewardPopupController : MonoBehaviour
    {
        private MainBattleLoop _battle;
        private GameObject _backgroundPanel;
        private Image _timeFill;
        private TMP_Text _amountText;
        private Button _receiveButton;

        public void Bind(MainBattleLoop battle)
        {
            if (_battle != null)
                _battle.StateChanged -= Refresh;
            _battle = battle;
            FindReferences();
            if (_receiveButton != null)
            {
                _receiveButton.onClick.RemoveListener(Receive);
                _receiveButton.onClick.AddListener(Receive);
            }
            if (_battle != null)
                _battle.StateChanged += Refresh;
            Refresh();
        }

        private void FindReferences()
        {
            Transform popupRoot = transform.parent;
            _backgroundPanel = FindDirectChild(popupRoot, "Panel")?.gameObject;
            Transform timeSlider = FindDescendant(transform, "TimeSlider");
            _timeFill = FindDescendant(timeSlider, "Fill")?.GetComponent<Image>();
            Transform rewardItem = FindDescendant(transform, "RewardItem");
            _amountText = FindDescendant(rewardItem, "AmountText")?.GetComponent<TMP_Text>();
            _receiveButton = FindDescendant(transform, "Btn_Receive")?.GetComponent<Button>();
        }

        private void Refresh()
        {
            bool visible = _battle != null && _battle.PendingOfflineSeconds > 0d;
            if (_timeFill != null)
                _timeFill.fillAmount = _battle != null ? _battle.OfflineRewardProgress01 : 0f;
            if (_amountText != null)
                _amountText.text = MainHUDController.FormatNumber(_battle?.PendingOfflineGold ?? 0d);
            SetVisible(visible);
        }

        private void Receive()
        {
            if (_battle == null || !_battle.TryReceiveOfflineReward())
                return;
            SetVisible(false);
        }

        private void SetVisible(bool visible)
        {
            if (_backgroundPanel != null)
                _backgroundPanel.SetActive(visible);
            if (gameObject.activeSelf != visible)
                gameObject.SetActive(visible);
        }

        private static Transform FindDirectChild(Transform root, string objectName)
        {
            if (root == null)
                return null;
            for (int i = 0; i < root.childCount; i++)
            {
                Transform child = root.GetChild(i);
                if (child.name == objectName)
                    return child;
            }
            return null;
        }

        private static Transform FindDescendant(Transform root, string objectName)
        {
            if (root == null)
                return null;
            foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
                if (child.name == objectName)
                    return child;
            return null;
        }

        private void OnDestroy()
        {
            if (_battle != null)
                _battle.StateChanged -= Refresh;
            if (_receiveButton != null)
                _receiveButton.onClick.RemoveListener(Receive);
        }
    }
}

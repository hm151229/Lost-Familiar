using System;
using System.Collections;
using System.Collections.Generic;
using LostFamiliar.Core;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace LostFamiliar.Battle
{
    public enum BattlePhase { Normal, EnteringBoss, Boss, Returning, StageClear }

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

        [Header("UI")]
        [SerializeField] private BossChallengeButtonPresenter bossChallengePresenter;
        [SerializeField] private MainHUDController mainHud;
        [SerializeField] private RewardFeedController rewardFeed;
        [SerializeField] private GuideMissionPanelController guideMissionPanel;
        [SerializeField] private OfflineRewardPopupController offlineRewardPopup;
        [SerializeField] private EquipmentPopupController equipmentPopup;
        [SerializeField] private GachaPopupController gachaPopup;
        [SerializeField] private UpgradePopupController upgradePopup;
        [SerializeField] private SkillBarController skillBar;
        [SerializeField] private AdventureTowerPopupController adventureTowerPopup;

        [SerializeField] private Vector3 bossPlayerPosition = new Vector3(-1.35f, -.8f, 0f);
        [SerializeField, Min(1f)] private float bossSpawnDistance = 2.8f;

        public StageDatabase Database => stageDatabase;
        public EquipmentDatabase EquipmentDatabase => equipmentDatabase;
        public EquipmentInventory EquipmentInventory { get; private set; }
        public SkillInventory SkillInventory { get; private set; }
        public GachaSystem GachaSystem { get; private set; }
        public UpgradeSystem UpgradeSystem { get; private set; }
        public OfflineRewardSystem OfflineRewardSystem { get; private set; }
        public GuideMissionSystem GuideMissionSystem { get; private set; }
        public TowerSystem TowerSystem { get; private set; }
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
            GuideMissionSystem?.CurrentMission ?? GuideMissionCatalog.Get(0);
        public int GuideMissionProgress =>
            GuideMissionSystem?.GetProgress(
                CurrentGuideMission,
                Math.Max(0, StageNumber - 1)) ?? 0;
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

            stageDatabase = database;
            player = playerActor;
            _saveData ??= SaveService.Load();
            _saveData.Normalize();
            UpgradeSystem = new UpgradeSystem(_saveData);
            OfflineRewardSystem = new OfflineRewardSystem(_saveData);
            GuideMissionSystem = new GuideMissionSystem(_saveData);
            TowerSystem = new TowerSystem(_saveData);
            double offlineSeconds = OfflineRewardSystem?.CaptureElapsedSeconds() ?? 0d;
            RefreshDailyTowerTickets();
            equipmentDatabase ??= Resources.Load<EquipmentDatabase>("Equipment/DefaultEquipmentDatabase");
            InitializeEquipmentInventory();
            SkillInventory = new SkillInventory(_saveData);
            GachaSystem = new GachaSystem(_saveData, equipmentDatabase, SkillInventory);
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
            BindRuntimeUi();
            QueueOfflineReward(offlineSeconds);
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

            Canvas[] canvases = UnityEngine.Object.FindObjectsByType<Canvas>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);
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
            GuideMissionSystem = new GuideMissionSystem(_saveData);
            TowerSystem = new TowerSystem(_saveData);
            InitializeEquipmentInventory();
            SkillInventory = new SkillInventory(_saveData);
            GachaSystem = new GachaSystem(_saveData, equipmentDatabase, SkillInventory);
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
            if (TowerSystem == null)
                return null;

            if (TowerSystem.RefreshDailyTickets())
                Save();

            return TowerSystem.GetProgress(type);
        }

        public bool RefreshDailyTowerTickets()
        {
            if (TowerSystem == null)
                return false;

            bool changed = TowerSystem.RefreshDailyTickets();
            if (changed)
                Save();
            return changed;
        }

        public bool TryBeginTowerRun(TowerType type, int floor, out TowerRunSetup setup)
        {
            setup = default;
            if (TowerSystem == null ||
                !TowerSystem.TryBeginRun(type, floor, out setup))
                return false;

            Save();
            NotifyStateChanged();
            return true;
        }

        public bool TryGetActiveTowerRun(out TowerRunSetup setup)
        {
            setup = default;

            return TowerSystem != null &&
                   TowerSystem.TryGetActiveRun(out setup);
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
            if (TowerSystem == null ||
                !TowerSystem.TryCompleteRun(
                    cleared,
                    remainingTime,
                    out TowerRunResult result))
                return default;

            if (result.grade != TowerGrade.F)
            {
                GuideMissionSystem?.AddActionProgress(
                    result.type == TowerType.Gold
                        ? GuideMissionType.ClearGoldTower
                        : GuideMissionType.ClearGemTower,
                    1);
            }

            if (result.goldReward > 0d)
                PublishReward(RewardType.Gold, result.goldReward, "골드의 탑");
            if (result.gemReward > 0)
                PublishReward(RewardType.Gem, result.gemReward, "보석의 탑");

            Save();
            NotifyStateChanged();
            return result;
        }

        public bool TrySweepTower(TowerType type, int floor, out TowerRunResult result)
        {
            result = default;
            if (TowerSystem == null ||
                !TowerSystem.TrySweep(type, floor, out result))
                return false;

            GuideMissionSystem?.AddActionProgress(
                type == TowerType.Gold
                    ? GuideMissionType.ClearGoldTower
                    : GuideMissionType.ClearGemTower,
                1);

            if (result.goldReward > 0d)
                PublishReward(RewardType.Gold, result.goldReward, "골드의 탑 자동 토벌");
            if (result.gemReward > 0)
                PublishReward(RewardType.Gem, result.gemReward, "보석의 탑 자동 토벌");

            Save();
            NotifyStateChanged();
            return true;
        }

        public void CancelTowerRun()
        {
            if (TowerSystem == null || !TowerSystem.CancelRun())
                return;

            Save();
            NotifyStateChanged();
        }

        public void GrantTowerTickets(TowerType type, int amount)
        {
            if (TowerSystem == null || !TowerSystem.GrantTickets(type, amount))
                return;

            Save();
            NotifyStateChanged();
        }

        public int GetGachaLevel(GachaCategory category)
        {
            return GachaSystem?.GetLevel(category) ?? 1;
        }

        public int GetGachaProgress(GachaCategory category)
        {
            return GachaSystem?.GetProgress(category) ?? 0;
        }

        public bool TryGacha(GachaCategory category, int drawCount, out List<GachaReward> rewards)
        {
            rewards = new List<GachaReward>();
            if (GachaSystem == null ||
                !GachaSystem.TryDraw(category, drawCount, out rewards))
                return false;

            List<string> equipmentIds = new List<string>();
            foreach (GachaReward reward in rewards)
            {
                if (reward.equipment != null)
                    equipmentIds.Add(reward.equipment.Id);
                else if (reward.skill != null)
                    SkillInventory?.Grant(reward.skill.id);
            }
            if (equipmentIds.Count > 0)
                EquipmentInventory?.GrantBatch(equipmentIds);

            GuideMissionSystem?.AddActionProgress(GuideMissionType.Gacha, drawCount);

            if (category == GachaCategory.Skill)
                ApplyPlayerProgression();
            Save();
            NotifyStateChanged();
            return true;
        }

        public bool TryClaimGuideMission()
        {
            if (GuideMissionSystem == null ||
                !GuideMissionSystem.TryClaim(
                    Math.Max(0, StageNumber - 1),
                    out GuideMissionDefinition mission))
                return false;

            GameAudioManager.Instance.PlaySfx("SFX_Mission_Complete");
            if (mission.gemReward > 0)
                PublishReward(RewardType.Gem, mission.gemReward);
            Save();
            NotifyStateChanged();
            return true;
        }

        private void AddGuideMissionActionProgress(GuideMissionType type, int amount)
        {
            GuideMissionSystem?.AddActionProgress(type, amount);
        }

        public int CheatGrantAllSkills()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (SkillInventory == null)
                return 0;

            int grantedCount = 0;
            foreach (SkillData skill in Resources.LoadAll<SkillData>("StageData/Skills"))
            {
                if (skill == null || string.IsNullOrWhiteSpace(skill.id))
                    continue;

                SkillSaveEntry entry = SkillInventory.GetState(skill.id);
                if (entry != null && entry.level > 0)
                    continue;

                if (SkillInventory.Grant(skill.id) != null)
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
            return SkillInventory?.GetOwnedSkills() ?? Array.Empty<SkillData>();
        }

        public SkillSaveEntry GetSkillState(string skillId)
        {
            return SkillInventory?.GetState(skillId);
        }

        public int UnlockedSkillSlotCount =>
            SkillInventory?.GetUnlockedSlotCount(PlayerLevel) ?? 0;

        public bool IsSkillSlotUnlocked(int slotIndex)
        {
            return SkillInventory?.IsSlotUnlocked(slotIndex, PlayerLevel) ?? false;
        }

        public string GetEquippedSkillId(int slotIndex)
        {
            return SkillInventory?.GetEquippedSkillId(slotIndex) ?? string.Empty;
        }

        public SkillData GetEquippedSkill(int slotIndex)
        {
            return SkillInventory?.GetEquippedSkill(slotIndex);
        }

        public bool TryEquipSkill(string skillId, int slotIndex)
        {
            if (SkillInventory == null ||
                !SkillInventory.TryEquip(skillId, slotIndex, PlayerLevel))
                return false;

            SyncEquippedSkills();
            Save();
            NotifyStateChanged();
            return true;
        }

        public void UnequipSkill(int slotIndex)
        {
            if (SkillInventory == null ||
                !SkillInventory.Unequip(slotIndex))
                return;

            SyncEquippedSkills();
            Save();
            NotifyStateChanged();
        }

        public bool CanUpgradeSkill(string skillId)
        {
            return SkillInventory?.CanUpgrade(skillId) ?? false;
        }

        public bool TryUpgradeSkill(string skillId)
        {
            if (SkillInventory == null ||
                !SkillInventory.TryUpgrade(skillId))
                return false;

            ApplyPlayerProgression();
            SyncEquippedSkills();
            Save();
            NotifyStateChanged();
            return true;
        }

        public int TryUpgradeAllSkills()
        {
            if (SkillInventory == null)
                return 0;

            int upgradedCount = SkillInventory.TryUpgradeAll();

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
            SkillInventory?.AddOwnedBonuses(ref bonuses);
            target.ApplyProgression(_saveData, bonuses);
        }

        private void SyncEquippedSkills()
        {
            SyncEquippedSkills(player);
        }

        private void SyncEquippedSkills(PlayerAutoCombat target)
        {
            if (target == null || SkillInventory == null)
                return;

            SkillInventory.BuildEquippedSkills(
                PlayerLevel,
                out SkillData[] equipped,
                out int[] levels);

            target.SetEquippedSkills(equipped, levels);
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

        private void BindRuntimeUi()
        {
            if (mainHud == null)
                Debug.LogWarning("MainHUDController가 연결되지 않았습니다.", this);

            if (rewardFeed == null)
                Debug.LogWarning("RewardFeedController가 연결되지 않았습니다.", this);

            if (guideMissionPanel == null)
                Debug.LogWarning("GuideMissionPanelController가 연결되지 않았습니다.", this);

            if (offlineRewardPopup == null)
                Debug.LogWarning("OfflineRewardPopupController가 연결되지 않았습니다.", this);

            if (equipmentPopup == null)
                Debug.LogWarning("EquipmentPopupController가 연결되지 않았습니다.", this);

            if (gachaPopup == null)
                Debug.LogWarning("GachaPopupController가 연결되지 않았습니다.", this);

            if (upgradePopup == null)
                Debug.LogWarning("UpgradePopupController가 연결되지 않았습니다.", this);

            if (skillBar == null)
                Debug.LogWarning("SkillBarController가 연결되지 않았습니다.", this);

            if (adventureTowerPopup == null)
                Debug.LogWarning("AdventureTowerPopupController가 연결되지 않았습니다.", this);

            bossChallengePresenter?.Bind(this);
            mainHud?.Bind(this);
            rewardFeed?.Bind(this);
            guideMissionPanel?.Bind(this);
            offlineRewardPopup?.Bind(this);
            equipmentPopup?.Bind(this);
            gachaPopup?.Bind(this);
            upgradePopup?.Bind(this);
            skillBar?.Bind(this);
            adventureTowerPopup?.Bind(this);
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

}

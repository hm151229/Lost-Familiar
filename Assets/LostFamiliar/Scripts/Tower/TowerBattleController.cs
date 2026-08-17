using System;
using System.Collections;
using System.Collections.Generic;
using LostFamiliar.Core;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace LostFamiliar.Battle
{
    public sealed class TowerBattleController : MonoBehaviour
    {
        private const int TowerCombatGroup = 1;
        private const int TowerWorldLayer = 30;

        [Header("Tower World")]
        [SerializeField] private PlayerAutoCombat player;
        [SerializeField] private GameObject worldRoot;
        [SerializeField] private Camera towerCamera;
        [SerializeField] private CameraFollow2D cameraFollow;
        [SerializeField] private AudioListener towerAudioListener;
        [SerializeField] private GameObject background;
        [SerializeField] private BackgroundTiler2D backgroundTiler;
        [SerializeField] private Transform enemyBase;

        [Header("Battle UI")]
        [SerializeField] private TMP_Text towerNameText;
        [SerializeField] private TMP_Text timeText;
        [SerializeField] private Image bossHpFill;
        [SerializeField] private SkillBarController skillBar;

        [Header("Popup")]
        [SerializeField] private GameObject popupPanel;
        [SerializeField] private GameObject pausePopup;
        [SerializeField] private GameObject resultPopup;

        [Header("Pause Buttons")]
        [SerializeField] private Button exitButton;
        [SerializeField] private Button exitConfirmButton;
        [SerializeField] private Button exitCancelButton;

        [Header("Result")]
        [SerializeField] private Button resultConfirmButton;
        [SerializeField] private Button retryButton;
        [SerializeField] private Button nextButton;

        [Header("Result Reward")]
        [SerializeField] private Image resultRewardIcon;
        [SerializeField] private TMP_Text resultRewardAmountText;

        [Header("Result Tickets")]
        [SerializeField] private Sprite goldTicketIcon;
        [SerializeField] private Sprite gemTicketIcon;
        [SerializeField] private Image retryTicketIcon;
        [SerializeField] private TMP_Text retryTicketCountText;
        [SerializeField] private Image nextTicketIcon;
        [SerializeField] private TMP_Text nextTicketCountText;

        [Header("Result Reward Icons")]
        [SerializeField] private Sprite goldRewardIcon;
        [SerializeField] private Sprite gemRewardIcon;

        private MainBattleLoop _main;
        private TowerRunSetup _setup;
        private Color _defaultTimeTextColor = Color.white;
        private EnemyActor _currentBoss;
        private readonly List<EnemyActor> _enemies = new();
        private readonly List<EnemyActor> _dyingEnemies = new();
        private readonly List<(Behaviour component, bool enabled)> _hiddenBehaviours = new();
        private readonly List<(Renderer component, bool enabled)> _hiddenRenderers = new();
        private float _remainingTime;
        private int _normalRemaining;
        private int _bossRemaining;
        private bool _paused;
        private bool _finished;
        private bool _completionPending;
        private double _totalStageHealth;

        private void Start()
        {
            _main = FindFirstObjectByType<MainBattleLoop>();
            if (_main == null || !_main.TryGetActiveTowerRun(out _setup))
            {
                Debug.LogWarning("진행 중인 탑 입장 정보가 없어 탑 전투를 시작하지 못했습니다.", this);
                return;
            }

            if (player == null)
            {
                Debug.LogError("TowerBattleController에 PlayerAutoCombat이 연결되지 않았습니다.", this);
                _main.CancelTowerRun();
                return;
            }

            if (timeText != null)
                _defaultTimeTextColor = timeText.color;

            HideOtherScenePresentation();

            if (towerAudioListener != null)
                towerAudioListener.enabled = true;

            if (pausePopup != null)
                pausePopup.SetActive(false);

            if (resultPopup != null)
                resultPopup.SetActive(false);

            if (popupPanel != null)
                popupPanel.SetActive(false);

            player.gameObject.SetActive(true);
            player.enabled = true;
            player.SetCombatGroup(TowerCombatGroup);
            _main.ConfigureTowerPlayer(player);
            ConfigureTowerWorldAndCamera();
            _remainingTime = _setup.timeLimit;
            _normalRemaining = _setup.normalEnemyCount;
            _bossRemaining = _setup.bossCount;
            _totalStageHealth = CalculateTotalStageHealth();
            if (towerNameText != null)
                towerNameText.text =
                    $"{(_setup.type == TowerType.Gold ? "골드의 탑" : "보석의 탑")} Lv.{_setup.floor}";

            BindButtons();
            skillBar?.BindTower(_main, player);

            SpawnNormalEnemies();
            SpawnNextBoss();
            UpdateUi();
        }

        private void Update()
        {
            if (_main == null || _finished || _paused || _completionPending) return;
            if (player != null && !player.IsAlive)
            {
                StartCoroutine(TimeoutReturnRoutine());
                return;
            }
            _remainingTime = Mathf.Max(0f, _remainingTime - Time.deltaTime);
            UpdateUi();
            if (_remainingTime <= 0f) StartCoroutine(TimeoutReturnRoutine());
        }

        private void SpawnNormalEnemies()
        {
            EnemyData data = _main.CurrentStage?.region?.PickEnemy(_main.StageNumber);
            for (int i = 0; i < _setup.normalEnemyCount; i++)
            {
                float side = i % 2 == 0 ? 1f : -1f;
                Vector3 position = player.transform.position +
                    new Vector3(side * UnityEngine.Random.Range(4.5f, 6.5f), UnityEngine.Random.Range(-2.4f, 2.4f), 0f);
                SpawnEnemy(data, false, position, _setup.normalEnemyHealth);
            }
        }

        private void SpawnNextBoss()
        {
            if (_finished || _bossRemaining <= 0) return;
            EnemyData data = _main.CurrentStage?.Boss ?? _main.CurrentStage?.region?.PickEnemy(_main.StageNumber);
            Vector3 position = enemyBase != null
                ? enemyBase.position
                : player.transform.position + Vector3.right * 4f;
            _currentBoss = SpawnEnemy(data, true, position, _setup.bossHealth);
            UpdateUi();
        }

        private EnemyActor SpawnEnemy(EnemyData data, bool boss, Vector3 position, double desiredHealth)
        {
            if (data == null || data.prefab == null)
            {
                Debug.LogError($"Tower enemy prefab이 없습니다: {data?.name}", this);
                return null;
            }

            GameObject instance = Instantiate(data.prefab);
            SceneManager.MoveGameObjectToScene(instance, gameObject.scene);
            instance.transform.position = position;
            instance.SetActive(true);
            SetLayerRecursively(instance, TowerWorldLayer);
            EnemyActor enemy = instance.GetComponent<EnemyActor>();
            if (enemy == null)
            {
                Debug.LogError($"Enemy prefab '{data.name}'에 EnemyActor가 없습니다.", instance);
                Destroy(instance);
                return null;
            }

            double healthMultiplier = desiredHealth / Math.Max(1d, data.baseHealth);
            double attackMultiplier = _setup.enemyAttack / Math.Max(1d, data.baseAttack);
            enemy.Initialize(data, player, healthMultiplier, attackMultiplier, boss, 1f, 1f);
            if (boss) enemy.SetWorldHealthBarVisible(true);
            enemy.Died += OnEnemyDied;
            _enemies.Add(enemy);
            return enemy;
        }

        private void OnEnemyDied(EnemyActor enemy)
        {
            if (enemy == null) return;
            enemy.Died -= OnEnemyDied;
            _enemies.Remove(enemy);
            _dyingEnemies.Add(enemy);
            if (enemy.IsBoss)
            {
                _bossRemaining--;
                _currentBoss = null;
                if (_bossRemaining > 0) SpawnNextBoss();
            }
            else _normalRemaining--;

            if (_normalRemaining <= 0 && _bossRemaining <= 0 && !_completionPending)
                StartCoroutine(CompleteAfterDeathEffects());
        }

        private IEnumerator CompleteAfterDeathEffects()
        {
            _completionPending = true;
            if (player != null) player.enabled = false;
            float waitLimit = 2f;
            while (waitLimit > 0f)
            {
                _dyingEnemies.RemoveAll(enemy => enemy == null);
                if (_dyingEnemies.Count == 0) break;
                waitLimit -= Time.deltaTime;
                yield return null;
            }

            if (_finished) yield break;
            foreach (EnemyActor enemy in EnemyActor.Active)
            {
                if (enemy != null && enemy.CombatGroup == TowerCombatGroup && enemy.Health > 0f)
                {
                    _completionPending = false;
                    if (player != null) player.enabled = true;
                    yield break;
                }
            }

            if (_normalRemaining > 0 || _bossRemaining > 0)
            {
                _completionPending = false;
                if (player != null) player.enabled = true;
                yield break;
            }

            if (bossHpFill != null) bossHpFill.fillAmount = 0f;
            Finish(true);
        }

        private void Finish(bool cleared)
        {
            if (_finished) return;
            _finished = true;
            SetTowerCombatEnabled(false);
            TowerRunResult result = _main.CompleteTowerRun(cleared, _remainingTime);
            GameAudioManager.Instance.PlayBgm(
                cleared ? "BGM_Result_Victory" : "BGM_Result_Defeat", false);
            SetPopupVisible(resultPopup, true);
            UpdateResultReward(result);
            UpdateResultActionTickets(cleared);
        }

        public void OpenPause()
        {
            if (_finished || _paused || _completionPending) return;
            _paused = true;
            SetTowerCombatEnabled(false);
            SetPopupVisible(pausePopup, true);
        }

        public void Resume()
        {
            if (_finished) return;
            _paused = false;
            SetTowerCombatEnabled(true);
            SetPopupVisible(pausePopup, false);
        }

        public void ExitTower()
        {
            if (!_finished) _main?.CancelTowerRun();
            _finished = true;
            ReturnToAdventureAndUnload();
        }

        public void CloseResult() => ReturnToAdventureAndUnload();

        public void RetryFloor() => TryRestartAtFloor(_setup.floor);

        public void NextFloor() => TryRestartAtFloor(_setup.floor + 1);

        private void TryRestartAtFloor(int floor)
        {
            if (!_finished || _main == null ||
                !_main.TryBeginTowerRun(_setup.type, floor, out TowerRunSetup nextSetup)) return;

            ClearTowerEnemies();
            _setup = nextSetup;
            _remainingTime = _setup.timeLimit;
            _normalRemaining = _setup.normalEnemyCount;
            _bossRemaining = _setup.bossCount;
            _totalStageHealth = CalculateTotalStageHealth();
            _finished = false;
            _completionPending = false;
            _paused = false;
            GameAudioManager.Instance.PlayBgm("BGM_Tower");
            SetPopupVisible(resultPopup, false);
            SetPopupVisible(pausePopup, false);
            player.ResetPosition();
            _main.ConfigureTowerPlayer(player);
            player.enabled = true;
            if (towerNameText != null)
                towerNameText.text =
                    $"{(_setup.type == TowerType.Gold ? "골드의 탑" : "보석의 탑")} Lv.{_setup.floor}";
            SpawnNormalEnemies();
            SpawnNextBoss();
            UpdateUi();
        }

        private IEnumerator TimeoutReturnRoutine()
        {
            if (_finished) yield break;
            _finished = true;
            _paused = true;
            SetTowerCombatEnabled(false);
            _main.CompleteTowerRun(false, 0f);
            GameAudioManager.Instance.PlayBgm("BGM_Result_Defeat", false);
            yield return new WaitForSecondsRealtime(2f);

            Image fade = CreateFadeOverlay();
            const float duration = .55f;
            for (float elapsed = 0f; elapsed < duration; elapsed += Time.unscaledDeltaTime)
            {
                if (fade != null)
                {
                    Color color = fade.color;
                    color.a = Mathf.Clamp01(elapsed / duration);
                    fade.color = color;
                }
                yield return null;
            }
            ReturnToAdventureAndUnload();
        }

        private Image CreateFadeOverlay()
        {
            GameObject canvasObject = new GameObject("TowerTimeoutFade", typeof(RectTransform), typeof(Canvas), typeof(GraphicRaycaster));
            SceneManager.MoveGameObjectToScene(canvasObject, gameObject.scene);
            Canvas canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = short.MaxValue;
            GameObject imageObject = new GameObject("Fade", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            imageObject.transform.SetParent(canvasObject.transform, false);
            RectTransform rect = imageObject.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            Image image = imageObject.GetComponent<Image>();
            image.color = new Color(0f, 0f, 0f, 0f);
            image.raycastTarget = true;
            return image;
        }

        private void ClearTowerEnemies()
        {
            foreach (EnemyActor enemy in _enemies.ToArray())
            {
                if (enemy == null) continue;
                enemy.Died -= OnEnemyDied;
                Destroy(enemy.gameObject);
            }
            _enemies.Clear();
            foreach (EnemyActor enemy in _dyingEnemies)
                if (enemy != null) Destroy(enemy.gameObject);
            _dyingEnemies.Clear();
            _currentBoss = null;
        }

        private void UpdateResultReward(TowerRunResult result)
        {
            if (resultRewardAmountText != null)
            {
                resultRewardAmountText.text = result.type == TowerType.Gold
                    ? MainHUDController.FormatNumber(result.goldReward)
                    : result.gemReward.ToString();
            }

            if (resultRewardIcon != null)
            {
                resultRewardIcon.sprite = result.type == TowerType.Gold
                    ? goldRewardIcon
                    : gemRewardIcon;
            }
        }

        private void UpdateResultActionTickets(bool cleared)
        {
            TowerProgressData progress = _main?.GetTowerProgress(_setup.type);
            int ticketCount = progress?.tickets ?? 0;
            bool hasTicket = ticketCount > 0;
            if (retryButton != null) retryButton.interactable = hasTicket;
            if (nextButton != null)
                nextButton.interactable = hasTicket && cleared && progress != null &&
                    _setup.floor + 1 <= progress.highestUnlockedFloor;

            Sprite ticketSprite = _setup.type == TowerType.Gold
                ? goldTicketIcon
                : gemTicketIcon;

            UpdateButtonTicket(
                retryTicketIcon,
                retryTicketCountText,
                ticketSprite,
                ticketCount);

            UpdateButtonTicket(
                nextTicketIcon,
                nextTicketCountText,
                ticketSprite,
                ticketCount);
        }

        private static void UpdateButtonTicket(
            Image icon,
            TMP_Text countText,
            Sprite ticketSprite,
            int ticketCount)
        {
            if (icon != null && ticketSprite != null)
                icon.sprite = ticketSprite;

            if (countText != null)
                countText.text = Mathf.Max(0, ticketCount).ToString();
        }

        private void SetPopupVisible(GameObject popup, bool visible)
        {
            if (popup != null) popup.SetActive(visible);
            if (popupPanel != null)
                popupPanel.SetActive(visible ||
                    (pausePopup != null && pausePopup.activeSelf) ||
                    (resultPopup != null && resultPopup.activeSelf));
        }

        private void ReturnToAdventureAndUnload()
        {
            _main?.ShowAdventurePopup();
            UnloadTowerScene();
        }

        private void SetTowerCombatEnabled(bool enabled)
        {
            if (player != null) player.enabled = enabled;
            foreach (EnemyActor enemy in _enemies)
                if (enemy != null) enemy.enabled = enabled;
        }

        private void UpdateUi()
        {
            if (timeText != null)
            {
                timeText.text = _remainingTime.ToString("0.0");
                timeText.color = _remainingTime <= 5f
                    ? new Color32(0xE5, 0x40, 0x26, 0xFF)
                    : _remainingTime <= 12f
                        ? new Color32(0xF1, 0x70, 0x41, 0xFF)
                        : _remainingTime <= 20f
                            ? new Color32(0xFF, 0xBF, 0x67, 0xFF)
                            : _defaultTimeTextColor;
            }
            if (bossHpFill != null)
            {
                double remainingHealth = 0d;
                foreach (EnemyActor enemy in _enemies)
                    if (enemy != null) remainingHealth += Math.Max(0f, enemy.Health);
                int unspawnedBosses = Math.Max(0, _bossRemaining - (_currentBoss != null ? 1 : 0));
                remainingHealth += unspawnedBosses * _setup.bossHealth;
                bossHpFill.fillAmount = _totalStageHealth > 0d
                    ? Mathf.Clamp01((float)(remainingHealth / _totalStageHealth))
                    : 0f;
            }
        }

        private double CalculateTotalStageHealth() =>
            _setup.normalEnemyCount * _setup.normalEnemyHealth +
            _setup.bossCount * _setup.bossHealth;

        private void UnloadTowerScene()
        {
            if (gameObject.scene.IsValid() && SceneManager.sceneCount > 1)
                SceneManager.UnloadSceneAsync(gameObject.scene);
        }

        private void HideOtherScenePresentation()
        {
            foreach (GameObject root in GetOtherSceneRoots())
            {
                foreach (Behaviour component in root.GetComponentsInChildren<Behaviour>(true))
                {
                    if (component is not Camera && component is not AudioListener &&
                        component is not Canvas && component is not GraphicRaycaster &&
                        component is not EventSystem)
                        continue;
                    _hiddenBehaviours.Add((component, component.enabled));
                    component.enabled = false;
                }

                foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>(true))
                {
                    _hiddenRenderers.Add((renderer, renderer.enabled));
                    renderer.enabled = false;
                }
            }
        }

        private void ConfigureTowerWorldAndCamera()
        {
            if (worldRoot != null)
                SetLayerRecursively(worldRoot, TowerWorldLayer);

            SetLayerRecursively(player.gameObject, TowerWorldLayer);

            if (towerCamera != null)
            {
                towerCamera.enabled = true;
                towerCamera.cullingMask = 1 << TowerWorldLayer;
            }

            if (cameraFollow != null)
            {
                cameraFollow.Bind(player.transform);
                cameraFollow.SnapToTarget();
            }
            else
            {
                Debug.LogError("Tower CameraFollow2D가 연결되지 않았습니다.", this);
            }

            if (background != null)
                SetLayerRecursively(background, TowerWorldLayer);

            if (backgroundTiler != null)
            {
                backgroundTiler.Bind(player.transform);
            }
            else
            {
                Debug.LogError("Tower BackgroundTiler2D가 연결되지 않았습니다.", this);
            }
        }

        private static void SetLayerRecursively(GameObject root, int layer)
        {
            if (root == null) return;
            foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
                child.gameObject.layer = layer;
        }

        private IEnumerable<GameObject> GetOtherSceneRoots()
        {
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                Scene scene = SceneManager.GetSceneAt(i);
                if (!scene.IsValid() || !scene.isLoaded || scene == gameObject.scene) continue;
                foreach (GameObject root in scene.GetRootGameObjects()) yield return root;
            }
        }

        private void RestoreOtherScenePresentation()
        {
            foreach ((Behaviour component, bool wasEnabled) in _hiddenBehaviours)
                if (component != null) component.enabled = wasEnabled;
            foreach ((Renderer component, bool wasEnabled) in _hiddenRenderers)
                if (component != null) component.enabled = wasEnabled;
            _hiddenBehaviours.Clear();
            _hiddenRenderers.Clear();
        }

        private void OnDestroy()
        {
            RestoreOtherScenePresentation();
            foreach (EnemyActor enemy in _enemies)
                if (enemy != null) enemy.Died -= OnEnemyDied;
            if (!_finished) _main?.CancelTowerRun();
        }

        private void BindButtons()
        {
            exitButton?.onClick.AddListener(OpenPause);
            exitConfirmButton?.onClick.AddListener(ExitTower);
            exitCancelButton?.onClick.AddListener(Resume);

            resultConfirmButton?.onClick.AddListener(CloseResult);
            retryButton?.onClick.AddListener(RetryFloor);
            nextButton?.onClick.AddListener(NextFloor);
        }

    }
}

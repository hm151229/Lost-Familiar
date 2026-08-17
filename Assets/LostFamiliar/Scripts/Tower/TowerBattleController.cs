using System;
using System.Collections;
using System.Collections.Generic;
using LostFamiliar.Core;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace LostFamiliar.Battle
{
    public sealed class TowerBattleController : MonoBehaviour
    {
        private const int TowerCombatGroup = 1;
        private const int TowerWorldLayer = 30;
        private MainBattleLoop _main;
        private PlayerAutoCombat _player;
        private TowerRunSetup _setup;
        private TMP_Text _towerNameText;
        private TMP_Text _timeText;
        private Color _defaultTimeTextColor = Color.white;
        private Image _bossHpFill;
        private GameObject _pausePopup;
        private GameObject _resultPopup;
        private GameObject _popupPanel;
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

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
            for (int i = 0; i < SceneManager.sceneCount; i++)
                TryInstall(SceneManager.GetSceneAt(i));
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode) => TryInstall(scene);

        private static void TryInstall(Scene scene)
        {
            if (!scene.IsValid() || !scene.isLoaded || scene.name != "TowerBattleScene") return;
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                TowerBattleController existing = root.GetComponentInChildren<TowerBattleController>(true);
                if (existing != null) return;
            }
            GameObject host = new GameObject("TowerBattleController");
            SceneManager.MoveGameObjectToScene(host, scene);
            host.AddComponent<TowerBattleController>();
        }

        private void Start()
        {
            _main = FindFirstObjectByType<MainBattleLoop>();
            if (_main == null || !_main.TryGetActiveTowerRun(out _setup))
            {
                Debug.LogWarning("진행 중인 탑 입장 정보가 없어 탑 전투를 시작하지 못했습니다.", this);
                return;
            }

            _player = FindInScene<PlayerAutoCombat>();
            _towerNameText = FindComponent<TMP_Text>("TowerNameText");
            _timeText = FindComponent<TMP_Text>("TimeText");
            if (_timeText != null) _defaultTimeTextColor = _timeText.color;
            _bossHpFill = FindComponent<Image>("Fill", "HpBar");
            _pausePopup = FindObject("PausePopup");
            _resultPopup = FindObject("ResultPopup");
            _popupPanel = FindObject("Panel", "PopupRoot");
            HideOtherScenePresentation();
            AudioListener towerListener = FindInScene<AudioListener>();
            if (towerListener != null) towerListener.enabled = true;
            if (_pausePopup != null) _pausePopup.SetActive(false);
            if (_resultPopup != null) _resultPopup.SetActive(false);
            if (_popupPanel != null) _popupPanel.SetActive(false);

            if (_player == null)
            {
                Debug.LogError("TowerBattleScene에서 PlayerAutoCombat을 찾지 못했습니다.", this);
                _main.CancelTowerRun();
                return;
            }

            _player.gameObject.SetActive(true);
            _player.enabled = true;
            _player.SetCombatGroup(TowerCombatGroup);
            _main.ConfigureTowerPlayer(_player);
            ConfigureTowerWorldAndCamera();
            _remainingTime = _setup.timeLimit;
            _normalRemaining = _setup.normalEnemyCount;
            _bossRemaining = _setup.bossCount;
            _totalStageHealth = CalculateTotalStageHealth();
            if (_towerNameText != null)
                _towerNameText.text = $"{(_setup.type == TowerType.Gold ? "골드의 탑" : "보석의 탑")} Lv.{_setup.floor}";

            BindButton("ExitButton", OpenPause);
            BindButton("ResumeButton", Resume);
            BindButton("ContinueButton", Resume);
            BindButton("Btn_Resume", Resume);
            BindButton("QuitButton", ExitTower);
            BindButton("ExitConfirmButton", ExitTower);
            BindButton("Btn_Exit", ExitTower);
            BindButton("ConfirmButton", CloseResult);
            BindButton("Btn_Confirm", CloseResult);
            BindButton("Btn_Yes", ExitTower);
            BindButton("Btn_No", Resume);
            BindButton("Btn_Retry", RetryFloor);
            BindButton("Btn_Next", NextFloor);

            GameObject skillUi = FindObject("SkillUI");
            if (skillUi != null)
            {
                SkillBarController bar = skillUi.GetComponent<SkillBarController>();
                if (bar != null)
                    bar.BindTower(_main, _player);
                else
                    Debug.LogError("TowerBattleScene의 SkillUI에 SkillBarController가 연결되지 않았습니다.", skillUi);
            }

            SpawnNormalEnemies();
            SpawnNextBoss();
            UpdateUi();
        }

        private void Update()
        {
            if (_main == null || _finished || _paused || _completionPending) return;
            if (_player != null && !_player.IsAlive)
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
                Vector3 position = _player.transform.position +
                    new Vector3(side * UnityEngine.Random.Range(4.5f, 6.5f), UnityEngine.Random.Range(-2.4f, 2.4f), 0f);
                SpawnEnemy(data, false, position, _setup.normalEnemyHealth);
            }
        }

        private void SpawnNextBoss()
        {
            if (_finished || _bossRemaining <= 0) return;
            EnemyData data = _main.CurrentStage?.Boss ?? _main.CurrentStage?.region?.PickEnemy(_main.StageNumber);
            Transform anchor = FindObject("EnemyBase")?.transform;
            Vector3 position = anchor != null
                ? anchor.position
                : _player.transform.position + Vector3.right * 4f;
            _currentBoss = SpawnEnemy(data, true, position, _setup.bossHealth);
            UpdateUi();
        }

        private EnemyActor SpawnEnemy(EnemyData data, bool boss, Vector3 position, double desiredHealth)
        {
            if (data == null) return null;
            GameObject instance = data.prefab != null
                ? Instantiate(data.prefab)
                : GameObject.CreatePrimitive(boss ? PrimitiveType.Capsule : PrimitiveType.Sphere);
            SceneManager.MoveGameObjectToScene(instance, gameObject.scene);
            instance.transform.position = position;
            instance.SetActive(true);
            SetLayerRecursively(instance, TowerWorldLayer);
            EnemyActor enemy = instance.GetComponent<EnemyActor>() ?? instance.AddComponent<EnemyActor>();
            double healthMultiplier = desiredHealth / Math.Max(1d, data.baseHealth);
            double attackMultiplier = _setup.enemyAttack / Math.Max(1d, data.baseAttack);
            enemy.Initialize(data, _player, healthMultiplier, attackMultiplier, boss, 1f, 1f);
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
            if (_player != null) _player.enabled = false;
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
                    if (_player != null) _player.enabled = true;
                    yield break;
                }
            }

            if (_normalRemaining > 0 || _bossRemaining > 0)
            {
                _completionPending = false;
                if (_player != null) _player.enabled = true;
                yield break;
            }

            if (_bossHpFill != null) _bossHpFill.fillAmount = 0f;
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
            SetPopupVisible(_resultPopup, true);
            SetOptionalText("GradeText", result.grade.ToString());
            string reward = result.type == TowerType.Gold
                ? $"골드 +{MainHUDController.FormatNumber(result.goldReward)}"
                : $"보석 +{result.gemReward}";
            SetOptionalText("RewardText", reward);
            UpdateResultReward(result);
            UpdateResultActionTickets(cleared);
        }

        public void OpenPause()
        {
            if (_finished || _paused || _completionPending) return;
            _paused = true;
            SetTowerCombatEnabled(false);
            SetPopupVisible(_pausePopup, true);
        }

        public void Resume()
        {
            if (_finished) return;
            _paused = false;
            SetTowerCombatEnabled(true);
            SetPopupVisible(_pausePopup, false);
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
            SetPopupVisible(_resultPopup, false);
            SetPopupVisible(_pausePopup, false);
            _player.ResetPosition();
            _main.ConfigureTowerPlayer(_player);
            _player.enabled = true;
            if (_towerNameText != null)
                _towerNameText.text = $"{(_setup.type == TowerType.Gold ? "골드의 탑" : "보석의 탑")} Lv.{_setup.floor}";
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
            GameObject rewardItem = FindDescendant(_resultPopup, "RewardItem");
            TMP_Text amount = FindDescendantComponent<TMP_Text>(rewardItem, "AmountText");
            if (amount != null)
                amount.text = result.type == TowerType.Gold
                    ? MainHUDController.FormatNumber(result.goldReward)
                    : result.gemReward.ToString();

            Image icon = FindDescendantComponent<Image>(rewardItem, "IconImage");
            GameObject adventure = FindOtherSceneObject("AdventurePopup");
            GameObject sourceReward = FindDescendant(adventure, "RewardItem");
            Image sourceIcon = FindDescendantComponent<Image>(sourceReward, "IconImage");
            if (icon != null && sourceIcon != null) icon.sprite = sourceIcon.sprite;
        }

        private void UpdateResultActionTickets(bool cleared)
        {
            TowerProgressData progress = _main?.GetTowerProgress(_setup.type);
            int ticketCount = progress?.tickets ?? 0;
            bool hasTicket = ticketCount > 0;
            Button retryButton = FindComponent<Button>("Btn_Retry");
            Button nextButton = FindComponent<Button>("Btn_Next");
            if (retryButton != null) retryButton.interactable = hasTicket;
            if (nextButton != null)
                nextButton.interactable = hasTicket && cleared && progress != null &&
                    _setup.floor + 1 <= progress.highestUnlockedFloor;

            GameObject adventure = FindOtherSceneObject("AdventurePopup");
            Image sourceTicketIcon = FindDescendantComponent<Image>(adventure, "Icon_Ticket");
            UpdateButtonTicket(retryButton, sourceTicketIcon?.sprite, ticketCount);
            UpdateButtonTicket(nextButton, sourceTicketIcon?.sprite, ticketCount);
        }

        private static void UpdateButtonTicket(Button button, Sprite ticketSprite, int ticketCount)
        {
            if (button == null) return;
            GameObject ticketRoot = FindDescendant(button.gameObject, "Ticket");
            if (ticketRoot != null && ticketRoot.GetComponent<KeepGraphicVisualWhenButtonDisabled>() == null)
                ticketRoot.AddComponent<KeepGraphicVisualWhenButtonDisabled>();
            Image icon = FindDescendantComponent<Image>(button.gameObject, "TicketIcon");
            TMP_Text countText = FindDescendantComponent<TMP_Text>(button.gameObject, "CountText");
            if (icon != null && ticketSprite != null) icon.sprite = ticketSprite;
            if (countText != null) countText.text = Mathf.Max(0, ticketCount).ToString();
            ButtonChildDisabledVisual visual = button.GetComponent<ButtonChildDisabledVisual>();
            visual?.RefreshGraphics();
        }

        private void SetPopupVisible(GameObject popup, bool visible)
        {
            if (popup != null) popup.SetActive(visible);
            if (_popupPanel != null)
                _popupPanel.SetActive(visible ||
                    (_pausePopup != null && _pausePopup.activeSelf) ||
                    (_resultPopup != null && _resultPopup.activeSelf));
        }

        private void ReturnToAdventureAndUnload()
        {
            GameObject adventure = FindOtherSceneObject("AdventurePopup");
            if (adventure != null) adventure.SetActive(true);
            UnloadTowerScene();
        }

        private void SetTowerCombatEnabled(bool enabled)
        {
            if (_player != null) _player.enabled = enabled;
            foreach (EnemyActor enemy in _enemies)
                if (enemy != null) enemy.enabled = enabled;
        }

        private void UpdateUi()
        {
            if (_timeText != null)
            {
                _timeText.text = _remainingTime.ToString("0.0");
                _timeText.color = _remainingTime <= 5f
                    ? new Color32(0xE5, 0x40, 0x26, 0xFF)
                    : _remainingTime <= 12f
                        ? new Color32(0xF1, 0x70, 0x41, 0xFF)
                        : _remainingTime <= 20f
                            ? new Color32(0xFF, 0xBF, 0x67, 0xFF)
                            : _defaultTimeTextColor;
            }
            if (_bossHpFill != null)
            {
                double remainingHealth = 0d;
                foreach (EnemyActor enemy in _enemies)
                    if (enemy != null) remainingHealth += Math.Max(0f, enemy.Health);
                int unspawnedBosses = Math.Max(0, _bossRemaining - (_currentBoss != null ? 1 : 0));
                remainingHealth += unspawnedBosses * _setup.bossHealth;
                _bossHpFill.fillAmount = _totalStageHealth > 0d
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
            GameObject world = FindObject("World");
            if (world != null) SetLayerRecursively(world, TowerWorldLayer);
            SetLayerRecursively(_player.gameObject, TowerWorldLayer);

            Camera towerCamera = FindInScene<Camera>();
            if (towerCamera != null)
            {
                towerCamera.enabled = true;
                towerCamera.cullingMask = 1 << TowerWorldLayer;
                CameraFollow2D follow = towerCamera.GetComponent<CameraFollow2D>();
                if (follow == null) follow = towerCamera.gameObject.AddComponent<CameraFollow2D>();
                follow.Bind(_player.transform);
                follow.SnapToTarget();
            }

            GameObject background = FindObject("Background");
            if (background != null)
            {
                SetLayerRecursively(background, TowerWorldLayer);
                BackgroundTiler2D tiler = background.GetComponent<BackgroundTiler2D>();
                if (tiler == null) tiler = background.AddComponent<BackgroundTiler2D>();
                tiler.Bind(_player.transform);
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

        private void BindButton(string name, UnityAction action)
        {
            Button button = FindComponent<Button>(name);
            if (button != null) button.onClick.AddListener(action);
        }

        private void SetOptionalText(string name, string value)
        {
            TMP_Text text = FindComponent<TMP_Text>(name);
            if (text != null) text.text = value;
        }

        private T FindInScene<T>() where T : Component
        {
            foreach (GameObject root in gameObject.scene.GetRootGameObjects())
            {
                T result = root.GetComponentInChildren<T>(true);
                if (result != null) return result;
            }
            return null;
        }

        private GameObject FindObject(string name, string parentName = null)
        {
            foreach (GameObject root in gameObject.scene.GetRootGameObjects())
                foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
                    if (child.name == name && (parentName == null || child.parent?.name == parentName))
                        return child.gameObject;
            return null;
        }

        private T FindComponent<T>(string name, string parentName = null) where T : Component
        {
            GameObject found = FindObject(name, parentName);
            return found != null ? found.GetComponent<T>() : null;
        }

        private GameObject FindOtherSceneObject(string name)
        {
            foreach (GameObject root in GetOtherSceneRoots())
            {
                if (root.name == name) return root;
                foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
                    if (child.name == name) return child.gameObject;
            }
            return null;
        }

        private static GameObject FindDescendant(GameObject root, string name)
        {
            if (root == null) return null;
            foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
                if (child.name == name) return child.gameObject;
            return null;
        }

        private static T FindDescendantComponent<T>(GameObject root, string name) where T : Component
        {
            GameObject found = FindDescendant(root, name);
            return found != null ? found.GetComponent<T>() : null;
        }
    }
}

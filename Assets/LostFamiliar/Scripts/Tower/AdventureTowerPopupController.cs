using System.Collections;
using LostFamiliar.Core;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace LostFamiliar.Battle
{
    [DisallowMultipleComponent]
    public sealed class AdventureTowerPopupController : MonoBehaviour
    {
        [Header("탑 UI 이미지")]
        [SerializeField] private Sprite goldTicketIcon;
        [SerializeField] private Sprite gemTicketIcon;
        [SerializeField] private Sprite goldTowerPreview;
        [SerializeField] private Sprite gemTowerPreview;
        [SerializeField] private Sprite goldRewardIcon;
        [SerializeField] private Sprite gemRewardIcon;

        private MainBattleLoop _battle;

        public Sprite GoldTicketIcon => goldTicketIcon;
        public Sprite GemTicketIcon => gemTicketIcon;
        private TowerType _selectedType = TowerType.Gold;
        private int _selectedFloor = 1;
        private Button _goldTab;
        private Button _gemTab;
        private Button _leftButton;
        private Button _rightButton;
        private Button _sweepButton;
        private Button _challengeButton;
        private Button _closeButton;
        private GameObject _autoSweepLock;
        private Image _ticketIcon;
        private Image _previewImage;
        private Image _rewardIcon;
        private TMP_Text _ticketCountText;
        private TMP_Text _towerNameText;
        private TMP_Text _descriptionText;
        private TMP_Text _levelText;
        private TMP_Text _recordTimeText;
        private TMP_Text _gradeText;
        private TMP_Text _rewardAmountText;
        private TMP_Text _goldText;
        private TMP_Text _gemText;
        private GameObject _resultPopup;
        private Image _resultRewardIcon;
        private TMP_Text _resultRewardAmountText;
        private Button _resultCloseButton;
        private RectTransform _resultBackground;
        private Vector3 _resultBackgroundBaseScale = Vector3.one;
        private Coroutine _resultOpenRoutine;
        private bool _towerLoading;
        private Color _goldTowerNameColor = Color.white;
        private bool _towerNameColorCached;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            GameObject popup = SkillBarController.FindSceneObject("AdventurePopup");
            if (popup == null) return;
            AdventureTowerPopupController controller = popup.GetComponent<AdventureTowerPopupController>();
            if (controller == null) controller = popup.AddComponent<AdventureTowerPopupController>();
            controller.Bind(Object.FindFirstObjectByType<MainBattleLoop>());
        }

        private void Awake()
        {
            FindReferences();
            SetResultPopupVisible(false);
        }

        private void Start()
        {
            if (_battle == null) Bind(Object.FindFirstObjectByType<MainBattleLoop>());
        }

        private void OnEnable()
        {
            _towerLoading = false;
            if (_battle != null)
            {
                TowerProgressData progress = _battle.GetTowerProgress(_selectedType);
                _selectedFloor = progress != null ? progress.highestUnlockedFloor : 1;
                Refresh();
            }
        }

        private void OnDisable() => CloseResultPopup();

        public void Bind(MainBattleLoop battle)
        {
            if (_battle != null) _battle.StateChanged -= Refresh;
            _battle = battle;
            if (_battle != null) _battle.StateChanged += Refresh;
            FindReferences();
            SelectTower(_selectedType);
        }

        private void FindReferences()
        {
            Transform root = transform;
            _goldTab = Find<Button>(root, "GoldTowerTab");
            _gemTab = Find<Button>(root, "GemTowerTab");
            _leftButton = Find<Button>(root, "Btn_Left");
            _rightButton = Find<Button>(root, "Btn_Right");
            _sweepButton = Find<Button>(root, "Btn_AutoSweep");
            _autoSweepLock = FindTransform(_sweepButton != null ? _sweepButton.transform : null, "Lock")?.gameObject;
            _challengeButton = Find<Button>(root, "Btn_Challenge");
            _closeButton = Find<Button>(root, "Btn_Close");
            _ticketIcon = Find<Image>(root, "Icon_Ticket");
            _previewImage = Find<Image>(root, "TowerPreviewPanel");
            _ticketCountText = Find<TMP_Text>(FindTransform(root, "TicketInfo"), "CountText");
            _towerNameText = Find<TMP_Text>(FindTransform(root, "TowerDescription"), "TowerNameText");
            if (_towerNameText != null && !_towerNameColorCached)
            {
                _goldTowerNameColor = _towerNameText.color;
                _towerNameColorCached = true;
            }
            _descriptionText = Find<TMP_Text>(FindTransform(root, "TowerDescription"), "DescriptionText");
            _levelText = Find<TMP_Text>(FindTransform(root, "LevelSelector"), "LevelText");
            _recordTimeText = Find<TMP_Text>(FindTransform(root, "RecordPanel"), "RecordTimeText");
            _gradeText = Find<TMP_Text>(FindTransform(root, "RecordPanel"), "GradeText");
            Transform reward = FindTransform(root, "RewardItem");
            _rewardIcon = Find<Image>(reward, "IconImage");
            _rewardAmountText = Find<TMP_Text>(reward, "AmountText");
            Transform header = FindTransform(root, "Header");
            Transform currencyGroup = FindTransform(header, "CurrencyGroup");
            _goldText = Find<TMP_Text>(FindTransform(currencyGroup, "GoldPanel"), "AmountText");
            _gemText = Find<TMP_Text>(FindTransform(currencyGroup, "GemPanel"), "AmountText");

            Transform resultPopup = FindTransform(root, "ResultPopup");
            _resultPopup = resultPopup != null ? resultPopup.gameObject : null;
            Transform resultReward = FindTransform(resultPopup, "RewardItem");
            _resultRewardIcon = Find<Image>(resultReward, "IconImage");
            _resultRewardAmountText = Find<TMP_Text>(resultReward, "AmountText");
            Transform resultBackground = FindTransform(resultPopup, "BG");
            _resultBackground = resultBackground as RectTransform;
            if (_resultBackground != null)
                _resultBackgroundBaseScale = _resultBackground.localScale;
            if (resultPopup != null)
            {
                _resultCloseButton = resultPopup.GetComponent<Button>() ??
                                     resultPopup.gameObject.AddComponent<Button>();
                _resultCloseButton.transition = Selectable.Transition.None;
                _resultCloseButton.targetGraphic = null;
            }

            ReplaceClick(_goldTab, () => SelectTower(TowerType.Gold));
            ReplaceClick(_gemTab, () => SelectTower(TowerType.Gem));
            ReplaceClick(_leftButton, () => ChangeFloor(-1));
            ReplaceClick(_rightButton, () => ChangeFloor(1));
            ReplaceClick(_sweepButton, Sweep);
            ReplaceClick(_challengeButton, Challenge);
            ReplaceClick(_closeButton, Close);
            ReplaceClick(_resultCloseButton, CloseResultPopup);
        }

        private void SelectTower(TowerType type)
        {
            _selectedType = type;
            TowerProgressData progress = _battle?.GetTowerProgress(type);
            _selectedFloor = progress != null ? progress.highestUnlockedFloor : 1;
            Refresh();
        }

        private void ChangeFloor(int direction)
        {
            TowerProgressData progress = _battle?.GetTowerProgress(_selectedType);
            int highest = progress?.highestUnlockedFloor ?? 1;
            int nextFloor = _selectedFloor + direction;
            if (nextFloor < 1 || nextFloor > highest) return;
            _selectedFloor = nextFloor;
            Refresh();
        }

        private void Refresh()
        {
            if (_battle == null) return;
            TowerProgressData progress = _battle.GetTowerProgress(_selectedType);
            if (progress == null) return;
            _selectedFloor = Mathf.Clamp(_selectedFloor, 1, progress.highestUnlockedFloor);
            bool gold = _selectedType == TowerType.Gold;

            if (_ticketIcon != null) _ticketIcon.sprite = gold ? goldTicketIcon : gemTicketIcon;
            if (_previewImage != null) _previewImage.sprite = gold ? goldTowerPreview : gemTowerPreview;
            if (_ticketCountText != null) _ticketCountText.text = $"{progress.tickets}/{TowerBalance.DailyTickets}";
            if (_goldText != null) _goldText.text = MainHUDController.FormatNumber(_battle.Gold);
            if (_gemText != null) _gemText.text = _battle.Gems.ToString();
            if (_towerNameText != null)
            {
                _towerNameText.text = gold ? "골드의 탑" : "보석의 탑";
                _towerNameText.color = gold
                    ? _goldTowerNameColor
                    : new Color32(0xB1, 0xB2, 0xFF, 0xFF);
            }
            if (_descriptionText != null) _descriptionText.text = gold
                ? "황금의 마력이 깃든 탑입니다.\n골드를 대량으로 획득할 수 있습니다."
                : "신비로운 마력이 깃든 탑입니다.\n보석을 대량으로 획득할 수 있습니다.";
            if (_levelText != null) _levelText.text = $"Lv.{_selectedFloor}";

            TowerGrade grade = progress.GetBestGrade(_selectedFloor);
            float clearTime = progress.GetBestClearTime(_selectedFloor);
            bool cleared = grade != TowerGrade.F;
            if (_autoSweepLock != null)
                _autoSweepLock.SetActive(!cleared || grade < TowerGrade.A);
            if (_sweepButton != null)
            {
                GlobalButtonAudio sweepAudio =
                    _sweepButton.GetComponent<GlobalButtonAudio>() ??
                    _sweepButton.gameObject.AddComponent<GlobalButtonAudio>();
                sweepAudio.SetLogicalLocked(
                    progress.tickets <= 0 || !cleared || grade < TowerGrade.A);
            }
            if (_recordTimeText != null) _recordTimeText.text = clearTime >= 0f ? $"Time {clearTime:00.0}초" : "Time --.-";
            if (_gradeText != null) _gradeText.text = cleared ? grade.ToString() : "-";

            if (_rewardIcon != null) _rewardIcon.sprite = gold
                ? (goldRewardIcon != null ? goldRewardIcon : goldTicketIcon)
                : (gemRewardIcon != null ? gemRewardIcon : gemTicketIcon);
            if (_rewardAmountText != null) _rewardAmountText.text = gold
                ? MainHUDController.FormatNumber(TowerBalance.BaseGoldReward(_selectedFloor))
                : TowerBalance.BaseGemReward(_selectedFloor).ToString();

            // AdventurePopup buttons always keep their normal visual state. Availability is
            // checked inside each click handler instead of using Button.interactable=false.
            if (_leftButton != null) _leftButton.interactable = true;
            if (_rightButton != null) _rightButton.interactable = true;
            if (_sweepButton != null) _sweepButton.interactable = true;
            if (_challengeButton != null) _challengeButton.interactable = true;
            if (_goldTab != null) _goldTab.interactable = true;
            if (_gemTab != null) _gemTab.interactable = true;
        }

        private void Sweep()
        {
            TowerProgressData progress = _battle?.GetTowerProgress(_selectedType);
            if (progress == null || progress.tickets <= 0 ||
                progress.GetBestGrade(_selectedFloor) < TowerGrade.A) return;
            if (_battle.TrySweepTower(
                    _selectedType, _selectedFloor, out TowerRunResult result))
            {
                Refresh();
                ShowSweepResult(result);
            }
        }

        private void ShowSweepResult(TowerRunResult result)
        {
            bool gold = result.type == TowerType.Gold;
            if (_resultRewardIcon != null)
                _resultRewardIcon.sprite = gold
                    ? (goldRewardIcon != null ? goldRewardIcon : goldTicketIcon)
                    : (gemRewardIcon != null ? gemRewardIcon : gemTicketIcon);
            if (_resultRewardAmountText != null)
                _resultRewardAmountText.text = gold
                    ? MainHUDController.FormatNumber(result.goldReward)
                    : result.gemReward.ToString();

            SetResultPopupVisible(true);
            GameAudioManager.Instance.PlayBgm("BGM_Result_Victory", false);
            if (_resultOpenRoutine != null)
                StopCoroutine(_resultOpenRoutine);
            _resultOpenRoutine = StartCoroutine(AnimateResultPopupOpen());
        }

        private IEnumerator AnimateResultPopupOpen()
        {
            if (_resultBackground == null)
                yield break;

            const float growDuration = .18f;
            const float settleDuration = .1f;
            Vector3 startScale = _resultBackgroundBaseScale * .82f;
            Vector3 overshootScale = _resultBackgroundBaseScale * 1.055f;
            _resultBackground.localScale = startScale;

            for (float elapsed = 0f; elapsed < growDuration; elapsed += Time.unscaledDeltaTime)
            {
                float progress = Mathf.Clamp01(elapsed / growDuration);
                progress = 1f - Mathf.Pow(1f - progress, 3f);
                _resultBackground.localScale = Vector3.LerpUnclamped(
                    startScale, overshootScale, progress);
                yield return null;
            }

            for (float elapsed = 0f; elapsed < settleDuration; elapsed += Time.unscaledDeltaTime)
            {
                float progress = Mathf.SmoothStep(0f, 1f, elapsed / settleDuration);
                _resultBackground.localScale = Vector3.LerpUnclamped(
                    overshootScale, _resultBackgroundBaseScale, progress);
                yield return null;
            }

            _resultBackground.localScale = _resultBackgroundBaseScale;
            _resultOpenRoutine = null;
        }

        private void CloseResultPopup()
        {
            bool wasVisible = _resultPopup != null && _resultPopup.activeSelf;
            SetResultPopupVisible(false);
            if (wasVisible)
                GameAudioManager.Instance.PlayBgm("BGM_MainBattle");
        }

        private void SetResultPopupVisible(bool visible)
        {
            if (_resultPopup == null) return;
            if (!visible)
            {
                if (_resultOpenRoutine != null)
                {
                    StopCoroutine(_resultOpenRoutine);
                    _resultOpenRoutine = null;
                }
                if (_resultBackground != null)
                    _resultBackground.localScale = _resultBackgroundBaseScale;
            }
            if (visible)
                _resultPopup.transform.SetAsLastSibling();
            _resultPopup.SetActive(visible);
        }

        private void Challenge()
        {
            if (_towerLoading || _battle == null) return;
            TowerProgressData progress = _battle.GetTowerProgress(_selectedType);
            if (progress == null || progress.tickets <= 0 ||
                _selectedFloor > progress.highestUnlockedFloor) return;
            if (!_battle.TryBeginTowerRun(_selectedType, _selectedFloor, out _)) return;
            _towerLoading = true;
            SceneManager.LoadSceneAsync("TowerBattleScene", LoadSceneMode.Additive);
            gameObject.SetActive(false);
        }

        private void Close() => gameObject.SetActive(false);

        private static void ReplaceClick(Button button, UnityEngine.Events.UnityAction action)
        {
            if (button == null) return;
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(action);
        }

        private static Transform FindTransform(Transform root, string name)
        {
            if (root == null) return null;
            foreach (Transform item in root.GetComponentsInChildren<Transform>(true))
                if (item.name.Trim() == name) return item;
            return null;
        }

        private static T Find<T>(Transform root, string name) where T : Component
        {
            Transform found = FindTransform(root, name);
            return found != null ? found.GetComponent<T>() : null;
        }

        private void OnDestroy()
        {
            if (_battle != null) _battle.StateChanged -= Refresh;
        }
    }
}

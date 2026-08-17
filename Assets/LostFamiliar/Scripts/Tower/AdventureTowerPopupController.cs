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

        [Header("탭")]
        [SerializeField] private Button goldTab;
        [SerializeField] private Button gemTab;

        [Header("층 선택")]
        [SerializeField] private Button leftButton;
        [SerializeField] private Button rightButton;
        [SerializeField] private TMP_Text levelText;

        [Header("탑 정보")]
        [SerializeField] private Image ticketIcon;
        [SerializeField] private TMP_Text ticketCountText;
        [SerializeField] private Image previewImage;
        [SerializeField] private TMP_Text towerNameText;
        [SerializeField] private TMP_Text descriptionText;
        [SerializeField] private TMP_Text recordTimeText;
        [SerializeField] private TMP_Text gradeText;

        [Header("보상")]
        [SerializeField] private Image rewardIcon;
        [SerializeField] private TMP_Text rewardAmountText;

        [Header("재화")]
        [SerializeField] private TMP_Text goldText;
        [SerializeField] private TMP_Text gemText;

        [Header("액션")]
        [SerializeField] private Button sweepButton;
        [SerializeField] private GameObject autoSweepLock;
        [SerializeField] private GlobalButtonAudio sweepButtonAudio;
        [SerializeField] private Button challengeButton;
        [SerializeField] private Button closeButton;

        [Header("소탕 결과")]
        [SerializeField] private GameObject resultPopup;
        [SerializeField] private Image resultRewardIcon;
        [SerializeField] private TMP_Text resultRewardAmountText;
        [SerializeField] private Button resultCloseButton;
        [SerializeField] private RectTransform resultBackground;

        private MainBattleLoop _battle;

        public Sprite GoldTicketIcon => goldTicketIcon;
        public Sprite GemTicketIcon => gemTicketIcon;
        private TowerType _selectedType = TowerType.Gold;
        private int _selectedFloor = 1;
        private Vector3 _resultBackgroundBaseScale = Vector3.one;
        private Coroutine _resultOpenRoutine;
        private bool _towerLoading;
        private Color _goldTowerNameColor = Color.white;

        private void Awake()
        {
            CacheInitialVisualState();
            BindButtons();
            SetResultPopupVisible(false);
        }

        private void CacheInitialVisualState()
        {
            if (towerNameText != null)
                _goldTowerNameColor = towerNameText.color;

            if (resultBackground != null)
                _resultBackgroundBaseScale = resultBackground.localScale;
        }

        private void BindButtons()
        {
            ReplaceClick(goldTab, () => SelectTower(TowerType.Gold));
            ReplaceClick(gemTab, () => SelectTower(TowerType.Gem));
            ReplaceClick(leftButton, () => ChangeFloor(-1));
            ReplaceClick(rightButton, () => ChangeFloor(1));
            ReplaceClick(sweepButton, Sweep);
            ReplaceClick(challengeButton, Challenge);
            ReplaceClick(closeButton, Close);
            ReplaceClick(resultCloseButton, CloseResultPopup);
        }

        private void OnEnable()
        {
            _towerLoading = false;

            if (_battle == null)
                return;

            _battle.RefreshDailyTowerTickets();

            TowerProgressData progress = _battle.GetTowerProgress(_selectedType);
            _selectedFloor = progress != null ? progress.highestUnlockedFloor : 1;
            Refresh();
        }

        private void OnDisable() => CloseResultPopup();

        public void Bind(MainBattleLoop battle)
        {
            if (_battle != null) _battle.StateChanged -= Refresh;
            _battle = battle;
            if (_battle != null) _battle.StateChanged += Refresh;
            SelectTower(_selectedType);
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

            if (ticketIcon != null) ticketIcon.sprite = gold ? goldTicketIcon : gemTicketIcon;
            if (previewImage != null) previewImage.sprite = gold ? goldTowerPreview : gemTowerPreview;
            if (ticketCountText != null) ticketCountText.text = $"{progress.tickets}/{TowerBalance.DailyTickets}";
            if (goldText != null) goldText.text = MainHUDController.FormatNumber(_battle.Gold);
            if (gemText != null) gemText.text = _battle.Gems.ToString();
            if (towerNameText != null)
            {
                towerNameText.text = gold ? "골드의 탑" : "보석의 탑";
                towerNameText.color = gold
                    ? _goldTowerNameColor
                    : new Color32(0xB1, 0xB2, 0xFF, 0xFF);
            }
            if (descriptionText != null) descriptionText.text = gold
                ? "황금의 마력이 깃든 탑입니다.\n골드를 대량으로 획득할 수 있습니다."
                : "신비로운 마력이 깃든 탑입니다.\n보석을 대량으로 획득할 수 있습니다.";
            if (levelText != null) levelText.text = $"Lv.{_selectedFloor}";

            TowerGrade grade = progress.GetBestGrade(_selectedFloor);
            float clearTime = progress.GetBestClearTime(_selectedFloor);
            bool cleared = grade != TowerGrade.F;
            if (autoSweepLock != null)
                autoSweepLock.SetActive(!cleared || grade < TowerGrade.A);
            if (sweepButtonAudio != null)
            {
                sweepButtonAudio.SetLogicalLocked(
                    progress.tickets <= 0 || !cleared || grade < TowerGrade.A);
            }
            if (recordTimeText != null) recordTimeText.text = clearTime >= 0f ? $"Time {clearTime:00.0}초" : "Time --.-";
            if (gradeText != null) gradeText.text = cleared ? grade.ToString() : "-";

            if (rewardIcon != null) rewardIcon.sprite = gold
                ? (goldRewardIcon != null ? goldRewardIcon : goldTicketIcon)
                : (gemRewardIcon != null ? gemRewardIcon : gemTicketIcon);
            if (rewardAmountText != null) rewardAmountText.text = gold
                ? MainHUDController.FormatNumber(TowerBalance.BaseGoldReward(_selectedFloor))
                : TowerBalance.BaseGemReward(_selectedFloor).ToString();

            // AdventurePopup buttons always keep their normal visual state. Availability is
            // checked inside each click handler instead of using Button.interactable=false.
            if (leftButton != null) leftButton.interactable = true;
            if (rightButton != null) rightButton.interactable = true;
            if (sweepButton != null) sweepButton.interactable = true;
            if (challengeButton != null) challengeButton.interactable = true;
            if (goldTab != null) goldTab.interactable = true;
            if (gemTab != null) gemTab.interactable = true;
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
            if (resultRewardIcon != null)
                resultRewardIcon.sprite = gold
                    ? (goldRewardIcon != null ? goldRewardIcon : goldTicketIcon)
                    : (gemRewardIcon != null ? gemRewardIcon : gemTicketIcon);
            if (resultRewardAmountText != null)
                resultRewardAmountText.text = gold
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
            if (resultBackground == null)
                yield break;

            const float growDuration = .18f;
            const float settleDuration = .1f;
            Vector3 startScale = _resultBackgroundBaseScale * .82f;
            Vector3 overshootScale = _resultBackgroundBaseScale * 1.055f;
            resultBackground.localScale = startScale;

            for (float elapsed = 0f; elapsed < growDuration; elapsed += Time.unscaledDeltaTime)
            {
                float progress = Mathf.Clamp01(elapsed / growDuration);
                progress = 1f - Mathf.Pow(1f - progress, 3f);
                resultBackground.localScale = Vector3.LerpUnclamped(
                    startScale, overshootScale, progress);
                yield return null;
            }

            for (float elapsed = 0f; elapsed < settleDuration; elapsed += Time.unscaledDeltaTime)
            {
                float progress = Mathf.SmoothStep(0f, 1f, elapsed / settleDuration);
                resultBackground.localScale = Vector3.LerpUnclamped(
                    overshootScale, _resultBackgroundBaseScale, progress);
                yield return null;
            }

            resultBackground.localScale = _resultBackgroundBaseScale;
            _resultOpenRoutine = null;
        }

        private void CloseResultPopup()
        {
            bool wasVisible = resultPopup != null && resultPopup.activeSelf;
            SetResultPopupVisible(false);
            if (wasVisible)
                GameAudioManager.Instance.PlayBgm("BGM_MainBattle");
        }

        private void SetResultPopupVisible(bool visible)
        {
            if (resultPopup == null) return;
            if (!visible)
            {
                if (_resultOpenRoutine != null)
                {
                    StopCoroutine(_resultOpenRoutine);
                    _resultOpenRoutine = null;
                }
                if (resultBackground != null)
                    resultBackground.localScale = _resultBackgroundBaseScale;
            }
            if (visible)
                resultPopup.transform.SetAsLastSibling();
            resultPopup.SetActive(visible);
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

        private void OnDestroy()
        {
            if (_battle != null) _battle.StateChanged -= Refresh;
        }
    }
}

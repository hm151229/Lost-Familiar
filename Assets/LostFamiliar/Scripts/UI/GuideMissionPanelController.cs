using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace LostFamiliar.Battle
{
    [DisallowMultipleComponent]
    public sealed class GuideMissionPanelController : MonoBehaviour
    {
        [SerializeField] private Button panelButton;
        [SerializeField] private TMP_Text titleText;
        [SerializeField] private TMP_Text missionText;
        [SerializeField] private TMP_Text rewardAmountText;
        [SerializeField] private Image rewardIconImage;
        [SerializeField] private GameObject clearIconImage;

        [Header("Navigation")]
        [SerializeField] private Button summonButton;
        [SerializeField] private Button upgradeButton;

        [Header("Reward Icons")]
        [SerializeField] private Sprite goldTowerTicketIcon;
        [SerializeField] private Sprite gemTowerTicketIcon;

        private MainBattleLoop _battle;
        private bool _clickBound;
        private RectTransform _guideArrow;
        private CanvasGroup _noticeCanvasGroup;
        private TMP_Text _noticeText;
        private Image _completionGlow;
        private Coroutine _arrowRoutine;
        private Coroutine _noticeRoutine;
        private Coroutine _completionEffectRoutine;
        private Sprite _defaultGemRewardIcon;
        private Vector3 _normalPanelScale = Vector3.one;
        private Color _normalTitleColor = Color.white;
        private bool _visualDefaultsCached;

        private void Awake()
        {
            if (rewardIconImage != null)
                _defaultGemRewardIcon = rewardIconImage.sprite;

            CacheVisualDefaults();
        }

        public void Bind(MainBattleLoop battle)
        {
            if (_battle != null)
                _battle.StateChanged -= Refresh;

            _battle = battle;

            if (!_clickBound && panelButton != null)
            {
                panelButton.onClick.AddListener(ClaimReward);
                _clickBound = true;
            }

            if (summonButton != null)
            {
                summonButton.onClick.RemoveListener(HideNavigationArrow);
                summonButton.onClick.AddListener(HideNavigationArrow);
            }

            if (upgradeButton != null)
            {
                upgradeButton.onClick.RemoveListener(HideNavigationArrow);
                upgradeButton.onClick.AddListener(HideNavigationArrow);
            }

            if (_battle != null)
                _battle.StateChanged += Refresh;

            Refresh();
        }

        public void Refresh()
        {
            if (_battle == null)
                return;

            LostFamiliar.Core.GuideMissionDefinition mission = _battle.CurrentGuideMission;
            int missionNumber = mission.index >= int.MaxValue ? int.MaxValue : mission.index + 1;
            int progress = _battle.GuideMissionProgress;
            bool complete = _battle.CanClaimGuideMission;

            if (titleText != null)
                titleText.text = complete ? "보상받기" : $"미션 {missionNumber:N0}";
            if (missionText != null)
                missionText.text = $"{mission.Title}  {progress:N0}/{mission.target:N0}";
            if (rewardAmountText != null)
                rewardAmountText.text = mission.RewardText;
            RefreshRewardIcon(mission);
            if (clearIconImage != null)
                clearIconImage.SetActive(complete);
            SetCompletionEffect(complete);
        }

        private void ClaimReward()
        {
            if (_battle == null)
                return;

            if (_battle.CanClaimGuideMission)
            {
                HideNavigationArrow();
                _battle.TryClaimGuideMission();
                return;
            }

            ShowCurrentMissionGuide();
        }

        private void ShowCurrentMissionGuide()
        {
            LostFamiliar.Core.GuideMissionDefinition mission = _battle.CurrentGuideMission;
            switch (mission.type)
            {
                case LostFamiliar.Core.GuideMissionType.DefeatMonsters:
                    ShowNotice($"몬스터 {mission.target:N0}마리 처치해주세요");
                    break;
                case LostFamiliar.Core.GuideMissionType.Gacha:
                    ShowGachaArrow();
                    break;
                case LostFamiliar.Core.GuideMissionType.ClearStage:
                    ShowNotice($"스테이지 {mission.target:N0}를 통과해주세요");
                    break;
                case LostFamiliar.Core.GuideMissionType.ReachStatLevel:
                case LostFamiliar.Core.GuideMissionType.ReachTotalUpgradeLevel:
                    ShowNavigationArrow(upgradeButton);
                    break;
                case LostFamiliar.Core.GuideMissionType.ClearGoldTower:
                    ShowNotice("모험에서 골드의 탑을 1회 클리어해주세요");
                    break;
                case LostFamiliar.Core.GuideMissionType.ClearGemTower:
                    ShowNotice("모험에서 보석의 탑을 1회 클리어해주세요");
                    break;
            }
        }

        private void RefreshRewardIcon(LostFamiliar.Core.GuideMissionDefinition mission)
        {
            if (rewardIconImage == null)
                return;

            Sprite icon = _defaultGemRewardIcon;
            if (mission.goldTowerTicketReward > 0)
                icon = goldTowerTicketIcon;
            else if (mission.gemTowerTicketReward > 0)
                icon = gemTowerTicketIcon;

            if (icon != null)
                rewardIconImage.sprite = icon;
        }

        private void ShowGachaArrow()
        {
            ShowNavigationArrow(summonButton);
        }

        private void ShowNavigationArrow(Button targetButton)
        {
            if (targetButton == null)
                return;

            EnsureGuideArrow(targetButton);
            if (_guideArrow == null)
                return;

            if (_guideArrow.parent != targetButton.transform)
            {
                _guideArrow.SetParent(targetButton.transform, false);
                ConfigureArrowTransform();
            }

            _guideArrow.gameObject.SetActive(true);
            _guideArrow.SetAsLastSibling();
            if (_arrowRoutine != null)
                StopCoroutine(_arrowRoutine);
            _arrowRoutine = StartCoroutine(FloatArrow());
        }

        private void EnsureGuideArrow(Button targetButton)
        {
            if (_guideArrow != null || targetButton == null)
                return;

            GameObject arrowObject = new GameObject(
                "Guide_GachaArrow",
                typeof(RectTransform),
                typeof(CanvasRenderer));
            arrowObject.transform.SetParent(targetButton.transform, false);
            _guideArrow = arrowObject.GetComponent<RectTransform>();
            ConfigureArrowTransform();

            Sprite arrowSprite = _battle?.Database?.guideFingerSprite;
            if (arrowSprite != null)
            {
                Image arrowImage = arrowObject.AddComponent<Image>();
                arrowImage.sprite = arrowSprite;
                arrowImage.preserveAspect = true;
                arrowImage.raycastTarget = false;
                _guideArrow.localRotation = Quaternion.identity;
            }
            else
            {
                TextMeshProUGUI arrowText = arrowObject.AddComponent<TextMeshProUGUI>();
                arrowText.text = "▼";
                arrowText.fontSize = 70f;
                arrowText.alignment = TextAlignmentOptions.Center;
                arrowText.color = Color.white;
                arrowText.raycastTarget = false;
                if (missionText != null)
                    arrowText.font = missionText.font;
            }
        }

        private void ConfigureArrowTransform()
        {
            if (_guideArrow == null)
                return;

            _guideArrow.anchorMin = new Vector2(.5f, 1f);
            _guideArrow.anchorMax = new Vector2(.5f, 1f);
            _guideArrow.pivot = new Vector2(.5f, 0f);
            _guideArrow.sizeDelta = new Vector2(90f, 90f);
            _guideArrow.anchoredPosition = new Vector2(0f, 28f);
        }

        private IEnumerator FloatArrow()
        {
            const float baseY = 28f;
            while (_guideArrow != null && _guideArrow.gameObject.activeSelf)
            {
                Vector2 position = _guideArrow.anchoredPosition;
                position.y = baseY + Mathf.Sin(Time.unscaledTime * 4f) * 14f;
                _guideArrow.anchoredPosition = position;
                yield return null;
            }

            _arrowRoutine = null;
        }

        private void HideNavigationArrow()
        {
            if (_arrowRoutine != null)
            {
                StopCoroutine(_arrowRoutine);
                _arrowRoutine = null;
            }
            if (_guideArrow != null)
                _guideArrow.gameObject.SetActive(false);
        }

        private void ShowNotice(string message)
        {
            EnsureNoticePopup();
            if (_noticeCanvasGroup == null || _noticeText == null)
                return;

            _noticeText.text = message;
            _noticeCanvasGroup.alpha = 1f;
            _noticeCanvasGroup.gameObject.SetActive(true);
            _noticeCanvasGroup.transform.SetAsLastSibling();
            if (_noticeRoutine != null)
                StopCoroutine(_noticeRoutine);
            _noticeRoutine = StartCoroutine(HideNoticeAfterDelay());
        }

        private void CacheVisualDefaults()
        {
            if (_visualDefaultsCached)
                return;

            _visualDefaultsCached = true;
            _normalPanelScale = transform.localScale;
            if (titleText != null)
                _normalTitleColor = titleText.color;
        }

        private void SetCompletionEffect(bool active)
        {
            if (active)
            {
                EnsureCompletionGlow();
                if (_completionGlow != null)
                    _completionGlow.gameObject.SetActive(true);
                if (_completionEffectRoutine == null && isActiveAndEnabled)
                    _completionEffectRoutine = StartCoroutine(PlayCompletionEffect());
                return;
            }

            if (_completionEffectRoutine != null)
            {
                StopCoroutine(_completionEffectRoutine);
                _completionEffectRoutine = null;
            }
            if (_completionGlow != null)
                _completionGlow.gameObject.SetActive(false);
            transform.localScale = _normalPanelScale;
            if (titleText != null)
                titleText.color = _normalTitleColor;
        }

        private void EnsureCompletionGlow()
        {
            if (_completionGlow != null || panelButton?.image == null)
                return;

            GameObject glowObject = new GameObject(
                "GuideMissionCompleteGlow",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image));
            glowObject.transform.SetParent(transform, false);
            glowObject.transform.SetAsFirstSibling();

            RectTransform glowRect = glowObject.GetComponent<RectTransform>();
            glowRect.anchorMin = Vector2.zero;
            glowRect.anchorMax = Vector2.one;
            glowRect.offsetMin = new Vector2(-8f, -8f);
            glowRect.offsetMax = new Vector2(8f, 8f);

            _completionGlow = glowObject.GetComponent<Image>();
            _completionGlow.sprite = panelButton.image.sprite;
            _completionGlow.type = panelButton.image.type;
            _completionGlow.preserveAspect = panelButton.image.preserveAspect;
            _completionGlow.raycastTarget = false;
        }

        private IEnumerator PlayCompletionEffect()
        {
            Color glowColor = new Color(1f, .78f, .18f, 0f);
            Color brightTitleColor = new Color(1f, .88f, .3f, 1f);

            while (_battle != null && _battle.CanClaimGuideMission)
            {
                float wave = (Mathf.Sin(Time.unscaledTime * 5f) + 1f) * .5f;
                transform.localScale = _normalPanelScale * Mathf.Lerp(1f, 1.035f, wave);

                if (_completionGlow != null)
                {
                    glowColor.a = Mathf.Lerp(.08f, .42f, wave);
                    _completionGlow.color = glowColor;
                }
                if (titleText != null)
                    titleText.color = Color.Lerp(_normalTitleColor, brightTitleColor, wave);

                yield return null;
            }

            _completionEffectRoutine = null;
            if (_completionGlow != null)
                _completionGlow.gameObject.SetActive(false);
            transform.localScale = _normalPanelScale;
            if (titleText != null)
                titleText.color = _normalTitleColor;
        }

        private void EnsureNoticePopup()
        {
            if (_noticeCanvasGroup != null)
                return;

            Canvas canvas = GetComponentInParent<Canvas>();
            if (canvas == null)
                return;

            GameObject popup = new GameObject(
                "GuideNoticePopup",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image),
                typeof(CanvasGroup));
            popup.transform.SetParent(canvas.transform, false);
            RectTransform popupRect = popup.GetComponent<RectTransform>();
            popupRect.anchorMin = new Vector2(.5f, .5f);
            popupRect.anchorMax = new Vector2(.5f, .5f);
            popupRect.pivot = new Vector2(.5f, .5f);
            popupRect.sizeDelta = new Vector2(760f, 150f);
            popupRect.anchoredPosition = new Vector2(0f, 80f);

            Image background = popup.GetComponent<Image>();
            background.color = new Color(0f, 0f, 0f, .82f);
            background.raycastTarget = false;
            _noticeCanvasGroup = popup.GetComponent<CanvasGroup>();
            _noticeCanvasGroup.blocksRaycasts = false;
            _noticeCanvasGroup.interactable = false;

            GameObject textObject = new GameObject(
                "MessageText",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(TextMeshProUGUI));
            textObject.transform.SetParent(popup.transform, false);
            RectTransform textRect = textObject.GetComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = new Vector2(35f, 15f);
            textRect.offsetMax = new Vector2(-35f, -15f);
            _noticeText = textObject.GetComponent<TextMeshProUGUI>();
            _noticeText.fontSize = 38f;
            _noticeText.alignment = TextAlignmentOptions.Center;
            _noticeText.color = Color.white;
            _noticeText.raycastTarget = false;
            if (missionText != null)
                _noticeText.font = missionText.font;
        }

        private IEnumerator HideNoticeAfterDelay()
        {
            yield return new WaitForSecondsRealtime(1.7f);
            float elapsed = 0f;
            const float fadeDuration = .3f;
            while (elapsed < fadeDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                if (_noticeCanvasGroup != null)
                    _noticeCanvasGroup.alpha = 1f - Mathf.Clamp01(elapsed / fadeDuration);
                yield return null;
            }

            if (_noticeCanvasGroup != null)
                _noticeCanvasGroup.gameObject.SetActive(false);
            _noticeRoutine = null;
        }

        private void OnDestroy()
        {
            SetCompletionEffect(false);
            if (_battle != null)
                _battle.StateChanged -= Refresh;
            if (_clickBound && panelButton != null)
                panelButton.onClick.RemoveListener(ClaimReward);
            if (summonButton != null)
                summonButton.onClick.RemoveListener(HideNavigationArrow);
            if (upgradeButton != null)
                upgradeButton.onClick.RemoveListener(HideNavigationArrow);
        }
    }
}

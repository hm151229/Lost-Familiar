using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace LostFamiliar.Battle
{
    [DisallowMultipleComponent]
    public sealed class MainHUDController : MonoBehaviour
    {
        private const string SafeAreaPath = "Canvas/SafeArea";

        [Header("플레이어 레벨 / 경험치")]
        [SerializeField] private TMP_Text playerLevelText;
        [SerializeField] private Image playerExperienceFill;
        [SerializeField] private TMP_Text playerExperiencePercentText;
        [SerializeField] private TMP_Text playerExperienceValueText;

        [Header("스테이지 / 진행 경험치")]
        [SerializeField] private TMP_Text stageText;
        [SerializeField] private TMP_Text regionNameText;
        [SerializeField] private Image stageExperienceFill;
        [SerializeField] private TMP_Text stageExperiencePercentText;
        [SerializeField] private TMP_Text stageExperienceValueText;
        [SerializeField] private TMP_Text bossTimerText;
        [SerializeField] private Color bossHealthFillColor = new Color(.95f, .16f, .2f, 1f);

        [Header("플레이어 재화")]
        [SerializeField] private TMP_Text goldText;
        [SerializeField] private TMP_Text gemText;

        [Header("선택 연결")]
        [SerializeField] private TMP_Text playerHealthText;
        [SerializeField] private TMP_Text playerAttackText;

        private MainBattleLoop _battle;
        private Color _stageProgressFillColor = Color.white;

        public void Bind(MainBattleLoop battle)
        {
            if (_battle != null)
                _battle.StateChanged -= Refresh;

            _battle = battle;
            AutoFindReferences();
            ConfigureFillImage(playerExperienceFill);
            ConfigureFillImage(stageExperienceFill);
            if (stageExperienceFill != null)
                _stageProgressFillColor = stageExperienceFill.color;
            _battle.StateChanged += Refresh;
            Refresh();
        }

        [ContextMenu("Auto Find UI References")]
        public void AutoFindReferences()
        {
            if (playerLevelText == null)
                playerLevelText = Find<TMP_Text>(SafeAreaPath + "/TopUI/ProfilePanel/LevelText");
            if (playerExperienceFill == null)
                playerExperienceFill = Find<Image>(SafeAreaPath + "/TopUI/ProfilePanel/ExpBar/Fill");
            if (playerExperiencePercentText == null)
                playerExperiencePercentText = Find<TMP_Text>(SafeAreaPath + "/TopUI/ProfilePanel/ExpBar/PercentText");

            if (stageText == null)
                stageText = Find<TMP_Text>(SafeAreaPath + "/StageUI/StageText");
            if (stageExperienceFill == null)
                stageExperienceFill = Find<Image>(SafeAreaPath + "/StageUI/ProgressBar/Fill");
            if (stageExperiencePercentText == null)
                stageExperiencePercentText = Find<TMP_Text>(SafeAreaPath + "/StageUI/ProgressBar/PercentText");
            if (bossTimerText == null)
                bossTimerText = Find<TMP_Text>(SafeAreaPath + "/StageUI/BossTimerText");

            if (goldText == null)
                goldText = Find<TMP_Text>(SafeAreaPath + "/TopUI/CurrencyGroup/GoldPanel/AmountText");
            if (gemText == null)
                gemText = Find<TMP_Text>(SafeAreaPath + "/TopUI/CurrencyGroup/GemPanel/AmountText");
        }

        public void Refresh()
        {
            if (_battle == null || _battle.CurrentStage == null)
                return;

            SetText(playerLevelText, $"Lv.{_battle.PlayerLevel}");
            SetFill(playerExperienceFill, _battle.PlayerExperience01);
            SetText(playerExperiencePercentText, $"{_battle.PlayerExperience01 * 100f:0}%");
            SetText(playerExperienceValueText,
                $"{FormatNumber(_battle.PlayerExperience)} / {FormatNumber(_battle.PlayerExperienceToLevel)}");

            bool isBossBattle = _battle.Phase == BattlePhase.EnteringBoss || _battle.Phase == BattlePhase.Boss;
            SetText(stageText, isBossBattle
                ? $"STAGE {_battle.StageNumber} BOSS"
                : $"STAGE {_battle.StageNumber}");
            SetText(regionNameText, _battle.CurrentStage.DisplayName);
            RefreshStageGauge();
            RefreshBossTimer();

            SetText(goldText, FormatNumber(_battle.Gold));
            SetText(gemText, FormatGem(_battle.Gems));

            if (_battle.Player != null)
            {
                SetText(playerHealthText,
                    $"HP {FormatNumber(_battle.Player.Health)} / {FormatNumber(_battle.Player.MaxHealth)}");
                SetText(playerAttackText, $"ATK {FormatNumber(_battle.Player.AttackDamage)}");
            }
        }

        private void Update()
        {
            if (_battle != null && _battle.Phase == BattlePhase.Boss)
                RefreshStageGauge();
            RefreshBossTimer();
        }

        private void RefreshStageGauge()
        {
            if (_battle == null || _battle.CurrentStage == null)
                return;

            bool showBossHealth = _battle.Phase == BattlePhase.EnteringBoss || _battle.Phase == BattlePhase.Boss;
            if (stageExperienceFill != null)
                stageExperienceFill.color = showBossHealth ? bossHealthFillColor : _stageProgressFillColor;

            if (!showBossHealth)
            {
                SetFill(stageExperienceFill, _battle.StageExperience01);
                SetText(stageExperiencePercentText, $"{_battle.StageExperience01 * 100f:0}%");
                SetText(stageExperienceValueText,
                    $"{_battle.StageExperience} / {_battle.CurrentStage.experienceToBoss}");
                return;
            }

            EnemyActor boss = _battle.CurrentBoss;
            float health01 = boss == null || boss.MaxHealth <= 0f
                ? 1f
                : Mathf.Clamp01(boss.Health / boss.MaxHealth);
            SetFill(stageExperienceFill, health01);
            SetText(stageExperiencePercentText, $"{health01 * 100f:0}%");
            SetText(stageExperienceValueText, boss == null
                ? "BOSS"
                : $"{FormatNumber(boss.Health)} / {FormatNumber(boss.MaxHealth)}");
        }

        private void RefreshBossTimer()
        {
            if (_battle == null || bossTimerText == null)
                return;

            bool visible = _battle.Phase == BattlePhase.EnteringBoss || _battle.Phase == BattlePhase.Boss;
            bossTimerText.gameObject.SetActive(visible);
            if (!visible)
                return;

            float remaining = _battle.Phase == BattlePhase.EnteringBoss
                ? _battle.BossTimeLimit
                : _battle.BossTimeRemaining;
            int seconds = Mathf.Max(0, Mathf.CeilToInt(remaining));
            SetText(bossTimerText, $"TIME {seconds / 60:00}:{seconds % 60:00}");
        }

        private static T Find<T>(string path) where T : Component
        {
            GameObject target = GameObject.Find(path);
            return target != null ? target.GetComponent<T>() : null;
        }

        private static void ConfigureFillImage(Image image)
        {
            if (image == null)
                return;

            image.type = Image.Type.Filled;
            image.fillMethod = Image.FillMethod.Horizontal;
            image.fillOrigin = (int)Image.OriginHorizontal.Left;
            image.fillClockwise = true;
        }

        private static void SetFill(Image image, float value)
        {
            if (image != null)
                image.fillAmount = Mathf.Clamp01(value);
        }

        private static void SetText(TMP_Text text, string value)
        {
            if (text != null)
                text.text = value;
        }

        public static string FormatNumber(double value)
        {
            double absolute = System.Math.Abs(value);
            if (absolute >= 1_000_000_000_000_000d) return $"{value / 1_000_000_000_000_000d:0.##}Qa";
            if (absolute >= 1_000_000_000_000d) return $"{value / 1_000_000_000_000d:0.##}T";
            if (absolute >= 1_000_000_000d) return $"{value / 1_000_000_000d:0.##}B";
            if (absolute >= 1_000_000d) return $"{value / 1_000_000d:0.##}M";
            if (absolute >= 1_000d) return $"{value / 1_000d:0.##}K";
            return $"{value:0}";
        }

        public static string FormatGem(int value) => Mathf.Max(0, value).ToString();

        private void OnDestroy()
        {
            if (_battle != null)
                _battle.StateChanged -= Refresh;
        }
    }

    [DisallowMultipleComponent]
    public sealed class GuideMissionPanelController : MonoBehaviour
    {
        [SerializeField] private Button panelButton;
        [SerializeField] private TMP_Text titleText;
        [SerializeField] private TMP_Text missionText;
        [SerializeField] private TMP_Text rewardAmountText;
        [SerializeField] private GameObject clearIconImage;

        private MainBattleLoop _battle;
        private bool _clickBound;
        private Button _summonButton;
        private Button _upgradeButton;
        private RectTransform _guideArrow;
        private CanvasGroup _noticeCanvasGroup;
        private TMP_Text _noticeText;
        private Image _completionGlow;
        private Coroutine _arrowRoutine;
        private Coroutine _noticeRoutine;
        private Coroutine _completionEffectRoutine;
        private Vector3 _normalPanelScale = Vector3.one;
        private Color _normalTitleColor = Color.white;
        private bool _visualDefaultsCached;

        public void Bind(MainBattleLoop battle)
        {
            if (_battle != null)
                _battle.StateChanged -= Refresh;

            _battle = battle;
            AutoFindReferences();
            CacheVisualDefaults();

            if (!_clickBound && panelButton != null)
            {
                panelButton.onClick.AddListener(ClaimReward);
                _clickBound = true;
            }

            _summonButton ??= FindSceneComponent<Button>("SummonButton");
            if (_summonButton != null)
            {
                _summonButton.onClick.RemoveListener(HideNavigationArrow);
                _summonButton.onClick.AddListener(HideNavigationArrow);
            }
            _upgradeButton ??= FindSceneComponent<Button>("UpgradeButton");
            if (_upgradeButton != null)
            {
                _upgradeButton.onClick.RemoveListener(HideNavigationArrow);
                _upgradeButton.onClick.AddListener(HideNavigationArrow);
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
                rewardAmountText.text = mission.gemReward.ToString();
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
                    ShowNavigationArrow(_upgradeButton);
                    break;
            }
        }

        private void ShowGachaArrow()
        {
            ShowNavigationArrow(_summonButton);
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

            Sprite arrowSprite = _battle?.Database?.guideFingerSprite ?? FindLoadedSprite("UI_Finger");
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

        private void AutoFindReferences()
        {
            panelButton ??= GetComponent<Button>();
            titleText ??= FindChild<TMP_Text>("TitleText");
            missionText ??= FindChild<TMP_Text>("MissionText");

            Transform reward = FindChildTransform("Reward");
            rewardAmountText ??= FindChild<TMP_Text>(reward, "AmountText");
            clearIconImage ??= FindChildTransform("ClearIconImage")?.gameObject;
        }

        private T FindChild<T>(string objectName) where T : Component =>
            FindChild<T>(transform, objectName);

        private static T FindChild<T>(Transform root, string objectName) where T : Component
        {
            if (root == null)
                return null;

            foreach (T component in root.GetComponentsInChildren<T>(true))
            {
                if (component.name == objectName)
                    return component;
            }

            return null;
        }

        private Transform FindChildTransform(string objectName)
        {
            foreach (Transform child in GetComponentsInChildren<Transform>(true))
            {
                if (child.name == objectName)
                    return child;
            }

            return null;
        }

        private static T FindSceneComponent<T>(string objectName) where T : Component
        {
            foreach (T candidate in Resources.FindObjectsOfTypeAll<T>())
            {
                GameObject sceneObject = candidate.gameObject;
                if (candidate.name == objectName && sceneObject.scene.IsValid() && sceneObject.scene.isLoaded)
                    return candidate;
            }

            return null;
        }

        private static Sprite FindLoadedSprite(string spriteName)
        {
            foreach (Sprite sprite in Resources.FindObjectsOfTypeAll<Sprite>())
            {
                if (sprite.name == spriteName)
                    return sprite;
            }

            return null;
        }

        private void OnDestroy()
        {
            SetCompletionEffect(false);
            if (_battle != null)
                _battle.StateChanged -= Refresh;
            if (_clickBound && panelButton != null)
                panelButton.onClick.RemoveListener(ClaimReward);
            if (_summonButton != null)
                _summonButton.onClick.RemoveListener(HideNavigationArrow);
            if (_upgradeButton != null)
                _upgradeButton.onClick.RemoveListener(HideNavigationArrow);
        }
    }
}

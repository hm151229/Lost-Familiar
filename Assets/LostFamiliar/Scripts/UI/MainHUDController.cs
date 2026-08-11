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
        [SerializeField] private GameObject bossTimerIcon;
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
                bossTimerText = Find<TMP_Text>(SafeAreaPath + "/StageUI/BossTimer/BossTimerText");
            if (bossTimerIcon == null)
                bossTimerIcon = FindChildObject(bossTimerText != null ? bossTimerText.transform.parent : null, "TimerIcon")
                    ?? FindObject(SafeAreaPath + "/StageUI/BossTimer/TimerIcon");

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
            if (_battle == null)
                return;

            bool visible = _battle.Phase == BattlePhase.EnteringBoss || _battle.Phase == BattlePhase.Boss;
            if (bossTimerText != null) bossTimerText.gameObject.SetActive(visible);
            if (bossTimerIcon != null) bossTimerIcon.SetActive(visible);
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

        private static GameObject FindObject(string path) => GameObject.Find(path);

        private static GameObject FindChildObject(Transform root, string objectName)
        {
            if (root == null) return null;
            foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
                if (child.name == objectName) return child.gameObject;
            return null;
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

}

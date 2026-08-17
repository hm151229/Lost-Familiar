using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace LostFamiliar.Battle
{
    [DisallowMultipleComponent]
    public sealed class MainHUDController : MonoBehaviour
    {
        [Header("플레이어 레벨 / 경험치")]
        [SerializeField] private TMP_Text playerLevelText;
        [SerializeField] private Image playerExperienceFill;
        [SerializeField] private TMP_Text playerExperiencePercentText;

        [Header("스테이지 / 진행 경험치")]
        [SerializeField] private TMP_Text stageText;
        [SerializeField] private Image stageExperienceFill;
        [SerializeField] private TMP_Text stageExperiencePercentText;
        [SerializeField] private TMP_Text bossTimerText;
        [SerializeField] private GameObject bossTimerIcon;
        [SerializeField] private Color bossHealthFillColor = new Color(.95f, .16f, .2f, 1f);

        [Header("플레이어 재화")]
        [SerializeField] private TMP_Text goldText;
        [SerializeField] private TMP_Text gemText;

        private MainBattleLoop _battle;
        private Color _stageProgressFillColor = Color.white;

        public void Bind(MainBattleLoop battle)
        {
            if (_battle != null)
                _battle.StateChanged -= Refresh;

            _battle = battle;
            ConfigureFillImage(playerExperienceFill);
            ConfigureFillImage(stageExperienceFill);
            if (stageExperienceFill != null)
                _stageProgressFillColor = stageExperienceFill.color;

            if (_battle != null)
            {
                _battle.StateChanged += Refresh;
                Refresh();
            }
        }

        private void OnValidate()
        {
            if (playerLevelText == null)
                Debug.LogWarning(
                    $"{nameof(MainHUDController)}: Player Level Text가 연결되지 않았습니다.",
                    this);

            if (playerExperienceFill == null)
                Debug.LogWarning(
                    $"{nameof(MainHUDController)}: Player Experience Fill이 연결되지 않았습니다.",
                    this);

            if (stageText == null)
                Debug.LogWarning(
                    $"{nameof(MainHUDController)}: Stage Text가 연결되지 않았습니다.",
                    this);

            if (stageExperienceFill == null)
                Debug.LogWarning(
                    $"{nameof(MainHUDController)}: Stage Experience Fill이 연결되지 않았습니다.",
                    this);

            if (goldText == null)
                Debug.LogWarning(
                    $"{nameof(MainHUDController)}: Gold Text가 연결되지 않았습니다.",
                    this);

            if (gemText == null)
                Debug.LogWarning(
                    $"{nameof(MainHUDController)}: Gem Text가 연결되지 않았습니다.",
                    this);
        }

        public void Refresh()
        {
            if (_battle == null || _battle.CurrentStage == null)
                return;

            SetText(playerLevelText, $"Lv.{_battle.PlayerLevel}");
            SetFill(playerExperienceFill, _battle.PlayerExperience01);
            SetText(playerExperiencePercentText, $"{_battle.PlayerExperience01 * 100f:0}%");

            bool isBossBattle = _battle.Phase == BattlePhase.EnteringBoss || _battle.Phase == BattlePhase.Boss;
            SetText(stageText, isBossBattle
                ? $"STAGE {_battle.StageNumber} BOSS"
                : $"STAGE {_battle.StageNumber}");
            RefreshStageGauge();
            RefreshBossTimer();

            SetText(goldText, FormatNumber(_battle.Gold));
            SetText(gemText, FormatGem(_battle.Gems));
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
                return;
            }

            EnemyActor boss = _battle.CurrentBoss;
            float health01 = boss == null || boss.MaxHealth <= 0f
                ? 1f
                : Mathf.Clamp01(boss.Health / boss.MaxHealth);
            SetFill(stageExperienceFill, health01);
            SetText(stageExperiencePercentText, $"{health01 * 100f:0}%");
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

using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace LostFamiliar.Battle
{
    [DisallowMultipleComponent]
    public sealed class UpgradePopupController : MonoBehaviour
    {
        [Header("팝업 버튼")]
        [SerializeField] private Button openButton;
        [SerializeField] private Button closeButton;

        [Header("강화 배수")]
        [SerializeField] private Button x1Button;
        [SerializeField] private Button x10Button;
        [SerializeField] private Button x30Button;

        [Header("총 강화 레벨")]
        [SerializeField] private TMP_Text totalLevelText;
        [SerializeField] private TMP_Text progressText;
        [SerializeField] private Image progressFill;
        [SerializeField] private Button totalLevelUpButton;

        [Header("보유 재화")]
        [SerializeField] private TMP_Text goldText;
        [SerializeField] private TMP_Text gemText;

        [Header("강화 항목")]
        [SerializeField] private UpgradeStatRowUI[] rows;

        private MainBattleLoop _battle;
        private int _selectedAmount = 1;

        private void Awake()
        {
            if (openButton != null)
                openButton.onClick.AddListener(Open);
            if (closeButton != null)
                closeButton.onClick.AddListener(Close);
            if (x1Button != null)
                x1Button.onClick.AddListener(() => SelectAmount(1));
            if (x10Button != null)
                x10Button.onClick.AddListener(() => SelectAmount(10));
            if (x30Button != null)
                x30Button.onClick.AddListener(() => SelectAmount(30));

            if (totalLevelUpButton != null)
                totalLevelUpButton.onClick.AddListener(UpgradeTotalLevel);

            SelectAmount(1);
        }

        private void OnEnable() => Refresh();

        public void Open()
        {
            gameObject.SetActive(true);
            Refresh();
        }

        public void Close() => gameObject.SetActive(false);

        private void UpgradeTotalLevel()
        {
            if (_battle != null)
                _battle.TryIncreaseTotalUpgradeLevel();
        }

        private void SelectAmount(int amount)
        {
            _selectedAmount = Mathf.Max(1, amount);
            if (rows != null)
            {
                foreach (UpgradeStatRowUI row in rows)
                    row?.SetUpgradeAmount(_selectedAmount);
            }

            SetSelectedState(x1Button, _selectedAmount == 1);
            SetSelectedState(x10Button, _selectedAmount == 10);
            SetSelectedState(x30Button, _selectedAmount == 30);
        }

        public void Bind(MainBattleLoop battle)
        {
            if (_battle == battle)
                return;

            if (_battle != null)
                _battle.StateChanged -= Refresh;

            _battle = battle;
            if (_battle != null)
                _battle.StateChanged += Refresh;

            if (rows != null)
            {
                foreach (UpgradeStatRowUI row in rows)
                    row?.Bind(_battle);
            }

            if (isActiveAndEnabled)
                Refresh();
        }

        private void Refresh()
        {
            if (_battle == null)
                return;

            int totalLevel = _battle.TotalUpgradeLevel;
            int progress = _battle.TotalUpgradeProgress;
            int required = Mathf.Max(1, _battle.TotalUpgradeProgressRequired);

            if (totalLevelText != null)
                totalLevelText.text = totalLevel.ToString();
            if (goldText != null)
                goldText.text = MainHUDController.FormatNumber(_battle.Gold);
            if (gemText != null)
                gemText.text = MainHUDController.FormatGem(_battle.Gems);
            if (progressText != null)
                progressText.text = $"{progress}/{required}";
            if (progressFill != null)
                progressFill.fillAmount = Mathf.Clamp01(progress / (float)required);
            if (totalLevelUpButton != null)
                totalLevelUpButton.interactable = _battle.CanIncreaseTotalUpgradeLevel;
        }

        private static void SetSelectedState(Button button, bool selected)
        {
            if (button != null)
                button.interactable = !selected;
        }

        private void OnDestroy()
        {
            if (_battle != null)
                _battle.StateChanged -= Refresh;
        }
    }
}

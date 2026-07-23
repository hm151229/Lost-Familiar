using System.Collections;
using LostFamiliar.Core;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace LostFamiliar.Battle
{
    [DisallowMultipleComponent]
    public sealed class UpgradeStatRowUI : MonoBehaviour
    {
        [SerializeField] private StatType statType = StatType.Attack;

        [Header("텍스트 연결")]
        [SerializeField] private TMP_Text statNameText;
        [SerializeField] private TMP_Text levelText;
        [SerializeField] private TMP_Text currentValueText;
        [SerializeField] private TMP_Text nextValueText;
        [SerializeField] private TMP_Text increaseValueText;
        [SerializeField] private TMP_Text costText;

        [Header("강화 버튼")]
        [SerializeField] private Button upgradeButton;
        [SerializeField] private TMP_Text upgradeButtonText;
        [SerializeField] private Color insufficientCostColor = new Color(.85f, .16f, .14f, 1f);
        [SerializeField, Range(0f, 1f)] private float disabledButtonTextAlpha = 0.4f;
        [SerializeField, Min(0.1f)] private float holdStartDelay = 0.4f;
        [SerializeField, Min(0.02f)] private float repeatInterval = 0.08f;

        private MainBattleLoop _battle;
        private Coroutine _holdRoutine;
        private bool _holding;
        private int _upgradeAmount = 1;
        private Color _normalCostColor;
        private Color _normalButtonTextColor;
        private bool _colorsCached;

        private void Awake() => EnsureUpgradeButton();

        private void Start()
        {
            if (_battle == null)
                Bind(FindFirstObjectByType<MainBattleLoop>());
        }

        public void Bind(MainBattleLoop battle)
        {
            EnsureUpgradeButton();
            if (_battle != null)
                _battle.StateChanged -= Refresh;

            _battle = battle;
            if (_battle != null)
                _battle.StateChanged += Refresh;
            Refresh();
        }

        public void SetUpgradeAmount(int amount)
        {
            _upgradeAmount = Mathf.Max(1, amount);
            Refresh();
        }

        internal void BeginUpgradePress()
        {
            if (_battle == null || upgradeButton == null || !upgradeButton.interactable)
                return;

            _holding = true;
            if (_battle.TryUpgradeMany(statType, _upgradeAmount) <= 0)
            {
                StopHolding();
                return;
            }

            if (_holdRoutine != null)
                StopCoroutine(_holdRoutine);
            _holdRoutine = StartCoroutine(HoldUpgradeRoutine());
        }

        internal void EndUpgradePress() => StopHolding();

        private IEnumerator HoldUpgradeRoutine()
        {
            yield return new WaitForSecondsRealtime(holdStartDelay);
            while (_holding)
            {
                if (_battle.TryUpgradeMany(statType, _upgradeAmount) <= 0)
                {
                    StopHolding();
                    yield break;
                }

                yield return new WaitForSecondsRealtime(repeatInterval);
            }
        }

        private void StopHolding()
        {
            _holding = false;
            if (_holdRoutine == null)
                return;

            StopCoroutine(_holdRoutine);
            _holdRoutine = null;
        }

        public void Refresh()
        {
            if (_battle == null)
                return;

            int level = _battle.GetStatLevel(statType);
            int maxLevel = _battle.GetMaxStatLevel(statType);
            bool isMax = level >= maxLevel;
            int previewLevels = isMax ? 0 : Mathf.Min(_upgradeAmount, maxLevel - level);
            double currentValue = _battle.GetStatValue(statType);
            double nextValue = _battle.GetStatValue(statType, previewLevels);

            SetText(statNameText, GetStatName(statType));
            SetText(levelText, $"Lv.{level}");
            SetText(currentValueText, FormatStatValue(statType, currentValue));
            SetText(nextValueText, isMax ? "MAX" : FormatStatValue(statType, nextValue));
            SetText(increaseValueText, isMax
                ? string.Empty
                : FormatIncreaseValue(statType, nextValue - currentValue));
            SetText(costText, isMax
                ? "MAX"
                : MainHUDController.FormatNumber(_battle.GetUpgradeCost(statType, previewLevels)));

            bool canUpgrade = !isMax && _battle.CanUpgrade(statType, previewLevels);
            if (upgradeButton != null)
                upgradeButton.interactable = canUpgrade;

            CacheTextColors();
            if (costText != null)
                costText.color = !isMax && !canUpgrade ? insufficientCostColor : _normalCostColor;
            if (upgradeButtonText != null)
            {
                Color color = _normalButtonTextColor;
                color.a = canUpgrade
                    ? _normalButtonTextColor.a
                    : _normalButtonTextColor.a * disabledButtonTextAlpha;
                upgradeButtonText.color = color;
            }
        }

        private void EnsureUpgradeButton()
        {
            if (upgradeButton == null)
            {
                Button[] buttons = GetComponentsInChildren<Button>(true);
                foreach (Button button in buttons)
                {
                    if (button.name == "Btn_LevelUp")
                    {
                        upgradeButton = button;
                        break;
                    }
                }

                if (upgradeButton == null && buttons.Length > 0)
                    upgradeButton = buttons[0];
            }

            if (upgradeButton == null)
                return;

            if (upgradeButtonText == null)
                upgradeButtonText = upgradeButton.GetComponentInChildren<TMP_Text>(true);

            CacheTextColors();

            UpgradeButtonPressRelay relay = upgradeButton.GetComponent<UpgradeButtonPressRelay>();
            if (relay == null)
                relay = upgradeButton.gameObject.AddComponent<UpgradeButtonPressRelay>();
            relay.Bind(this);
        }

        private void CacheTextColors()
        {
            if (_colorsCached)
                return;

            if (costText == null || upgradeButtonText == null)
                return;

            _normalCostColor = costText.color;
            _normalButtonTextColor = upgradeButtonText.color;
            _colorsCached = true;
        }

        private static string GetStatName(StatType type)
        {
            return type switch
            {
                StatType.Attack => "공격력",
                StatType.CriticalChance => "치명타 확률",
                StatType.CriticalDamage => "치명타 피해량",
                StatType.SkillDamage => "스킬 데미지",
                StatType.BossDamage => "보스 데미지",
                _ => type.ToString()
            };
        }

        private static string FormatStatValue(StatType type, double value)
        {
            return type == StatType.Attack
                ? MainHUDController.FormatNumber(value)
                : $"{value:0.#}%";
        }

        private static string FormatIncreaseValue(StatType type, double value)
        {
            return type == StatType.Attack
                ? $"(+{MainHUDController.FormatNumber(value)})"
                : $"(+{value:0.#}%)";
        }

        private static void SetText(TMP_Text text, string value)
        {
            if (text != null)
                text.text = value;
        }

        private void OnDisable() => StopHolding();

        private void OnDestroy()
        {
            if (_battle != null)
                _battle.StateChanged -= Refresh;
        }
    }

    [DisallowMultipleComponent]
    internal sealed class UpgradeButtonPressRelay : MonoBehaviour,
        IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
    {
        private UpgradeStatRowUI _owner;

        public void Bind(UpgradeStatRowUI owner) => _owner = owner;
        public void OnPointerDown(PointerEventData eventData) => _owner?.BeginUpgradePress();
        public void OnPointerUp(PointerEventData eventData) => _owner?.EndUpgradePress();
        public void OnPointerExit(PointerEventData eventData) => _owner?.EndUpgradePress();
    }
}

using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace LostFamiliar.Battle
{
    [DisallowMultipleComponent]
    public sealed class UpgradePopupController : MonoBehaviour
    {
        private const string PopupObjectName = "UpgradePopup";

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
        [SerializeField] private TMP_Text totalLevelUpButtonText;
        [SerializeField, Range(0f, 1f)] private float disabledButtonTextAlpha = 0.4f;

        [Header("보유 재화")]
        [SerializeField] private TMP_Text goldText;
        [SerializeField] private TMP_Text gemText;

        private MainBattleLoop _battle;
        private UpgradeStatRowUI[] _rows;
        private int _selectedAmount = 1;
        private bool _initialized;
        private Color _normalTotalButtonTextColor;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            foreach (RectTransform rect in Resources.FindObjectsOfTypeAll<RectTransform>())
            {
                if (rect.name != PopupObjectName || !rect.gameObject.scene.IsValid() ||
                    !rect.gameObject.scene.isLoaded)
                    continue;

                UpgradePopupController controller = rect.GetComponent<UpgradePopupController>();
                if (controller == null)
                    controller = rect.gameObject.AddComponent<UpgradePopupController>();
                controller.Initialize();
                return;
            }
        }

        private void Awake()
        {
            if (!_initialized)
                Initialize();
        }

        private void Initialize()
        {
            if (_initialized)
                return;

            _initialized = true;
            AutoFindReferences();

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

            BindBattle(FindFirstObjectByType<MainBattleLoop>());
            SelectAmount(1);
            Refresh();
            gameObject.SetActive(false);
        }

        public void Open()
        {
            gameObject.SetActive(true);
            if (_battle == null)
                BindBattle(FindFirstObjectByType<MainBattleLoop>());
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
            if (_rows != null)
            {
                foreach (UpgradeStatRowUI row in _rows)
                    row?.SetUpgradeAmount(_selectedAmount);
            }

            SetSelectedState(x1Button, _selectedAmount == 1);
            SetSelectedState(x10Button, _selectedAmount == 10);
            SetSelectedState(x30Button, _selectedAmount == 30);
        }

        private void BindBattle(MainBattleLoop battle)
        {
            if (_battle != null)
                _battle.StateChanged -= Refresh;

            _battle = battle;
            if (_battle != null)
                _battle.StateChanged += Refresh;

            if (_rows != null)
            {
                foreach (UpgradeStatRowUI row in _rows)
                    row?.Bind(_battle);
            }
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
            if (totalLevelUpButtonText != null)
            {
                Color color = _normalTotalButtonTextColor;
                color.a = _battle.CanIncreaseTotalUpgradeLevel
                    ? _normalTotalButtonTextColor.a
                    : _normalTotalButtonTextColor.a * disabledButtonTextAlpha;
                totalLevelUpButtonText.color = color;
            }
        }

        private void AutoFindReferences()
        {
            openButton ??= FindSceneButton("UpgradeButton");
            closeButton ??= FindChild<Button>(transform, "Btn_Close");
            x1Button ??= FindChild<Button>(transform, "Btn_X1");
            x10Button ??= FindChild<Button>(transform, "Btn_X10");
            x30Button ??= FindChild<Button>(transform, "Btn_X30");
            totalLevelUpButton ??= FindChild<Button>(transform, "Btn_TotalLevelUp");
            if (totalLevelUpButtonText == null && totalLevelUpButton != null)
                totalLevelUpButtonText = totalLevelUpButton.GetComponentInChildren<TMP_Text>(true);
            if (totalLevelUpButtonText != null)
                _normalTotalButtonTextColor = totalLevelUpButtonText.color;
            totalLevelText ??= FindChild<TMP_Text>(transform, "TotalLevelText");
            progressText ??= FindChild<TMP_Text>(transform, "ProgressText");
            goldText ??= FindCurrencyAmountText("GoldPanel");
            gemText ??= FindCurrencyAmountText("GemPanel");

            Transform sliderRoot = FindChildTransform(transform, "Slider_TotalExp");
            if (progressFill == null && sliderRoot != null)
            {
                progressFill = FindChild<Image>(sliderRoot, "Fill");
                if (progressFill != null)
                {
                    progressFill.type = Image.Type.Filled;
                    progressFill.fillMethod = Image.FillMethod.Horizontal;
                    progressFill.fillOrigin = (int)Image.OriginHorizontal.Left;
                }
            }

            _rows = GetComponentsInChildren<UpgradeStatRowUI>(true);
        }

        private TMP_Text FindCurrencyAmountText(string panelName)
        {
            Transform currencyGroup = FindChildTransform(transform, "CurrencyGroup");
            Transform panel = currencyGroup != null
                ? FindChildTransform(currencyGroup, panelName)
                : null;
            return panel != null ? FindChild<TMP_Text>(panel, "AmountText") : null;
        }

        private static void SetSelectedState(Button button, bool selected)
        {
            if (button != null)
                button.interactable = !selected;
        }

        private static Button FindSceneButton(string objectName)
        {
            foreach (Button button in Resources.FindObjectsOfTypeAll<Button>())
            {
                if (button.name == objectName && button.gameObject.scene.IsValid() &&
                    button.gameObject.scene.isLoaded)
                    return button;
            }
            return null;
        }

        private static T FindChild<T>(Transform root, string objectName) where T : Component
        {
            Transform child = FindChildTransform(root, objectName);
            return child != null ? child.GetComponent<T>() : null;
        }

        private static Transform FindChildTransform(Transform root, string objectName)
        {
            foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
            {
                if (child.name == objectName)
                    return child;
            }
            return null;
        }

        private void OnDestroy()
        {
            if (_battle != null)
                _battle.StateChanged -= Refresh;
        }
    }
}

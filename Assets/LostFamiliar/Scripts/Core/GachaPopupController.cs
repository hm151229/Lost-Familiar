using System.Collections.Generic;
using System.Text;
using LostFamiliar.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace LostFamiliar.Battle
{
    [DisallowMultipleComponent]
    public sealed class GachaPopupController : MonoBehaviour
    {
        private static readonly Color SelectedColor = Color.white;
        private static readonly Color UnselectedColor = new Color32(0xB2, 0xA2, 0xA2, 0xFF);

        private MainBattleLoop _battle;
        private readonly Dictionary<GachaCategory, Button> _tabs = new Dictionary<GachaCategory, Button>();
        private GachaCategory _selected = GachaCategory.Armor;
        private TMP_Text _levelTitleText;
        private TMP_Text _levelText;
        private TMP_Text _progressText;
        private Image _progressFill;
        private TMP_Text _goldText;
        private TMP_Text _gemText;
        private Button _summon10Button;
        private Button _summon30Button;
        private bool _listenersBound;

        private void Awake()
        {
            FindReferences();
            BindListeners();
        }

        private void OnEnable()
        {
            FindReferences();
            BindListeners();
            BindBattle(FindFirstObjectByType<MainBattleLoop>());
            Refresh();
        }

        private void BindBattle(MainBattleLoop battle)
        {
            if (_battle == battle)
                return;
            if (_battle != null)
                _battle.StateChanged -= Refresh;
            _battle = battle;
            if (_battle != null)
                _battle.StateChanged += Refresh;
        }

        private void FindReferences()
        {
            RegisterTab(GachaCategory.Armor, "Tab_Armor");
            RegisterTab(GachaCategory.Accessory, "Tab_Accessory");
            RegisterTab(GachaCategory.Skill, "Tab_Skill");
            RegisterTab(GachaCategory.Weapon, "Tab_Weapon");

            Transform levelPanel = FindDescendant("GachaLevelPanel");
            _levelTitleText ??= GetChild<TMP_Text>(levelPanel, "LevelTitleText");
            _levelText ??= GetChild<TMP_Text>(levelPanel, "LevelText");
            _progressText ??= GetChild<TMP_Text>(levelPanel, "ProgressText");
            _progressFill ??= GetChild<Image>(levelPanel, "Fill");

            _summon10Button ??= GetButton("Btn_Summon10");
            _summon30Button ??= GetButton("Btn_Summon30");
            SetCostText(_summon10Button, GachaBalance.Cost(10));
            SetCostText(_summon30Button, GachaBalance.Cost(30));

            Transform header = FindDescendant("Header");
            Transform goldPanel = FindDescendant(header, "GoldPanel");
            Transform gemPanel = FindDescendant(header, "GemPanel");
            _goldText ??= GetChild<TMP_Text>(goldPanel, "AmountText");
            _gemText ??= GetChild<TMP_Text>(gemPanel, "AmountText");
        }

        private void BindListeners()
        {
            if (_listenersBound)
                return;
            _listenersBound = true;
            foreach (KeyValuePair<GachaCategory, Button> pair in _tabs)
            {
                GachaCategory category = pair.Key;
                pair.Value.onClick.AddListener(() => SelectCategory(category));
            }
            _summon10Button?.onClick.AddListener(() => Summon(10));
            _summon30Button?.onClick.AddListener(() => Summon(30));
            GetButton("Btn_Close")?.onClick.AddListener(Close);
        }

        private void RegisterTab(GachaCategory category, string objectName)
        {
            if (_tabs.ContainsKey(category))
                return;
            Button button = GetButton(objectName);
            if (button != null)
                _tabs.Add(category, button);
        }

        private void SelectCategory(GachaCategory category)
        {
            _selected = category;
            Refresh();
        }

        private void Summon(int count)
        {
            if (_battle == null || !_battle.TryGacha(_selected, count, out List<GachaReward> rewards))
                return;

            StringBuilder summary = new StringBuilder();
            summary.Append($"[{CategoryName(_selected)} 뽑기 {count}회] ");
            for (int i = 0; i < rewards.Count; i++)
            {
                if (i > 0) summary.Append(", ");
                summary.Append(rewards[i].DisplayName);
            }
            Debug.Log(summary.ToString(), this);
            Refresh();
        }

        private void Refresh()
        {
            if (!isActiveAndEnabled || _battle == null)
                return;

            int level = _battle.GetGachaLevel(_selected);
            int progress = _battle.GetGachaProgress(_selected);
            int required = GachaBalance.RequiredDraws(level);
            if (_levelTitleText != null)
                _levelTitleText.text = $"{CategoryName(_selected)} 뽑기 레벨";
            if (_levelText != null)
                _levelText.text = $"Lv.{level}";
            if (_progressText != null)
                _progressText.text = required <= 0 ? "MAX" : $"{progress} / {required}";
            if (_progressFill != null)
            {
                _progressFill.type = Image.Type.Filled;
                _progressFill.fillMethod = Image.FillMethod.Horizontal;
                _progressFill.fillOrigin = (int)Image.OriginHorizontal.Left;
                _progressFill.fillAmount = required <= 0 ? 1f : Mathf.Clamp01(progress / (float)required);
            }

            if (_goldText != null) _goldText.text = MainHUDController.FormatNumber(_battle.Gold);
            if (_gemText != null) _gemText.text = MainHUDController.FormatGem(_battle.Gems);
            if (_summon10Button != null) _summon10Button.interactable = _battle.Gems >= GachaBalance.Cost(10);
            if (_summon30Button != null) _summon30Button.interactable = _battle.Gems >= GachaBalance.Cost(30);

            foreach (KeyValuePair<GachaCategory, Button> pair in _tabs)
                if (pair.Value.image != null)
                    pair.Value.image.color = pair.Key == _selected ? SelectedColor : UnselectedColor;
        }

        private void SetCostText(Button button, int cost)
        {
            TMP_Text text = button != null ? GetChild<TMP_Text>(button.transform, "CostText") : null;
            if (text != null)
                text.text = cost.ToString();
        }

        private void Close() => gameObject.SetActive(false);

        private static string CategoryName(GachaCategory category) => category switch
        {
            GachaCategory.Armor => "방어구",
            GachaCategory.Accessory => "장신구",
            GachaCategory.Skill => "스킬",
            GachaCategory.Weapon => "무기",
            _ => "뽑기"
        };

        private Button GetButton(string objectName)
        {
            Transform target = FindDescendant(objectName);
            return target != null ? target.GetComponent<Button>() : null;
        }

        private Transform FindDescendant(string objectName) => FindDescendant(transform, objectName);

        private static Transform FindDescendant(Transform root, string objectName)
        {
            if (root == null)
                return null;
            foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
                if (child.name == objectName)
                    return child;
            return null;
        }

        private static T GetChild<T>(Transform root, string objectName) where T : Component
        {
            Transform child = FindDescendant(root, objectName);
            return child != null ? child.GetComponent<T>() : null;
        }

        private void OnDestroy()
        {
            if (_battle != null)
                _battle.StateChanged -= Refresh;
        }
    }
}

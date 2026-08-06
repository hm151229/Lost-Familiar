using System.Collections;
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
        private static readonly Color SelectedTabColor = new Color32(0x97, 0xA5, 0xFF, 0xFF);
        private static readonly Color DefaultTabColor = Color.white;
        private static readonly Color SelectedTabTextColor = Color.white;
        private static readonly Color DefaultTabTextColor = new Color32(0x1A, 0x0F, 0x0F, 0xFF);
        private static readonly Vector2 SelectedTabSize = new Vector2(230f, 230f);
        private static readonly Vector2 DefaultTabSize = new Vector2(200f, 200f);

        [Header("Summon Preview Sprites")]
        [SerializeField] private Sprite armorPreviewSprite;
        [SerializeField] private Sprite accessoryPreviewSprite;
        [SerializeField] private Sprite skillPreviewSprite;
        [SerializeField] private Sprite weaponPreviewSprite;

        private MainBattleLoop _battle;
        private readonly Dictionary<GachaCategory, Button> _tabs = new Dictionary<GachaCategory, Button>();
        private GachaCategory _selected = GachaCategory.Armor;
        private TMP_Text _categoryTitleText;
        private TMP_Text _levelTitleText;
        private TMP_Text _levelText;
        private TMP_Text _progressText;
        private Image _progressFill;
        private TMP_Text _goldText;
        private TMP_Text _gemText;
        private Button _summon10Button;
        private Button _summon30Button;
        private Image _summonPreviewIcon;
        private RectTransform _summonPreviewIconRect;
        private Vector2 _summonPreviewBasePosition;
        private Quaternion _summonPreviewBaseRotation = Quaternion.identity;
        private bool _summonPreviewTransformCached;
        private GameObject _resultPanel;
        private Button _resultBackgroundButton;
        private GameObject _summon10Result;
        private GameObject _summon30Result;
        private Transform _summon10SlotGroup;
        private Transform _summon30SlotGroup;
        private GameObject _inventorySlotPrefab;
        private Coroutine _resultRevealRoutine;
        private bool _resultSlotsInitialized;
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

            Transform rightGroup = FindDescendant("RightGroup");
            _categoryTitleText ??= GetChild<TMP_Text>(rightGroup, "TitleText");

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

            Transform summonPreview = FindDescendant("SummonPreview");
            DisableDecorativeRaycasts(summonPreview);
            _summonPreviewIcon ??= GetChild<Image>(summonPreview, "IconImage");
            if (!_summonPreviewTransformCached && _summonPreviewIcon != null)
            {
                _summonPreviewIconRect = _summonPreviewIcon.rectTransform;
                _summonPreviewBasePosition = _summonPreviewIconRect.anchoredPosition;
                _summonPreviewBaseRotation = _summonPreviewIconRect.localRotation;
                _summonPreviewTransformCached = true;
            }

            Transform result = FindDescendant("ResultPanel");
            _resultPanel ??= result?.gameObject;
            Transform resultBackground = FindDescendant(result, "BG");
            if (_resultBackgroundButton == null && resultBackground != null)
            {
                _resultBackgroundButton = resultBackground.GetComponent<Button>() ??
                                          resultBackground.gameObject.AddComponent<Button>();
                _resultBackgroundButton.targetGraphic = resultBackground.GetComponent<Graphic>();
                _resultBackgroundButton.transition = Selectable.Transition.None;
            }
            Transform summon10 = FindDescendant(result, "Summon10");
            Transform summon30 = FindDescendant(result, "Summon30");
            _summon10Result ??= summon10?.gameObject;
            _summon30Result ??= summon30?.gameObject;
            _summon10SlotGroup ??= FindDescendant(summon10, "ResultSlotGroup");
            _summon30SlotGroup ??= FindDescendant(summon30, "ResultSlotGroup");
            _inventorySlotPrefab ??= Resources.Load<GameObject>("Prefabs/InventorySlot");
            InitializeResultPanel(result, resultBackground);
        }

        private static void DisableDecorativeRaycasts(Transform root)
        {
            if (root == null)
                return;

            foreach (Graphic graphic in root.GetComponentsInChildren<Graphic>(true))
            {
                // Keep actual controls clickable, but let decorative frames, images,
                // and labels pass pointer events through to the category tabs below.
                if (graphic.GetComponentInParent<Button>(true) == null)
                    graphic.raycastTarget = false;
            }
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
            _resultBackgroundButton?.onClick.AddListener(CloseResultPanel);
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
            ShowResultPanel(rewards, count);
            Refresh();
        }

        private void InitializeResultPanel(Transform resultPanel, Transform resultBackground)
        {
            if (resultPanel == null)
                return;

            foreach (Graphic graphic in resultPanel.GetComponentsInChildren<Graphic>(true))
                graphic.raycastTarget = graphic.transform == resultBackground;
            foreach (Selectable selectable in resultPanel.GetComponentsInChildren<Selectable>(true))
                if (selectable != _resultBackgroundButton)
                    selectable.interactable = false;

            if (_resultSlotsInitialized)
                return;
            _resultSlotsInitialized = true;
            ClearSlotGroup(_summon10SlotGroup);
            ClearSlotGroup(_summon30SlotGroup);
            _resultPanel.SetActive(false);
        }

        private void ShowResultPanel(IReadOnlyList<GachaReward> rewards, int count)
        {
            if (_resultPanel == null || _inventorySlotPrefab == null)
                return;

            ClearSlotGroup(_summon10SlotGroup);
            ClearSlotGroup(_summon30SlotGroup);

            bool show10 = count == 10;
            _summon10Result?.SetActive(show10);
            _summon30Result?.SetActive(!show10 && count == 30);
            Transform targetGroup = show10 ? _summon10SlotGroup : _summon30SlotGroup;
            if (_resultBackgroundButton != null)
                _resultBackgroundButton.interactable = false;
            _resultPanel.SetActive(true);
            GameAudioManager.Instance.PlaySfx("SFX_Summon_Result_Open");
            if (targetGroup == null || rewards == null)
            {
                if (_resultBackgroundButton != null)
                    _resultBackgroundButton.interactable = true;
                return;
            }

            if (_resultRevealRoutine != null)
                StopCoroutine(_resultRevealRoutine);
            _resultRevealRoutine = StartCoroutine(RevealResultSlots(targetGroup, rewards));
        }

        private IEnumerator RevealResultSlots(Transform targetGroup, IReadOnlyList<GachaReward> rewards)
        {
            for (int i = 0; i < rewards.Count; i++)
            {
                GachaReward reward = rewards[i];
                GameObject slot = Instantiate(_inventorySlotPrefab, targetGroup);
                slot.name = $"ResultSlot{i + 1:00}";
                InventorySlotView view = slot.GetComponent<InventorySlotView>() ??
                                         slot.AddComponent<InventorySlotView>();
                Sprite icon = reward.equipment != null ? reward.equipment.icon : reward.skill?.icon;
                view.Render(
                    icon,
                    EquipmentBalance.RarityColor(reward.rarity),
                    false,
                    0,
                    false,
                    0,
                    1,
                    false,
                    false,
                    false);
                ConfigureResultSlot(slot.transform);
                StartCoroutine(AnimateResultSlot(slot.transform));
                yield return new WaitForSecondsRealtime(.025f);
            }

            // Keep the result locked until the final slot has finished popping in.
            yield return new WaitForSecondsRealtime(.16f);
            if (_resultBackgroundButton != null)
                _resultBackgroundButton.interactable = true;
            _resultRevealRoutine = null;
        }

        private static void ConfigureResultSlot(Transform slot)
        {
            Transform background = FindDescendant(slot, "BG");
            Transform icon = FindDescendant(slot, "Icon_Item");
            foreach (Transform child in slot.GetComponentsInChildren<Transform>(true))
            {
                if (child == slot)
                    continue;
                bool keep = child == background || child == icon ||
                            (background != null && background.IsChildOf(child)) ||
                            (icon != null && icon.IsChildOf(child));
                child.gameObject.SetActive(keep);
            }
            foreach (Graphic graphic in slot.GetComponentsInChildren<Graphic>(true))
                graphic.raycastTarget = false;
            foreach (Selectable selectable in slot.GetComponentsInChildren<Selectable>(true))
                selectable.interactable = false;
        }

        private static IEnumerator AnimateResultSlot(Transform slot)
        {
            if (slot == null)
                yield break;

            Vector3 targetScale = slot.localScale;
            slot.localScale = targetScale * .15f;
            const float duration = .16f;
            float elapsed = 0f;
            while (elapsed < duration && slot != null)
            {
                elapsed += Time.unscaledDeltaTime;
                float progress = Mathf.Clamp01(elapsed / duration);
                float shifted = progress - 1f;
                const float overshoot = 1.70158f;
                float eased = 1f + (overshoot + 1f) * shifted * shifted * shifted +
                              overshoot * shifted * shifted;
                slot.localScale = targetScale * Mathf.LerpUnclamped(.15f, 1f, eased);
                yield return null;
            }
            if (slot != null)
                slot.localScale = targetScale;
        }

        private static void ClearSlotGroup(Transform group)
        {
            if (group == null)
                return;
            for (int i = group.childCount - 1; i >= 0; i--)
            {
                GameObject child = group.GetChild(i).gameObject;
                child.SetActive(false);
                Destroy(child);
            }
        }

        private void CloseResultPanel()
        {
            if (_resultRevealRoutine != null)
            {
                StopCoroutine(_resultRevealRoutine);
                _resultRevealRoutine = null;
            }
            if (_resultPanel != null)
                _resultPanel.SetActive(false);
        }

        private void Refresh()
        {
            if (!isActiveAndEnabled || _battle == null)
                return;

            int level = _battle.GetGachaLevel(_selected);
            int progress = _battle.GetGachaProgress(_selected);
            int required = GachaBalance.RequiredDraws(level);
            if (_categoryTitleText != null)
                _categoryTitleText.text = $"{CategoryName(_selected)} 소환";
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

            RefreshTabs();

            RefreshSummonPreview();
        }

        private void RefreshTabs()
        {
            foreach (KeyValuePair<GachaCategory, Button> pair in _tabs)
            {
                Button button = pair.Value;
                if (button == null)
                    continue;

                bool selected = pair.Key == _selected;
                if (button.image != null)
                    button.image.color = selected ? SelectedTabColor : DefaultTabColor;

                TMP_Text titleText = GetChild<TMP_Text>(button.transform, "TitleText");
                if (titleText != null)
                    titleText.color = selected ? SelectedTabTextColor : DefaultTabTextColor;

                if (button.transform is RectTransform rectTransform)
                    rectTransform.sizeDelta = selected ? SelectedTabSize : DefaultTabSize;
            }
        }

        private void RefreshSummonPreview()
        {
            if (_summonPreviewIcon == null)
                return;
            _summonPreviewIcon.sprite = _selected switch
            {
                GachaCategory.Armor => armorPreviewSprite,
                GachaCategory.Accessory => accessoryPreviewSprite,
                GachaCategory.Skill => skillPreviewSprite,
                GachaCategory.Weapon => weaponPreviewSprite,
                _ => null
            };
            _summonPreviewIcon.enabled = _summonPreviewIcon.sprite != null;
            _summonPreviewIcon.preserveAspect = true;
        }

        private void Update()
        {
            if (_summonPreviewIconRect == null || !_summonPreviewIconRect.gameObject.activeInHierarchy)
                return;
            float phase = Time.unscaledTime * 2.25f;
            _summonPreviewIconRect.anchoredPosition = _summonPreviewBasePosition +
                                                        Vector2.up * (Mathf.Sin(phase) * 14f);
            _summonPreviewIconRect.localRotation = _summonPreviewBaseRotation *
                                                   Quaternion.Euler(0f, 0f, Mathf.Sin(phase * .72f) * 1.5f);
        }

        private void SetCostText(Button button, int cost)
        {
            TMP_Text text = button != null ? GetChild<TMP_Text>(button.transform, "CostText") : null;
            if (text != null)
                text.text = cost.ToString();
        }

        private void Close() => gameObject.SetActive(false);

        private void OnDisable()
        {
            CloseResultPanel();
            if (_summonPreviewIconRect != null)
            {
                _summonPreviewIconRect.anchoredPosition = _summonPreviewBasePosition;
                _summonPreviewIconRect.localRotation = _summonPreviewBaseRotation;
            }
        }

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
            _resultBackgroundButton?.onClick.RemoveListener(CloseResultPanel);
        }
    }
}

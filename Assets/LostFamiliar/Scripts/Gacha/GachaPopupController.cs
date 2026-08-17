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

        [Header("Tabs")]
        [SerializeField] private Button armorTabButton;
        [SerializeField] private Button accessoryTabButton;
        [SerializeField] private Button skillTabButton;
        [SerializeField] private Button weaponTabButton;
        [SerializeField] private TMP_Text armorTabTitle;
        [SerializeField] private TMP_Text accessoryTabTitle;
        [SerializeField] private TMP_Text skillTabTitle;
        [SerializeField] private TMP_Text weaponTabTitle;

        [Header("Right Panel")]
        [SerializeField] private TMP_Text categoryTitleText;
        [SerializeField] private TMP_Text levelTitleText;
        [SerializeField] private TMP_Text levelText;
        [SerializeField] private TMP_Text progressText;
        [SerializeField] private Image progressFill;

        [Header("Currency")]
        [SerializeField] private TMP_Text goldText;
        [SerializeField] private TMP_Text gemText;

        [Header("Summon")]
        [SerializeField] private Button summon10Button;
        [SerializeField] private Button summon30Button;
        [SerializeField] private Button closeButton;
        [SerializeField] private Image summonPreviewIcon;
        [SerializeField] private TMP_Text summon10CostText;
        [SerializeField] private TMP_Text summon30CostText;

        [Header("Result")]
        [SerializeField] private GameObject resultPanel;
        [SerializeField] private Button resultBackgroundButton;
        [SerializeField] private GameObject summon10Result;
        [SerializeField] private GameObject summon30Result;
        [SerializeField] private Transform summon10SlotGroup;
        [SerializeField] private Transform summon30SlotGroup;

        [Header("Prefabs")]
        [SerializeField] private GameObject inventorySlotPrefab;

        private MainBattleLoop _battle;
        private readonly Dictionary<GachaCategory, Button> _tabs = new Dictionary<GachaCategory, Button>();
        private readonly Dictionary<GachaCategory, TMP_Text> _tabTitles =
            new Dictionary<GachaCategory, TMP_Text>();
        private GachaCategory _selected = GachaCategory.Armor;
        private RectTransform _summonPreviewIconRect;
        private Vector2 _summonPreviewBasePosition;
        private Quaternion _summonPreviewBaseRotation = Quaternion.identity;
        private bool _summonPreviewTransformCached;
        private Coroutine _resultRevealRoutine;
        private bool _resultSlotsInitialized;
        private bool _listenersBound;

        private void Awake()
        {
            BuildTabLookup();
            CacheSummonPreviewTransform();
            ConfigureResultPanel();
            BindListeners();

            if (summon10CostText != null)
                summon10CostText.text = GachaBalance.Cost(10).ToString();
            if (summon30CostText != null)
                summon30CostText.text = GachaBalance.Cost(30).ToString();

            DisableDecorativeRaycasts(
                summonPreviewIcon != null ? summonPreviewIcon.transform.parent : null);
        }

        private void OnEnable()
        {
            Refresh();
        }

        public void Bind(MainBattleLoop battle)
        {
            BindBattle(battle);
            if (isActiveAndEnabled)
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

        private void BuildTabLookup()
        {
            _tabs.Clear();
            _tabTitles.Clear();

            RegisterTab(GachaCategory.Armor, armorTabButton, armorTabTitle);
            RegisterTab(GachaCategory.Accessory, accessoryTabButton, accessoryTabTitle);
            RegisterTab(GachaCategory.Skill, skillTabButton, skillTabTitle);
            RegisterTab(GachaCategory.Weapon, weaponTabButton, weaponTabTitle);
        }

        private void RegisterTab(GachaCategory category, Button button, TMP_Text title)
        {
            if (button != null)
                _tabs[category] = button;
            if (title != null)
                _tabTitles[category] = title;
        }

        private void CacheSummonPreviewTransform()
        {
            if (_summonPreviewTransformCached || summonPreviewIcon == null)
                return;

            _summonPreviewIconRect = summonPreviewIcon.rectTransform;
            _summonPreviewBasePosition = _summonPreviewIconRect.anchoredPosition;
            _summonPreviewBaseRotation = _summonPreviewIconRect.localRotation;
            _summonPreviewTransformCached = true;
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
            summon10Button?.onClick.AddListener(() => Summon(10));
            summon30Button?.onClick.AddListener(() => Summon(30));
            closeButton?.onClick.AddListener(Close);
            resultBackgroundButton?.onClick.AddListener(CloseResultPanel);
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

        private void ConfigureResultPanel()
        {
            if (resultPanel == null)
                return;

            Transform backgroundTransform = resultBackgroundButton != null
                ? resultBackgroundButton.transform
                : null;

            if (resultBackgroundButton != null)
                resultBackgroundButton.transition = Selectable.Transition.None;

            foreach (Graphic graphic in resultPanel.GetComponentsInChildren<Graphic>(true))
                graphic.raycastTarget = backgroundTransform != null && graphic.transform == backgroundTransform;
            foreach (Selectable selectable in resultPanel.GetComponentsInChildren<Selectable>(true))
                if (selectable != resultBackgroundButton)
                    selectable.interactable = false;

            if (_resultSlotsInitialized)
                return;
            _resultSlotsInitialized = true;
            ClearSlotGroup(summon10SlotGroup);
            ClearSlotGroup(summon30SlotGroup);
            resultPanel.SetActive(false);
        }

        private void ShowResultPanel(IReadOnlyList<GachaReward> rewards, int count)
        {
            if (resultPanel == null || inventorySlotPrefab == null)
                return;

            ClearSlotGroup(summon10SlotGroup);
            ClearSlotGroup(summon30SlotGroup);

            bool show10 = count == 10;
            summon10Result?.SetActive(show10);
            summon30Result?.SetActive(!show10 && count == 30);
            Transform targetGroup = show10 ? summon10SlotGroup : summon30SlotGroup;
            if (resultBackgroundButton != null)
                resultBackgroundButton.interactable = false;
            resultPanel.SetActive(true);
            GameAudioManager.Instance.PlaySfx("SFX_Summon_Result_Open");
            if (targetGroup == null || rewards == null)
            {
                if (resultBackgroundButton != null)
                    resultBackgroundButton.interactable = true;
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
                GameObject slot = Instantiate(inventorySlotPrefab, targetGroup);
                slot.name = $"ResultSlot{i + 1:00}";
                InventorySlotView view = slot.GetComponent<InventorySlotView>();
                if (view == null)
                {
                    Debug.LogError("InventorySlot prefab에 InventorySlotView가 없습니다.", slot);
                    Destroy(slot);
                    continue;
                }
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
            if (resultBackgroundButton != null)
                resultBackgroundButton.interactable = true;
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
            if (resultPanel != null)
                resultPanel.SetActive(false);
        }

        private void Refresh()
        {
            if (!isActiveAndEnabled || _battle == null)
                return;

            int level = _battle.GetGachaLevel(_selected);
            int progress = _battle.GetGachaProgress(_selected);
            int required = GachaBalance.RequiredDraws(level);
            if (categoryTitleText != null)
                categoryTitleText.text = $"{CategoryName(_selected)} 소환";
            if (levelText != null)
                levelText.text = $"Lv.{level}";
            if (progressText != null)
                progressText.text = required <= 0 ? "MAX" : $"{progress} / {required}";
            if (progressFill != null)
            {
                progressFill.type = Image.Type.Filled;
                progressFill.fillMethod = Image.FillMethod.Horizontal;
                progressFill.fillOrigin = (int)Image.OriginHorizontal.Left;
                progressFill.fillAmount = required <= 0 ? 1f : Mathf.Clamp01(progress / (float)required);
            }

            if (goldText != null) goldText.text = MainHUDController.FormatNumber(_battle.Gold);
            if (gemText != null) gemText.text = MainHUDController.FormatGem(_battle.Gems);
            if (summon10Button != null) summon10Button.interactable = _battle.Gems >= GachaBalance.Cost(10);
            if (summon30Button != null) summon30Button.interactable = _battle.Gems >= GachaBalance.Cost(30);

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

                if (_tabTitles.TryGetValue(pair.Key, out TMP_Text title))
                    title.color = selected ? SelectedTabTextColor : DefaultTabTextColor;

                if (button.transform is RectTransform rectTransform)
                    rectTransform.sizeDelta = selected ? SelectedTabSize : DefaultTabSize;
            }
        }

        private void RefreshSummonPreview()
        {
            if (summonPreviewIcon == null)
                return;
            summonPreviewIcon.sprite = _selected switch
            {
                GachaCategory.Armor => armorPreviewSprite,
                GachaCategory.Accessory => accessoryPreviewSprite,
                GachaCategory.Skill => skillPreviewSprite,
                GachaCategory.Weapon => weaponPreviewSprite,
                _ => null
            };
            summonPreviewIcon.enabled = summonPreviewIcon.sprite != null;
            summonPreviewIcon.preserveAspect = true;
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

        private static Transform FindDescendant(Transform root, string objectName)
        {
            if (root == null)
                return null;
            foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
                if (child.name == objectName)
                    return child;
            return null;
        }

        private void OnDestroy()
        {
            if (_battle != null)
                _battle.StateChanged -= Refresh;
            resultBackgroundButton?.onClick.RemoveListener(CloseResultPanel);
        }
    }
}

using System;
using System.Collections.Generic;
using LostFamiliar.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace LostFamiliar.Battle
{
    [DisallowMultipleComponent]
    public sealed class EquipmentPopupController : MonoBehaviour
    {
        private enum Filter { Weapon, Head, Body, Accessory, Shoes }

        private static readonly Color TypeUnselected = new Color32(0xEA, 0xD5, 0xB4, 0xFF);

        [Header("Character Stand Motion")]
        [SerializeField] private RectTransform characterStand;
        [SerializeField, Min(.1f)] private float characterBreathSpeed = 1.8f;
        [SerializeField, Range(0f, .1f)] private float characterBreathScaleAmount = .012f;
        [SerializeField, Range(0f, 15f)] private float characterBreathMoveAmount = .6f;
        [SerializeField, Range(0f, 2f)] private float characterSwayAngle = .35f;

        [Header("References")]
        [SerializeField] private Transform inventoryContent;

        [Header("Currency")]
        [SerializeField] private TMP_Text goldText;
        [SerializeField] private TMP_Text gemText;

        [Header("Actions")]
        [SerializeField] private Button mergeAllButton;
        [SerializeField] private Button autoEquipButton;
        [SerializeField] private Button closeButton;
        [SerializeField] private GameObject mergeAllRedDot;
        [SerializeField] private GameObject autoEquipRedDot;

        [Header("Category Tabs")]
        [SerializeField] private Button weaponTabButton;
        [SerializeField] private Button headTabButton;
        [SerializeField] private Button bodyTabButton;
        [SerializeField] private Button accessoryTabButton;
        [SerializeField] private Button shoesTabButton;
        [SerializeField] private GameObject weaponTabRedDot;
        [SerializeField] private GameObject headTabRedDot;
        [SerializeField] private GameObject bodyTabRedDot;
        [SerializeField] private GameObject accessoryTabRedDot;
        [SerializeField] private GameObject shoesTabRedDot;

        [Header("Equipped Slots")]
        [SerializeField] private EquipmentSlotItemUI weaponSlot;
        [SerializeField] private EquipmentSlotItemUI headSlot;
        [SerializeField] private EquipmentSlotItemUI bodySlot;
        [SerializeField] private EquipmentSlotItemUI shoesSlot;
        [SerializeField] private EquipmentSlotItemUI accessorySlot1;
        [SerializeField] private EquipmentSlotItemUI accessorySlot2;

        [Header("Stats")]
        [SerializeField] private TMP_Text attackStatText;
        [SerializeField] private TMP_Text criticalRateStatText;
        [SerializeField] private TMP_Text criticalDamageStatText;
        [SerializeField] private TMP_Text skillDamageStatText;
        [SerializeField] private TMP_Text bossDamageStatText;

        private MainBattleLoop _battle;
        private readonly List<EquipmentSlotItemUI> _inventorySlots = new List<EquipmentSlotItemUI>();
        private readonly Dictionary<Filter, Button> _tabButtons = new Dictionary<Filter, Button>();
        private readonly Dictionary<Filter, GameObject> _tabRedDots = new Dictionary<Filter, GameObject>();
        private Filter _filter = Filter.Weapon;
        private bool _listenersBound;
        private bool _refreshPending;
        private Vector2 _characterStandBasePosition;
        private Vector3 _characterStandBaseScale;
        private Quaternion _characterStandBaseRotation;
        private bool _characterStandBaseCached;

        private void Awake()
        {
            CacheCharacterStandBase();
            BuildTabLookup();
            BindListeners();
            CacheInventorySlots();
        }

        private void OnEnable()
        {
            CacheCharacterStandBase();
            RefreshNow();
        }

        private void OnDisable()
        {
            ResetCharacterStandMotion();
        }

        public void RefreshNow()
        {
            BindListeners();
            RefreshAll();
            _refreshPending = true;
        }

        public void Bind(MainBattleLoop battle)
        {
            BindBattle(battle);
            RefreshAll();
        }

        private void BindBattle(MainBattleLoop battle)
        {
            if (_battle == battle)
                return;
            if (_battle != null)
                _battle.StateChanged -= RefreshAll;
            _battle = battle;
            if (_battle != null)
                _battle.StateChanged += RefreshAll;
        }

        private void BuildTabLookup()
        {
            _tabButtons.Clear();
            _tabRedDots.Clear();

            RegisterTab(Filter.Weapon, weaponTabButton, weaponTabRedDot);
            RegisterTab(Filter.Head, headTabButton, headTabRedDot);
            RegisterTab(Filter.Body, bodyTabButton, bodyTabRedDot);
            RegisterTab(Filter.Accessory, accessoryTabButton, accessoryTabRedDot);
            RegisterTab(Filter.Shoes, shoesTabButton, shoesTabRedDot);
        }

        private void RegisterTab(Filter filter, Button button, GameObject redDot)
        {
            if (button != null)
                _tabButtons[filter] = button;

            if (redDot != null)
            {
                _tabRedDots[filter] = redDot;
                SetActive(redDot, false);
            }
        }

        private void BindListeners()
        {
            if (_listenersBound)
                return;
            _listenersBound = true;

            foreach (KeyValuePair<Filter, Button> pair in _tabButtons)
            {
                Filter captured = pair.Key;
                pair.Value.onClick.AddListener(() => SelectFilter(captured));
            }

            mergeAllButton?.onClick.AddListener(UpgradeAll);
            autoEquipButton?.onClick.AddListener(AutoEquip);
            closeButton?.onClick.AddListener(Close);
        }

        private void SelectFilter(Filter filter)
        {
            _filter = filter;
            RefreshInventory();
            RefreshTabColors();
            RefreshCurrencies();
            RefreshActionRedDots();
        }

        private void RefreshActionRedDots()
        {
            EquipmentInventory inventory = _battle?.EquipmentInventory;
            SetActive(mergeAllRedDot,
                inventory != null && inventory.HasUpgradeableEquipment(GetSelectedEquipmentType()));
            SetActive(autoEquipRedDot,
                inventory != null && inventory.CanAutoEquipBetter(GetSelectedEquipmentType()));

            foreach (KeyValuePair<Filter, GameObject> pair in _tabRedDots)
                RefreshTabRedDot(pair.Key, inventory);
        }

        private void RefreshTabRedDot(Filter filter, EquipmentInventory inventory)
        {
            if (!_tabRedDots.TryGetValue(filter, out GameObject redDot))
                return;
            EquipmentType type = GetEquipmentType(filter);
            bool hasAction = inventory != null &&
                (inventory.HasUpgradeableEquipment(type) || inventory.CanAutoEquipBetter(type));
            SetActive(redDot, hasAction);
        }

        private void RefreshCurrencies()
        {
            if (_battle == null)
                return;
            if (goldText != null)
                goldText.text = MainHUDController.FormatNumber(_battle.Gold);
            if (gemText != null)
                gemText.text = MainHUDController.FormatGem(_battle.Gems);
        }

        private void UpgradeAll()
        {
            EquipmentInventory inventory = _battle?.EquipmentInventory;
            inventory?.TryUpgradeAll(GetSelectedEquipmentType());
            GameAudioManager.Instance.PlaySfx("SFX_Stat_Upgrade");
            SetActive(mergeAllRedDot, false);
            RefreshTabRedDot(_filter, inventory);
            Canvas.ForceUpdateCanvases();
            _refreshPending = true;
        }

        private EquipmentType GetSelectedEquipmentType() => GetEquipmentType(_filter);

        private static EquipmentType GetEquipmentType(Filter filter) => filter switch
        {
            Filter.Weapon => EquipmentType.Weapon,
            Filter.Head => EquipmentType.Head,
            Filter.Body => EquipmentType.Body,
            Filter.Accessory => EquipmentType.Accessory,
            Filter.Shoes => EquipmentType.Shoes,
            _ => EquipmentType.Weapon
        };

        private void AutoEquip()
        {
            EquipmentInventory inventory = _battle?.EquipmentInventory;
            inventory?.AutoEquipBest(GetSelectedEquipmentType());
            SetActive(autoEquipRedDot, false);
            RefreshTabRedDot(_filter, inventory);
            Canvas.ForceUpdateCanvases();
            _refreshPending = true;
        }

        private void LateUpdate()
        {
            UpdateCharacterStandMotion();

            if (_refreshPending)
            {
                _refreshPending = false;
                RefreshAll();
                return;
            }

            // 팝업 최초 활성화와 저장 데이터 초기화 순서가 달라도
            // 데이터가 준비되는 즉시 버튼/탭 레드닷이 표시되도록 계속 동기화한다.
            RefreshActionRedDots();
            RefreshCurrencies();
        }

        private void CacheCharacterStandBase()
        {
            if (characterStand == null || _characterStandBaseCached)
                return;

            _characterStandBasePosition = characterStand.anchoredPosition;
            _characterStandBaseScale = characterStand.localScale;
            _characterStandBaseRotation = characterStand.localRotation;
            _characterStandBaseCached = true;
        }

        private void UpdateCharacterStandMotion()
        {
            if (characterStand == null || !_characterStandBaseCached)
                return;

            float phase = Time.unscaledTime * characterBreathSpeed;
            float inhale = (Mathf.Sin(phase) + 1f) * .5f;
            inhale = Mathf.SmoothStep(0f, 1f, inhale);
            float breath = inhale * 2f - 1f;
            float scaleY = 1f + breath * characterBreathScaleAmount;
            float scaleX = 1f - breath * characterBreathScaleAmount * .18f;

            characterStand.localScale = Vector3.Scale(
                _characterStandBaseScale,
                new Vector3(scaleX, scaleY, 1f));

            // Compensate for center-pivot scaling so the character's feet remain planted.
            float footCompensation = characterStand.rect.height * characterStand.pivot.y *
                                     _characterStandBaseScale.y * (scaleY - 1f);
            float subtleBob = Mathf.Sin(phase * .5f + .7f) * characterBreathMoveAmount;
            characterStand.anchoredPosition = _characterStandBasePosition +
                                               Vector2.up * (footCompensation + subtleBob);
            float sway = Mathf.Sin(phase * .43f + 1.1f) * characterSwayAngle;
            characterStand.localRotation = _characterStandBaseRotation *
                                           Quaternion.Euler(0f, 0f, sway);
        }

        private void ResetCharacterStandMotion()
        {
            if (characterStand == null || !_characterStandBaseCached)
                return;

            characterStand.anchoredPosition = _characterStandBasePosition;
            characterStand.localScale = _characterStandBaseScale;
            characterStand.localRotation = _characterStandBaseRotation;
        }

        private void Close() => gameObject.SetActive(false);

        private void RefreshAll()
        {
            if (!isActiveAndEnabled)
                return;
            RefreshEquippedSlots();
            RefreshStats();
            RefreshInventory();
            RefreshTabColors();
        }

        private void RefreshEquippedSlots()
        {
            EquipmentInventory inventory = _battle?.EquipmentInventory;
            if (inventory == null)
                return;

            BindEquipped(weaponSlot, EquipmentSlot.Weapon);
            BindEquipped(headSlot, EquipmentSlot.Head);
            BindEquipped(bodySlot, EquipmentSlot.Body);
            BindEquipped(shoesSlot, EquipmentSlot.Shoes);
            BindEquipped(accessorySlot1, EquipmentSlot.Accessory1);
            BindEquipped(accessorySlot2, EquipmentSlot.Accessory2);
        }

        private void BindEquipped(EquipmentSlotItemUI ui, EquipmentSlot equipmentSlot)
        {
            if (ui == null || _battle?.EquipmentInventory == null)
                return;

            string id = _battle.EquipmentInventory.GetEquippedId(equipmentSlot);
            EquipmentData data = _battle.EquipmentInventory.Database?.Get(id);
            ui.Bind(data, _battle, EquipmentSlotDisplayMode.EquippedSlot);
        }

        private void RefreshStats()
        {
            if (_battle?.EquipmentInventory == null)
                return;

            EquipmentBonuses stats = _battle.EquipmentInventory.CalculateBonuses();
            SetStat(attackStatText, stats.attackPercent);
            SetStat(criticalRateStatText, stats.criticalChancePercentPoint);
            SetStat(criticalDamageStatText, stats.criticalDamagePercent);
            SetStat(skillDamageStatText, stats.skillDamagePercent);
            SetStat(bossDamageStatText, stats.bossDamagePercent);
        }

        private static void SetStat(TMP_Text text, float value)
        {
            if (text != null)
                text.text = $"{value:0.##}%";
        }

        private void RefreshInventory()
        {
            EquipmentInventory inventory = _battle?.EquipmentInventory;
            if (inventoryContent == null || inventory?.Database?.items == null)
                return;

            CacheInventorySlots();
            List<EquipmentData> visible = new List<EquipmentData>();
            foreach (EquipmentData data in inventory.Database.items)
            {
                EquipmentSaveEntry state = data != null ? inventory.GetState(data.Id) : null;
                if (data != null && state != null && state.level > 0 && MatchesFilter(data.type))
                    visible.Add(data);
            }

            visible.Sort((a, b) =>
            {
                int rarity = a.rarity.CompareTo(b.rarity);
                if (rarity != 0)
                    return rarity;

                int power = inventory.GetPowerScore(a).CompareTo(inventory.GetPowerScore(b));
                if (power != 0)
                    return power;

                int type = a.type.CompareTo(b.type);
                return type != 0 ? type : string.Compare(a.displayName, b.displayName, StringComparison.Ordinal);
            });

            EnsureInventorySlotCount(visible.Count);
            for (int i = 0; i < _inventorySlots.Count; i++)
            {
                bool active = i < visible.Count;
                _inventorySlots[i].gameObject.SetActive(active);
                if (active)
                    _inventorySlots[i].Bind(visible[i], _battle, EquipmentSlotDisplayMode.EquipmentPopup);
            }
        }

        private bool MatchesFilter(EquipmentType type) => _filter switch
        {
            Filter.Weapon => type == EquipmentType.Weapon,
            Filter.Head => type == EquipmentType.Head,
            Filter.Body => type == EquipmentType.Body,
            Filter.Accessory => type == EquipmentType.Accessory,
            Filter.Shoes => type == EquipmentType.Shoes,
            _ => true
        };

        private void CacheInventorySlots()
        {
            if (_inventorySlots.Count > 0 || inventoryContent == null)
                return;
            foreach (Transform child in inventoryContent)
            {
                EquipmentSlotItemUI item = child.GetComponent<EquipmentSlotItemUI>();
                if (item != null)
                    _inventorySlots.Add(item);
            }
        }

        private void EnsureInventorySlotCount(int count)
        {
            if (count <= _inventorySlots.Count || _inventorySlots.Count == 0)
                return;
            EquipmentSlotItemUI template = _inventorySlots[0];
            while (_inventorySlots.Count < count)
            {
                EquipmentSlotItemUI clone = Instantiate(template, inventoryContent);
                clone.name = $"InventorySlot ({_inventorySlots.Count})";
                _inventorySlots.Add(clone);
            }
        }

        private void RefreshTabColors()
        {
            foreach (KeyValuePair<Filter, Button> pair in _tabButtons)
            {
                Image image = pair.Value.image;
                if (image == null)
                    continue;
                image.color = pair.Key == _filter
                    ? Color.white
                    : TypeUnselected;
            }
        }

        private static void SetActive(GameObject target, bool active)
        {
            if (target != null && target.activeSelf != active)
                target.SetActive(active);
        }

        private void OnDestroy()
        {
            if (_battle != null)
                _battle.StateChanged -= RefreshAll;
        }
    }
}

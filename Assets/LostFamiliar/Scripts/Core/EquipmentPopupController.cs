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

        private MainBattleLoop _battle;
        private Transform _equippedRoot;
        private Transform _inventoryContent;
        private Transform _statPanel;
        private TMP_Text _goldText;
        private TMP_Text _gemText;
        private GameObject _mergeAllRedDot;
        private GameObject _autoEquipRedDot;
        private readonly List<EquipmentSlotItemUI> _inventorySlots = new List<EquipmentSlotItemUI>();
        private readonly Dictionary<Filter, Button> _tabButtons = new Dictionary<Filter, Button>();
        private readonly Dictionary<Filter, GameObject> _tabRedDots = new Dictionary<Filter, GameObject>();
        private Filter _filter = Filter.Weapon;
        private bool _listenersBound;
        private bool _refreshPending;

        private void Awake()
        {
            FindReferences();
            BindListeners();
        }

        private void OnEnable()
        {
            RefreshNow();
        }

        public void RefreshNow()
        {
            FindReferences();
            BindListeners();
            MainBattleLoop battle = FindFirstObjectByType<MainBattleLoop>();
            if (_battle != battle || _battle?.EquipmentInventory == null)
                BindBattle(battle);
            RefreshAll();
            _refreshPending = true;
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

        private void FindReferences()
        {
            _equippedRoot ??= FindDescendant("EquipSlotGroup");
            _statPanel ??= FindDescendant("StatPanel");
            Transform inventory = FindDescendant("Inventory");
            if (_inventoryContent == null && inventory != null)
                _inventoryContent = FindDescendant(inventory, "Content");

            Transform header = FindDescendant("Header");
            Transform goldPanel = FindDescendant(header, "GoldPanel");
            Transform gemPanel = FindDescendant(header, "GemPanel");
            _goldText ??= GetChild<TMP_Text>(goldPanel, "AmountText");
            _gemText ??= GetChild<TMP_Text>(gemPanel, "AmountText");

            Transform mergeButton = FindDescendant("Btn_MergeAll");
            Transform autoEquipButton = FindDescendant("Btn_AutoEquip");
            _mergeAllRedDot ??= FindDescendant(mergeButton, "Icon_RedDot")?.gameObject;
            _autoEquipRedDot ??= FindDescendant(autoEquipButton, "Icon_RedDot")?.gameObject;
            if (_battle == null)
            {
                SetActive(_mergeAllRedDot, false);
                SetActive(_autoEquipRedDot, false);
            }

            RegisterTab(Filter.Weapon, "Btn_Weapon");
            RegisterTab(Filter.Head, "Btn_Hat");
            RegisterTab(Filter.Body, "Btn_Armor");
            RegisterTab(Filter.Accessory, "Btn_Accessory");
            RegisterTab(Filter.Shoes, "Btn_Boots");
        }

        private void RegisterTab(Filter filter, string name)
        {
            if (!_tabButtons.ContainsKey(filter))
            {
                Transform target = FindDescendant(name);
                Button button = target != null ? target.GetComponent<Button>() : null;
                if (button != null)
                {
                    _tabButtons.Add(filter, button);
                    Transform redDot = FindDescendant(button.transform, "Icon_RedDot");
                    if (redDot != null)
                    {
                        _tabRedDots[filter] = redDot.gameObject;
                        SetActive(redDot.gameObject, false);
                    }
                }
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

            AddClick("Btn_MergeAll", UpgradeAll);
            AddClick("Btn_AutoEquip", AutoEquip);
            AddClick("Btn_Close", Close);
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
            SetActive(_mergeAllRedDot,
                inventory != null && inventory.HasUpgradeableEquipment(GetSelectedEquipmentType()));
            SetActive(_autoEquipRedDot,
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
            if (_goldText != null)
                _goldText.text = MainHUDController.FormatNumber(_battle.Gold);
            if (_gemText != null)
                _gemText.text = MainHUDController.FormatGem(_battle.Gems);
        }

        private void UpgradeAll()
        {
            EquipmentInventory inventory = _battle?.EquipmentInventory;
            inventory?.TryUpgradeAll(GetSelectedEquipmentType());
            SetActive(_mergeAllRedDot, false);
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
            SetActive(_autoEquipRedDot, false);
            RefreshTabRedDot(_filter, inventory);
            Canvas.ForceUpdateCanvases();
            _refreshPending = true;
        }

        private void LateUpdate()
        {
            if (_battle == null)
                BindBattle(FindFirstObjectByType<MainBattleLoop>());

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
            if (_equippedRoot == null || inventory == null)
                return;

            BindEquipped("Slot_Hat", EquipmentSlot.Head);
            BindEquipped("Slot_Armor", EquipmentSlot.Body);
            BindEquipped("Slot_Boots", EquipmentSlot.Shoes);
            BindEquipped("Slot_Accessory01", EquipmentSlot.Accessory1);
            BindEquipped("Slot_Accessory02", EquipmentSlot.Accessory2);
            BindEquipped("Slot_Weapon", EquipmentSlot.Weapon);
        }

        private void BindEquipped(string objectName, EquipmentSlot equipmentSlot)
        {
            Transform target = FindDescendant(_equippedRoot, objectName);
            EquipmentSlotItemUI ui = target != null ? target.GetComponent<EquipmentSlotItemUI>() : null;
            if (ui == null)
                return;

            string id = _battle.EquipmentInventory.GetEquippedId(equipmentSlot);
            EquipmentData data = _battle.EquipmentInventory.Database?.Get(id);
            ui.Bind(data, _battle, EquipmentSlotDisplayMode.EquippedSlot);
        }

        private void RefreshStats()
        {
            if (_statPanel == null || _battle?.EquipmentInventory == null)
                return;

            EquipmentBonuses stats = _battle.EquipmentInventory.CalculateBonuses();
            SetStat("Stat_Attack", stats.attackPercent);
            SetStat("Stat_CriticalRate", stats.criticalChancePercentPoint);
            SetStat("Stat_CriticalDamage", stats.criticalDamagePercent);
            SetStat("Stat_SkillDamage", stats.skillDamagePercent);
            SetStat("Stat_BossDamage", stats.bossDamagePercent);
        }

        private void SetStat(string rowName, float value)
        {
            Transform row = FindDescendant(_statPanel, rowName);
            Transform valueObject = row != null ? FindDescendant(row, "ValueText") : null;
            TMP_Text text = valueObject != null ? valueObject.GetComponent<TMP_Text>() : null;
            if (text != null)
                text.text = $"{value:0.##}%";
        }

        private void RefreshInventory()
        {
            EquipmentInventory inventory = _battle?.EquipmentInventory;
            if (_inventoryContent == null || inventory?.Database?.items == null)
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
            if (_inventorySlots.Count > 0)
                return;
            _inventorySlots.AddRange(_inventoryContent.GetComponentsInChildren<EquipmentSlotItemUI>(true));
        }

        private void EnsureInventorySlotCount(int count)
        {
            if (count <= _inventorySlots.Count || _inventorySlots.Count == 0)
                return;
            EquipmentSlotItemUI template = _inventorySlots[0];
            while (_inventorySlots.Count < count)
            {
                EquipmentSlotItemUI clone = Instantiate(template, _inventoryContent);
                clone.name = $"EquipSlot ({_inventorySlots.Count})";
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

        private void AddClick(string objectName, UnityEngine.Events.UnityAction action)
        {
            Transform target = FindDescendant(objectName);
            Button button = target != null ? target.GetComponent<Button>() : null;
            button?.onClick.AddListener(action);
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
                _battle.StateChanged -= RefreshAll;
        }
    }
}

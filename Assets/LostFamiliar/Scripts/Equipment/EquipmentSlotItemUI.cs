using LostFamiliar.Core;
using UnityEngine;

namespace LostFamiliar.Battle
{
    public enum EquipmentSlotDisplayMode { EquipmentPopup, SummonPopup, EquippedSlot }

    [DisallowMultipleComponent]
    [RequireComponent(typeof(InventorySlotView))]
    public sealed class EquipmentSlotItemUI : MonoBehaviour
    {
        [SerializeField] private EquipmentData equipmentData;
        [SerializeField] private EquipmentSlotDisplayMode displayMode = EquipmentSlotDisplayMode.EquipmentPopup;

        private MainBattleLoop _battle;
        private InventorySlotView _view;

        public EquipmentData Data => equipmentData;
        public EquipmentSlotDisplayMode DisplayMode => displayMode;

        private void Awake() => _view = GetComponent<InventorySlotView>();

        private void OnEnable()
        {
            if (_battle == null && Application.isPlaying)
                BindBattle(FindFirstObjectByType<MainBattleLoop>());
            Refresh();
        }

        public void Bind(EquipmentData data, MainBattleLoop battle,
            EquipmentSlotDisplayMode mode = EquipmentSlotDisplayMode.EquipmentPopup)
        {
            equipmentData = data;
            displayMode = mode;
            BindBattle(battle);
            Refresh();
        }

        public void SetData(EquipmentData data) { equipmentData = data; Refresh(); }
        public void SetDisplayMode(EquipmentSlotDisplayMode mode) { displayMode = mode; Refresh(); }

        public void Refresh()
        {
            _view ??= GetComponent<InventorySlotView>();
            if (_view == null) return;
            if (equipmentData == null) { _view.Clear(); return; }

            EquipmentInventory inventory = _battle?.EquipmentInventory;
            EquipmentSaveEntry state = inventory?.GetState(equipmentData.Id);
            int level = state?.level ?? 0;
            int duplicates = state?.duplicates ?? 0;
            int required = level > 0 ? EquipmentBalance.DuplicateRequirement(level) : 1;
            bool isMax = level >= equipmentData.maxLevel;
            bool showLevel = displayMode != EquipmentSlotDisplayMode.SummonPopup;
            bool showProgress = displayMode == EquipmentSlotDisplayMode.EquipmentPopup;
            bool equipped = displayMode == EquipmentSlotDisplayMode.EquipmentPopup &&
                            inventory != null && inventory.IsEquipped(equipmentData.Id);

            _view.Render(
                equipmentData.icon,
                EquipmentBalance.RarityColor(equipmentData.rarity),
                showLevel,
                level,
                showProgress,
                duplicates,
                required,
                isMax,
                showLevel && inventory != null && inventory.CanUpgrade(equipmentData.Id),
                equipped);
        }

        private void BindBattle(MainBattleLoop battle)
        {
            if (_battle == battle) return;
            if (_battle != null) _battle.StateChanged -= Refresh;
            _battle = battle;
            if (_battle != null) _battle.StateChanged += Refresh;
        }

        private void OnDestroy()
        {
            if (_battle != null) _battle.StateChanged -= Refresh;
        }
    }
}

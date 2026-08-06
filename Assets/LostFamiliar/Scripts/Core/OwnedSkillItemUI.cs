using LostFamiliar.Core;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace LostFamiliar.Battle
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(InventorySlotView))]
    public sealed class OwnedSkillItemUI : MonoBehaviour
    {
        private SkillData _skill;
        private MainBattleLoop _battle;
        private SkillPopupController _popup;
        private InventorySlotView _view;
        private Button _button;
        private UnityAction _clickAction;

        private void Awake()
        {
            _view = GetComponent<InventorySlotView>();
            _button = GetComponent<Button>();
        }

        public void Bind(SkillData skill, MainBattleLoop battle, SkillPopupController popup)
        {
            _skill = skill;
            _battle = battle;
            _popup = popup;
            _view ??= GetComponent<InventorySlotView>();
            _button ??= GetComponent<Button>();

            if (_button != null && _clickAction != null)
                _button.onClick.RemoveListener(_clickAction);
            _clickAction = () => _popup?.SelectSkill(_skill);
            _button?.onClick.AddListener(_clickAction);
            Refresh();
        }

        public void Refresh()
        {
            _view ??= GetComponent<InventorySlotView>();
            if (_view == null) return;
            if (_skill == null) { _view.Clear(); return; }

            SkillSaveEntry state = _battle?.GetSkillState(_skill.id);
            int level = state?.level ?? 0;
            int duplicates = state?.duplicates ?? 0;
            int required = level > 0 ? SkillBalance.DuplicateRequirement(level) : 1;

            _view.Render(
                _skill.icon,
                EquipmentBalance.RarityColor(_skill.rarity),
                true,
                level,
                true,
                duplicates,
                required,
                level >= _skill.maxLevel,
                _battle != null && _battle.CanUpgradeSkill(_skill.id),
                false);
        }

        private void OnDestroy()
        {
            if (_button != null && _clickAction != null)
                _button.onClick.RemoveListener(_clickAction);
        }
    }
}

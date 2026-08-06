using LostFamiliar.Core;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace LostFamiliar.Battle
{
    [DisallowMultipleComponent]
    public sealed class EquippedSkillPopupSlotUI : MonoBehaviour
    {
        private MainBattleLoop _battle;
        private SkillPopupController _popup;
        private int _slotIndex;
        private Button _button;
        private Image _background;
        private Image _skillIcon;
        private GameObject _plusIcon;
        private Vector3 _plusIconBaseScale = Vector3.one;
        private GameObject _lockIcon;
        private RectTransform _selectIcon;
        private Vector2 _selectBasePosition;
        private UnityAction _clickAction;
        private bool _selecting;

        public void Bind(MainBattleLoop battle, SkillPopupController popup, int slotIndex)
        {
            _battle = battle;
            _popup = popup;
            _slotIndex = slotIndex;
            FindReferences();
            if (_button != null && _clickAction != null)
                _button.onClick.RemoveListener(_clickAction);
            _clickAction = () => _popup?.SelectReplacementSlot(_slotIndex);
            _button?.onClick.AddListener(_clickAction);
            Refresh();
        }

        public void Refresh()
        {
            FindReferences();
            bool unlocked = _battle != null && _battle.IsSkillSlotUnlocked(_slotIndex);
            SkillData skill = unlocked ? _battle.GetEquippedSkill(_slotIndex) : null;
            bool equipped = skill != null;
            _selecting = unlocked && _popup != null && _popup.IsSelectingReplacement;

            if (_background != null)
                _background.color = equipped ? EquipmentBalance.RarityColor(skill.rarity) : Color.white;
            if (_skillIcon != null)
            {
                _skillIcon.sprite = equipped ? skill.icon : null;
                _skillIcon.enabled = equipped && skill.icon != null;
                _skillIcon.preserveAspect = true;
            }
            SetActive(_plusIcon, unlocked && !equipped);
            SetActive(_lockIcon, !unlocked);
            SetActive(_selectIcon != null ? _selectIcon.gameObject : null, _selecting);
            if (_button != null) _button.interactable = unlocked && (_selecting || equipped);
        }

        private void Update()
        {
            if (_selecting && _selectIcon != null)
                _selectIcon.anchoredPosition = _selectBasePosition + Vector2.up * (Mathf.Sin(Time.unscaledTime * 5f) * 7f);

            if (_plusIcon == null)
                return;
            if (!_plusIcon.activeInHierarchy)
            {
                _plusIcon.transform.localScale = _plusIconBaseScale;
                return;
            }

            float pulse = (Mathf.Sin(Time.unscaledTime * 5f) + 1f) * .5f;
            _plusIcon.transform.localScale = _plusIconBaseScale * Mathf.Lerp(.82f, 1.12f, pulse);
        }

        private void FindReferences()
        {
            _button ??= GetComponent<Button>();
            _background ??= Find<Image>("BG");
            _skillIcon ??= Find<Image>("SkillIconImage");
            if (_plusIcon == null)
            {
                _plusIcon = (FindTransform("PlusIconmage") ?? FindTransform("PlusIconImage"))?.gameObject;
                if (_plusIcon != null)
                    _plusIconBaseScale = _plusIcon.transform.localScale;
            }
            _lockIcon ??= FindTransform("LockIconImage")?.gameObject;
            if (_selectIcon == null)
            {
                _selectIcon = FindTransform("SelectIconImage") as RectTransform;
                if (_selectIcon != null) _selectBasePosition = _selectIcon.anchoredPosition;
            }
        }

        private T Find<T>(string name) where T : Component => FindTransform(name)?.GetComponent<T>();
        private Transform FindTransform(string name) => SkillBarController.FindDescendant(transform, name);
        private static void SetActive(GameObject target, bool active) { if (target != null && target.activeSelf != active) target.SetActive(active); }

        private void OnDisable()
        {
            if (_selectIcon != null) _selectIcon.anchoredPosition = _selectBasePosition;
            if (_plusIcon != null) _plusIcon.transform.localScale = _plusIconBaseScale;
        }

        private void OnDestroy()
        {
            if (_button != null && _clickAction != null)
                _button.onClick.RemoveListener(_clickAction);
        }
    }
}

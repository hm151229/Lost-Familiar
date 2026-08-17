using LostFamiliar.Core;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace LostFamiliar.Battle
{
    [DisallowMultipleComponent]
    public sealed class EquippedSkillPopupSlotUI : MonoBehaviour
    {
        [SerializeField] private Button button;
        [SerializeField] private Image background;
        [SerializeField] private Image skillIcon;
        [SerializeField] private GameObject plusIcon;
        [SerializeField] private GameObject lockIcon;
        [SerializeField] private RectTransform selectIcon;

        private MainBattleLoop _battle;
        private SkillPopupController _popup;
        private int _slotIndex;
        private Vector3 _plusIconBaseScale = Vector3.one;
        private Vector2 _selectBasePosition;
        private UnityAction _clickAction;
        private bool _selecting;

        private void Awake()
        {
            if (plusIcon != null)
                _plusIconBaseScale = plusIcon.transform.localScale;

            if (selectIcon != null)
                _selectBasePosition = selectIcon.anchoredPosition;
        }

        public void Bind(MainBattleLoop battle, SkillPopupController popup, int slotIndex)
        {
            _battle = battle;
            _popup = popup;
            _slotIndex = slotIndex;
            if (button != null && _clickAction != null)
                button.onClick.RemoveListener(_clickAction);
            _clickAction = () => _popup?.SelectReplacementSlot(_slotIndex);
            button?.onClick.AddListener(_clickAction);
            Refresh();
        }

        public void Refresh()
        {
            bool unlocked = _battle != null && _battle.IsSkillSlotUnlocked(_slotIndex);
            SkillData skill = unlocked ? _battle.GetEquippedSkill(_slotIndex) : null;
            bool equipped = skill != null;
            _selecting = unlocked && _popup != null && _popup.IsSelectingReplacement;

            if (background != null)
                background.color = equipped ? EquipmentBalance.RarityColor(skill.rarity) : Color.white;
            if (skillIcon != null)
            {
                skillIcon.sprite = equipped ? skill.icon : null;
                skillIcon.enabled = equipped && skill.icon != null;
                skillIcon.preserveAspect = true;
            }
            SetActive(plusIcon, unlocked && !equipped);
            SetActive(lockIcon, !unlocked);
            SetActive(selectIcon != null ? selectIcon.gameObject : null, _selecting);
            if (button != null) button.interactable = unlocked && (_selecting || equipped);
        }

        private void Update()
        {
            if (_selecting && selectIcon != null)
                selectIcon.anchoredPosition = _selectBasePosition + Vector2.up * (Mathf.Sin(Time.unscaledTime * 5f) * 7f);

            if (plusIcon == null)
                return;
            if (!plusIcon.activeInHierarchy)
            {
                plusIcon.transform.localScale = _plusIconBaseScale;
                return;
            }

            float pulse = (Mathf.Sin(Time.unscaledTime * 5f) + 1f) * .5f;
            plusIcon.transform.localScale = _plusIconBaseScale * Mathf.Lerp(.82f, 1.12f, pulse);
        }

        private static void SetActive(GameObject target, bool active) { if (target != null && target.activeSelf != active) target.SetActive(active); }

        private void OnDisable()
        {
            if (selectIcon != null) selectIcon.anchoredPosition = _selectBasePosition;
            if (plusIcon != null) plusIcon.transform.localScale = _plusIconBaseScale;
        }

        private void OnDestroy()
        {
            if (button != null && _clickAction != null)
                button.onClick.RemoveListener(_clickAction);
        }
    }
}

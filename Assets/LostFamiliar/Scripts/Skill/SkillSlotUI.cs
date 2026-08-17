using LostFamiliar.Core;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace LostFamiliar.Battle
{
    [DisallowMultipleComponent]
    public sealed class SkillSlotUI : MonoBehaviour
    {
        [SerializeField] private Button button;
        [SerializeField] private Image iconImage;
        [SerializeField] private Image iconMask;
        [SerializeField] private Image coolTimeMaskImage;
        [SerializeField] private GameObject plusIcon;
        [SerializeField] private GameObject lockIcon;

        private MainBattleLoop _battle;
        private SkillPopupBridge _popup;
        private int _slotIndex;
        private Vector3 _plusIconBaseScale = Vector3.one;
        private UnityAction _clickAction;
        private bool _showCooldown;
        private PlayerAutoCombat _cooldownPlayer;
        private Color _iconMaskDefaultColor = Color.white;

        private void Awake()
        {
            if (iconMask != null)
                _iconMaskDefaultColor = iconMask.color;

            if (plusIcon != null)
                _plusIconBaseScale = plusIcon.transform.localScale;
        }

        public void Bind(MainBattleLoop battle, SkillPopupBridge popup, int slotIndex,
            PlayerAutoCombat cooldownPlayer = null)
        {
            _battle = battle;
            _popup = popup;
            _slotIndex = slotIndex;
            _cooldownPlayer = cooldownPlayer ?? battle?.Player;

            if (button != null && _clickAction != null)
                button.onClick.RemoveListener(_clickAction);

            _clickAction = OpenPopup;
            button?.onClick.AddListener(_clickAction);
            Refresh();
        }

        public void Refresh()
        {
            bool unlocked = _battle != null && _battle.IsSkillSlotUnlocked(_slotIndex);
            SkillData skill = unlocked ? _battle.GetEquippedSkill(_slotIndex) : null;
            bool equipped = skill != null;

            if (button != null) button.interactable = unlocked;
            if (lockIcon != null) lockIcon.SetActive(!unlocked);
            if (plusIcon != null) plusIcon.SetActive(unlocked && !equipped);
            if (iconMask != null)
            {
                iconMask.gameObject.SetActive(!unlocked || equipped);
                iconMask.color = equipped
                    ? EquipmentBalance.RarityColor(skill.rarity)
                    : _iconMaskDefaultColor;
            }
            if (iconImage != null)
            {
                iconImage.gameObject.SetActive(equipped);
                iconImage.sprite = equipped ? skill.icon : null;
                iconImage.preserveAspect = true;
            }

            _showCooldown = equipped;
            UpdateCooldown();
        }

        private void Update()
        {
            if (_showCooldown)
                UpdateCooldown();
            UpdatePlusIconPulse();
        }

        private void UpdatePlusIconPulse()
        {
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

        private void UpdateCooldown()
        {
            if (coolTimeMaskImage == null)
                return;
            float fill = _showCooldown && _cooldownPlayer != null
                ? 1f - _cooldownPlayer.GetSkillCooldown01(_slotIndex)
                : 0f;
            coolTimeMaskImage.fillAmount = Mathf.Clamp01(fill);
            coolTimeMaskImage.gameObject.SetActive(_showCooldown && fill > .001f);
        }

        private void OpenPopup()
        {
            if (_battle == null || !_battle.IsSkillSlotUnlocked(_slotIndex))
                return;
            _popup?.Open(_slotIndex);
        }

        private void OnDestroy()
        {
            if (button != null && _clickAction != null)
                button.onClick.RemoveListener(_clickAction);
        }

        private void OnDisable()
        {
            if (plusIcon != null)
                plusIcon.transform.localScale = _plusIconBaseScale;
        }
    }
}

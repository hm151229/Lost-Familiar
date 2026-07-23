using LostFamiliar.Core;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace LostFamiliar.Battle
{
    [DisallowMultipleComponent]
    public sealed class SkillBarController : MonoBehaviour
    {
        private MainBattleLoop _battle;
        private SkillSlotUI[] _slots;
        private SkillPopupBridge _popup;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void InstallOnSceneLoad()
        {
            Install(Object.FindFirstObjectByType<MainBattleLoop>());
        }

        private void Start()
        {
            if (_battle == null)
                Bind(Object.FindFirstObjectByType<MainBattleLoop>());
        }

        public static void Install(MainBattleLoop battle)
        {
            GameObject skillUi = FindSceneObject("SkillUI");
            if (skillUi == null)
                return;

            SkillBarController controller = skillUi.GetComponent<SkillBarController>();
            if (controller == null)
                controller = skillUi.AddComponent<SkillBarController>();
            controller.Bind(battle);
        }

        public void Bind(MainBattleLoop battle)
        {
            if (_battle != null)
                _battle.StateChanged -= Refresh;
            _battle = battle;
            if (_battle != null)
                _battle.StateChanged += Refresh;

            GameObject popupObject = FindSceneObject("SkillPopup");
            if (popupObject != null)
            {
                _popup = popupObject.GetComponent<SkillPopupBridge>();
                if (_popup == null)
                    _popup = popupObject.AddComponent<SkillPopupBridge>();
                _popup.Bind(_battle);
            }

            _slots = new SkillSlotUI[SkillBalance.MaxEquippedSkillCount];
            for (int i = 0; i < _slots.Length; i++)
            {
                Transform slotTransform = FindDescendant(transform, $"SkillSlot{i + 1:00}");
                if (slotTransform == null)
                    continue;
                SkillSlotUI slot = slotTransform.GetComponent<SkillSlotUI>();
                if (slot == null)
                    slot = slotTransform.gameObject.AddComponent<SkillSlotUI>();
                slot.Bind(_battle, _popup, i);
                _slots[i] = slot;
            }
            Refresh();
        }

        private void Refresh()
        {
            if (_slots == null)
                return;
            foreach (SkillSlotUI slot in _slots)
                slot?.Refresh();
        }

        internal static GameObject FindSceneObject(string objectName)
        {
            foreach (Transform candidate in Resources.FindObjectsOfTypeAll<Transform>())
            {
                GameObject sceneObject = candidate.gameObject;
                if (candidate.name == objectName && sceneObject.scene.IsValid() && sceneObject.scene.isLoaded)
                    return sceneObject;
            }
            return null;
        }

        internal static Transform FindDescendant(Transform root, string objectName)
        {
            if (root == null)
                return null;
            foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
                if (child.name == objectName) return child;
            return null;
        }

        private void OnDestroy()
        {
            if (_battle != null)
                _battle.StateChanged -= Refresh;
        }
    }

    [DisallowMultipleComponent]
    public sealed class SkillSlotUI : MonoBehaviour
    {
        private MainBattleLoop _battle;
        private SkillPopupBridge _popup;
        private int _slotIndex;
        private Button _button;
        private Image _iconImage;
        private Image _iconMask;
        private Image _coolTimeMaskImage;
        private GameObject _plusIconImage;
        private GameObject _lockIconImage;
        private UnityAction _clickAction;
        private bool _showCooldown;

        public void Bind(MainBattleLoop battle, SkillPopupBridge popup, int slotIndex)
        {
            _battle = battle;
            _popup = popup;
            _slotIndex = slotIndex;
            FindReferences();

            if (_button != null && _clickAction != null)
                _button.onClick.RemoveListener(_clickAction);
            _clickAction = OpenPopup;
            _button?.onClick.AddListener(_clickAction);
            Refresh();
        }

        private void FindReferences()
        {
            _button ??= GetComponent<Button>();
            _iconImage ??= GetImage("IconImage");
            _iconMask ??= GetImage("IconMask");
            _coolTimeMaskImage ??= GetImage("CoolTimeMaskImage");
            _plusIconImage ??= SkillBarController.FindDescendant(transform, "PlusIconImage")?.gameObject;
            _lockIconImage ??= SkillBarController.FindDescendant(transform, "LockIconImage")?.gameObject;
        }

        public void Refresh()
        {
            FindReferences();
            bool unlocked = _battle != null && _battle.IsSkillSlotUnlocked(_slotIndex);
            SkillData skill = unlocked ? _battle.GetEquippedSkill(_slotIndex) : null;
            bool equipped = skill != null;

            if (_button != null) _button.interactable = unlocked;
            if (_lockIconImage != null) _lockIconImage.SetActive(!unlocked);
            if (_plusIconImage != null) _plusIconImage.SetActive(unlocked && !equipped);
            if (_iconMask != null)
            {
                _iconMask.gameObject.SetActive(!unlocked || equipped);
                if (equipped)
                    _iconMask.color = EquipmentBalance.RarityColor(skill.rarity);
            }
            if (_iconImage != null)
            {
                _iconImage.gameObject.SetActive(equipped);
                _iconImage.sprite = equipped ? skill.icon : null;
                _iconImage.preserveAspect = true;
            }

            _showCooldown = equipped;
            UpdateCooldown();
        }

        private void Update()
        {
            if (_showCooldown)
                UpdateCooldown();
        }

        private void UpdateCooldown()
        {
            if (_coolTimeMaskImage == null)
                return;
            float fill = _showCooldown && _battle?.Player != null
                ? 1f - _battle.Player.GetSkillCooldown01(_slotIndex)
                : 0f;
            _coolTimeMaskImage.type = Image.Type.Filled;
            _coolTimeMaskImage.fillAmount = Mathf.Clamp01(fill);
            _coolTimeMaskImage.gameObject.SetActive(_showCooldown && fill > .001f);
        }

        private void OpenPopup()
        {
            if (_battle == null || !_battle.IsSkillSlotUnlocked(_slotIndex))
                return;
            _popup?.Open(_slotIndex);
        }

        private Image GetImage(string objectName)
        {
            Transform child = SkillBarController.FindDescendant(transform, objectName);
            return child != null ? child.GetComponent<Image>() : null;
        }

        private void OnDestroy()
        {
            if (_button != null && _clickAction != null)
                _button.onClick.RemoveListener(_clickAction);
        }
    }

    [DisallowMultipleComponent]
    public sealed class SkillPopupBridge : MonoBehaviour
    {
        public int SelectedSlotIndex { get; private set; } = -1;
        public MainBattleLoop Battle { get; private set; }

        private Button _closeButton;
        private UnityAction _closeAction;

        public void Bind(MainBattleLoop battle)
        {
            Battle = battle;
            Transform close = SkillBarController.FindDescendant(transform, "Btn_Close");
            _closeButton = close != null ? close.GetComponent<Button>() : null;
            if (_closeButton != null && _closeAction == null)
            {
                _closeAction = Close;
                _closeButton.onClick.AddListener(_closeAction);
            }
        }

        public void Open(int slotIndex)
        {
            SelectedSlotIndex = slotIndex;
            gameObject.SetActive(true);
        }

        public void Close() => gameObject.SetActive(false);

        private void OnDestroy()
        {
            if (_closeButton != null && _closeAction != null)
                _closeButton.onClick.RemoveListener(_closeAction);
        }
    }
}

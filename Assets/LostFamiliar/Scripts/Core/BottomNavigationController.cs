using UnityEngine;
using UnityEngine.UI;

namespace LostFamiliar.Battle
{
    [DisallowMultipleComponent]
    public sealed class BottomNavigationController : MonoBehaviour
    {
        [Header("하단 메뉴 버튼")]
        [SerializeField] private Button summonButton;
        [SerializeField] private Button equipmentButton;
        [SerializeField] private Button upgradeButton;
        [SerializeField] private Button adventureButton;

        [Header("연결 팝업")]
        [SerializeField] private GameObject summonPopup;
        [SerializeField] private GameObject equipmentPopup;
        [SerializeField] private GameObject upgradePopup;
        [SerializeField] private GameObject adventurePopup;

        [Header("선택 표현")]
        [SerializeField, Min(1f)] private float normalHeight = 200f;
        [SerializeField, Min(1f)] private float selectedHeight = 230f;
        [SerializeField] private Color normalColor = new Color32(0x9C, 0x95, 0x91, 0xFF);
        [SerializeField] private Color selectedColor = Color.white;

        private Button[] _buttons;
        private GameObject[] _popups;
        private bool _initialized;

        private void Awake() => Initialize();

        private void Initialize()
        {
            if (_initialized)
                return;

            _initialized = true;
            AutoFindReferences();
            _buttons = new[] { summonButton, equipmentButton, upgradeButton, adventureButton };
            _popups = new[] { summonPopup, equipmentPopup, upgradePopup, adventurePopup };

            if (summonButton != null)
                summonButton.onClick.AddListener(OpenSummon);
            if (equipmentButton != null)
                equipmentButton.onClick.AddListener(OpenEquipment);
            if (upgradeButton != null)
                upgradeButton.onClick.AddListener(OpenUpgrade);
            if (adventureButton != null)
                adventureButton.onClick.AddListener(OpenAdventure);

            CloseAll();
        }

        private void LateUpdate() => RefreshVisuals();

        public void CloseAll()
        {
            if (_popups == null)
                return;

            foreach (GameObject popup in _popups)
            {
                if (popup != null)
                    popup.SetActive(false);
            }
            RefreshVisuals();
        }

        private void OpenSummon() => OpenOnly(0);
        private void OpenEquipment()
        {
            OpenOnly(1);
            equipmentPopup?.GetComponent<EquipmentPopupController>()?.RefreshNow();
        }
        private void OpenUpgrade() => OpenOnly(2);
        private void OpenAdventure() => OpenOnly(3);

        private void OpenOnly(int selectedIndex)
        {
            for (int i = 0; i < _popups.Length; i++)
            {
                if (_popups[i] != null)
                    _popups[i].SetActive(i == selectedIndex);
            }
            RefreshVisuals();
        }

        private void RefreshVisuals()
        {
            if (_buttons == null || _popups == null)
                return;

            for (int i = 0; i < _buttons.Length; i++)
            {
                bool selected = _popups[i] != null && _popups[i].activeInHierarchy;
                ApplyVisual(_buttons[i], selected);
            }
        }

        private void ApplyVisual(Button button, bool selected)
        {
            if (button == null)
                return;

            RectTransform rect = button.transform as RectTransform;
            if (rect != null)
                rect.SetSizeWithCurrentAnchors(
                    RectTransform.Axis.Vertical,
                    selected ? selectedHeight : normalHeight);

            Image image = button.GetComponent<Image>();
            if (image != null)
                image.color = selected ? selectedColor : normalColor;
        }

        private void AutoFindReferences()
        {
            summonButton ??= FindSceneComponent<Button>("SummonButton");
            equipmentButton ??= FindSceneComponent<Button>("EquipmentButton");
            upgradeButton ??= FindSceneComponent<Button>("UpgradeButton");
            adventureButton ??= FindSceneComponent<Button>("AdventureButton");
            summonPopup ??= FindSceneObject("SummonPopup");
            equipmentPopup ??= FindSceneObject("EquipmentPopup");
            upgradePopup ??= FindSceneObject("UpgradePopup");
            adventurePopup ??= FindSceneObject("AdventurePopup");
        }

        private static T FindSceneComponent<T>(string objectName) where T : Component
        {
            GameObject sceneObject = FindSceneObject(objectName);
            return sceneObject != null ? sceneObject.GetComponent<T>() : null;
        }

        private static GameObject FindSceneObject(string objectName)
        {
            foreach (Transform candidate in Resources.FindObjectsOfTypeAll<Transform>())
            {
                if (candidate.name == objectName && candidate.gameObject.scene.IsValid() &&
                    candidate.gameObject.scene.isLoaded)
                    return candidate.gameObject;
            }
            return null;
        }

        private void OnDestroy()
        {
            if (summonButton != null)
                summonButton.onClick.RemoveListener(OpenSummon);
            if (equipmentButton != null)
                equipmentButton.onClick.RemoveListener(OpenEquipment);
            if (upgradeButton != null)
                upgradeButton.onClick.RemoveListener(OpenUpgrade);
            if (adventureButton != null)
                adventureButton.onClick.RemoveListener(OpenAdventure);
        }
    }
}

using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace LostFamiliar.Battle
{
    [DisallowMultipleComponent]
    public sealed class SideMenuPopupController : MonoBehaviour
    {
        [Serializable]
        private sealed class PopupBinding
        {
            public string buttonObjectName;
            public string popupObjectName;
            [NonSerialized] public Button openButton;
            [NonSerialized] public Button closeButton;
            [NonSerialized] public GameObject popup;
            [NonSerialized] public UnityAction openAction;
            [NonSerialized] public UnityAction closeAction;
        }

        private static readonly (string button, string popup)[] DefaultBindings =
        {
            ("AttendanceButton", "AttendancePopup"),
            ("CollectionButton", "CollectionPopup"),
            ("SettingButton", "SettingPopup"),
            ("ShopButton", "ShopPopup"),
            ("EventButton", "EventPopup"),
            ("MailButton", "MailPopup"),
            ("MissionButton", "MissionPopup"),
            ("PassButton", "PassPopup")
        };

        [SerializeField] private List<PopupBinding> bindings = new List<PopupBinding>();

        private bool _initialized;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            GameObject popupRoot = FindSceneObject("Popup");
            if (popupRoot == null)
                return;

            SideMenuPopupController controller = popupRoot.GetComponent<SideMenuPopupController>();
            if (controller == null)
                controller = popupRoot.AddComponent<SideMenuPopupController>();

            controller.Initialize();
        }

        private void Awake() => Initialize();

        private void Initialize()
        {
            if (_initialized)
                return;

            _initialized = true;
            CreateDefaultBindingsIfNeeded();

            foreach (PopupBinding binding in bindings)
            {
                if (binding == null)
                    continue;

                binding.openButton = FindSceneComponent<Button>(binding.buttonObjectName);
                binding.popup = FindSceneObject(binding.popupObjectName);
                binding.closeButton = FindDescendantComponent<Button>(binding.popup, "Btn_Close");

                if (binding.openButton != null && binding.popup != null)
                {
                    GameObject targetPopup = binding.popup;
                    binding.openAction = () => OpenOnly(targetPopup);
                    binding.openButton.onClick.AddListener(binding.openAction);
                }

                if (binding.closeButton != null && binding.popup != null)
                {
                    GameObject targetPopup = binding.popup;
                    binding.closeAction = () => targetPopup.SetActive(false);
                    binding.closeButton.onClick.AddListener(binding.closeAction);
                }

                if (binding.popup != null)
                    binding.popup.SetActive(false);
            }
        }

        private void CreateDefaultBindingsIfNeeded()
        {
            if (bindings.Count > 0)
                return;

            foreach ((string button, string popup) in DefaultBindings)
            {
                bindings.Add(new PopupBinding
                {
                    buttonObjectName = button,
                    popupObjectName = popup
                });
            }
        }

        private void OpenOnly(GameObject selectedPopup)
        {
            foreach (PopupBinding binding in bindings)
            {
                if (binding?.popup != null)
                    binding.popup.SetActive(binding.popup == selectedPopup);
            }
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
                GameObject sceneObject = candidate.gameObject;
                if (candidate.name == objectName && sceneObject.scene.IsValid() && sceneObject.scene.isLoaded)
                    return sceneObject;
            }

            return null;
        }

        private static T FindDescendantComponent<T>(GameObject root, string objectName) where T : Component
        {
            if (root == null)
                return null;

            foreach (T component in root.GetComponentsInChildren<T>(true))
            {
                if (component.name == objectName)
                    return component;
            }

            return null;
        }

        private void OnDestroy()
        {
            foreach (PopupBinding binding in bindings)
            {
                if (binding == null)
                    continue;

                if (binding.openButton != null && binding.openAction != null)
                    binding.openButton.onClick.RemoveListener(binding.openAction);
                if (binding.closeButton != null && binding.closeAction != null)
                    binding.closeButton.onClick.RemoveListener(binding.closeAction);
            }
        }
    }
}

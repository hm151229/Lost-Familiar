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
            [SerializeField] private Button openButton;
            [SerializeField] private Button closeButton;
            [SerializeField] private GameObject popup;

            [NonSerialized] public UnityAction openAction;
            [NonSerialized] public UnityAction closeAction;

            public Button OpenButton => openButton;
            public Button CloseButton => closeButton;
            public GameObject Popup => popup;
        }

        [SerializeField] private List<PopupBinding> bindings = new List<PopupBinding>();

        private void Awake()
        {
            BindPopups();
            CloseAll();
        }

        private void BindPopups()
        {
            foreach (PopupBinding binding in bindings)
            {
                if (binding == null)
                    continue;

                if (binding.OpenButton != null && binding.Popup != null)
                {
                    GameObject targetPopup = binding.Popup;
                    binding.openAction = () => OpenOnly(targetPopup);
                    binding.OpenButton.onClick.AddListener(binding.openAction);
                }

                if (binding.CloseButton != null && binding.Popup != null)
                {
                    GameObject targetPopup = binding.Popup;
                    binding.closeAction = () => targetPopup.SetActive(false);
                    binding.CloseButton.onClick.AddListener(binding.closeAction);
                }
            }
        }

        private void CloseAll()
        {
            foreach (PopupBinding binding in bindings)
                if (binding?.Popup != null)
                    binding.Popup.SetActive(false);
        }

        private void OpenOnly(GameObject selectedPopup)
        {
            foreach (PopupBinding binding in bindings)
            {
                if (binding?.Popup != null)
                    binding.Popup.SetActive(binding.Popup == selectedPopup);
            }
        }

        private void OnDestroy()
        {
            foreach (PopupBinding binding in bindings)
            {
                if (binding == null)
                    continue;

                if (binding.OpenButton != null && binding.openAction != null)
                    binding.OpenButton.onClick.RemoveListener(binding.openAction);

                if (binding.CloseButton != null && binding.closeAction != null)
                    binding.CloseButton.onClick.RemoveListener(binding.closeAction);
            }
        }
    }
}

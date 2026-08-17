using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace LostFamiliar.Battle
{
    [DisallowMultipleComponent]
    public sealed class SkillPopupBridge : MonoBehaviour
    {
        public int SelectedSlotIndex { get; private set; } = -1;
        public MainBattleLoop Battle { get; private set; }

        [SerializeField] private Button closeButton;
        [SerializeField] private SkillPopupController controller;

        private UnityAction _closeAction;

        public void Bind(MainBattleLoop battle)
        {
            Battle = battle;
            controller?.Bind(battle);

            if (closeButton != null && _closeAction == null)
            {
                _closeAction = Close;
                closeButton.onClick.AddListener(_closeAction);
            }
        }

        public void Open(int slotIndex)
        {
            SelectedSlotIndex = slotIndex;
            gameObject.SetActive(true);
            controller?.Open(slotIndex);
        }

        public void Close()
        {
            controller?.Close();
            gameObject.SetActive(false);
        }

        private void OnDestroy()
        {
            if (closeButton != null && _closeAction != null)
                closeButton.onClick.RemoveListener(_closeAction);
        }
    }
}

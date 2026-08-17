using UnityEngine;
using UnityEngine.EventSystems;

namespace LostFamiliar.Battle
{
    [DisallowMultipleComponent]
    public sealed class UpgradeButtonPressRelay : MonoBehaviour,
        IPointerDownHandler,
        IPointerUpHandler,
        IPointerExitHandler
    {
        [SerializeField] private UpgradeStatRowUI owner;

        public void OnPointerDown(PointerEventData eventData)
        {
            owner?.BeginUpgradePress();
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            owner?.EndUpgradePress();
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            owner?.EndUpgradePress();
        }
    }
}

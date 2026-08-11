using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace LostFamiliar.Battle
{
    [DisallowMultipleComponent]
    public sealed class OfflineRewardPopupController : MonoBehaviour
    {
        private MainBattleLoop _battle;
        private GameObject _backgroundPanel;
        private Image _timeFill;
        private TMP_Text _amountText;
        private Button _receiveButton;

        public void Bind(MainBattleLoop battle)
        {
            if (_battle != null)
                _battle.StateChanged -= Refresh;
            _battle = battle;
            FindReferences();
            if (_receiveButton != null)
            {
                _receiveButton.onClick.RemoveListener(Receive);
                _receiveButton.onClick.AddListener(Receive);
            }
            if (_battle != null)
                _battle.StateChanged += Refresh;
            Refresh();
        }

        private void FindReferences()
        {
            Transform popupRoot = transform.parent;
            _backgroundPanel = FindDirectChild(popupRoot, "Panel")?.gameObject;
            Transform timeSlider = FindDescendant(transform, "TimeSlider");
            _timeFill = FindDescendant(timeSlider, "Fill")?.GetComponent<Image>();
            Transform rewardItem = FindDescendant(transform, "RewardItem");
            _amountText = FindDescendant(rewardItem, "AmountText")?.GetComponent<TMP_Text>();
            _receiveButton = FindDescendant(transform, "Btn_Receive")?.GetComponent<Button>();
        }

        private void Refresh()
        {
            bool visible = _battle != null && _battle.PendingOfflineSeconds > 0d;
            if (_timeFill != null)
                _timeFill.fillAmount = _battle != null ? _battle.OfflineRewardProgress01 : 0f;
            if (_amountText != null)
                _amountText.text = MainHUDController.FormatNumber(_battle?.PendingOfflineGold ?? 0d);
            SetVisible(visible);
        }

        private void Receive()
        {
            if (_battle == null || !_battle.TryReceiveOfflineReward())
                return;
            SetVisible(false);
        }

        private void SetVisible(bool visible)
        {
            if (_backgroundPanel != null)
                _backgroundPanel.SetActive(visible);
            if (gameObject.activeSelf != visible)
                gameObject.SetActive(visible);
        }

        private static Transform FindDirectChild(Transform root, string objectName)
        {
            if (root == null)
                return null;
            for (int i = 0; i < root.childCount; i++)
            {
                Transform child = root.GetChild(i);
                if (child.name == objectName)
                    return child;
            }
            return null;
        }

        private static Transform FindDescendant(Transform root, string objectName)
        {
            if (root == null)
                return null;
            foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
                if (child.name == objectName)
                    return child;
            return null;
        }

        private void OnDestroy()
        {
            if (_battle != null)
                _battle.StateChanged -= Refresh;
            if (_receiveButton != null)
                _receiveButton.onClick.RemoveListener(Receive);
        }
    }
}

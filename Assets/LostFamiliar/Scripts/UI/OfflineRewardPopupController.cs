using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace LostFamiliar.Battle
{
    [DisallowMultipleComponent]
    public sealed class OfflineRewardPopupController : MonoBehaviour
    {
        [SerializeField] private GameObject backgroundPanel;
        [SerializeField] private Image timeFill;
        [SerializeField] private TMP_Text amountText;
        [SerializeField] private Button receiveButton;

        private MainBattleLoop _battle;

        public void Bind(MainBattleLoop battle)
        {
            if (_battle != null)
                _battle.StateChanged -= Refresh;

            _battle = battle;

            if (receiveButton != null)
            {
                receiveButton.onClick.RemoveListener(Receive);
                receiveButton.onClick.AddListener(Receive);
            }

            if (_battle != null)
                _battle.StateChanged += Refresh;

            Refresh();
        }

        private void Refresh()
        {
            bool visible = _battle != null && _battle.PendingOfflineSeconds > 0d;
            if (timeFill != null)
                timeFill.fillAmount = _battle != null ? _battle.OfflineRewardProgress01 : 0f;
            if (amountText != null)
                amountText.text = MainHUDController.FormatNumber(_battle?.PendingOfflineGold ?? 0d);
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
            if (backgroundPanel != null)
                backgroundPanel.SetActive(visible);
            if (gameObject.activeSelf != visible)
                gameObject.SetActive(visible);
        }

        private void OnDestroy()
        {
            if (_battle != null)
                _battle.StateChanged -= Refresh;
            if (receiveButton != null)
                receiveButton.onClick.RemoveListener(Receive);
        }
    }
}

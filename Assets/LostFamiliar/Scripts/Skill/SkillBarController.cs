using UnityEngine;

namespace LostFamiliar.Battle
{
    [DisallowMultipleComponent]
    public sealed class SkillBarController : MonoBehaviour
    {
        [SerializeField] private SkillSlotUI[] slots;
        [SerializeField] private SkillPopupBridge popup;

        private MainBattleLoop _battle;

        public void Bind(MainBattleLoop battle)
        {
            if (_battle != null)
                _battle.StateChanged -= Refresh;
            _battle = battle;
            if (_battle != null)
                _battle.StateChanged += Refresh;

            popup?.Bind(_battle);

            if (slots != null)
            {
                for (int i = 0; i < slots.Length; i++)
                    slots[i]?.Bind(_battle, popup, i);
            }

            Refresh();
        }

        public void BindTower(MainBattleLoop battle, PlayerAutoCombat towerPlayer)
        {
            if (_battle != null)
                _battle.StateChanged -= Refresh;

            _battle = battle;
            popup = null;

            if (slots != null)
            {
                for (int i = 0; i < slots.Length; i++)
                    slots[i]?.Bind(_battle, null, i, towerPlayer);
            }

            Refresh();
        }

        private void Refresh()
        {
            if (slots == null)
                return;

            foreach (SkillSlotUI slot in slots)
                slot?.Refresh();
        }

        private void OnDestroy()
        {
            if (_battle != null)
                _battle.StateChanged -= Refresh;
        }
    }

}

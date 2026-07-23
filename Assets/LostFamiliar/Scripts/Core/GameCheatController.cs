using LostFamiliar.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace LostFamiliar.Battle
{
    [DisallowMultipleComponent]
    public sealed class GameCheatController : MonoBehaviour
    {
        [Header("재화 지급 치트 (V)")]
        [SerializeField, Min(0f)] private double cheatGold = 100000d;
        [SerializeField, Min(0)] private int cheatGems = 10000;

        private bool _resetting;

        private void Update()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (_resetting || Keyboard.current == null)
                return;

            MainBattleLoop battle = GetComponent<MainBattleLoop>();
            if (battle == null)
                battle = FindFirstObjectByType<MainBattleLoop>();

            if (Keyboard.current.vKey.wasPressedThisFrame)
            {
                if (battle != null)
                {
                    battle.AddCurrencies(cheatGold, cheatGems);
                    Debug.Log($"[CHEAT] 골드 {cheatGold:N0}, 젬 {cheatGems:N0} 지급");
                }
                return;
            }

            if (!Keyboard.current.cKey.wasPressedThisFrame)
                return;

            _resetting = true;
            if (battle != null)
            {
                battle.ResetProgress();
                Debug.Log("[CHEAT] 저장 데이터와 현재 전투 상태를 초기화했습니다.");
            }
            else
            {
                SaveService.Delete();
                Debug.LogWarning("[CHEAT] MainBattleLoop를 찾지 못해 저장 데이터만 초기화했습니다.");
            }
            _resetting = false;
#endif
        }
    }
}

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

        [Header("Stage 이동 치트")]
        [SerializeField, Min(1)] private int targetStage = 30;

        private bool _resetting;

        private void Update()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (_resetting || Keyboard.current == null)
                return;

            MainBattleLoop battle = GetComponent<MainBattleLoop>();
            if (battle == null)
                battle = FindFirstObjectByType<MainBattleLoop>();

            if (Keyboard.current.f6Key.wasPressedThisFrame)
            {
                MoveToTargetStage(battle);
                return;
            }

            if (Keyboard.current.pageUpKey.wasPressedThisFrame)
            {
                MoveToStage(battle, (battle != null ? battle.StageNumber : targetStage) + 1);
                return;
            }

            if (Keyboard.current.pageDownKey.wasPressedThisFrame)
            {
                MoveToStage(battle, (battle != null ? battle.StageNumber : targetStage) - 1);
                return;
            }

            if (Keyboard.current.vKey.wasPressedThisFrame)
            {
                if (battle != null)
                {
                    battle.AddCurrencies(cheatGold, cheatGems);
                    Debug.Log($"[CHEAT] 골드 {cheatGold:N0}, 젬 {cheatGems:N0} 지급");
                }
                return;
            }

            if (Keyboard.current.kKey.wasPressedThisFrame)
            {
                if (battle != null)
                {
                    int granted = battle.CheatGrantAllSkills();
                    Debug.Log($"[CHEAT] Granted one copy of every missing skill. New skills: {granted}");
                }
                else
                {
                    Debug.LogWarning("[CHEAT] MainBattleLoop was not found, so skills could not be granted.");
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

        [ContextMenu("Cheat/입력한 스테이지로 이동")]
        public void MoveToTargetStage()
        {
            MainBattleLoop battle = GetComponent<MainBattleLoop>();
            if (battle == null)
                battle = FindFirstObjectByType<MainBattleLoop>();
            MoveToTargetStage(battle);
        }

        private void MoveToTargetStage(MainBattleLoop battle)
        {
            MoveToStage(battle, targetStage);
        }

        private void MoveToStage(MainBattleLoop battle, int stage)
        {
            targetStage = Mathf.Max(1, stage);
            if (battle == null)
            {
                Debug.LogWarning("[CHEAT] MainBattleLoop을 찾지 못해 스테이지를 이동할 수 없습니다.");
                return;
            }

            if (battle.CheatMoveToStage(targetStage))
                Debug.Log($"[CHEAT] STAGE {targetStage}로 이동했습니다.");
            else
                Debug.LogWarning($"[CHEAT] STAGE {targetStage}에 사용할 데이터가 없어 이동하지 못했습니다.");
        }
    }
}

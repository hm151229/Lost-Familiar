using UnityEngine;

namespace LostFamiliar.Core
{
    public static class GameBootstrap
    {
        // 실제 필드 전투는 LostFamiliar.Battle.BattleBootstrap이 시작한다.
        // 이전 수치 시뮬레이션 부트스트랩은 호환을 위해 남겨두되 실행하지 않는다.
        private static void StartGame()
        {
            if (Object.FindFirstObjectByType<IdleGameController>() != null) return;
            var root = new GameObject("LostFamiliar_Core");
            root.AddComponent<IdleGameController>();
            root.AddComponent<PrototypeUI>();
            Screen.orientation = ScreenOrientation.Portrait;
            Application.targetFrameRate = 60;
        }
    }
}

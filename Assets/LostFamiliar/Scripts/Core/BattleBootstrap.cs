using UnityEngine;

namespace LostFamiliar.Battle
{
    public static class BattleBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Boot()
        {
            if (Object.FindFirstObjectByType<MainBattleLoop>() != null) return;
            LostFamiliar.Core.IdleGameController old = Object.FindFirstObjectByType<LostFamiliar.Core.IdleGameController>();
            if (old != null) Object.Destroy(old.gameObject);

            EnsureCamera();
            var playerObject = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            playerObject.name = "Player_BlackCat_Placeholder";
            playerObject.transform.position = Vector3.zero;
            playerObject.GetComponent<Renderer>().material.color = new Color(.05f, .05f, .08f);
            Object.Destroy(playerObject.GetComponent<Collider>());
            var player = playerObject.AddComponent<PlayerAutoCombat>();

            StageData stage = BuildRuntimeStage(player);
            var root = new GameObject("Main_AutoBattle");
            var battle = root.AddComponent<MainBattleLoop>();
            battle.Initialize(new[] { stage }, player);
            root.AddComponent<BattlePrototypeHUD>();
        }

        private static StageData BuildRuntimeStage(PlayerAutoCombat player)
        {
            EnemyData mushroom = ScriptableObject.CreateInstance<EnemyData>();
            mushroom.displayName = "마력 버섯"; mushroom.baseHealth = 25; mushroom.baseAttack = 3; mushroom.stageExperience = 10;
            EnemyData boss = ScriptableObject.CreateInstance<EnemyData>();
            boss.displayName = "거대 버섯왕"; boss.baseHealth = 40; boss.baseAttack = 5; boss.moveSpeed = 1; boss.goldReward = 25;
            SkillData burst = ScriptableObject.CreateInstance<SkillData>();
            burst.displayName = "마력 폭발"; burst.cooldown = 5; burst.damageMultiplier = 2.5f; burst.targetType = SkillTargetType.AllEnemies; burst.radius = 6;
            player.SetEquippedSkills(new[] { burst });
            StageData stage = ScriptableObject.CreateInstance<StageData>();
            stage.displayName = "마법 숲"; stage.experienceToBoss = 100; stage.spawnInterval = 1.2f; stage.maxAliveEnemies = 7;
            stage.normalEnemies = new[] { new EnemySpawnEntry { enemy = mushroom, weight = 1 } }; stage.boss = boss;
            return stage;
        }

        private static void EnsureCamera()
        {
            Camera camera = Camera.main;
            if (camera == null) { var obj = new GameObject("Main Camera"); obj.tag = "MainCamera"; camera = obj.AddComponent<Camera>(); }
            camera.orthographic = true; camera.orthographicSize = 6f; camera.transform.position = new Vector3(0, 0, -10f); camera.clearFlags = CameraClearFlags.SolidColor;
        }
    }

    public sealed class BattlePrototypeHUD : MonoBehaviour
    {
        private void OnGUI()
        {
            MainBattleLoop battle = GetComponent<MainBattleLoop>();
            if (battle == null || battle.CurrentStage == null) return;
            float progress = Mathf.Clamp01((float)battle.StageExperience / battle.CurrentStage.experienceToBoss);
            GUI.Box(new Rect(15, 15, Screen.width - 30, 105), GUIContent.none);
            GUI.Label(new Rect(30, 25, Screen.width - 60, 25), $"STAGE {battle.StageNumber} · {battle.CurrentStage.displayName} · {battle.Phase}");
            GUI.Label(new Rect(30, 50, Screen.width - 60, 22), $"PLAYER HP {battle.Player.Health:0}/{battle.Player.MaxHealth:0}    GOLD {battle.Gold}");
            GUI.Box(new Rect(30, 78, Screen.width - 60, 22), GUIContent.none);
            GUI.DrawTexture(new Rect(32, 80, (Screen.width - 64) * progress, 18), Texture2D.whiteTexture);
            GUI.Label(new Rect(30, 78, Screen.width - 60, 22), battle.Phase == BattlePhase.Boss ? "BOSS BATTLE" : $"BOSS GAUGE {progress * 100:0}%");
            if (battle.Player.EquippedSkills == null) return;
            for (int i = 0; i < battle.Player.EquippedSkills.Length; i++)
            {
                SkillData skill = battle.Player.EquippedSkills[i];
                if (skill != null) GUI.Label(new Rect(20, Screen.height - 45 - i * 28, 300, 25), $"{skill.displayName}  {battle.Player.GetSkillCooldown01(i) * 100:0}%");
            }
        }
    }
}

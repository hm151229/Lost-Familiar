using UnityEngine;

namespace LostFamiliar.Core
{
    public sealed class PrototypeUI : MonoBehaviour
    {
        private GUIStyle _title;
        private GUIStyle _label;
        private GUIStyle _center;

        private void OnGUI()
        {
            var game = IdleGameController.Instance;
            if (game == null) return;
            EnsureStyles();

            float scale = Mathf.Min(Screen.width / 540f, Screen.height / 960f);
            var old = GUI.matrix;
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));
            float width = Screen.width / scale;
            float height = Screen.height / scale;

            GUI.Box(new Rect(12, 12, width - 24, height - 24), GUIContent.none);
            GUI.Label(new Rect(24, 24, width - 48, 46), "Lost Familiar : 사역마다냥!", _title);
            GUI.Label(new Rect(28, 78, width - 56, 28), $"STAGE {game.Data.stage}   Lv.{game.Data.playerLevel}", _center);
            DrawBar(new Rect(28, 112, width - 56, 22), (float)(game.Data.playerExperience / GameBalance.ExperienceToLevel(game.Data.playerLevel)), new Color(.35f,.65f,1f), "PLAYER EXP");
            GUI.Label(new Rect(28, 145, width - 56, 26), $"Gold  {Short(game.Data.gold)}      Gem  {game.Data.gems}", _center);

            GUI.Box(new Rect(28, 185, width - 56, 230), GUIContent.none);
            GUI.Label(new Rect(40, 202, width - 80, 34), game.IsBoss ? "⚠ STAGE BOSS" : "야생 몬스터", _title);
            GUI.Label(new Rect(40, 245, width - 80, 70), game.IsBoss ? "👹" : "🍄", new GUIStyle(_title) { fontSize = 52 });
            DrawBar(new Rect(48, 330, width - 96, 28), (float)(game.EnemyHealth / game.EnemyMaxHealth), new Color(.9f,.25f,.25f), $"HP {Short(game.EnemyHealth)} / {Short(game.EnemyMaxHealth)}");
            DrawBar(new Rect(48, 370, width - 96, 20), game.Data.stageProgress / 100f, new Color(.7f,.35f,1f), game.IsBoss ? "BOSS BATTLE" : $"STAGE {game.Data.stageProgress:0}%");

            GUI.Label(new Rect(28, 430, width - 56, 25), $"🐈‍⬛  HP {Short(game.PlayerHealth)}/{Short(game.MaxHealth)}   ATK {Short(game.Attack)}   {game.AttacksPerSecond:0.00}/s", _center);
            GUI.Label(new Rect(28, 466, width - 56, 28), "골드 강화", _title);

            float y = 505;
            DrawUpgrade(game, StatType.Attack, "공격력", $"+2  (현재 Lv.{game.Data.attackLevel})", ref y, width);
            DrawUpgrade(game, StatType.MaxHealth, "체력", $"+10  (현재 Lv.{game.Data.healthLevel})", ref y, width);
            DrawUpgrade(game, StatType.AttackSpeed, "공격속도", $"+5%  (현재 Lv.{game.Data.attackSpeedLevel})", ref y, width);
            DrawUpgrade(game, StatType.CriticalChance, "치명타 확률", $"{game.CriticalChance * 100:0.0}%", ref y, width);
            DrawUpgrade(game, StatType.CriticalDamage, "치명타 피해", $"{game.CriticalMultiplier * 100:0}%", ref y, width);

            GUI.Label(new Rect(28, height - 55, width - 56, 24), "자동 전투 진행 중 · 10초마다 자동 저장", _center);
            GUI.matrix = old;
        }

        private void DrawUpgrade(IdleGameController game, StatType type, string name, string value, ref float y, float width)
        {
            double cost = GameBalance.UpgradeCost(type, game.Data.GetStatLevel(type));
            GUI.Label(new Rect(36, y, width - 250, 42), $"{name}  {value}", _label);
            GUI.enabled = game.Data.gold >= cost;
            if (GUI.Button(new Rect(width - 205, y, 165, 42), $"강화  {Short(cost)} G")) game.TryUpgrade(type);
            GUI.enabled = true;
            y += 50;
        }

        private void DrawBar(Rect rect, float value, Color color, string text)
        {
            GUI.Box(rect, GUIContent.none);
            var fill = new Rect(rect.x + 2, rect.y + 2, (rect.width - 4) * Mathf.Clamp01(value), rect.height - 4);
            Color old = GUI.color; GUI.color = color; GUI.DrawTexture(fill, Texture2D.whiteTexture); GUI.color = old;
            GUI.Label(rect, text, _center);
        }

        private void EnsureStyles()
        {
            if (_title != null) return;
            _title = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontSize = 24, fontStyle = FontStyle.Bold };
            _label = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleLeft, fontSize = 16 };
            _center = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontSize = 15, fontStyle = FontStyle.Bold };
        }

        private static string Short(double value)
        {
            if (value >= 1_000_000_000) return $"{value / 1_000_000_000:0.##}B";
            if (value >= 1_000_000) return $"{value / 1_000_000:0.##}M";
            if (value >= 1_000) return $"{value / 1_000:0.##}K";
            return $"{System.Math.Max(0d, value):0}";
        }
    }
}

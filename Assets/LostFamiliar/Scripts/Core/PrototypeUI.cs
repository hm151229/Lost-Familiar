using LostFamiliar.Battle;
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
            MainBattleLoop battle = Object.FindFirstObjectByType<MainBattleLoop>();
            if (battle == null || battle.Player == null || battle.CurrentStage == null)
                return;

            EnsureStyles();
            float scale = Mathf.Min(Screen.width / 540f, Screen.height / 960f);
            Matrix4x4 previousMatrix = GUI.matrix;
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));
            float width = Screen.width / scale;
            float height = Screen.height / scale;

            GUI.Box(new Rect(12, 12, width - 24, height - 24), GUIContent.none);
            GUI.Label(new Rect(24, 24, width - 48, 46), "Lost Familiar : 사역마다냥!", _title);
            GUI.Label(new Rect(28, 78, width - 56, 28),
                $"STAGE {battle.StageNumber} · {battle.CurrentStage.DisplayName} · Lv.{battle.PlayerLevel}", _center);
            DrawBar(
                new Rect(28, 112, width - 56, 22),
                (float)(battle.PlayerExperience / battle.PlayerExperienceToLevel),
                new Color(.35f, .65f, 1f),
                "PLAYER EXP");
            GUI.Label(new Rect(28, 145, width - 56, 26),
                $"Gold  {Short(battle.Gold)}      Gem  {battle.Gems}", _center);

            GUI.Label(new Rect(28, 190, width - 56, 25),
                $"HP {battle.Player.Health:0}/{battle.Player.MaxHealth:0}   ATK {Short(battle.Player.AttackDamage)}   {battle.Player.AttacksPerSecond:0.00}/s",
                _center);
            GUI.Label(new Rect(28, 230, width - 56, 28), "골드 강화", _title);

            float y = 275f;
            DrawUpgrade(battle, StatType.Attack, "공격력", "+2", ref y, width);
            DrawUpgrade(battle, StatType.CriticalChance, "치명타 확률", $"{battle.Player.CriticalChance * 100:0.0}%", ref y, width);
            DrawUpgrade(battle, StatType.CriticalDamage, "치명타 피해", $"{battle.Player.CriticalMultiplier * 100:0}%", ref y, width);
            DrawUpgrade(battle, StatType.SkillDamage, "스킬 데미지", $"{battle.Player.SkillDamageMultiplier * 100:0.#}%", ref y, width);
            DrawUpgrade(battle, StatType.BossDamage, "보스 데미지", $"{battle.Player.BossDamageMultiplier * 100:0.#}%", ref y, width);

            GUI.Label(new Rect(28, height - 55, width - 56, 24), "자동 전투 진행 중 · 10초마다 자동 저장", _center);
            GUI.matrix = previousMatrix;
        }

        private void DrawUpgrade(MainBattleLoop battle, StatType type, string name, string value, ref float y, float width)
        {
            double cost = battle.GetUpgradeCost(type);
            GUI.Label(new Rect(36, y, width - 250, 42),
                $"{name}  {value}  (Lv.{battle.GetStatLevel(type)})", _label);
            GUI.enabled = battle.Gold >= cost;
            if (GUI.Button(new Rect(width - 205, y, 165, 42), $"강화  {Short(cost)} G"))
                battle.TryUpgrade(type);
            GUI.enabled = true;
            y += 50f;
        }

        private void DrawBar(Rect rect, float value, Color color, string text)
        {
            GUI.Box(rect, GUIContent.none);
            Rect fill = new Rect(rect.x + 2, rect.y + 2, (rect.width - 4) * Mathf.Clamp01(value), rect.height - 4);
            Color previousColor = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(fill, Texture2D.whiteTexture);
            GUI.color = previousColor;
            GUI.Label(rect, text, _center);
        }

        private void EnsureStyles()
        {
            if (_title != null)
                return;

            _title = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 24,
                fontStyle = FontStyle.Bold
            };
            _label = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleLeft, fontSize = 16 };
            _center = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 15,
                fontStyle = FontStyle.Bold
            };
        }

        private static string Short(double value)
        {
            if (value >= 1_000_000_000d) return $"{value / 1_000_000_000d:0.##}B";
            if (value >= 1_000_000d) return $"{value / 1_000_000d:0.##}M";
            if (value >= 1_000d) return $"{value / 1_000d:0.##}K";
            return $"{System.Math.Max(0d, value):0}";
        }
    }
}

using System;
using UnityEngine;

namespace LostFamiliar.Core
{
    public enum StatType { Attack, MaxHealth, AttackSpeed, CriticalChance, CriticalDamage }

    [Serializable]
    public sealed class GameSaveData
    {
        public int version = 1;
        public double gold;
        public int gems;
        public int playerLevel = 1;
        public double playerExperience;
        public int stage = 1;
        public float stageProgress;
        public int attackLevel;
        public int healthLevel;
        public int attackSpeedLevel;
        public int criticalChanceLevel;
        public int criticalDamageLevel;

        public int GetStatLevel(StatType type) => type switch
        {
            StatType.Attack => attackLevel,
            StatType.MaxHealth => healthLevel,
            StatType.AttackSpeed => attackSpeedLevel,
            StatType.CriticalChance => criticalChanceLevel,
            StatType.CriticalDamage => criticalDamageLevel,
            _ => 0
        };

        public void IncreaseStatLevel(StatType type)
        {
            switch (type)
            {
                case StatType.Attack: attackLevel++; break;
                case StatType.MaxHealth: healthLevel++; break;
                case StatType.AttackSpeed: attackSpeedLevel++; break;
                case StatType.CriticalChance: criticalChanceLevel++; break;
                case StatType.CriticalDamage: criticalDamageLevel++; break;
            }
        }
    }

    public static class SaveService
    {
        private const string Key = "LostFamiliar.Save.v1";
        public static GameSaveData Load()
        {
            if (!PlayerPrefs.HasKey(Key)) return new GameSaveData();
            try { return JsonUtility.FromJson<GameSaveData>(PlayerPrefs.GetString(Key)) ?? new GameSaveData(); }
            catch (Exception) { return new GameSaveData(); }
        }

        public static void Save(GameSaveData data)
        {
            PlayerPrefs.SetString(Key, JsonUtility.ToJson(data));
            PlayerPrefs.Save();
        }

        public static void Delete() => PlayerPrefs.DeleteKey(Key);
    }
}

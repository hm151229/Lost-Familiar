using System;
using UnityEngine;

namespace LostFamiliar.Core
{
    public static class SaveService
    {
        private const string Key = "LostFamiliar.Save.v1";
        public static GameSaveData Load()
        {
            if (!PlayerPrefs.HasKey(Key)) return new GameSaveData();
            try
            {
                GameSaveData data = JsonUtility.FromJson<GameSaveData>(PlayerPrefs.GetString(Key)) ?? new GameSaveData();
                data.Normalize();
                return data;
            }
            catch (Exception) { return new GameSaveData(); }
        }

        public static void Save(GameSaveData data)
        {
            PlayerPrefs.SetString(Key, JsonUtility.ToJson(data));
            PlayerPrefs.Save();
        }

        public static void Delete()
        {
            PlayerPrefs.DeleteKey(Key);
            PlayerPrefs.Save();
        }
    }
}

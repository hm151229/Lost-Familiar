#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace LostFamiliar.Core.Editor
{
    [InitializeOnLoad]
    internal static class GameAudioLibraryBuilder
    {
        private const string SoundFolder = "Assets/LostFamiliar/Sound";
        private const string ResourcesFolder = "Assets/LostFamiliar/Resources";
        private const string AssetPath = ResourcesFolder + "/GameAudioLibrary.asset";

        static GameAudioLibraryBuilder()
        {
            EditorApplication.delayCall += Rebuild;
        }

        [MenuItem("Tools/Lost Familiar/Rebuild Audio Library")]
        private static void Rebuild()
        {
            if (!AssetDatabase.IsValidFolder(SoundFolder)) return;
            bool hadSavedVolumeValues = File.Exists(AssetPath) &&
                                        File.ReadAllText(AssetPath).Contains("volume:");
            if (!AssetDatabase.IsValidFolder(ResourcesFolder))
                AssetDatabase.CreateFolder("Assets/LostFamiliar", "Resources");

            GameAudioLibrary library = AssetDatabase.LoadAssetAtPath<GameAudioLibrary>(AssetPath);
            if (library == null)
            {
                library = ScriptableObject.CreateInstance<GameAudioLibrary>();
                AssetDatabase.CreateAsset(library, AssetPath);
            }

            List<GameAudioLibrary.Entry> entries = new List<GameAudioLibrary.Entry>();
            Dictionary<string, float> previousVolumes = new Dictionary<string, float>(System.StringComparer.OrdinalIgnoreCase);
            foreach (GameAudioLibrary.Entry entry in library.Entries)
                if (!string.IsNullOrEmpty(entry.id))
                    previousVolumes[entry.id] = entry.volume;
            foreach (string guid in AssetDatabase.FindAssets("t:AudioClip", new[] { SoundFolder }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                AudioClip clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
                if (clip == null) continue;
                string id = Path.GetFileNameWithoutExtension(path);
                entries.Add(new GameAudioLibrary.Entry
                {
                    id = id,
                    clip = clip,
                    volume = hadSavedVolumeValues && previousVolumes.TryGetValue(id, out float volume)
                        ? volume
                        : 1f
                });
            }
            entries.Sort((a, b) => string.CompareOrdinal(a.id, b.id));
            library.EditorSetEntries(entries);
            EditorUtility.SetDirty(library);
            AssetDatabase.SaveAssets();
        }
    }
}
#endif

using System;
using System.Collections.Generic;
using UnityEngine;

namespace LostFamiliar.Core
{
    [CreateAssetMenu(menuName = "Lost Familiar/Audio Library", fileName = "GameAudioLibrary")]
    public sealed class GameAudioLibrary : ScriptableObject
    {
        [Serializable]
        public struct Entry
        {
            public string id;
            public AudioClip clip;
            [Range(0f, 1f)] public float volume;
        }

        [SerializeField] private List<Entry> entries = new List<Entry>();
        private Dictionary<string, AudioClip> _lookup;

        public IReadOnlyList<Entry> Entries => entries;

        public AudioClip Get(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            if (_lookup == null)
            {
                _lookup = new Dictionary<string, AudioClip>(StringComparer.OrdinalIgnoreCase);
                foreach (Entry entry in entries)
                    if (!string.IsNullOrEmpty(entry.id) && entry.clip != null)
                        _lookup[entry.id] = entry.clip;
            }
            return _lookup.TryGetValue(id, out AudioClip clip) ? clip : null;
        }

        public float GetVolume(string id)
        {
            if (string.IsNullOrEmpty(id)) return 1f;
            foreach (Entry entry in entries)
                if (string.Equals(entry.id, id, StringComparison.OrdinalIgnoreCase))
                    return Mathf.Clamp01(entry.volume);
            return 1f;
        }

#if UNITY_EDITOR
        public void EditorSetEntries(List<Entry> value)
        {
            entries = value ?? new List<Entry>();
            _lookup = null;
        }
#endif
    }
}

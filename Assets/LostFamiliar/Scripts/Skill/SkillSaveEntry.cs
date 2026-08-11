using System;

namespace LostFamiliar.Core
{
    [Serializable]
    public sealed class SkillSaveEntry
    {
        public string skillId;
        public int level;
        public int duplicates;
    }
}

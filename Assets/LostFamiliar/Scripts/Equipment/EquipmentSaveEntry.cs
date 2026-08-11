using System;

namespace LostFamiliar.Core
{
    [Serializable]
    public sealed class EquipmentSaveEntry
    {
        public string equipmentId;
        public int level;
        public int duplicates;
    }
}

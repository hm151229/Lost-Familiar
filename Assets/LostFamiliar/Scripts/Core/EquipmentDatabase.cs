using UnityEngine;

namespace LostFamiliar.Core
{
    [CreateAssetMenu(menuName = "Lost Familiar/Equipment/Equipment Database", fileName = "EquipmentDatabase")]
    public sealed class EquipmentDatabase : ScriptableObject
    {
        public int contentVersion;
        public EquipmentData[] items;

        public EquipmentData Get(string id)
        {
            if (string.IsNullOrWhiteSpace(id) || items == null)
                return null;

            foreach (EquipmentData item in items)
            {
                if (item != null && item.Id == id)
                    return item;
            }
            return null;
        }
    }
}

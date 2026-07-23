using LostFamiliar.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace LostFamiliar.Battle
{
    public enum EquipmentSlotDisplayMode { EquipmentPopup, SummonPopup, EquippedSlot }

    [DisallowMultipleComponent]
    public sealed class EquipmentSlotItemUI : MonoBehaviour
    {
        [Header("표시할 장비")]
        [SerializeField] private EquipmentData equipmentData;
        [SerializeField] private EquipmentSlotDisplayMode displayMode = EquipmentSlotDisplayMode.EquipmentPopup;

        [Header("UI 연결")]
        [SerializeField] private Image backgroundImage;
        [SerializeField] private Image itemIcon;
        [SerializeField] private TMP_Text levelText;
        [SerializeField] private GameObject upgradeIconRoot;
        [SerializeField] private GameObject progressRoot;
        [SerializeField] private GameObject installationMaskRoot;
        [SerializeField] private Image progressFill;
        [SerializeField] private TMP_Text progressText;

        private MainBattleLoop _battle;

        public EquipmentData Data => equipmentData;
        public EquipmentSlotDisplayMode DisplayMode => displayMode;

        private void Awake() => AutoFindReferences();

        private void OnEnable()
        {
            if (_battle == null && Application.isPlaying)
                BindBattle(FindFirstObjectByType<MainBattleLoop>());
            Refresh();
        }

        public void Bind(
            EquipmentData data,
            MainBattleLoop battle,
            EquipmentSlotDisplayMode mode = EquipmentSlotDisplayMode.EquipmentPopup)
        {
            equipmentData = data;
            displayMode = mode;
            BindBattle(battle);
            Refresh();
        }

        public void SetData(EquipmentData data)
        {
            equipmentData = data;
            Refresh();
        }

        public void SetDisplayMode(EquipmentSlotDisplayMode mode)
        {
            displayMode = mode;
            Refresh();
        }

        public void Refresh()
        {
            AutoFindReferences();
            bool hasData = equipmentData != null;

            if (backgroundImage != null)
                backgroundImage.color = hasData
                    ? EquipmentBalance.RarityColor(equipmentData.rarity)
                    : Color.white;

            if (itemIcon != null)
            {
                itemIcon.sprite = hasData ? equipmentData.icon : null;
                itemIcon.enabled = hasData && equipmentData.icon != null;
                itemIcon.preserveAspect = true;
            }

            bool showLevel = hasData && displayMode != EquipmentSlotDisplayMode.SummonPopup;
            bool showProgress = displayMode == EquipmentSlotDisplayMode.EquipmentPopup;
            SetActive(levelText != null ? levelText.gameObject : null, showLevel);
            SetActive(progressRoot, showProgress);

            EquipmentInventory inventory = _battle?.EquipmentInventory;
            EquipmentSaveEntry state = hasData ? inventory?.GetState(equipmentData.Id) : null;
            bool showInstallationMask = displayMode == EquipmentSlotDisplayMode.EquipmentPopup &&
                                        hasData && inventory != null && inventory.IsEquipped(equipmentData.Id);
            SetActive(installationMaskRoot, showInstallationMask);
            int level = state?.level ?? 0;
            int duplicates = state?.duplicates ?? 0;
            int required = level > 0
                ? EquipmentBalance.DuplicateRequirement(level)
                : 1;
            bool isMax = hasData && level >= equipmentData.maxLevel;
            bool canUpgrade = showLevel && hasData && inventory != null &&
                              inventory.CanUpgrade(equipmentData.Id);

            if (levelText != null)
                levelText.text = $"Lv.{level}";
            SetActive(upgradeIconRoot, canUpgrade);

            if (progressText != null)
                progressText.text = isMax ? "MAX" : $"{duplicates}/{required}";
            if (progressFill != null)
            {
                progressFill.type = Image.Type.Filled;
                progressFill.fillMethod = Image.FillMethod.Horizontal;
                progressFill.fillOrigin = (int)Image.OriginHorizontal.Left;
                progressFill.fillAmount = isMax
                    ? 1f
                    : Mathf.Clamp01(duplicates / (float)Mathf.Max(1, required));
            }
        }

        private void BindBattle(MainBattleLoop battle)
        {
            if (_battle == battle)
                return;

            if (_battle != null)
                _battle.StateChanged -= Refresh;
            _battle = battle;
            if (_battle != null)
                _battle.StateChanged += Refresh;
        }

        private void AutoFindReferences()
        {
            backgroundImage ??= FindChild<Image>("BG");
            itemIcon ??= FindChild<Image>("Icon_Item");
            levelText ??= FindChild<TMP_Text>("LevelText");
            upgradeIconRoot ??= FindChildTransform("Icon_Upgrade")?.gameObject;
            progressRoot ??= FindChildTransform("Progress")?.gameObject;
            installationMaskRoot ??= FindChildTransform("InstallationMask")?.gameObject;
            progressFill ??= FindChild<Image>("Fill");
            progressText ??= FindChild<TMP_Text>("AmountText");
        }

        private T FindChild<T>(string objectName) where T : Component
        {
            Transform child = FindChildTransform(objectName);
            return child != null ? child.GetComponent<T>() : null;
        }

        private Transform FindChildTransform(string objectName)
        {
            foreach (Transform child in GetComponentsInChildren<Transform>(true))
            {
                if (child.name == objectName)
                    return child;
            }
            return null;
        }

        private static void SetActive(GameObject target, bool active)
        {
            if (target != null && target.activeSelf != active)
                target.SetActive(active);
        }

        private void OnDestroy()
        {
            if (_battle != null)
                _battle.StateChanged -= Refresh;
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            AutoFindReferences();
            Refresh();
        }
#endif
    }
}

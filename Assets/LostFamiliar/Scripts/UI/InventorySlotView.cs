using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace LostFamiliar.Battle
{
    /// <summary>
    /// 장비와 스킬 종류를 모르는 공용 인벤토리 슬롯 표현 컴포넌트입니다.
    /// 데이터 계산은 EquipmentSlotItemUI / OwnedSkillItemUI가 담당합니다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class InventorySlotView : MonoBehaviour
    {
        [SerializeField] private Image backgroundImage;
        [SerializeField] private Image iconImage;
        [SerializeField] private TMP_Text levelText;
        [SerializeField] private GameObject upgradeIconRoot;
        [SerializeField] private GameObject progressRoot;
        [SerializeField] private GameObject installationMaskRoot;
        [SerializeField] private Image progressFill;
        [SerializeField] private TMP_Text progressText;

        public void Render(
            Sprite icon,
            Color rarityColor,
            bool showLevel,
            int level,
            bool showProgress,
            int progress,
            int required,
            bool isMax,
            bool canUpgrade,
            bool isEquipped)
        {
            if (backgroundImage != null)
                backgroundImage.color = rarityColor;
            if (iconImage != null)
            {
                iconImage.sprite = icon;
                iconImage.enabled = icon != null;
                iconImage.preserveAspect = true;
            }
            if (levelText != null)
            {
                levelText.text = $"Lv.{Mathf.Max(0, level)}";
                SetActive(levelText.gameObject, showLevel);
            }
            SetActive(upgradeIconRoot, canUpgrade);
            SetActive(progressRoot, showProgress);
            SetActive(installationMaskRoot, isEquipped);

            if (progressText != null)
                progressText.text = isMax ? "MAX" : $"{Mathf.Max(0, progress)}/{Mathf.Max(1, required)}";
            if (progressFill != null)
            {
                progressFill.type = Image.Type.Filled;
                progressFill.fillMethod = Image.FillMethod.Horizontal;
                progressFill.fillOrigin = (int)Image.OriginHorizontal.Left;
                progressFill.fillAmount = isMax
                    ? 1f
                    : Mathf.Clamp01(progress / (float)Mathf.Max(1, required));
            }
        }

        public void Clear()
        {
            Render(null, Color.white, false, 0, false, 0, 1, false, false, false);
        }

        private void AutoFindReferences()
        {
            backgroundImage ??= Find<Image>("BG");
            iconImage ??= Find<Image>("Icon_Item");
            levelText ??= Find<TMP_Text>("LevelText");
            upgradeIconRoot ??= FindTransform("Icon_Upgrade")?.gameObject;
            progressRoot ??= FindTransform("Progress")?.gameObject;
            installationMaskRoot ??= FindTransform("InstallationMask")?.gameObject;
            progressFill ??= Find<Image>("Fill");
            progressText ??= Find<TMP_Text>("AmountText");
        }

        private T Find<T>(string objectName) where T : Component => FindTransform(objectName)?.GetComponent<T>();

        private Transform FindTransform(string objectName)
        {
            foreach (Transform child in GetComponentsInChildren<Transform>(true))
                if (child.name == objectName) return child;
            return null;
        }

        private static void SetActive(GameObject target, bool active)
        {
            if (target != null && target.activeSelf != active)
                target.SetActive(active);
        }

#if UNITY_EDITOR
        private void OnValidate() => AutoFindReferences();
#endif
    }
}

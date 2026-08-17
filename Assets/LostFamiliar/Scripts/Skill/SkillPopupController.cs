using System.Collections.Generic;
using LostFamiliar.Core;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace LostFamiliar.Battle
{
    [DisallowMultipleComponent]
    public sealed class SkillPopupController : MonoBehaviour
    {
        [Header("Owned Skills")]
        [SerializeField] private Transform ownedContent;
        [SerializeField] private GameObject ownedSlotPrefab;

        [Header("Equipped Skills")]
        [SerializeField] private EquippedSkillPopupSlotUI[] equippedSlots;

        [Header("Selected Skill")]
        [SerializeField] private GameObject selectedPanel;
        [SerializeField] private GameObject emptyState;
        [SerializeField] private Image selectedIcon;
        [SerializeField] private Image rarityBadge;
        [SerializeField] private TMP_Text rarityText;
        [SerializeField] private TMP_Text skillNameText;
        [SerializeField] private TMP_Text descriptionText;
        [SerializeField] private TMP_Text ownedEffectText;
        [SerializeField] private TMP_Text selectedLevelText;
        [SerializeField] private Image selectedProgressFill;
        [SerializeField] private TMP_Text selectedProgressText;

        [Header("Total Owned Effects")]
        [SerializeField] private TMP_Text attackEffectText;
        [SerializeField] private TMP_Text criticalRateEffectText;
        [SerializeField] private TMP_Text criticalDamageEffectText;
        [SerializeField] private TMP_Text skillDamageEffectText;
        [SerializeField] private TMP_Text bossDamageEffectText;

        [Header("Actions")]
        [SerializeField] private Button equipButton;
        [SerializeField] private Button mergeAllButton;
        [SerializeField] private GameObject mergeRedDot;

        public bool IsSelectingReplacement { get; private set; }

        private MainBattleLoop _battle;
        private SkillData _selectedSkill;
        private readonly List<OwnedSkillItemUI> _ownedSlots = new();
        private UnityAction _equipAction;
        private UnityAction _mergeAction;

        public void Bind(MainBattleLoop battle)
        {
            if (_battle != null)
                _battle.StateChanged -= Refresh;
            _battle = battle;
            if (_battle != null)
                _battle.StateChanged += Refresh;

            BindButtons();
            BindEquippedSlots();
            Refresh();
        }

        public void Open(int preferredSlotIndex = -1)
        {
            IsSelectingReplacement = false;
            _selectedSkill = null;
            Refresh();
        }

        public void Close()
        {
            IsSelectingReplacement = false;
            RefreshEquippedSlots();
        }

        public void SelectSkill(SkillData skill)
        {
            _selectedSkill = skill;
            IsSelectingReplacement = false;
            Refresh();
        }

        public void SelectReplacementSlot(int slotIndex)
        {
            if (_battle == null || !_battle.IsSkillSlotUnlocked(slotIndex))
                return;

            if (IsSelectingReplacement)
            {
                if (_selectedSkill == null)
                    return;

                if (_battle.TryEquipSkill(_selectedSkill.id, slotIndex))
                    IsSelectingReplacement = false;
            }
            else if (_battle.GetEquippedSkill(slotIndex) != null)
            {
                _battle.UnequipSkill(slotIndex);
            }

            Refresh();
        }

        private void EquipSelected()
        {
            if (_battle == null || _selectedSkill == null)
                return;

            for (int i = 0; i < _battle.UnlockedSkillSlotCount; i++)
            {
                if (_battle.GetEquippedSkill(i) != null)
                    continue;
                _battle.TryEquipSkill(_selectedSkill.id, i);
                IsSelectingReplacement = false;
                Refresh();
                return;
            }

            IsSelectingReplacement = true;
            RefreshEquippedSlots();
        }

        private void MergeAll()
        {
            _battle?.TryUpgradeAllSkills();
            GameAudioManager.Instance.PlaySfx("SFX_Stat_Upgrade");
            Refresh();
        }

        private void Refresh()
        {
            BuildOwnedSlots();
            RefreshEquippedSlots();
            RefreshSelectedPanel();
            RefreshTotalOwnedEffects();
            RefreshMergeButton();
        }

        private void BindEquippedSlots()
        {
            if (equippedSlots == null)
                return;

            for (int i = 0; i < equippedSlots.Length; i++)
                equippedSlots[i]?.Bind(_battle, this, i);
        }

        private void BuildOwnedSlots()
        {
            IReadOnlyList<SkillData> owned = _battle?.GetOwnedSkills();
            int count = owned?.Count ?? 0;

            while (_ownedSlots.Count < count && ownedSlotPrefab != null && ownedContent != null)
            {
                GameObject clone = Instantiate(ownedSlotPrefab, ownedContent);
                clone.name = $"OwnedSkillSlot{_ownedSlots.Count + 1:00}";
                OwnedSkillItemUI item = clone.GetComponent<OwnedSkillItemUI>();
                if (item == null)
                {
                    Debug.LogError("Owned skill slot prefab에 OwnedSkillItemUI가 없습니다.", clone);
                    Destroy(clone);
                    break;
                }
                _ownedSlots.Add(item);
            }

            while (_ownedSlots.Count > count)
            {
                int last = _ownedSlots.Count - 1;
                OwnedSkillItemUI extra = _ownedSlots[last];
                _ownedSlots.RemoveAt(last);
                if (extra != null) Destroy(extra.gameObject);
            }

            for (int i = 0; i < _ownedSlots.Count; i++)
            {
                _ownedSlots[i].gameObject.SetActive(true);
                _ownedSlots[i].Bind(owned[i], _battle, this);
            }

        }

        private void RefreshEquippedSlots()
        {
            if (equippedSlots == null)
                return;

            foreach (EquippedSkillPopupSlotUI slot in equippedSlots)
                slot?.Refresh();
        }

        private void RefreshSelectedPanel()
        {
            bool selected = _selectedSkill != null;
            RefreshSelectedPanelVisibility(selected);
            if (!selected) return;

            SkillSaveEntry state = _battle?.GetSkillState(_selectedSkill.id);
            int level = state?.level ?? 0;
            int duplicates = state?.duplicates ?? 0;
            int required = level > 0 ? SkillBalance.DuplicateRequirement(level) : 1;
            bool isMax = level >= _selectedSkill.maxLevel;
            Color rarityColor = EquipmentBalance.RarityColor(_selectedSkill.rarity);
            if (selectedIcon != null)
            {
                selectedIcon.sprite = _selectedSkill.icon;
                selectedIcon.enabled = _selectedSkill.icon != null;
                selectedIcon.preserveAspect = true;
            }
            if (rarityBadge != null) rarityBadge.color = rarityColor;
            if (rarityText != null)
                rarityText.text = SkillUiFormatting.Rarity(_selectedSkill.rarity);
            if (skillNameText != null) skillNameText.text = _selectedSkill.displayName;
            if (descriptionText != null) descriptionText.text = _selectedSkill.description;
            if (ownedEffectText != null) ownedEffectText.text = SkillUiFormatting.Effect(_selectedSkill, level);
            if (selectedLevelText != null) selectedLevelText.text = $"Lv.{Mathf.Max(0, level)}";
            if (selectedProgressText != null)
                selectedProgressText.text = isMax ? "MAX" : $"{Mathf.Max(0, duplicates)}/{Mathf.Max(1, required)}";
            if (selectedProgressFill != null)
            {
                selectedProgressFill.fillAmount = isMax
                    ? 1f
                    : Mathf.Clamp01(duplicates / (float)Mathf.Max(1, required));
            }

            bool alreadyEquipped = false;
            if (_battle != null)
                for (int i = 0; i < _battle.UnlockedSkillSlotCount; i++)
                    if (_battle.GetEquippedSkillId(i) == _selectedSkill.id) alreadyEquipped = true;
            if (equipButton != null)
                equipButton.interactable = _battle != null && level > 0 && !alreadyEquipped && _battle.UnlockedSkillSlotCount > 0;
        }

        private void RefreshSelectedPanelVisibility(bool selected)
        {
            if (selectedPanel == null)
                return;

            selectedPanel.SetActive(true);
            Transform panel = selectedPanel.transform;
            for (int i = 0; i < panel.childCount; i++)
            {
                GameObject child = panel.GetChild(i).gameObject;
                bool isEmptyState = child == emptyState || child.name == "EmptyState";
                bool isBackground = child.name == "BG";
                child.SetActive(selected ? !isEmptyState : isBackground || isEmptyState);
            }
        }

        private void RefreshTotalOwnedEffects()
        {
            if (_battle == null)
                return;

            var totals = new Dictionary<EquipmentEffectType, float>();
            foreach (SkillData skill in _battle.GetOwnedSkills())
            {
                SkillSaveEntry state = _battle.GetSkillState(skill.id);
                float value = SkillBalance.OwnedEffectValue(skill, state?.level ?? 0);
                totals[skill.ownedEffectType] = totals.TryGetValue(skill.ownedEffectType, out float old) ? old + value : value;
            }

            SetTotalEffect(attackEffectText, totals, EquipmentEffectType.AttackPercent);
            SetTotalEffect(criticalRateEffectText, totals, EquipmentEffectType.CriticalChancePercentPoint);
            SetTotalEffect(criticalDamageEffectText, totals, EquipmentEffectType.CriticalDamagePercent);
            SetTotalEffect(skillDamageEffectText, totals, EquipmentEffectType.SkillDamagePercent);
            SetTotalEffect(bossDamageEffectText, totals, EquipmentEffectType.BossDamagePercent);
        }

        private static void SetTotalEffect(
            TMP_Text text,
            IReadOnlyDictionary<EquipmentEffectType, float> totals,
            EquipmentEffectType type)
        {
            if (text == null)
                return;

            float value = totals.TryGetValue(type, out float total) ? total : 0f;
            text.text = $"+{value:0.##}%";
        }

        private void RefreshMergeButton()
        {
            bool canMerge = false;
            if (_battle != null)
                foreach (SkillData skill in _battle.GetOwnedSkills())
                    if (_battle.CanUpgradeSkill(skill.id)) { canMerge = true; break; }
            if (mergeAllButton != null) mergeAllButton.interactable = canMerge;
            if (mergeRedDot != null) mergeRedDot.SetActive(canMerge);
        }

        private void BindButtons()
        {
            if (equipButton != null)
            {
                if (_equipAction != null) equipButton.onClick.RemoveListener(_equipAction);
                _equipAction = EquipSelected;
                equipButton.onClick.AddListener(_equipAction);
            }
            if (mergeAllButton != null)
            {
                if (_mergeAction != null) mergeAllButton.onClick.RemoveListener(_mergeAction);
                _mergeAction = MergeAll;
                mergeAllButton.onClick.AddListener(_mergeAction);
            }
        }

        private void OnDestroy()
        {
            if (_battle != null) _battle.StateChanged -= Refresh;
            if (equipButton != null && _equipAction != null) equipButton.onClick.RemoveListener(_equipAction);
            if (mergeAllButton != null && _mergeAction != null) mergeAllButton.onClick.RemoveListener(_mergeAction);
        }
    }
}

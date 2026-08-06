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
        public bool IsSelectingReplacement { get; private set; }

        private MainBattleLoop _battle;
        private SkillData _selectedSkill;
        private readonly List<OwnedSkillItemUI> _ownedSlots = new();
        private readonly List<EquippedSkillPopupSlotUI> _equippedSlots = new();
        private Transform _ownedContent;
        private GameObject _ownedSlotPrefab;
        private bool _ownedContentInitialized;
        private GameObject _selectedPanel;
        private GameObject _emptyState;
        private Image _selectedIcon;
        private Image _rarityBadge;
        private TMP_Text _rarityText;
        private TMP_Text _skillNameText;
        private TMP_Text _descriptionText;
        private TMP_Text _ownedEffectText;
        private TMP_Text _selectedLevelText;
        private Image _selectedProgressFill;
        private TMP_Text _selectedProgressText;
        private readonly Dictionary<EquipmentEffectType, TMP_Text> _totalEffectTexts = new();
        private Button _equipButton;
        private Button _mergeAllButton;
        private GameObject _mergeRedDot;
        private UnityAction _equipAction;
        private UnityAction _mergeAction;

        public void Bind(MainBattleLoop battle)
        {
            if (_battle != null)
                _battle.StateChanged -= Refresh;
            _battle = battle;
            if (_battle != null)
                _battle.StateChanged += Refresh;

            FindReferences();
            BindButtons();
            BuildEquippedSlots();
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
            FindReferences();
            BuildOwnedSlots();
            RefreshEquippedSlots();
            RefreshSelectedPanel();
            RefreshTotalOwnedEffects();
            RefreshMergeButton();
        }

        private void BuildEquippedSlots()
        {
            _equippedSlots.Clear();
            Transform section = SkillBarController.FindDescendant(transform, "EquippedSkillSection");
            if (section == null) return;
            for (int i = 0; i < SkillBalance.MaxEquippedSkillCount; i++)
            {
                Transform slotTransform = SkillBarController.FindDescendant(section, $"EquippedSkillSlot{i + 1:00}");
                if (slotTransform == null) continue;
                EquippedSkillPopupSlotUI slot = slotTransform.GetComponent<EquippedSkillPopupSlotUI>();
                if (slot == null) slot = slotTransform.gameObject.AddComponent<EquippedSkillPopupSlotUI>();
                slot.Bind(_battle, this, i);
                _equippedSlots.Add(slot);
            }
        }

        private void BuildOwnedSlots()
        {
            IReadOnlyList<SkillData> owned = _battle?.GetOwnedSkills();
            int count = owned?.Count ?? 0;
            if (!_ownedContentInitialized)
            {
                Transform section = SkillBarController.FindDescendant(transform, "OwnedSkillSection");
                ScrollRect scroll = section != null ? section.GetComponentInChildren<ScrollRect>(true) : null;
                _ownedContent = scroll != null ? scroll.content : SkillBarController.FindDescendant(section, "Content");
                _ownedSlotPrefab = Resources.Load<GameObject>("Prefabs/InventorySlot");
                if (_ownedContent != null)
                {
                    // Scene children are layout previews only. Runtime contents are always
                    // rebuilt from the actual owned-skill inventory.
                    for (int i = _ownedContent.childCount - 1; i >= 0; i--)
                    {
                        GameObject preview = _ownedContent.GetChild(i).gameObject;
                        preview.SetActive(false);
                        Destroy(preview);
                    }
                }
                _ownedContentInitialized = true;
            }

            while (_ownedSlots.Count < count && _ownedSlotPrefab != null && _ownedContent != null)
            {
                GameObject clone = Instantiate(_ownedSlotPrefab, _ownedContent);
                clone.name = $"OwnedSkillSlot{_ownedSlots.Count + 1:00}";
                OwnedSkillItemUI item = clone.GetComponent<OwnedSkillItemUI>() ?? clone.AddComponent<OwnedSkillItemUI>();
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
            if (_equippedSlots.Count == 0)
                BuildEquippedSlots();
            foreach (EquippedSkillPopupSlotUI slot in _equippedSlots)
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
            if (_selectedIcon != null)
            {
                _selectedIcon.sprite = _selectedSkill.icon;
                _selectedIcon.enabled = _selectedSkill.icon != null;
                _selectedIcon.preserveAspect = true;
            }
            if (_rarityBadge != null) _rarityBadge.color = rarityColor;
            if (_rarityText != null)
                _rarityText.text = SkillUiFormatting.Rarity(_selectedSkill.rarity);
            if (_skillNameText != null) _skillNameText.text = _selectedSkill.displayName;
            if (_descriptionText != null) _descriptionText.text = _selectedSkill.description;
            if (_ownedEffectText != null) _ownedEffectText.text = SkillUiFormatting.Effect(_selectedSkill, level);
            if (_selectedLevelText != null) _selectedLevelText.text = $"Lv.{Mathf.Max(0, level)}";
            if (_selectedProgressText != null)
                _selectedProgressText.text = isMax ? "MAX" : $"{Mathf.Max(0, duplicates)}/{Mathf.Max(1, required)}";
            if (_selectedProgressFill != null)
            {
                _selectedProgressFill.type = Image.Type.Filled;
                _selectedProgressFill.fillMethod = Image.FillMethod.Horizontal;
                _selectedProgressFill.fillOrigin = (int)Image.OriginHorizontal.Left;
                _selectedProgressFill.fillAmount = isMax
                    ? 1f
                    : Mathf.Clamp01(duplicates / (float)Mathf.Max(1, required));
            }

            bool alreadyEquipped = false;
            if (_battle != null)
                for (int i = 0; i < _battle.UnlockedSkillSlotCount; i++)
                    if (_battle.GetEquippedSkillId(i) == _selectedSkill.id) alreadyEquipped = true;
            if (_equipButton != null)
                _equipButton.interactable = _battle != null && level > 0 && !alreadyEquipped && _battle.UnlockedSkillSlotCount > 0;
        }

        private void RefreshSelectedPanelVisibility(bool selected)
        {
            if (_selectedPanel == null)
                return;

            _selectedPanel.SetActive(true);
            Transform panel = _selectedPanel.transform;
            for (int i = 0; i < panel.childCount; i++)
            {
                GameObject child = panel.GetChild(i).gameObject;
                bool isEmptyState = child == _emptyState || child.name == "EmptyState";
                bool isBackground = child.name == "BG";
                child.SetActive(selected ? !isEmptyState : isBackground || isEmptyState);
            }
        }

        private void RefreshTotalOwnedEffects()
        {
            if (_battle == null) return;
            var totals = new Dictionary<EquipmentEffectType, float>();
            foreach (SkillData skill in _battle.GetOwnedSkills())
            {
                SkillSaveEntry state = _battle.GetSkillState(skill.id);
                float value = SkillBalance.OwnedEffectValue(skill, state?.level ?? 0);
                totals[skill.ownedEffectType] = totals.TryGetValue(skill.ownedEffectType, out float old) ? old + value : value;
            }
            foreach (var pair in _totalEffectTexts)
            {
                float value = totals.TryGetValue(pair.Key, out float total) ? total : 0f;
                if (pair.Value != null) pair.Value.text = $"+{value:0.##}%";
            }
        }

        private void RefreshMergeButton()
        {
            bool canMerge = false;
            if (_battle != null)
                foreach (SkillData skill in _battle.GetOwnedSkills())
                    if (_battle.CanUpgradeSkill(skill.id)) { canMerge = true; break; }
            if (_mergeAllButton != null) _mergeAllButton.interactable = canMerge;
            if (_mergeRedDot != null) _mergeRedDot.SetActive(canMerge);
        }

        private void FindReferences()
        {
            Transform selected = SkillBarController.FindDescendant(transform, "SelectedSkillPanel");
            _selectedPanel ??= selected?.gameObject;
            _emptyState ??= SkillBarController.FindDescendant(selected, "EmptyState")?.gameObject;
            _selectedIcon ??= Find<Image>(selected, "Icon_Skill");
            _rarityBadge ??= Find<Image>(selected, "Badge_Rarity");
            _rarityText ??= Find<TMP_Text>(selected, "RarityText");
            _skillNameText ??= Find<TMP_Text>(selected, "SkillNameText");
            _descriptionText ??= Find<TMP_Text>(selected, "DescriptionText");
            _ownedEffectText ??= Find<TMP_Text>(selected, "OwnedEffectValueText");
            Transform levelProgress = SkillBarController.FindDescendant(selected, "LevelProgressGroup");
            _selectedLevelText ??= Find<TMP_Text>(levelProgress, "LevelText");
            Transform progressBar = SkillBarController.FindDescendant(levelProgress, "ProgressBar");
            _selectedProgressFill ??= Find<Image>(progressBar, "Fill");
            _selectedProgressText ??= Find<TMP_Text>(progressBar, "ProgressText");

            Transform totalSection = SkillBarController.FindDescendant(transform, "TotalOwnedEffectSection");
            FindTotalEffectText(totalSection, "Stat_Attack", EquipmentEffectType.AttackPercent);
            FindTotalEffectText(totalSection, "Stat_CriticalRate", EquipmentEffectType.CriticalChancePercentPoint);
            FindTotalEffectText(totalSection, "Stat_CriticalDamage", EquipmentEffectType.CriticalDamagePercent);
            FindTotalEffectText(totalSection, "Stat_SkillDamage", EquipmentEffectType.SkillDamagePercent);
            FindTotalEffectText(totalSection, "Stat_BossDamage", EquipmentEffectType.BossDamagePercent);
            Transform equip = SkillBarController.FindDescendant(transform, "Btn_Equip");
            _equipButton ??= equip?.GetComponent<Button>();
            Transform merge = SkillBarController.FindDescendant(transform, "Btn_MergeAll");
            _mergeAllButton ??= merge?.GetComponent<Button>();
            _mergeRedDot ??= SkillBarController.FindDescendant(merge, "Icon_RedDot")?.gameObject;
        }

        private void BindButtons()
        {
            if (_equipButton != null)
            {
                if (_equipAction != null) _equipButton.onClick.RemoveListener(_equipAction);
                _equipAction = EquipSelected;
                _equipButton.onClick.AddListener(_equipAction);
            }
            if (_mergeAllButton != null)
            {
                if (_mergeAction != null) _mergeAllButton.onClick.RemoveListener(_mergeAction);
                _mergeAction = MergeAll;
                _mergeAllButton.onClick.AddListener(_mergeAction);
            }
        }

        private static T Find<T>(Transform root, string name) where T : Component =>
            SkillBarController.FindDescendant(root, name)?.GetComponent<T>();

        private void FindTotalEffectText(Transform section, string cardName, EquipmentEffectType type)
        {
            if (_totalEffectTexts.ContainsKey(type))
                return;
            Transform card = SkillBarController.FindDescendant(section, cardName);
            TMP_Text value = Find<TMP_Text>(card, "ValueText");
            if (value != null) _totalEffectTexts[type] = value;
        }

        private void OnDestroy()
        {
            if (_battle != null) _battle.StateChanged -= Refresh;
            if (_equipButton != null && _equipAction != null) _equipButton.onClick.RemoveListener(_equipAction);
            if (_mergeAllButton != null && _mergeAction != null) _mergeAllButton.onClick.RemoveListener(_mergeAction);
        }
    }
}

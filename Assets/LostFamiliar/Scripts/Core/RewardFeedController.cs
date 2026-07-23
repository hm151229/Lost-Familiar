using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace LostFamiliar.Battle
{
    public enum RewardType
    {
        Gold,
        Gem,
        PlayerExperience
    }

    public readonly struct RewardNotification
    {
        public RewardType Type { get; }
        public double Amount { get; }
        public string LabelOverride { get; }
        public Sprite IconOverride { get; }

        public RewardNotification(RewardType type, double amount, string labelOverride = null, Sprite iconOverride = null)
        {
            Type = type;
            Amount = amount;
            LabelOverride = labelOverride;
            IconOverride = iconOverride;
        }
    }

    [Serializable]
    public sealed class RewardFeedVisual
    {
        public RewardType type;
        public Sprite icon;
        public string acquisitionText;

        public RewardFeedVisual() { }

        public RewardFeedVisual(RewardType type, string acquisitionText)
        {
            this.type = type;
            this.acquisitionText = acquisitionText;
        }
    }

    [DisallowMultipleComponent]
    public sealed class RewardFeedController : MonoBehaviour
    {
        [Header("프리팹 / 컨테이너")]
        [SerializeField] private Transform container;
        [SerializeField] private RewardFeedItemUI itemPrefab;
        [SerializeField] private string resourcesPrefabPath = "UI/RewardFeedItem";

        [Header("표시 규칙")]
        [SerializeField, Range(1, 4)] private int maxVisibleItems = 4;
        [SerializeField, Min(.1f)] private float visibleDuration = 3f;
        [SerializeField, Min(0f)] private float spacing = 4f;

        [Header("보상별 아이콘 / 문구")]
        [SerializeField] private RewardFeedVisual[] visuals =
        {
            new RewardFeedVisual(RewardType.Gold, "골드"),
            new RewardFeedVisual(RewardType.Gem, "보석"),
            new RewardFeedVisual(RewardType.PlayerExperience, "경험치")
        };

        private readonly List<RewardFeedItemUI> _visibleItems = new();
        private MainBattleLoop _battle;
        private bool _missingPrefabWarningShown;

        public void Bind(MainBattleLoop battle)
        {
            if (_battle != null)
                _battle.RewardGained -= OnRewardGained;

            _battle = battle;
            if (container == null)
                container = transform;
            if (itemPrefab == null && !string.IsNullOrWhiteSpace(resourcesPrefabPath))
                itemPrefab = Resources.Load<RewardFeedItemUI>(resourcesPrefabPath);
            EnsureLayout();
            _battle.RewardGained += OnRewardGained;
        }

        public void ShowReward(RewardNotification reward)
        {
            RemoveDestroyedItems();
            if (itemPrefab == null)
            {
                if (!_missingPrefabWarningShown)
                {
                    Debug.LogWarning(
                        "RewardFeedItemUI 프리팹이 연결되지 않았습니다. RewardFeedController의 Item Prefab에 연결하거나 " +
                        "Assets/LostFamiliar/Resources/UI/RewardFeedItem.prefab 경로에 생성해주세요.",
                        this);
                    _missingPrefabWarningShown = true;
                }
                return;
            }

            while (_visibleItems.Count >= maxVisibleItems)
                RemoveOldestImmediately();

            RewardFeedVisual visual = FindVisual(reward.Type);
            Sprite icon = reward.IconOverride != null ? reward.IconOverride : visual?.icon;
            string message = !string.IsNullOrWhiteSpace(reward.LabelOverride)
                ? reward.LabelOverride
                : visual?.acquisitionText ?? GetDefaultLabel(reward.Type);

            RewardFeedItemUI item = Instantiate(itemPrefab, container, false);
            item.transform.SetAsLastSibling();
            _visibleItems.Add(item);
            item.Show(icon, message, reward.Amount, visibleDuration, OnItemExpired);
        }

        private void OnRewardGained(RewardNotification reward) => ShowReward(reward);

        private void OnItemExpired(RewardFeedItemUI item)
        {
            _visibleItems.Remove(item);
        }

        private void RemoveOldestImmediately()
        {
            if (_visibleItems.Count == 0)
                return;

            RewardFeedItemUI oldest = _visibleItems[0];
            _visibleItems.RemoveAt(0);
            if (oldest != null)
                oldest.DismissImmediately();
        }

        private void RemoveDestroyedItems()
        {
            _visibleItems.RemoveAll(item => item == null);
        }

        private RewardFeedVisual FindVisual(RewardType type)
        {
            if (visuals == null)
                return null;

            foreach (RewardFeedVisual visual in visuals)
            {
                if (visual != null && visual.type == type)
                    return visual;
            }

            return null;
        }

        private static string GetDefaultLabel(RewardType type)
        {
            return type switch
            {
                RewardType.Gold => "골드",
                RewardType.Gem => "보석",
                RewardType.PlayerExperience => "경험치",
                _ => "보상"
            };
        }

        private void EnsureLayout()
        {
            if (container == null)
                return;

            VerticalLayoutGroup layout = container.GetComponent<VerticalLayoutGroup>();
            if (layout == null)
                layout = container.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.childAlignment = TextAnchor.UpperLeft;
            layout.spacing = spacing;
            layout.childControlWidth = true;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            layout.reverseArrangement = false;
        }

        private void OnValidate()
        {
            maxVisibleItems = Mathf.Clamp(maxVisibleItems, 1, 4);
            visibleDuration = Mathf.Max(.1f, visibleDuration);
            spacing = Mathf.Max(0f, spacing);
        }

        private void OnDestroy()
        {
            if (_battle != null)
                _battle.RewardGained -= OnRewardGained;
        }
    }
}

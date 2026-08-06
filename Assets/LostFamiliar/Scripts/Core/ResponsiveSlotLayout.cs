using UnityEngine;
using UnityEngine.UI;

namespace LostFamiliar.UI
{
    /// <summary>
    /// Resizes direct child slots to fit the current layout rect while preserving
    /// their aspect ratio. Attach this to the same object as a Grid, Horizontal,
    /// or Vertical Layout Group.
    /// </summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(RectTransform))]
    public sealed class ResponsiveSlotLayout : MonoBehaviour
    {
        [Header("Slot Size")]
        [SerializeField, Min(.01f)] private float widthToHeightRatio = 1f;
        [SerializeField, Min(0f)] private float minimumWidth;
        [SerializeField, Min(0f)] private float maximumWidth;

        [Header("Grid")]
        [Tooltip("0이면 GridLayoutGroup의 Constraint Count를 사용합니다.")]
        [SerializeField, Min(0)] private int gridColumnOverride;

        [Header("Children")]
        [SerializeField] private bool includeInactiveChildren;
        [Tooltip("활성화하면 자식의 LayoutElement를 자동으로 추가하고 크기를 적용합니다.")]
        [SerializeField] private bool createLayoutElements = true;

        private RectTransform _rectTransform;
        private GridLayoutGroup _grid;
        private HorizontalLayoutGroup _horizontal;
        private VerticalLayoutGroup _vertical;
        private Vector2 _lastRectSize = new Vector2(float.MinValue, float.MinValue);
        private int _lastChildCount = -1;
        private int _lastLayoutChildCount = -1;
        private bool _refreshQueued;

        private void Awake()
        {
            CacheComponents();
            RefreshNow();
        }

        private void OnEnable()
        {
            CacheComponents();
            RefreshNow();
        }

        private void OnValidate()
        {
            widthToHeightRatio = Mathf.Max(.01f, widthToHeightRatio);
            minimumWidth = Mathf.Max(0f, minimumWidth);
            maximumWidth = Mathf.Max(0f, maximumWidth);
            QueueRefresh();
        }

        private void OnRectTransformDimensionsChange() => QueueRefresh();
        private void OnTransformChildrenChanged() => QueueRefresh();

        private void LateUpdate()
        {
            if (_rectTransform == null)
                CacheComponents();

            Vector2 size = _rectTransform != null ? _rectTransform.rect.size : Vector2.zero;
            int layoutChildCount = CountLayoutChildren();
            if (_refreshQueued || size != _lastRectSize ||
                transform.childCount != _lastChildCount ||
                layoutChildCount != _lastLayoutChildCount)
                RefreshNow();
        }

        [ContextMenu("Refresh Responsive Layout")]
        public void RefreshNow()
        {
            CacheComponents();
            _refreshQueued = false;
            if (_rectTransform == null)
                return;

            if (_grid != null)
                RefreshGrid();
            else if (_horizontal != null)
                RefreshHorizontal();
            else if (_vertical != null)
                RefreshVertical();

            _lastRectSize = _rectTransform.rect.size;
            _lastChildCount = transform.childCount;
            _lastLayoutChildCount = CountLayoutChildren();
            LayoutRebuilder.MarkLayoutForRebuild(_rectTransform);
        }

        private void RefreshGrid()
        {
            int columns = gridColumnOverride > 0
                ? gridColumnOverride
                : Mathf.Max(1, _grid.constraintCount);
            if (_grid.constraint != GridLayoutGroup.Constraint.FixedColumnCount ||
                _grid.constraintCount != columns)
            {
                _grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
                _grid.constraintCount = columns;
            }

            float availableWidth = GetInnerWidth(_grid.padding) -
                                   _grid.spacing.x * Mathf.Max(0, columns - 1);
            float width = ClampWidth(availableWidth / columns);
            _grid.cellSize = new Vector2(width, width / widthToHeightRatio);
        }

        private void RefreshHorizontal()
        {
            int count = CountLayoutChildren();
            if (count <= 0)
                return;

            float availableWidth = GetInnerWidth(_horizontal.padding) -
                                   _horizontal.spacing * Mathf.Max(0, count - 1);
            float width = ClampWidth(availableWidth / count);
            _horizontal.childControlWidth = true;
            _horizontal.childControlHeight = true;
            _horizontal.childForceExpandWidth = false;
            _horizontal.childForceExpandHeight = false;
            ApplyChildSizes(width, width / widthToHeightRatio);
        }

        private void RefreshVertical()
        {
            // A VerticalLayoutGroup is treated as a single-column slot list.
            float width = ClampWidth(GetInnerWidth(_vertical.padding));
            _vertical.childControlWidth = true;
            _vertical.childControlHeight = true;
            _vertical.childForceExpandWidth = false;
            _vertical.childForceExpandHeight = false;
            ApplyChildSizes(width, width / widthToHeightRatio);
        }

        private float GetInnerWidth(RectOffset padding) => Mathf.Max(
            0f,
            _rectTransform.rect.width - padding.left - padding.right);

        private float ClampWidth(float width)
        {
            width = Mathf.Max(0f, width);
            if (minimumWidth > 0f)
                width = Mathf.Max(minimumWidth, width);
            if (maximumWidth > 0f)
                width = Mathf.Min(maximumWidth, width);
            return width;
        }

        private int CountLayoutChildren()
        {
            int count = 0;
            for (int i = 0; i < transform.childCount; i++)
            {
                GameObject child = transform.GetChild(i).gameObject;
                if (!includeInactiveChildren && !child.activeSelf)
                    continue;
                LayoutElement element = child.GetComponent<LayoutElement>();
                if (element != null && element.ignoreLayout)
                    continue;
                count++;
            }
            return count;
        }

        private void ApplyChildSizes(float width, float height)
        {
            for (int i = 0; i < transform.childCount; i++)
            {
                GameObject child = transform.GetChild(i).gameObject;
                if (!includeInactiveChildren && !child.activeSelf)
                    continue;

                LayoutElement element = child.GetComponent<LayoutElement>();
                if (element == null && createLayoutElements)
                    element = child.AddComponent<LayoutElement>();
                if (element == null || element.ignoreLayout)
                    continue;

                element.minWidth = width;
                element.preferredWidth = width;
                element.flexibleWidth = 0f;
                element.minHeight = height;
                element.preferredHeight = height;
                element.flexibleHeight = 0f;
            }
        }

        private void CacheComponents()
        {
            _rectTransform ??= GetComponent<RectTransform>();
            _grid ??= GetComponent<GridLayoutGroup>();
            _horizontal ??= GetComponent<HorizontalLayoutGroup>();
            _vertical ??= GetComponent<VerticalLayoutGroup>();
        }

        private void QueueRefresh() => _refreshQueued = true;
    }
}

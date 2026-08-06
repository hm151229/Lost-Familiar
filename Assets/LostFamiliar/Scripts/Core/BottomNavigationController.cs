using System.Collections;
using System.Collections.Generic;
using LostFamiliar.Core;
using UnityEngine;
using UnityEngine.UI;

namespace LostFamiliar.Battle
{
    [DisallowMultipleComponent]
    public sealed class BottomNavigationController : MonoBehaviour
    {
        [Header("하단 메뉴 버튼")]
        [SerializeField] private Button summonButton;
        [SerializeField] private Button equipmentButton;
        [SerializeField] private Button upgradeButton;
        [SerializeField] private Button adventureButton;

        [Header("연결 팝업")]
        [SerializeField] private GameObject summonPopup;
        [SerializeField] private GameObject equipmentPopup;
        [SerializeField] private GameObject upgradePopup;
        [SerializeField] private GameObject adventurePopup;

        [Header("선택 표현")]
        [SerializeField, Min(1f)] private float normalHeight = 200f;
        [SerializeField, Min(1f)] private float selectedHeight = 230f;
        [SerializeField] private Color normalColor = new Color32(0x9C, 0x95, 0x91, 0xFF);
        [SerializeField] private Color selectedColor = Color.white;

        [Header("Popup Entrance")]
        [SerializeField, Min(0f)] private float popupStartOffsetY = 320f;
        [SerializeField, Min(0.05f)] private float popupEntranceDuration = .38f;

        private Button[] _buttons;
        private GameObject[] _popups;
        private Vector2[] _popupBasePositions;
        private Vector3[] _popupBaseScales;
        private Coroutine[] _popupEntranceRoutines;
        private MainBattleLoop _battle;
        private GameObject _summonRedDot;
        private GameObject _equipmentRedDot;
        private GameObject _upgradeRedDot;
        private GameObject _adventureRedDot;
        private Vector3 _summonRedDotScale = Vector3.one;
        private Vector3 _equipmentRedDotScale = Vector3.one;
        private Vector3 _upgradeRedDotScale = Vector3.one;
        private Vector3 _adventureRedDotScale = Vector3.one;
        private bool _initialized;

        private void Awake() => Initialize();

        private void Initialize()
        {
            if (_initialized)
                return;

            _initialized = true;
            AutoFindReferences();
            _buttons = new[] { summonButton, equipmentButton, upgradeButton, adventureButton };
            _popups = new[] { summonPopup, equipmentPopup, upgradePopup, adventurePopup };
            CachePopupTransforms();

            if (summonButton != null)
                summonButton.onClick.AddListener(OpenSummon);
            if (equipmentButton != null)
                equipmentButton.onClick.AddListener(OpenEquipment);
            if (upgradeButton != null)
                upgradeButton.onClick.AddListener(OpenUpgrade);
            if (adventureButton != null)
                adventureButton.onClick.AddListener(OpenAdventure);

            CloseAll();
        }

        private void LateUpdate()
        {
            RefreshVisuals();
            RefreshRedDots();
        }

        public void CloseAll()
        {
            if (_popups == null)
                return;

            for (int i = 0; i < _popups.Length; i++)
            {
                GameObject popup = _popups[i];
                if (popup != null)
                {
                    ResetPopupTransform(i);
                    popup.SetActive(false);
                }
            }
            RefreshVisuals();
        }

        private void OpenSummon() => TogglePopup(0);
        private void OpenEquipment()
        {
            if (TogglePopup(1))
                equipmentPopup?.GetComponent<EquipmentPopupController>()?.RefreshNow();
        }
        private void OpenUpgrade() => TogglePopup(2);
        private void OpenAdventure() => TogglePopup(3);

        private bool TogglePopup(int popupIndex)
        {
            if (_popups == null || popupIndex < 0 || popupIndex >= _popups.Length ||
                _popups[popupIndex] == null)
                return false;

            if (_popups[popupIndex].activeInHierarchy)
            {
                return false;
            }

            OpenOnly(popupIndex);
            return true;
        }

        private void OpenOnly(int selectedIndex)
        {
            for (int i = 0; i < _popups.Length; i++)
            {
                if (_popups[i] != null)
                {
                    if (i != selectedIndex)
                        ResetPopupTransform(i);
                    _popups[i].SetActive(i == selectedIndex);
                }
            }
            PlayPopupEntrance(selectedIndex);
            RefreshVisuals();
        }

        private void CachePopupTransforms()
        {
            _popupBasePositions = new Vector2[_popups.Length];
            _popupBaseScales = new Vector3[_popups.Length];
            _popupEntranceRoutines = new Coroutine[_popups.Length];
            for (int i = 0; i < _popups.Length; i++)
            {
                RectTransform rect = _popups[i] != null ? _popups[i].transform as RectTransform : null;
                if (rect == null)
                    continue;
                _popupBasePositions[i] = rect.anchoredPosition;
                _popupBaseScales[i] = rect.localScale;
            }
        }

        private void PlayPopupEntrance(int popupIndex)
        {
            if (_popupEntranceRoutines == null || popupIndex < 0 || popupIndex >= _popups.Length)
                return;
            RectTransform rect = _popups[popupIndex] != null
                ? _popups[popupIndex].transform as RectTransform
                : null;
            if (rect == null)
                return;

            if (_popupEntranceRoutines[popupIndex] != null)
                StopCoroutine(_popupEntranceRoutines[popupIndex]);
            _popupEntranceRoutines[popupIndex] = StartCoroutine(PopupEntranceRoutine(popupIndex, rect));
        }

        private IEnumerator PopupEntranceRoutine(int popupIndex, RectTransform rect)
        {
            Vector2 basePosition = _popupBasePositions[popupIndex];
            Vector3 baseScale = _popupBaseScales[popupIndex];
            Vector3 startScale = new Vector3(baseScale.x, baseScale.y * .97f, baseScale.z);
            Vector2 startPosition = basePosition + Vector2.down * popupStartOffsetY;
            rect.anchoredPosition = startPosition;
            rect.localScale = startScale;

            float elapsed = 0f;
            while (elapsed < popupEntranceDuration && rect != null && rect.gameObject.activeInHierarchy)
            {
                elapsed += Time.unscaledDeltaTime;
                float progress = Mathf.Clamp01(elapsed / popupEntranceDuration);
                float shifted = progress - 1f;
                const float overshoot = 1.70158f;
                float eased = 1f + (overshoot + 1f) * shifted * shifted * shifted +
                              overshoot * shifted * shifted;
                rect.anchoredPosition = Vector2.LerpUnclamped(startPosition, basePosition, eased);
                rect.localScale = Vector3.Lerp(startScale, baseScale, Mathf.SmoothStep(0f, 1f, progress));
                yield return null;
            }

            if (rect != null)
            {
                rect.anchoredPosition = basePosition;
                rect.localScale = baseScale;
            }
            _popupEntranceRoutines[popupIndex] = null;
        }

        private void ResetPopupTransform(int popupIndex)
        {
            if (_popupEntranceRoutines == null || popupIndex < 0 || popupIndex >= _popups.Length)
                return;
            if (_popupEntranceRoutines[popupIndex] != null)
            {
                StopCoroutine(_popupEntranceRoutines[popupIndex]);
                _popupEntranceRoutines[popupIndex] = null;
            }

            RectTransform rect = _popups[popupIndex] != null
                ? _popups[popupIndex].transform as RectTransform
                : null;
            if (rect == null)
                return;
            rect.anchoredPosition = _popupBasePositions[popupIndex];
            rect.localScale = _popupBaseScales[popupIndex];
        }

        private void RefreshVisuals()
        {
            if (_buttons == null || _popups == null)
                return;

            for (int i = 0; i < _buttons.Length; i++)
            {
                bool selected = _popups[i] != null && _popups[i].activeInHierarchy;
                ApplyVisual(_buttons[i], selected);
            }
        }

        private void ApplyVisual(Button button, bool selected)
        {
            if (button == null)
                return;

            RectTransform rect = button.transform as RectTransform;
            if (rect != null)
                rect.SetSizeWithCurrentAnchors(
                    RectTransform.Axis.Vertical,
                    selected ? selectedHeight : normalHeight);

            Image image = button.GetComponent<Image>();
            if (image != null)
                image.color = selected ? selectedColor : normalColor;
        }

        private void AutoFindReferences()
        {
            summonButton ??= FindSceneComponent<Button>("SummonButton");
            equipmentButton ??= FindSceneComponent<Button>("EquipmentButton");
            upgradeButton ??= FindSceneComponent<Button>("UpgradeButton");
            adventureButton ??= FindSceneComponent<Button>("AdventureButton");
            summonPopup ??= FindSceneObject("SummonPopup");
            equipmentPopup ??= FindSceneObject("EquipmentPopup");
            upgradePopup ??= FindSceneObject("UpgradePopup");
            adventurePopup ??= FindSceneObject("AdventurePopup");

            CacheRedDot(summonButton, ref _summonRedDot, ref _summonRedDotScale);
            CacheRedDot(equipmentButton, ref _equipmentRedDot, ref _equipmentRedDotScale);
            CacheRedDot(upgradeButton, ref _upgradeRedDot, ref _upgradeRedDotScale);
            CacheRedDot(adventureButton, ref _adventureRedDot, ref _adventureRedDotScale);
        }

        private void RefreshRedDots()
        {
            _battle ??= FindFirstObjectByType<MainBattleLoop>();
            if (_battle == null)
            {
                SetRedDot(_summonRedDot, false, _summonRedDotScale);
                SetRedDot(_equipmentRedDot, false, _equipmentRedDotScale);
                SetRedDot(_upgradeRedDot, false, _upgradeRedDotScale);
                SetRedDot(_adventureRedDot, false, _adventureRedDotScale);
                return;
            }

            bool canGacha = _battle.Gems >= GachaBalance.Cost(10);
            EquipmentInventory inventory = _battle.EquipmentInventory;
            bool hasEquipmentAction = inventory != null &&
                (inventory.HasUpgradeableEquipment(EquipmentType.Head) ||
                 inventory.HasUpgradeableEquipment(EquipmentType.Body) ||
                 inventory.HasUpgradeableEquipment(EquipmentType.Shoes) ||
                 inventory.HasUpgradeableEquipment(EquipmentType.Accessory) ||
                 inventory.HasUpgradeableEquipment(EquipmentType.Weapon) ||
                 inventory.CanAutoEquipBetter());
            bool canUpgradeStat =
                _battle.CanUpgrade(StatType.Attack) ||
                _battle.CanUpgrade(StatType.CriticalChance) ||
                _battle.CanUpgrade(StatType.CriticalDamage) ||
                _battle.CanUpgrade(StatType.SkillDamage) ||
                _battle.CanUpgrade(StatType.BossDamage);
            TowerProgressData goldTower = _battle.GetTowerProgress(TowerType.Gold);
            TowerProgressData gemTower = _battle.GetTowerProgress(TowerType.Gem);
            bool hasTowerTicket = (goldTower?.tickets ?? 0) > 0 || (gemTower?.tickets ?? 0) > 0;

            SetRedDot(_summonRedDot, canGacha, _summonRedDotScale);
            SetRedDot(_equipmentRedDot, hasEquipmentAction, _equipmentRedDotScale);
            SetRedDot(_upgradeRedDot, canUpgradeStat, _upgradeRedDotScale);
            SetRedDot(_adventureRedDot, hasTowerTicket, _adventureRedDotScale);
        }

        private static void CacheRedDot(Button button, ref GameObject redDot, ref Vector3 baseScale)
        {
            if (redDot != null || button == null)
                return;

            Transform target = FindDescendant(button.transform, "RedDotIconImage");
            if (target == null)
                return;

            redDot = target.gameObject;
            baseScale = target.localScale;
            redDot.SetActive(false);
        }

        private static void SetRedDot(GameObject redDot, bool visible, Vector3 baseScale)
        {
            if (redDot == null)
                return;

            if (redDot.activeSelf != visible)
                redDot.SetActive(visible);

            if (!visible)
            {
                redDot.transform.localScale = baseScale;
                return;
            }

            float pulse = (Mathf.Sin(Time.unscaledTime * 5f) + 1f) * .5f;
            redDot.transform.localScale = baseScale * Mathf.Lerp(.82f, 1.12f, pulse);
        }

        private static Transform FindDescendant(Transform root, string objectName)
        {
            if (root == null)
                return null;
            foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
                if (child.name == objectName)
                    return child;
            return null;
        }

        private static T FindSceneComponent<T>(string objectName) where T : Component
        {
            GameObject sceneObject = FindSceneObject(objectName);
            return sceneObject != null ? sceneObject.GetComponent<T>() : null;
        }

        private static GameObject FindSceneObject(string objectName)
        {
            foreach (Transform candidate in Resources.FindObjectsOfTypeAll<Transform>())
            {
                if (candidate.name == objectName && candidate.gameObject.scene.IsValid() &&
                    candidate.gameObject.scene.isLoaded)
                    return candidate.gameObject;
            }
            return null;
        }

        private void OnDestroy()
        {
            if (summonButton != null)
                summonButton.onClick.RemoveListener(OpenSummon);
            if (equipmentButton != null)
                equipmentButton.onClick.RemoveListener(OpenEquipment);
            if (upgradeButton != null)
                upgradeButton.onClick.RemoveListener(OpenUpgrade);
            if (adventureButton != null)
                adventureButton.onClick.RemoveListener(OpenAdventure);
        }
    }

    // Marker for buttons that must block clicks without changing their appearance.
    public sealed class KeepButtonVisualWhenDisabled : MonoBehaviour { }

    // Graphics below this marker keep their original alpha even if an ancestor
    // button is disabled.
    public sealed class KeepGraphicVisualWhenButtonDisabled : MonoBehaviour { }

    // Unity's Button color transition only fades its Target Graphic. This system
    // gives every text/image added below the button the same disabled opacity.
    internal sealed class ButtonChildDisabledVisual : MonoBehaviour
    {
        private Button _button;
        private readonly Dictionary<Graphic, float> _baseAlpha = new Dictionary<Graphic, float>();
        private bool _wasInteractable;
        private bool _initialized;

        private void Awake()
        {
            _button = GetComponent<Button>();
            RefreshGraphics();
            _wasInteractable = _button == null || _button.interactable;
            _initialized = true;
            ApplyCurrentState();
        }

        internal void RefreshGraphics()
        {
            _button ??= GetComponent<Button>();
            if (_button == null)
                return;

            foreach (Graphic graphic in GetComponentsInChildren<Graphic>(true))
            {
                if (graphic == null || graphic == _button.targetGraphic)
                    continue;
                if (graphic.GetComponentInParent<Button>(true) != _button)
                    continue;
                if (graphic.GetComponentInParent<KeepGraphicVisualWhenButtonDisabled>(true) != null)
                {
                    if (_baseAlpha.TryGetValue(graphic, out float originalAlpha))
                    {
                        Color original = graphic.color;
                        original.a = originalAlpha;
                        graphic.color = original;
                        _baseAlpha.Remove(graphic);
                    }
                    continue;
                }
                if (!_baseAlpha.ContainsKey(graphic))
                    _baseAlpha.Add(graphic, graphic.color.a);
            }

            RemoveDestroyedGraphics();
            if (_initialized)
                ApplyCurrentState();
        }

        internal void RestoreAndRemove()
        {
            RestoreBaseAlpha();
            Destroy(this);
        }

        private void LateUpdate()
        {
            if (_button == null)
                return;

            bool interactable = _button.interactable;
            if (interactable != _wasInteractable)
            {
                if (interactable)
                    RestoreBaseAlpha();
                else
                    CaptureCurrentAlpha();
                _wasInteractable = interactable;
            }

            if (interactable)
                CaptureCurrentAlpha();
            else
                ApplyDisabledAlpha();
        }

        private void OnEnable()
        {
            if (!_initialized)
                return;
            RefreshGraphics();
            ApplyCurrentState();
        }

        private void ApplyCurrentState()
        {
            if (_button == null)
                return;
            if (_button.interactable)
                RestoreBaseAlpha();
            else
                ApplyDisabledAlpha();
            _wasInteractable = _button.interactable;
        }

        private void CaptureCurrentAlpha()
        {
            foreach (Graphic graphic in new List<Graphic>(_baseAlpha.Keys))
                if (graphic != null)
                    _baseAlpha[graphic] = graphic.color.a;
        }

        private void ApplyDisabledAlpha()
        {
            float disabledAlpha = _button.transition == Selectable.Transition.ColorTint
                ? _button.colors.disabledColor.a
                : .5f;
            foreach (KeyValuePair<Graphic, float> pair in _baseAlpha)
            {
                if (pair.Key == null)
                    continue;
                Color color = pair.Key.color;
                color.a = pair.Value * disabledAlpha;
                pair.Key.color = color;
            }
        }

        private void RestoreBaseAlpha()
        {
            foreach (KeyValuePair<Graphic, float> pair in _baseAlpha)
            {
                if (pair.Key == null)
                    continue;
                Color color = pair.Key.color;
                color.a = pair.Value;
                pair.Key.color = color;
            }
        }

        private void RemoveDestroyedGraphics()
        {
            foreach (Graphic graphic in new List<Graphic>(_baseAlpha.Keys))
                if (graphic == null)
                    _baseAlpha.Remove(graphic);
        }

        private void OnDisable()
        {
            RestoreBaseAlpha();
        }
    }

    internal sealed class ButtonDisabledVisualSystem : MonoBehaviour
    {
        private float _nextScanTime;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            if (FindFirstObjectByType<ButtonDisabledVisualSystem>() != null)
                return;

            GameObject system = new GameObject("ButtonDisabledVisualSystem");
            DontDestroyOnLoad(system);
            system.AddComponent<ButtonDisabledVisualSystem>();
        }

        private void OnEnable()
        {
            ScanButtons();
        }

        private void Update()
        {
            if (Time.unscaledTime < _nextScanTime)
                return;
            _nextScanTime = Time.unscaledTime + .5f;
            ScanButtons();
        }

        private static void ScanButtons()
        {
            Button[] buttons = FindObjectsByType<Button>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);
            foreach (Button button in buttons)
            {
                if (button == null || !button.gameObject.scene.IsValid())
                    continue;
                if (button.GetComponentInParent<InventorySlotView>(true) != null ||
                    button.GetComponent<KeepButtonVisualWhenDisabled>() != null)
                {
                    ButtonChildDisabledVisual existing =
                        button.GetComponent<ButtonChildDisabledVisual>();
                    if (existing != null)
                        existing.RestoreAndRemove();
                    continue;
                }
                ButtonChildDisabledVisual visual =
                    button.GetComponent<ButtonChildDisabledVisual>() ??
                    button.gameObject.AddComponent<ButtonChildDisabledVisual>();
                visual.RefreshGraphics();
            }
        }
    }
}

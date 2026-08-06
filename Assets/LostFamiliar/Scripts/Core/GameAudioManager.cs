using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace LostFamiliar.Core
{
    [DisallowMultipleComponent]
    public sealed class GameAudioManager : MonoBehaviour
    {
        private const string LibraryResourcePath = "GameAudioLibrary";
        private static GameAudioManager _instance;
        private GameAudioLibrary _library;
        private AudioSource _bgmSource;
        private AudioSource _sfxSource;
        private string _currentBgm;
        private bool _towerSceneLoaded;
        private bool _mainBattleSfxBlocked;
        private readonly List<Transform> _popupCandidates = new List<Transform>();

        public static GameAudioManager Instance
        {
            get
            {
                EnsureInstalled();
                return _instance;
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void EnsureInstalled()
        {
            if (_instance != null) return;
            GameObject host = new GameObject("GameAudioManager");
            DontDestroyOnLoad(host);
            _instance = host.AddComponent<GameAudioManager>();
        }

        private void Awake()
        {
            if (_instance != null && _instance != this) { Destroy(gameObject); return; }
            _instance = this;
            DontDestroyOnLoad(gameObject);
            _library = Resources.Load<GameAudioLibrary>(LibraryResourcePath);
            _bgmSource = CreateSource("BGM", true);
            _sfxSource = CreateSource("SFX", false);
            SceneManager.sceneLoaded += OnSceneLoaded;
            SceneManager.sceneUnloaded += OnSceneUnloaded;
            StartCoroutine(BindButtonsContinuously());
            StartCoroutine(RefreshBattleAudioContextContinuously());
        }

        private AudioSource CreateSource(string sourceName, bool loop)
        {
            GameObject child = new GameObject(sourceName);
            child.transform.SetParent(transform, false);
            AudioSource source = child.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.loop = loop;
            source.spatialBlend = 0f;
            return source;
        }

        private void Start()
        {
            PlaySceneBgm();
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            StopTemporaryLoops();
            CachePopupCandidates();
            RefreshBattleAudioContext();
            PlaySceneBgm();
        }

        private void OnSceneUnloaded(Scene scene)
        {
            StopTemporaryLoops();
            StartCoroutine(PlaySceneBgmNextFrame());
        }

        private void StopTemporaryLoops()
        {
            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                Transform child = transform.GetChild(i);
                if (child != null && child.name.StartsWith("TemporaryLoop_"))
                    Destroy(child.gameObject);
            }
        }

        private IEnumerator PlaySceneBgmNextFrame()
        {
            yield return null;
            CachePopupCandidates();
            RefreshBattleAudioContext();
            PlaySceneBgm();
        }

        private void PlaySceneBgm()
        {
            bool towerLoaded = false;
            for (int i = 0; i < SceneManager.sceneCount; i++)
                if (SceneManager.GetSceneAt(i).isLoaded &&
                    SceneManager.GetSceneAt(i).name.IndexOf("TowerBattle", System.StringComparison.OrdinalIgnoreCase) >= 0)
                    towerLoaded = true;
            PlayBgm(towerLoaded ? "BGM_Tower" : "BGM_MainBattle");
        }

        public void PlayBgm(string id, bool loop = true)
        {
            AudioClip clip = _library != null ? _library.Get(id) : null;
            if (clip == null || (_currentBgm == id && _bgmSource.isPlaying)) return;
            _currentBgm = id;
            _bgmSource.Stop();
            _bgmSource.clip = clip;
            _bgmSource.volume = _library.GetVolume(id);
            _bgmSource.loop = loop;
            _bgmSource.Play();
        }

        public void PlaySfx(string id, float volume = 1f)
        {
            AudioClip clip = _library != null ? _library.Get(id) : null;
            if (clip != null)
                _sfxSource.PlayOneShot(
                    clip, Mathf.Clamp01(volume) * _library.GetVolume(id));
        }

        public void PlayLoopForDuration(string id, float duration, float volume = 1f)
        {
            AudioClip clip = _library != null ? _library.Get(id) : null;
            if (clip != null)
                StartCoroutine(LoopRoutine(
                    clip,
                    Mathf.Max(.05f, duration),
                    Mathf.Clamp01(volume) * _library.GetVolume(id)));
        }

        public bool IsBattleAudioAllowed(int combatGroup)
        {
            return _towerSceneLoaded
                ? combatGroup > 0
                : combatGroup == 0 && !_mainBattleSfxBlocked;
        }

        private IEnumerator RefreshBattleAudioContextContinuously()
        {
            WaitForSecondsRealtime wait = new WaitForSecondsRealtime(.05f);
            while (true)
            {
                RefreshBattleAudioContext();
                yield return wait;
            }
        }

        private void RefreshBattleAudioContext()
        {
            bool towerLoaded = false;
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                Scene scene = SceneManager.GetSceneAt(i);
                if (scene.isLoaded && scene.name.IndexOf(
                        "TowerBattle", System.StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    towerLoaded = true;
                    break;
                }
            }

            bool blockMainBattleSfx = !towerLoaded && IsAnyPopupOpen();
            if (blockMainBattleSfx && !_mainBattleSfxBlocked)
                StopTemporaryLoops();
            _towerSceneLoaded = towerLoaded;
            _mainBattleSfxBlocked = blockMainBattleSfx;
        }

        private void CachePopupCandidates()
        {
            _popupCandidates.Clear();
            foreach (Transform candidate in Resources.FindObjectsOfTypeAll<Transform>())
            {
                GameObject popup = candidate.gameObject;
                if (!popup.scene.IsValid() || !popup.scene.isLoaded)
                    continue;
                if (!candidate.name.EndsWith("Popup", System.StringComparison.Ordinal))
                    continue;
                if (candidate.name == "MainPopup" || candidate.name == "Popup")
                    continue;
                _popupCandidates.Add(candidate);
            }
        }

        private bool IsAnyPopupOpen()
        {
            for (int i = _popupCandidates.Count - 1; i >= 0; i--)
            {
                Transform candidate = _popupCandidates[i];
                if (candidate == null)
                {
                    _popupCandidates.RemoveAt(i);
                    continue;
                }
                if (candidate.gameObject.activeInHierarchy)
                    return true;
            }
            return false;
        }

        private IEnumerator LoopRoutine(AudioClip clip, float duration, float volume)
        {
            AudioSource source = CreateSource("TemporaryLoop_" + clip.name, true);
            source.clip = clip;
            source.volume = Mathf.Clamp01(volume);
            source.Play();
            yield return new WaitForSeconds(duration);
            if (source != null) Destroy(source.gameObject);
        }

        private IEnumerator BindButtonsContinuously()
        {
            WaitForSecondsRealtime wait = new WaitForSecondsRealtime(.5f);
            while (true)
            {
                foreach (Button button in Resources.FindObjectsOfTypeAll<Button>())
                    if (button != null && button.gameObject.scene.IsValid() &&
                        button.GetComponent<GlobalButtonAudio>() == null)
                        button.gameObject.AddComponent<GlobalButtonAudio>();
                yield return wait;
            }
        }

        private void OnDestroy()
        {
            if (_instance != this) return;
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneUnloaded -= OnSceneUnloaded;
            _instance = null;
        }
    }

    [DisallowMultipleComponent]
    public sealed class GlobalButtonAudio : MonoBehaviour, IPointerDownHandler, IPointerClickHandler
    {
        private Button _button;
        private bool _wasUsableOnPointerDown;
        public bool LogicalLocked { get; private set; }

        private void Awake() => _button = GetComponent<Button>();

        public void SetLogicalLocked(bool locked) => LogicalLocked = locked;

        public void OnPointerDown(PointerEventData eventData)
        {
            if (eventData.button != PointerEventData.InputButton.Left) return;
            _wasUsableOnPointerDown = _button != null && _button.interactable && !LogicalLocked;
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (eventData.button != PointerEventData.InputButton.Left) return;
            GameAudioManager.Instance.PlaySfx(
                _wasUsableOnPointerDown ? "SFX_UI_Click" : "SFX_UI_Locked");
        }
    }
}

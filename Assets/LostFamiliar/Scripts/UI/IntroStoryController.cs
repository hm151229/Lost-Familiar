using System.Collections;
using LostFamiliar.Core;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace LostFamiliar.UI
{
    [DisallowMultipleComponent]
    public sealed class IntroStoryController : MonoBehaviour, IPointerClickHandler
    {
        [Header("UI")]
        [SerializeField] private Image introStoryImage;
        [SerializeField] private Button skipButton;

        [Header("Story")]
        [SerializeField] private Sprite[] storyImages = new Sprite[5];
        [SerializeField, Min(.1f)] private float imageDuration = 3f;
        [SerializeField, Min(.05f)] private float fadeDuration = .35f;
        [SerializeField] private string mainSceneName = "MainScene";

        private Coroutine _storyRoutine;
        private bool _advanceRequested;
        private bool _isFading;
        private bool _loading;

        private void Awake()
        {
            if (skipButton != null)
                skipButton.onClick.AddListener(Skip);
        }

        private void Start()
        {
            if (!HasValidConfiguration())
                return;

            _storyRoutine = StartCoroutine(PlayStory());
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (!_isFading && !_loading)
                _advanceRequested = true;
        }

        public void Skip()
        {
            if (_loading)
                return;

            if (_storyRoutine != null)
                StopCoroutine(_storyRoutine);

            _storyRoutine = StartCoroutine(FinishStory());
        }

        private IEnumerator PlayStory()
        {
            introStoryImage.sprite = storyImages[0];
            SetImageAlpha(1f);

            for (int index = 0; index < storyImages.Length; index++)
            {
                _advanceRequested = false;
                float elapsed = 0f;
                while (elapsed < imageDuration && !_advanceRequested)
                {
                    elapsed += Time.unscaledDeltaTime;
                    yield return null;
                }

                yield return FadeImage(0f);

                if (index >= storyImages.Length - 1)
                {
                    CompleteAndLoadMainScene();
                    yield break;
                }

                introStoryImage.sprite = storyImages[index + 1];
                yield return FadeImage(1f);
            }
        }

        private IEnumerator FinishStory()
        {
            _loading = true;
            if (skipButton != null)
                skipButton.interactable = false;

            yield return FadeImage(0f);
            CompleteAndLoadMainScene();
        }

        private IEnumerator FadeImage(float targetAlpha)
        {
            _isFading = true;
            float startAlpha = introStoryImage.color.a;
            float elapsed = 0f;

            while (elapsed < fadeDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float progress = Mathf.Clamp01(elapsed / fadeDuration);
                SetImageAlpha(Mathf.Lerp(startAlpha, targetAlpha, progress));
                yield return null;
            }

            SetImageAlpha(targetAlpha);
            _isFading = false;
        }

        private void CompleteAndLoadMainScene()
        {
            _loading = true;
            GameSaveData saveData = SaveService.Load();
            saveData.hasSeenIntroStory = true;
            SaveService.Save(saveData);
            SceneManager.LoadScene(mainSceneName);
        }

        private bool HasValidConfiguration()
        {
            if (introStoryImage == null)
            {
                Debug.LogError("IntroStoryImage가 연결되지 않았습니다.", this);
                return false;
            }

            if (storyImages == null || storyImages.Length == 0)
            {
                Debug.LogError("인트로 스토리 이미지가 연결되지 않았습니다.", this);
                return false;
            }

            for (int i = 0; i < storyImages.Length; i++)
            {
                if (storyImages[i] != null)
                    continue;

                Debug.LogError($"Story Images의 {i + 1}번째 이미지가 연결되지 않았습니다.", this);
                return false;
            }

            return true;
        }

        private void SetImageAlpha(float alpha)
        {
            Color color = introStoryImage.color;
            color.a = alpha;
            introStoryImage.color = color;
        }

        private void OnDestroy()
        {
            if (skipButton != null)
                skipButton.onClick.RemoveListener(Skip);
        }
    }
}

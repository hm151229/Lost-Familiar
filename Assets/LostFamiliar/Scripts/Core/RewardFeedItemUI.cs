using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace LostFamiliar.Battle
{
    [DisallowMultipleComponent]
    public sealed class RewardFeedItemUI : MonoBehaviour
    {
        [SerializeField] private Image rewardIcon;
        [SerializeField] private TMP_Text acquisitionText;
        [SerializeField] private TMP_Text amountText;
        [SerializeField, Min(0f)] private float fadeDuration = .25f;

        private CanvasGroup _canvasGroup;
        private Coroutine _lifetimeRoutine;
        private Action<RewardFeedItemUI> _expired;

        public void Show(
            Sprite icon,
            string message,
            double amount,
            float lifetime,
            Action<RewardFeedItemUI> expired)
        {
            AutoFindReferences();
            if (_canvasGroup == null)
                _canvasGroup = GetComponent<CanvasGroup>();
            if (_canvasGroup == null)
                _canvasGroup = gameObject.AddComponent<CanvasGroup>();
            _canvasGroup.alpha = 1f;
            _expired = expired;

            if (rewardIcon != null)
            {
                rewardIcon.sprite = icon;
                rewardIcon.enabled = icon != null;
            }

            if (acquisitionText != null)
                acquisitionText.text = message;
            if (amountText != null)
                amountText.text = $"+{MainHUDController.FormatNumber(amount)}";

            if (_lifetimeRoutine != null)
                StopCoroutine(_lifetimeRoutine);
            _lifetimeRoutine = StartCoroutine(LifetimeRoutine(Mathf.Max(.1f, lifetime)));
        }

        public void DismissImmediately()
        {
            _expired = null;
            if (_lifetimeRoutine != null)
                StopCoroutine(_lifetimeRoutine);
            _lifetimeRoutine = null;
            gameObject.SetActive(false);
            Destroy(gameObject);
        }

        [ContextMenu("Auto Find Item References")]
        public void AutoFindReferences()
        {
            if (rewardIcon == null)
                rewardIcon = FindImage("Icon", "RewardIcon", "UIIcon");

            TMP_Text[] texts = GetComponentsInChildren<TMP_Text>(true);
            foreach (TMP_Text text in texts)
            {
                string objectName = text.gameObject.name.ToLowerInvariant();
                if (amountText == null && (objectName.Contains("amount") || objectName.Contains("value")))
                    amountText = text;
                else if (acquisitionText == null &&
                         (objectName.Contains("acquisition") || objectName.Contains("reward") || objectName.Contains("label")))
                    acquisitionText = text;
            }

            if (acquisitionText == null && texts.Length > 0)
                acquisitionText = texts[0];
            if (amountText == null && texts.Length > 1)
                amountText = texts[texts.Length - 1];
        }

        private Image FindImage(params string[] candidateNames)
        {
            Image[] images = GetComponentsInChildren<Image>(true);
            foreach (string candidate in candidateNames)
            {
                foreach (Image image in images)
                {
                    if (string.Equals(image.gameObject.name, candidate, StringComparison.OrdinalIgnoreCase))
                        return image;
                }
            }

            return null;
        }

        private IEnumerator LifetimeRoutine(float lifetime)
        {
            float visibleDuration = Mathf.Max(0f, lifetime - fadeDuration);
            yield return new WaitForSecondsRealtime(visibleDuration);

            if (_canvasGroup != null && fadeDuration > 0f)
            {
                float elapsed = 0f;
                while (elapsed < fadeDuration)
                {
                    elapsed += Time.unscaledDeltaTime;
                    _canvasGroup.alpha = 1f - Mathf.Clamp01(elapsed / fadeDuration);
                    yield return null;
                }
            }

            _lifetimeRoutine = null;
            Action<RewardFeedItemUI> callback = _expired;
            _expired = null;
            callback?.Invoke(this);
            Destroy(gameObject);
        }
    }
}

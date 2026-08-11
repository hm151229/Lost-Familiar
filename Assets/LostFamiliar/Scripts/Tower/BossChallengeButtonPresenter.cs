using UnityEngine;
using UnityEngine.UI;

namespace LostFamiliar.Battle
{
    [DisallowMultipleComponent]
    public sealed class BossChallengeButtonPresenter : MonoBehaviour
    {
        [SerializeField] private GameObject buttonRoot;
        [SerializeField] private Button challengeButton;

        private MainBattleLoop _battle;
        private bool _missingButtonWarningShown;

        public void Bind(MainBattleLoop battle)
        {
            if (_battle != null)
                _battle.StateChanged -= Refresh;

            _battle = battle;
            if (_battle != null)
                _battle.StateChanged += Refresh;

            EnsureButton();
            Refresh();
        }

        private void EnsureButton()
        {
            if (buttonRoot != null && challengeButton != null)
                return;

            foreach (Transform candidate in Resources.FindObjectsOfTypeAll<Transform>())
            {
                if (candidate.name != "BossChallengeButton" ||
                    !candidate.gameObject.scene.IsValid() ||
                    !candidate.gameObject.scene.isLoaded)
                    continue;

                buttonRoot = candidate.gameObject;
                challengeButton = buttonRoot.GetComponent<Button>() ??
                                  buttonRoot.GetComponentInChildren<Button>(true);
                break;
            }

            if (challengeButton == null)
            {
                if (!_missingButtonWarningShown)
                {
                    Debug.LogWarning(
                        "씬에서 BossChallengeButton을 찾지 못했습니다. " +
                        "오브젝트 이름과 Button 컴포넌트를 확인해 주세요.", this);
                    _missingButtonWarningShown = true;
                }
                return;
            }

            challengeButton.onClick.RemoveListener(OnChallengeClicked);
            challengeButton.onClick.AddListener(OnChallengeClicked);
            _missingButtonWarningShown = false;
        }

        private void Refresh()
        {
            EnsureButton();
            if (buttonRoot != null)
                buttonRoot.SetActive(_battle != null && _battle.CanChallengeBoss);
        }

        private void OnChallengeClicked()
        {
            if (_battle != null && _battle.TryEnterBossBattle() && buttonRoot != null)
                buttonRoot.SetActive(false);
        }

        private void OnDestroy()
        {
            if (_battle != null)
                _battle.StateChanged -= Refresh;
            if (challengeButton != null)
                challengeButton.onClick.RemoveListener(OnChallengeClicked);
        }
    }
}

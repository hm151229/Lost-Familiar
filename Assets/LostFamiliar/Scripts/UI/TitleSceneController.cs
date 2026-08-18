using LostFamiliar.Core;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace LostFamiliar.UI
{
    [DisallowMultipleComponent]
    public sealed class TitleSceneController : MonoBehaviour
    {
        [SerializeField] private Button startButton;
        [SerializeField] private string introSceneName = "IntroStoryScene";
        [SerializeField] private string mainSceneName = "MainScene";

        private bool _loading;

        private void Awake()
        {
            if (startButton != null)
                startButton.onClick.AddListener(StartGame);
        }

        public void StartGame()
        {
            if (_loading)
                return;

            _loading = true;
            if (startButton != null)
                startButton.interactable = false;

            GameSaveData saveData = SaveService.Load();
            string targetScene = saveData.hasSeenIntroStory
                ? mainSceneName
                : introSceneName;

            SceneManager.LoadScene(targetScene);
        }

        private void OnDestroy()
        {
            if (startButton != null)
                startButton.onClick.RemoveListener(StartGame);
        }
    }
}

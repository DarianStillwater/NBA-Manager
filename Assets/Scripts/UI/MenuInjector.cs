using UnityEngine;
using UnityEngine.UI;
using NBAHeadCoach.Core;
using NBAHeadCoach.UI.Menu;
using static NBAHeadCoach.UI.Menu.MenuUI;

namespace NBAHeadCoach.UI
{
    /// <summary>
    /// Thin orchestrator — detects MainMenu scene and delegates to
    /// MainMenuBuilder, NewGameWizard, and LoadGameScreen.
    /// </summary>
    public class MenuInjector : MonoBehaviour
    {
        private string _lastScene;
        private bool _injected;
        private GameObject _menuRoot;

        private NewGameWizard _wizard;
        private LoadGameScreen _loadScreen;

        private void Update()
        {
            if (GameManager.Instance == null) return;
            string scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
            if (scene != _lastScene) { _lastScene = scene; _injected = false; _menuRoot = null; }
            if (scene != "MainMenu" || _injected) return;
            _injected = true;
            Invoke(nameof(InjectMenu), 0.2f);
        }

        private void InjectMenu()
        {
            var canvas = FindAnyObjectByType<Canvas>();
            if (canvas == null) return;
            Debug.Log("[MenuInjector] Injecting main menu UI");
            for (int i = 0; i < canvas.transform.childCount; i++)
                canvas.transform.GetChild(i).gameObject.SetActive(false);
            _menuRoot = new GameObject("MenuInjectorRoot", typeof(RectTransform));
            _menuRoot.transform.SetParent(canvas.transform, false);
            Stretch(_menuRoot);
            _menuRoot.AddComponent<Image>().color = UITheme.Background;
            BuildMainMenu();
        }

        private void BuildMainMenu()
        {
            var root = _menuRoot.GetComponent<RectTransform>();
            MainMenuBuilder.Build(root,
                onNewGame: () => {
                    _wizard = new NewGameWizard(this, root, BuildMainMenu);
                    _wizard.Show();
                },
                onLoadGame: () => {
                    _loadScreen = new LoadGameScreen(root, BuildMainMenu);
                    _loadScreen.Show();
                }
            );
        }
    }
}

using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
#endif

namespace NBAHeadCoach.Tools
{
    /// <summary>
    /// Editor tool to create the required game scenes
    /// </summary>
    public class GameSceneSetup : MonoBehaviour
    {
#if UNITY_EDITOR
        [MenuItem("NBA Head Coach/Create Game Scenes")]
        public static void CreateAllScenes()
        {
            // Ensure Scenes folder exists
            if (!AssetDatabase.IsValidFolder("Assets/Scenes"))
            {
                AssetDatabase.CreateFolder("Assets", "Scenes");
            }

            CreateBootScene();
            CreateMainMenuScene();
            CreateGameScene();
            CreateMatchScene();

            // Add scenes to build settings
            AddScenesToBuild();

            Debug.Log("[GameSceneSetup] All scenes created successfully!");
        }

        [MenuItem("NBA Head Coach/Create Boot Scene")]
        public static void CreateBootScene()
        {
            EnsureScenesFolder();
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // Create GameManager holder
            var gameManagerGO = new GameObject("GameManager");
            gameManagerGO.AddComponent<Core.GameManager>();

            // Create EventSystem
            var eventSystem = new GameObject("EventSystem");
            eventSystem.AddComponent<UnityEngine.EventSystems.EventSystem>();
            eventSystem.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();

            // Create Camera
            var cameraGO = new GameObject("Main Camera");
            var camera = cameraGO.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.1f, 0.1f, 0.15f);
            camera.tag = "MainCamera";

            // Create simple loading UI
            var canvasGO = new GameObject("BootCanvas");
            var canvas = canvasGO.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvasGO.AddComponent<CanvasScaler>();
            canvasGO.AddComponent<GraphicRaycaster>();

            var loadingText = new GameObject("LoadingText");
            loadingText.transform.SetParent(canvasGO.transform, false);
            var text = loadingText.AddComponent<Text>();
            text.text = "Loading...";
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = 48;
            text.color = Color.white;
            text.alignment = TextAnchor.MiddleCenter;
            var rectTransform = text.GetComponent<RectTransform>();
            rectTransform.anchorMin = Vector2.zero;
            rectTransform.anchorMax = Vector2.one;
            rectTransform.sizeDelta = Vector2.zero;

            // Save scene
            EditorSceneManager.SaveScene(scene, "Assets/Scenes/Boot.unity");
            Debug.Log("[GameSceneSetup] Boot scene created");
        }

        [MenuItem("NBA Head Coach/Create Main Menu Scene")]
        public static void CreateMainMenuScene()
        {
            EnsureScenesFolder();
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // Create EventSystem
            var eventSystem = new GameObject("EventSystem");
            eventSystem.AddComponent<UnityEngine.EventSystems.EventSystem>();
            eventSystem.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();

            // Create Camera
            var cameraGO = new GameObject("Main Camera");
            var camera = cameraGO.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.08f, 0.08f, 0.12f);
            camera.tag = "MainCamera";

            // Create Main Menu Canvas
            var canvasGO = new GameObject("MainMenuCanvas");
            var canvas = canvasGO.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            var scaler = canvasGO.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);

            canvasGO.AddComponent<GraphicRaycaster>();

            // Create menu content container
            var menuContent = CreateUIElement("MenuContent", canvasGO.transform);
            var menuContentRect = menuContent.GetComponent<RectTransform>();
            menuContentRect.anchorMin = Vector2.zero;
            menuContentRect.anchorMax = Vector2.one;
            menuContentRect.sizeDelta = Vector2.zero;

            // Title
            var titleGO = CreateUIElement("Title", menuContent.transform);
            var titleText = titleGO.AddComponent<Text>();
            titleText.text = "NBA HEAD COACH";
            titleText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            titleText.fontSize = 72;
            titleText.fontStyle = FontStyle.Bold;
            titleText.color = Color.white;
            titleText.alignment = TextAnchor.MiddleCenter;
            var titleRect = titleText.GetComponent<RectTransform>();
            titleRect.anchorMin = new Vector2(0.5f, 0.75f);
            titleRect.anchorMax = new Vector2(0.5f, 0.9f);
            titleRect.sizeDelta = new Vector2(800, 100);
            titleRect.anchoredPosition = Vector2.zero;

            // Subtitle
            var subtitleGO = CreateUIElement("Subtitle", menuContent.transform);
            var subtitleText = subtitleGO.AddComponent<Text>();
            subtitleText.text = "Career Mode";
            subtitleText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            subtitleText.fontSize = 32;
            subtitleText.color = new Color(0.7f, 0.7f, 0.7f);
            subtitleText.alignment = TextAnchor.MiddleCenter;
            var subtitleRect = subtitleText.GetComponent<RectTransform>();
            subtitleRect.anchorMin = new Vector2(0.5f, 0.68f);
            subtitleRect.anchorMax = new Vector2(0.5f, 0.75f);
            subtitleRect.sizeDelta = new Vector2(400, 50);
            subtitleRect.anchoredPosition = Vector2.zero;

            // Button container
            var buttonContainer = CreateUIElement("ButtonContainer", menuContent.transform);
            var vertLayout = buttonContainer.AddComponent<VerticalLayoutGroup>();
            vertLayout.spacing = 15;
            vertLayout.childAlignment = TextAnchor.MiddleCenter;
            vertLayout.childForceExpandWidth = false;
            vertLayout.childForceExpandHeight = false;
            var buttonContainerRect = buttonContainer.GetComponent<RectTransform>();
            buttonContainerRect.anchorMin = new Vector2(0.5f, 0.25f);
            buttonContainerRect.anchorMax = new Vector2(0.5f, 0.6f);
            buttonContainerRect.sizeDelta = new Vector2(300, 300);
            buttonContainerRect.anchoredPosition = Vector2.zero;

            // Create buttons
            CreateMenuButton("NewGameButton", "New Game", buttonContainer.transform);
            CreateMenuButton("LoadGameButton", "Load Game", buttonContainer.transform);
            CreateMenuButton("SettingsButton", "Settings", buttonContainer.transform);
            CreateMenuButton("QuitButton", "Quit", buttonContainer.transform);

            // Add MainMenuController
            var controllerGO = new GameObject("MainMenuController");
            controllerGO.AddComponent<UI.MainMenuController>();

            // Save scene
            EditorSceneManager.SaveScene(scene, "Assets/Scenes/MainMenu.unity");
            Debug.Log("[GameSceneSetup] MainMenu scene created");
        }

        [MenuItem("NBA Head Coach/Create Game Scene")]
        public static void CreateGameScene()
        {
            EnsureScenesFolder();
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // Create EventSystem
            var eventSystem = new GameObject("EventSystem");
            eventSystem.AddComponent<UnityEngine.EventSystems.EventSystem>();
            eventSystem.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();

            // Create Camera
            var cameraGO = new GameObject("Main Camera");
            var camera = cameraGO.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.08f, 0.08f, 0.12f);
            camera.tag = "MainCamera";

            // Create Main Canvas
            var canvasGO = new GameObject("MainCanvas");
            var canvas = canvasGO.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            var scaler = canvasGO.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);

            canvasGO.AddComponent<GraphicRaycaster>();

            // Create UIManager
            var uiManagerGO = new GameObject("UIManager");
            var uiManager = uiManagerGO.AddComponent<UI.UIManager>();
            uiManager.MainCanvas = canvasGO.transform;

            // ===== TOP HEADER BAR =====
            var headerBar = CreateUIElement("HeaderBar", canvasGO.transform);
            var headerBarRect = headerBar.GetComponent<RectTransform>();
            headerBarRect.anchorMin = new Vector2(0, 0.92f);
            headerBarRect.anchorMax = new Vector2(1, 1);
            headerBarRect.sizeDelta = Vector2.zero;
            var headerBarImage = headerBar.AddComponent<Image>();
            headerBarImage.color = new Color(0.12f, 0.12f, 0.18f);

            // Team/Coach name
            var teamNameGO = CreateUIElement("TeamName", headerBar.transform);
            var teamNameText = teamNameGO.AddComponent<Text>();
            teamNameText.text = "Los Angeles Lakers";
            teamNameText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            teamNameText.fontSize = 28;
            teamNameText.fontStyle = FontStyle.Bold;
            teamNameText.color = Color.white;
            teamNameText.alignment = TextAnchor.MiddleLeft;
            var teamNameRect = teamNameText.GetComponent<RectTransform>();
            teamNameRect.anchorMin = new Vector2(0, 0);
            teamNameRect.anchorMax = new Vector2(0.3f, 1);
            teamNameRect.offsetMin = new Vector2(20, 0);
            teamNameRect.offsetMax = Vector2.zero;

            // Date display
            var dateGO = CreateUIElement("DateText", headerBar.transform);
            var dateText = dateGO.AddComponent<Text>();
            dateText.text = "October 1, 2025";
            dateText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            dateText.fontSize = 22;
            dateText.color = new Color(0.8f, 0.8f, 0.8f);
            dateText.alignment = TextAnchor.MiddleCenter;
            var dateRect = dateText.GetComponent<RectTransform>();
            dateRect.anchorMin = new Vector2(0.4f, 0);
            dateRect.anchorMax = new Vector2(0.6f, 1);
            dateRect.sizeDelta = Vector2.zero;

            // Record display
            var recordGO = CreateUIElement("RecordText", headerBar.transform);
            var recordText = recordGO.AddComponent<Text>();
            recordText.text = "Record: 0-0";
            recordText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            recordText.fontSize = 22;
            recordText.color = new Color(0.8f, 0.8f, 0.8f);
            recordText.alignment = TextAnchor.MiddleRight;
            var recordRect = recordText.GetComponent<RectTransform>();
            recordRect.anchorMin = new Vector2(0.7f, 0);
            recordRect.anchorMax = new Vector2(1, 1);
            recordRect.offsetMin = Vector2.zero;
            recordRect.offsetMax = new Vector2(-20, 0);

            // ===== LEFT NAVIGATION PANEL =====
            var navPanel = CreateUIElement("NavigationPanel", canvasGO.transform);
            var navPanelRect = navPanel.GetComponent<RectTransform>();
            navPanelRect.anchorMin = new Vector2(0, 0);
            navPanelRect.anchorMax = new Vector2(0.15f, 0.92f);
            navPanelRect.sizeDelta = Vector2.zero;
            var navPanelImage = navPanel.AddComponent<Image>();
            navPanelImage.color = new Color(0.1f, 0.1f, 0.15f);

            // Navigation buttons container
            var navButtonsContainer = CreateUIElement("NavButtons", navPanel.transform);
            var navLayout = navButtonsContainer.AddComponent<VerticalLayoutGroup>();
            navLayout.spacing = 5;
            navLayout.padding = new RectOffset(10, 10, 20, 10);
            navLayout.childAlignment = TextAnchor.UpperCenter;
            navLayout.childForceExpandWidth = true;
            navLayout.childForceExpandHeight = false;
            var navButtonsRect = navButtonsContainer.GetComponent<RectTransform>();
            navButtonsRect.anchorMin = Vector2.zero;
            navButtonsRect.anchorMax = Vector2.one;
            navButtonsRect.sizeDelta = Vector2.zero;

            // Create nav buttons
            var dashboardBtn = CreateNavButton("DashboardButton", "Dashboard", navButtonsContainer.transform);
            var rosterBtn = CreateNavButton("RosterButton", "Roster", navButtonsContainer.transform);
            var scheduleBtn = CreateNavButton("ScheduleButton", "Schedule", navButtonsContainer.transform);
            var standingsBtn = CreateNavButton("StandingsButton", "Standings", navButtonsContainer.transform);
            var inboxBtn = CreateNavButton("InboxButton", "Inbox", navButtonsContainer.transform);

            // Spacer
            var spacer = CreateUIElement("Spacer", navButtonsContainer.transform);
            var spacerLayout = spacer.AddComponent<LayoutElement>();
            spacerLayout.flexibleHeight = 1;

            // Action buttons at bottom
            var advanceBtn = CreateNavButton("AdvanceDayButton", "Advance Day", navButtonsContainer.transform, new Color(0.2f, 0.4f, 0.3f));
            var simBtn = CreateNavButton("SimToNextButton", "Sim to Game", navButtonsContainer.transform, new Color(0.3f, 0.3f, 0.5f));
            var saveBtn = CreateNavButton("SaveButton", "Save Game", navButtonsContainer.transform, new Color(0.3f, 0.25f, 0.2f));
            var menuBtn = CreateNavButton("MenuButton", "Main Menu", navButtonsContainer.transform, new Color(0.3f, 0.2f, 0.2f));

            // ===== MAIN CONTENT AREA =====
            var contentParent = CreateUIElement("ContentParent", canvasGO.transform);
            var contentRect = contentParent.GetComponent<RectTransform>();
            contentRect.anchorMin = new Vector2(0.15f, 0);
            contentRect.anchorMax = new Vector2(1, 0.92f);
            contentRect.sizeDelta = Vector2.zero;
            uiManager.ContentParent = contentParent.transform;

            // ===== DASHBOARD PANEL =====
            var dashboardPanel = CreateUIElement("DashboardPanel", contentParent.transform);
            var dashboardRect = dashboardPanel.GetComponent<RectTransform>();
            dashboardRect.anchorMin = Vector2.zero;
            dashboardRect.anchorMax = Vector2.one;
            dashboardRect.sizeDelta = Vector2.zero;
            var dashboardBg = dashboardPanel.AddComponent<Image>();
            dashboardBg.color = new Color(0.08f, 0.08f, 0.12f);

            // Dashboard title
            var dashTitleGO = CreateUIElement("Title", dashboardPanel.transform);
            var dashTitleText = dashTitleGO.AddComponent<Text>();
            dashTitleText.text = "DASHBOARD";
            dashTitleText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            dashTitleText.fontSize = 32;
            dashTitleText.fontStyle = FontStyle.Bold;
            dashTitleText.color = Color.white;
            dashTitleText.alignment = TextAnchor.MiddleLeft;
            var dashTitleRect = dashTitleText.GetComponent<RectTransform>();
            dashTitleRect.anchorMin = new Vector2(0, 0.9f);
            dashTitleRect.anchorMax = new Vector2(1, 1);
            dashTitleRect.offsetMin = new Vector2(30, 0);
            dashTitleRect.offsetMax = new Vector2(-30, -10);

            // Next Game Card
            var nextGameCard = CreateInfoCard("NextGameCard", "NEXT GAME", "No games scheduled", dashboardPanel.transform);
            var nextGameRect = nextGameCard.GetComponent<RectTransform>();
            nextGameRect.anchorMin = new Vector2(0.02f, 0.5f);
            nextGameRect.anchorMax = new Vector2(0.48f, 0.88f);
            nextGameRect.sizeDelta = Vector2.zero;

            // Team Stats Card
            var teamStatsCard = CreateInfoCard("TeamStatsCard", "TEAM STATS", "Season statistics will appear here", dashboardPanel.transform);
            var teamStatsRect = teamStatsCard.GetComponent<RectTransform>();
            teamStatsRect.anchorMin = new Vector2(0.52f, 0.5f);
            teamStatsRect.anchorMax = new Vector2(0.98f, 0.88f);
            teamStatsRect.sizeDelta = Vector2.zero;

            // Recent Results Card
            var recentCard = CreateInfoCard("RecentResultsCard", "RECENT RESULTS", "No games played yet", dashboardPanel.transform);
            var recentRect = recentCard.GetComponent<RectTransform>();
            recentRect.anchorMin = new Vector2(0.02f, 0.1f);
            recentRect.anchorMax = new Vector2(0.48f, 0.48f);
            recentRect.sizeDelta = Vector2.zero;

            // News/Inbox Card
            var newsCard = CreateInfoCard("NewsCard", "LATEST NEWS", "Check your inbox for updates", dashboardPanel.transform);
            var newsRect = newsCard.GetComponent<RectTransform>();
            newsRect.anchorMin = new Vector2(0.52f, 0.1f);
            newsRect.anchorMax = new Vector2(0.98f, 0.48f);
            newsRect.sizeDelta = Vector2.zero;

            // ===== PLACEHOLDER PANELS (hidden by default) =====
            CreatePlaceholderPanel("RosterPanel", "ROSTER", contentParent.transform);
            CreatePlaceholderPanel("SchedulePanel", "SCHEDULE", contentParent.transform);
            CreatePlaceholderPanel("StandingsPanel", "STANDINGS", contentParent.transform);
            CreatePlaceholderPanel("InboxPanel", "INBOX", contentParent.transform);
            CreatePlaceholderPanel("PreGamePanel", "PRE-GAME", contentParent.transform);
            CreatePlaceholderPanel("PostGamePanel", "POST-GAME", contentParent.transform);

            // ===== GAME SCENE CONTROLLER =====
            var controllerGO = new GameObject("GameSceneController");
            var controller = controllerGO.AddComponent<UI.GameSceneController>();

            // Wire up references via SerializedObject
            var so = new SerializedObject(controller);
            so.FindProperty("_headerText").objectReferenceValue = teamNameText;
            so.FindProperty("_dateText").objectReferenceValue = dateText;
            so.FindProperty("_recordText").objectReferenceValue = recordText;
            so.FindProperty("_dashboardButton").objectReferenceValue = dashboardBtn.GetComponent<Button>();
            so.FindProperty("_rosterButton").objectReferenceValue = rosterBtn.GetComponent<Button>();
            so.FindProperty("_scheduleButton").objectReferenceValue = scheduleBtn.GetComponent<Button>();
            so.FindProperty("_standingsButton").objectReferenceValue = standingsBtn.GetComponent<Button>();
            so.FindProperty("_inboxButton").objectReferenceValue = inboxBtn.GetComponent<Button>();
            so.FindProperty("_advanceDayButton").objectReferenceValue = advanceBtn.GetComponent<Button>();
            so.FindProperty("_simToNextGameButton").objectReferenceValue = simBtn.GetComponent<Button>();
            so.FindProperty("_saveButton").objectReferenceValue = saveBtn.GetComponent<Button>();
            so.FindProperty("_menuButton").objectReferenceValue = menuBtn.GetComponent<Button>();
            so.FindProperty("_dashboardPanel").objectReferenceValue = dashboardPanel;
            so.FindProperty("_rosterPanel").objectReferenceValue = contentParent.transform.Find("RosterPanel")?.gameObject;
            so.FindProperty("_schedulePanel").objectReferenceValue = contentParent.transform.Find("SchedulePanel")?.gameObject;
            so.FindProperty("_standingsPanel").objectReferenceValue = contentParent.transform.Find("StandingsPanel")?.gameObject;
            so.FindProperty("_inboxPanel").objectReferenceValue = contentParent.transform.Find("InboxPanel")?.gameObject;
            so.FindProperty("_preGamePanel").objectReferenceValue = contentParent.transform.Find("PreGamePanel")?.gameObject;
            so.FindProperty("_postGamePanel").objectReferenceValue = contentParent.transform.Find("PostGamePanel")?.gameObject;
            so.FindProperty("_contentParent").objectReferenceValue = contentParent.transform;
            so.ApplyModifiedProperties();

            // Save scene
            EditorSceneManager.SaveScene(scene, "Assets/Scenes/Game.unity");
            Debug.Log("[GameSceneSetup] Game scene created with full UI");
        }

        private static GameObject CreateNavButton(string name, string label, Transform parent, Color? bgColor = null)
        {
            var buttonGO = new GameObject(name);
            buttonGO.transform.SetParent(parent, false);

            var buttonImage = buttonGO.AddComponent<Image>();
            buttonImage.color = bgColor ?? new Color(0.15f, 0.15f, 0.22f);

            var button = buttonGO.AddComponent<Button>();
            var colors = button.colors;
            colors.highlightedColor = new Color(0.25f, 0.25f, 0.35f);
            colors.pressedColor = new Color(0.1f, 0.1f, 0.15f);
            colors.selectedColor = new Color(0.2f, 0.3f, 0.4f);
            button.colors = colors;

            var layoutElement = buttonGO.AddComponent<LayoutElement>();
            layoutElement.preferredHeight = 45;
            layoutElement.minHeight = 40;

            var textGO = new GameObject("Text");
            textGO.transform.SetParent(buttonGO.transform, false);

            var text = textGO.AddComponent<Text>();
            text.text = label;
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = 18;
            text.color = Color.white;
            text.alignment = TextAnchor.MiddleCenter;

            var textRect = text.GetComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.sizeDelta = Vector2.zero;

            return buttonGO;
        }

        private static GameObject CreateInfoCard(string name, string title, string content, Transform parent)
        {
            var cardGO = new GameObject(name);
            cardGO.transform.SetParent(parent, false);
            cardGO.AddComponent<RectTransform>();

            var cardImage = cardGO.AddComponent<Image>();
            cardImage.color = new Color(0.12f, 0.12f, 0.18f);

            // Title
            var titleGO = CreateUIElement("Title", cardGO.transform);
            var titleText = titleGO.AddComponent<Text>();
            titleText.text = title;
            titleText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            titleText.fontSize = 18;
            titleText.fontStyle = FontStyle.Bold;
            titleText.color = new Color(0.6f, 0.7f, 0.9f);
            titleText.alignment = TextAnchor.MiddleLeft;
            var titleRect = titleText.GetComponent<RectTransform>();
            titleRect.anchorMin = new Vector2(0, 0.85f);
            titleRect.anchorMax = new Vector2(1, 1);
            titleRect.offsetMin = new Vector2(15, 0);
            titleRect.offsetMax = new Vector2(-15, -5);

            // Content
            var contentGO = CreateUIElement("Content", cardGO.transform);
            var contentText = contentGO.AddComponent<Text>();
            contentText.text = content;
            contentText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            contentText.fontSize = 16;
            contentText.color = new Color(0.8f, 0.8f, 0.8f);
            contentText.alignment = TextAnchor.UpperLeft;
            var contentRect = contentText.GetComponent<RectTransform>();
            contentRect.anchorMin = new Vector2(0, 0);
            contentRect.anchorMax = new Vector2(1, 0.85f);
            contentRect.offsetMin = new Vector2(15, 10);
            contentRect.offsetMax = new Vector2(-15, -5);

            return cardGO;
        }

        private static void CreatePlaceholderPanel(string name, string title, Transform parent)
        {
            var panelGO = CreateUIElement(name, parent);
            var panelRect = panelGO.GetComponent<RectTransform>();
            panelRect.anchorMin = Vector2.zero;
            panelRect.anchorMax = Vector2.one;
            panelRect.sizeDelta = Vector2.zero;

            var panelBg = panelGO.AddComponent<Image>();
            panelBg.color = new Color(0.08f, 0.08f, 0.12f);

            // Title
            var titleGO = CreateUIElement("Title", panelGO.transform);
            var titleText = titleGO.AddComponent<Text>();
            titleText.text = title;
            titleText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            titleText.fontSize = 32;
            titleText.fontStyle = FontStyle.Bold;
            titleText.color = Color.white;
            titleText.alignment = TextAnchor.MiddleLeft;
            var titleRect = titleText.GetComponent<RectTransform>();
            titleRect.anchorMin = new Vector2(0, 0.9f);
            titleRect.anchorMax = new Vector2(1, 1);
            titleRect.offsetMin = new Vector2(30, 0);
            titleRect.offsetMax = new Vector2(-30, -10);

            // Placeholder content
            var contentGO = CreateUIElement("PlaceholderText", panelGO.transform);
            var contentText = contentGO.AddComponent<Text>();
            contentText.text = $"{title} panel content will appear here.\n\nThis is a placeholder.";
            contentText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            contentText.fontSize = 24;
            contentText.color = new Color(0.6f, 0.6f, 0.6f);
            contentText.alignment = TextAnchor.MiddleCenter;
            var contentRect = contentText.GetComponent<RectTransform>();
            contentRect.anchorMin = new Vector2(0.1f, 0.3f);
            contentRect.anchorMax = new Vector2(0.9f, 0.7f);
            contentRect.sizeDelta = Vector2.zero;

            // Hide by default (except Dashboard)
            panelGO.SetActive(false);
        }

        [MenuItem("NBA Head Coach/Create Match Scene")]
        public static void CreateMatchScene()
        {
            EnsureScenesFolder();
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // Create EventSystem
            var eventSystem = new GameObject("EventSystem");
            eventSystem.AddComponent<UnityEngine.EventSystems.EventSystem>();
            eventSystem.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();

            // Create Camera
            var cameraGO = new GameObject("Main Camera");
            var camera = cameraGO.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.2f, 0.15f, 0.1f);
            camera.tag = "MainCamera";

            // Create Match Canvas
            var canvasGO = new GameObject("MatchCanvas");
            var canvas = canvasGO.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            var scaler = canvasGO.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);

            canvasGO.AddComponent<GraphicRaycaster>();

            // Placeholder match UI
            var matchUI = CreateUIElement("MatchUI", canvasGO.transform);
            var matchRect = matchUI.GetComponent<RectTransform>();
            matchRect.anchorMin = Vector2.zero;
            matchRect.anchorMax = Vector2.one;
            matchRect.sizeDelta = Vector2.zero;

            var matchText = matchUI.AddComponent<Text>();
            matchText.text = "Match Scene\n(Interactive match would display here)";
            matchText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            matchText.fontSize = 48;
            matchText.color = Color.white;
            matchText.alignment = TextAnchor.MiddleCenter;

            // Save scene
            EditorSceneManager.SaveScene(scene, "Assets/Scenes/Match.unity");
            Debug.Log("[GameSceneSetup] Match scene created");
        }

        private static void EnsureScenesFolder()
        {
            if (!AssetDatabase.IsValidFolder("Assets/Scenes"))
            {
                AssetDatabase.CreateFolder("Assets", "Scenes");
                AssetDatabase.Refresh();
            }
        }

        private static GameObject CreateUIElement(string name, Transform parent)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.AddComponent<RectTransform>();
            return go;
        }

        private static void CreateMenuButton(string name, string label, Transform parent)
        {
            var buttonGO = new GameObject(name);
            buttonGO.transform.SetParent(parent, false);

            var buttonImage = buttonGO.AddComponent<Image>();
            buttonImage.color = new Color(0.2f, 0.2f, 0.3f);

            var button = buttonGO.AddComponent<Button>();
            var colors = button.colors;
            colors.highlightedColor = new Color(0.3f, 0.3f, 0.5f);
            colors.pressedColor = new Color(0.15f, 0.15f, 0.25f);
            button.colors = colors;

            var buttonRect = buttonGO.GetComponent<RectTransform>();
            buttonRect.sizeDelta = new Vector2(280, 55);

            var layoutElement = buttonGO.AddComponent<LayoutElement>();
            layoutElement.preferredWidth = 280;
            layoutElement.preferredHeight = 55;

            var textGO = new GameObject("Text");
            textGO.transform.SetParent(buttonGO.transform, false);

            var text = textGO.AddComponent<Text>();
            text.text = label;
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = 24;
            text.color = Color.white;
            text.alignment = TextAnchor.MiddleCenter;

            var textRect = text.GetComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.sizeDelta = Vector2.zero;
        }

        private static void AddScenesToBuild()
        {
            var scenes = new[]
            {
                new EditorBuildSettingsScene("Assets/Scenes/Boot.unity", true),
                new EditorBuildSettingsScene("Assets/Scenes/MainMenu.unity", true),
                new EditorBuildSettingsScene("Assets/Scenes/Game.unity", true),
                new EditorBuildSettingsScene("Assets/Scenes/Match.unity", true)
            };

            EditorBuildSettings.scenes = scenes;
            Debug.Log("[GameSceneSetup] Scenes added to build settings");
        }
#endif
    }
}

using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using NBAHeadCoach.Core;
using NBAHeadCoach.Core.Data;
using NBAHeadCoach.Core.Util;

namespace NBAHeadCoach.UI
{
    public class MenuInjector : MonoBehaviour
    {
        private string _lastScene;
        private bool _injected;
        private GameObject _menuRoot;
        private GameObject _wizardRoot;
        private GameObject _loadRoot;

        private int _currentStep;
        private RectTransform _stepContainer;
        private Text _stepIndicatorText;
        private Button _nextBtn;
        private Button _backBtn;
        private Text _nextBtnText;

        private string _firstName = "";
        private string _lastName = "";
        private int _age = 45;
        private int _birthMonth = 6;
        private int _birthDay = 15;
        private int _birthYear = 1980;
        private int _backgroundIndex;
        private string _selectedTeamId;
        private Team _selectedTeam;
        private UserRole _selectedRole = UserRole.Both;
        private DifficultyPreset _selectedDifficulty = DifficultyPreset.Normal;

        private string _selectedSaveSlot;
        private Button _loadBtn;
        private Button _deleteBtn;
        private List<Image> _saveSlotHighlights = new List<Image>();

        private readonly CoachBg[] _backgrounds = new[]
        {
            new CoachBg("Former Player", "High reputation from playing career, still learning the coaching craft.", 70, 45, 60),
            new CoachBg("College Coach", "Balanced experience with proven track record at the collegiate level.", 55, 60, 55),
            new CoachBg("Assistant Coach", "Years of NBA experience as an assistant, strong tactical knowledge.", 50, 75, 50),
            new CoachBg("Int'l Coach", "Unique perspective from European or international leagues.", 45, 65, 70),
            new CoachBg("Analytics", "Data-driven approach, innovative but unproven at NBA level.", 40, 55, 80)
        };

        private struct CoachBg
        {
            public string Name, Desc;
            public int Rep, Tactical, Dev;
            public CoachBg(string n, string d, int r, int t, int dv) { Name = n; Desc = d; Rep = r; Tactical = t; Dev = dv; }
        }

        // ═══════════════════════════════════════════════════════
        //  LIFECYCLE
        // ═══════════════════════════════════════════════════════

        private void Update()
        {
            if (GameManager.Instance == null) return;
            string scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
            if (scene != _lastScene) { _lastScene = scene; _injected = false; _menuRoot = null; _wizardRoot = null; _loadRoot = null; }
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
            BuildMainMenu(_menuRoot.GetComponent<RectTransform>());
        }

        // ═══════════════════════════════════════════════════════
        //  MAIN MENU
        // ═══════════════════════════════════════════════════════

        private void BuildMainMenu(RectTransform root)
        {
            foreach (Transform child in root) Destroy(child.gameObject);
            var center = CreateChild(root, "Center");
            var cr = center.GetComponent<RectTransform>();
            cr.anchorMin = new Vector2(0.3f, 0.2f); cr.anchorMax = new Vector2(0.7f, 0.8f); cr.sizeDelta = Vector2.zero;

            var vlg = center.AddComponent<VerticalLayoutGroup>();
            vlg.spacing = 20; vlg.childAlignment = TextAnchor.MiddleCenter;
            vlg.childControlWidth = true; vlg.childControlHeight = false; vlg.childForceExpandWidth = true;

            var title = MkText(cr, "NBA HEAD COACH", 36, FontStyle.Bold, UITheme.AccentPrimary);
            title.alignment = TextAnchor.MiddleCenter;
            (title.gameObject.GetComponent<LayoutElement>() ?? title.gameObject.AddComponent<LayoutElement>()).preferredHeight = 60;

            var sub = MkText(cr, "Franchise Management Simulation", 16, FontStyle.Normal, UITheme.TextSecondary);
            sub.alignment = TextAnchor.MiddleCenter;
            sub.gameObject.AddComponent<LayoutElement>().preferredHeight = 30;

            CreateChild(cr, "Spacer").AddComponent<LayoutElement>().preferredHeight = 40;

            MkMenuBtn(cr, "NEW GAME", UITheme.AccentPrimary, () => {
                _currentStep = 0; _firstName = ""; _lastName = ""; _age = 45; _birthMonth = 6; _birthDay = 15; _birthYear = 1980; _backgroundIndex = 0;
                _selectedTeamId = null; _selectedTeam = null; _selectedRole = UserRole.Both; _selectedDifficulty = DifficultyPreset.Normal;
                ShowWizard();
            });
            MkMenuBtn(cr, "LOAD GAME", UITheme.AccentSecondary, ShowLoadGame);
            MkMenuBtn(cr, "QUIT", UITheme.TextSecondary, () => {
#if UNITY_EDITOR
                UnityEditor.EditorApplication.isPlaying = false;
#else
                Application.Quit();
#endif
            });
        }

        private void MkMenuBtn(RectTransform parent, string label, Color color, Action onClick)
        {
            var go = CreateChild(parent, $"Btn_{label}");
            go.AddComponent<LayoutElement>().preferredHeight = 50;
            go.AddComponent<Image>().color = new Color(color.r * 0.2f, color.g * 0.2f, color.b * 0.2f, 0.9f);
            var btn = go.AddComponent<Button>();
            var c = btn.colors; c.highlightedColor = new Color(color.r * 0.35f, color.g * 0.35f, color.b * 0.35f, 1f); btn.colors = c;
            btn.onClick.AddListener(() => onClick());
            var accent = CreateChild(go.GetComponent<RectTransform>(), "Accent");
            var ar = accent.GetComponent<RectTransform>(); ar.anchorMin = Vector2.zero; ar.anchorMax = new Vector2(0, 1); ar.sizeDelta = new Vector2(4, 0);
            accent.AddComponent<Image>().color = color;
            var t = MkText(go.GetComponent<RectTransform>(), label, 18, FontStyle.Bold, Color.white);
            t.alignment = TextAnchor.MiddleCenter; Stretch(t.gameObject);
        }

        // ═══════════════════════════════════════════════════════
        //  WIZARD SHELL
        // ═══════════════════════════════════════════════════════

        private void ShowWizard()
        {
            if (_wizardRoot != null) Destroy(_wizardRoot);
            _wizardRoot = CreateChild(_menuRoot.GetComponent<RectTransform>(), "Wizard");
            Stretch(_wizardRoot);
            _wizardRoot.AddComponent<Image>().color = UITheme.Background;
            var root = _wizardRoot.GetComponent<RectTransform>();

            // Step container
            var stepGo = CreateChild(root, "StepContainer");
            _stepContainer = stepGo.GetComponent<RectTransform>();
            _stepContainer.anchorMin = new Vector2(0.03f, 0.09f);
            _stepContainer.anchorMax = new Vector2(0.97f, 0.88f);
            _stepContainer.sizeDelta = Vector2.zero;

            // Top bar
            var top = CreateChild(root, "TopBar");
            var tr = top.GetComponent<RectTransform>(); tr.anchorMin = new Vector2(0, 0.9f); tr.anchorMax = Vector2.one; tr.sizeDelta = Vector2.zero;
            _stepIndicatorText = MkText(tr, "", 14, FontStyle.Bold, UITheme.TextSecondary);
            _stepIndicatorText.alignment = TextAnchor.MiddleCenter; Stretch(_stepIndicatorText.gameObject);

            // Bottom nav
            var nav = CreateChild(root, "NavBar");
            var nr = nav.GetComponent<RectTransform>(); nr.anchorMin = Vector2.zero; nr.anchorMax = new Vector2(1, 0.07f); nr.sizeDelta = Vector2.zero;
            var navHlg = nav.AddComponent<HorizontalLayoutGroup>();
            navHlg.spacing = 20; navHlg.childAlignment = TextAnchor.MiddleCenter;
            navHlg.childControlWidth = false; navHlg.childControlHeight = true; navHlg.padding = new RectOffset(40, 40, 4, 4);

            // Back
            var backGo = CreateChild(nr, "Back"); backGo.AddComponent<LayoutElement>().preferredWidth = 120;
            backGo.AddComponent<Image>().color = UITheme.PanelSurface;
            _backBtn = backGo.AddComponent<Button>(); _backBtn.onClick.AddListener(OnBack);
            var bt = MkText(backGo.GetComponent<RectTransform>(), "BACK", 13, FontStyle.Bold, UITheme.TextSecondary);
            bt.alignment = TextAnchor.MiddleCenter; Stretch(bt.gameObject);

            CreateChild(nr, "Flex").AddComponent<LayoutElement>().flexibleWidth = 1;

            // Next
            var nextGo = CreateChild(nr, "Next"); nextGo.AddComponent<LayoutElement>().preferredWidth = 160;
            nextGo.AddComponent<Image>().color = new Color32(40, 50, 70, 255);
            // Gold left accent on next button
            var na = CreateChild(nextGo.GetComponent<RectTransform>(), "Accent");
            var nar = na.GetComponent<RectTransform>(); nar.anchorMin = Vector2.zero; nar.anchorMax = new Vector2(0, 1); nar.sizeDelta = new Vector2(3, 0);
            na.AddComponent<Image>().color = UITheme.AccentPrimary;
            _nextBtn = nextGo.AddComponent<Button>(); _nextBtn.onClick.AddListener(OnNext);
            _nextBtnText = MkText(nextGo.GetComponent<RectTransform>(), "NEXT", 14, FontStyle.Bold, UITheme.AccentPrimary);
            _nextBtnText.alignment = TextAnchor.MiddleCenter; Stretch(_nextBtnText.gameObject);

            ShowStep();
        }

        private void ShowStep()
        {
            foreach (Transform child in _stepContainer) Destroy(child.gameObject);
            string[] names = { "Create Your Coach", "Select Your Team", "Choose Your Role", "Difficulty", "Confirm & Start" };
            string dots = ""; for (int i = 0; i < 5; i++) dots += i == _currentStep ? " \u25CF " : " \u25CB ";
            _stepIndicatorText.text = $"Step {_currentStep + 1} of 5: {names[_currentStep]}\n{dots}";
            _backBtn.gameObject.SetActive(_currentStep > 0);
            _nextBtnText.text = _currentStep == 4 ? "START CAREER" : "NEXT \u25B6";

            switch (_currentStep)
            {
                case 0: BuildCoachStep(); break;
                case 1: BuildTeamStep(); break;
                case 2: BuildRoleStep(); break;
                case 3: BuildDifficultyStep(); break;
                case 4: BuildConfirmStep(); break;
            }

        }

        private void OnNext()
        {
            if (_currentStep == 0 && (string.IsNullOrWhiteSpace(_firstName) || string.IsNullOrWhiteSpace(_lastName))) return;
            if (_currentStep == 1 && string.IsNullOrEmpty(_selectedTeamId)) return;
            if (_currentStep == 4) { StartGame(); return; }
            _currentStep++;
            ShowStep();
        }

        private void OnBack()
        {
            if (_currentStep == 0) { Destroy(_wizardRoot); BuildMainMenu(_menuRoot.GetComponent<RectTransform>()); return; }
            _currentStep--;
            ShowStep();
        }

        // ═══════════════════════════════════════════════════════
        //  STEP 1: COACH CREATION (absolute positioning)
        // ═══════════════════════════════════════════════════════

        private void BuildCoachStep()
        {
            var card = CreateChild(_stepContainer, "Card");
            var cr = card.GetComponent<RectTransform>();
            cr.anchorMin = new Vector2(0.18f, 0); cr.anchorMax = new Vector2(0.82f, 1); cr.sizeDelta = Vector2.zero;
            card.AddComponent<Image>().color = UITheme.CardBackground;

            // FM header bar
            var hdr = CreateChild(cr, "Header");
            var hr = hdr.GetComponent<RectTransform>(); hr.anchorMin = new Vector2(0, 1); hr.anchorMax = Vector2.one;
            hr.pivot = new Vector2(0.5f, 1); hr.sizeDelta = new Vector2(0, 32);
            hdr.AddComponent<Image>().color = UITheme.FMCardHeaderBg;
            // Gold accent line
            var acc = CreateChild(cr, "Accent");
            var acr = acc.GetComponent<RectTransform>(); acr.anchorMin = new Vector2(0, 1); acr.anchorMax = Vector2.one;
            acr.pivot = new Vector2(0.5f, 1); acr.anchoredPosition = new Vector2(0, -32); acr.sizeDelta = new Vector2(0, 3);
            acc.AddComponent<Image>().color = UITheme.AccentPrimary;
            var ht = MkText(hr, "CREATE YOUR COACH", 11, FontStyle.Bold, UITheme.AccentPrimary);
            ht.alignment = TextAnchor.MiddleLeft;
            var htr = ht.GetComponent<RectTransform>(); htr.anchorMin = Vector2.zero; htr.anchorMax = Vector2.one;
            htr.offsetMin = new Vector2(14, 0); htr.offsetMax = Vector2.zero;

            float y = -44; float pad = 28;

            // FIRST NAME
            PlaceLabel(cr, "FIRST NAME", pad, ref y);
            var fn = PlaceInput(cr, _firstName, "Enter first name...", pad, ref y);
            fn.onValueChanged.AddListener(v => _firstName = v);

            // LAST NAME
            y -= 6;
            PlaceLabel(cr, "LAST NAME", pad, ref y);
            var ln = PlaceInput(cr, _lastName, "Enter last name...", pad, ref y);
            ln.onValueChanged.AddListener(v => _lastName = v);

            // DATE OF BIRTH — uses button spinners instead of dropdowns
            y -= 6;
            PlaceLabel(cr, "DATE OF BIRTH", pad, ref y);

            int currentYear = DateTime.Now.Year;
            int minYear = currentYear - 70;
            int maxYear = currentYear - 30;
            string[] months = { "Jan", "Feb", "Mar", "Apr", "May", "Jun", "Jul", "Aug", "Sep", "Oct", "Nov", "Dec" };

            var dobRow = CreateChild(cr, "DOBRow");
            var dobr = dobRow.GetComponent<RectTransform>();
            dobr.anchorMin = new Vector2(0, 1); dobr.anchorMax = new Vector2(1, 1); dobr.pivot = new Vector2(0.5f, 1);
            dobr.offsetMin = new Vector2(pad, 0); dobr.offsetMax = new Vector2(-pad, 0);
            dobr.anchoredPosition = new Vector2(0, y); dobr.sizeDelta = new Vector2(dobr.sizeDelta.x, 32);
            var dobHlg = dobRow.AddComponent<HorizontalLayoutGroup>();
            dobHlg.spacing = 6; dobHlg.childControlWidth = true; dobHlg.childControlHeight = true; dobHlg.childForceExpandWidth = false;

            // Month spinner
            Text monthText = null;
            var monthSpinner = PlaceSpinner(dobr, months[_birthMonth - 1], 90, out monthText);

            // Day spinner
            Text dayText = null;
            var daySpinner = PlaceSpinner(dobr, _birthDay.ToString(), 60, out dayText);

            // Year spinner
            Text yearText = null;
            var yearSpinner = PlaceSpinner(dobr, _birthYear.ToString(), 90, out yearText);

            y -= 38;

            // Age display
            RecalcAge();
            var ageDisplay = PlaceLabel(cr, $"Age: {_age}", pad, ref y);
            ageDisplay.color = UITheme.TextSecondary;
            ageDisplay.fontStyle = FontStyle.Normal;

            // Shared update logic
            System.Action updateDOB = () =>
            {
                int maxDay = DateTime.DaysInMonth(_birthYear, _birthMonth);
                if (_birthDay > maxDay) _birthDay = maxDay;
                monthText.text = months[_birthMonth - 1];
                dayText.text = _birthDay.ToString();
                yearText.text = _birthYear.ToString();
                RecalcAge();
                ageDisplay.text = $"Age: {_age}";
            };

            // Wire month buttons
            monthSpinner.Item1.onClick.AddListener(() => { _birthMonth = _birthMonth <= 1 ? 12 : _birthMonth - 1; updateDOB(); });
            monthSpinner.Item2.onClick.AddListener(() => { _birthMonth = _birthMonth >= 12 ? 1 : _birthMonth + 1; updateDOB(); });

            // Wire day buttons
            daySpinner.Item1.onClick.AddListener(() => { int max = DateTime.DaysInMonth(_birthYear, _birthMonth); _birthDay = _birthDay <= 1 ? max : _birthDay - 1; updateDOB(); });
            daySpinner.Item2.onClick.AddListener(() => { int max = DateTime.DaysInMonth(_birthYear, _birthMonth); _birthDay = _birthDay >= max ? 1 : _birthDay + 1; updateDOB(); });

            // Wire year buttons
            yearSpinner.Item1.onClick.AddListener(() => { _birthYear = _birthYear <= minYear ? maxYear : _birthYear - 1; updateDOB(); });
            yearSpinner.Item2.onClick.AddListener(() => { _birthYear = _birthYear >= maxYear ? minYear : _birthYear + 1; updateDOB(); });

            y -= 4;

            // BACKGROUND
            y -= 6;
            PlaceLabel(cr, "BACKGROUND", pad, ref y);

            // Button row
            var bgRow = CreateChild(cr, "BgRow");
            var bgr = bgRow.GetComponent<RectTransform>();
            bgr.anchorMin = new Vector2(0, 1); bgr.anchorMax = new Vector2(1, 1); bgr.pivot = new Vector2(0.5f, 1);
            bgr.offsetMin = new Vector2(pad, 0); bgr.offsetMax = new Vector2(-pad, 0);
            bgr.anchoredPosition = new Vector2(0, y); bgr.sizeDelta = new Vector2(bgr.sizeDelta.x, 28);
            var bgHlg = bgRow.AddComponent<HorizontalLayoutGroup>();
            bgHlg.spacing = 4; bgHlg.childControlWidth = true; bgHlg.childControlHeight = true; bgHlg.childForceExpandWidth = true;
            y -= 34;

            // Desc
            var desc = PlaceText(cr, _backgrounds[_backgroundIndex].Desc, 12, FontStyle.Italic, UITheme.TextSecondary, pad, ref y, 32);
            y -= 4;
            var bgd = _backgrounds[_backgroundIndex];
            var stats = PlaceText(cr, $"Reputation: {bgd.Rep}  |  Tactical: {bgd.Tactical}  |  Development: {bgd.Dev}", 11, FontStyle.Normal, UITheme.TextSecondary, pad, ref y, 18);

            List<Image> bgHls = new List<Image>();
            for (int i = 0; i < _backgrounds.Length; i++)
            {
                int idx = i;
                var bgo = CreateChild(bgr, _backgrounds[i].Name);
                var bi = bgo.AddComponent<Image>(); bi.color = idx == _backgroundIndex ? UITheme.FMNavActive : UITheme.PanelSurface;
                bgHls.Add(bi);
                bgo.AddComponent<Button>().onClick.AddListener(() => {
                    _backgroundIndex = idx; var b = _backgrounds[idx];
                    desc.text = b.Desc; stats.text = $"Reputation: {b.Rep}  |  Tactical: {b.Tactical}  |  Development: {b.Dev}";
                    for (int j = 0; j < bgHls.Count; j++) bgHls[j].color = j == idx ? UITheme.FMNavActive : UITheme.PanelSurface;
                });
                var bt2 = MkText(bgo.GetComponent<RectTransform>(), _backgrounds[i].Name, 9, FontStyle.Normal, Color.white);
                bt2.alignment = TextAnchor.MiddleCenter; Stretch(bt2.gameObject);
            }
        }

        private System.Collections.IEnumerator ResizeGridNextFrame(GridLayoutGroup grid, RectTransform rect, int cols, int rows, int spacing, int padding)
        {
            yield return null; // wait one frame for layout
            if (grid == null || rect == null) yield break;
            float w = rect.rect.width;
            float h = rect.rect.height;
            float cellW = (w - padding * 2 - spacing * (cols - 1)) / cols;
            float cellH = (h - padding * 2 - spacing * (rows - 1)) / rows;
            grid.cellSize = new Vector2(Mathf.Floor(cellW), Mathf.Floor(cellH));
        }

        private void RecalcAge()
        {
            var dob = new DateTime(_birthYear, _birthMonth, Mathf.Min(_birthDay, DateTime.DaysInMonth(_birthYear, _birthMonth)));
            var now = DateTime.Now;
            _age = now.Year - dob.Year;
            if (now < dob.AddYears(_age)) _age--;
        }

        private System.Tuple<Button, Button> PlaceSpinner(RectTransform parent, string initialValue, float width, out Text valueText)
        {
            var go = CreateChild(parent, "Spinner");
            go.AddComponent<LayoutElement>().preferredWidth = width;
            go.AddComponent<Image>().color = UITheme.PanelSurface;
            var hlg = go.AddComponent<HorizontalLayoutGroup>();
            hlg.childControlWidth = true; hlg.childControlHeight = true;
            hlg.childForceExpandWidth = false; hlg.childForceExpandHeight = true;
            hlg.padding = new RectOffset(2, 2, 0, 0);

            // Left arrow button
            var leftGo = CreateChild(go.GetComponent<RectTransform>(), "Left");
            leftGo.AddComponent<LayoutElement>().preferredWidth = 20;
            leftGo.AddComponent<Image>().color = UITheme.CardBackground;
            var leftBtn = leftGo.AddComponent<Button>();
            var lt = MkText(leftGo.GetComponent<RectTransform>(), "\u25C0", 9, FontStyle.Normal, UITheme.TextSecondary);
            lt.alignment = TextAnchor.MiddleCenter; Stretch(lt.gameObject);

            // Value display
            var valGo = CreateChild(go.GetComponent<RectTransform>(), "Value");
            valGo.AddComponent<LayoutElement>().flexibleWidth = 1;
            var vt = MkText(valGo.GetComponent<RectTransform>(), initialValue, 12, FontStyle.Normal, Color.white);
            vt.alignment = TextAnchor.MiddleCenter; Stretch(vt.gameObject);
            valueText = vt;

            // Right arrow button
            var rightGo = CreateChild(go.GetComponent<RectTransform>(), "Right");
            rightGo.AddComponent<LayoutElement>().preferredWidth = 20;
            rightGo.AddComponent<Image>().color = UITheme.CardBackground;
            var rightBtn = rightGo.AddComponent<Button>();
            var rt2 = MkText(rightGo.GetComponent<RectTransform>(), "\u25B6", 9, FontStyle.Normal, UITheme.TextSecondary);
            rt2.alignment = TextAnchor.MiddleCenter; Stretch(rt2.gameObject);

            return new System.Tuple<Button, Button>(leftBtn, rightBtn);
        }

        private Dropdown PlaceDropdown(RectTransform parent, string[] options, int selected, float width)
        {
            var go = CreateChild(parent, "DD");
            var le = go.AddComponent<LayoutElement>(); le.preferredWidth = width; le.minHeight = 28;
            go.AddComponent<Image>().color = UITheme.PanelSurface;

            // Label
            var labelGo = CreateChild(go.GetComponent<RectTransform>(), "Label");
            Stretch(labelGo);
            labelGo.GetComponent<RectTransform>().offsetMin = new Vector2(8, 0);
            labelGo.GetComponent<RectTransform>().offsetMax = new Vector2(-20, 0);
            var label = labelGo.AddComponent<Text>();
            label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            label.fontSize = 13; label.color = Color.white; label.alignment = TextAnchor.MiddleLeft;
            label.horizontalOverflow = HorizontalWrapMode.Overflow;
            label.verticalOverflow = VerticalWrapMode.Overflow;

            // Arrow
            var arrowGo = CreateChild(go.GetComponent<RectTransform>(), "Arrow");
            var arr = arrowGo.GetComponent<RectTransform>();
            arr.anchorMin = new Vector2(1, 0); arr.anchorMax = Vector2.one;
            arr.sizeDelta = new Vector2(20, 0); arr.anchoredPosition = new Vector2(-10, 0);
            var arrowText = arrowGo.AddComponent<Text>();
            arrowText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            arrowText.text = "\u25BC"; arrowText.fontSize = 9; arrowText.color = UITheme.TextSecondary;
            arrowText.alignment = TextAnchor.MiddleCenter;

            // Template (dropdown popup)
            var templateGo = CreateChild(go.GetComponent<RectTransform>(), "Template");
            var tr = templateGo.GetComponent<RectTransform>();
            tr.anchorMin = new Vector2(0, 0); tr.anchorMax = new Vector2(1, 0);
            tr.pivot = new Vector2(0.5f, 1); tr.sizeDelta = new Vector2(0, 150);
            templateGo.AddComponent<Image>().color = UITheme.CardBackground;
            templateGo.AddComponent<ScrollRect>();

            var viewport = CreateChild(tr, "Viewport");
            Stretch(viewport); viewport.AddComponent<RectMask2D>();

            var contentGo = CreateChild(viewport.GetComponent<RectTransform>(), "Content");
            var crt = contentGo.GetComponent<RectTransform>();
            crt.anchorMin = new Vector2(0, 1); crt.anchorMax = Vector2.one;
            crt.pivot = new Vector2(0.5f, 1); crt.sizeDelta = new Vector2(0, 28);
            contentGo.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            // Item template
            var itemGo = CreateChild(crt, "Item");
            itemGo.AddComponent<LayoutElement>().preferredHeight = 24;
            var itemBg = itemGo.AddComponent<Image>(); itemBg.color = UITheme.PanelSurface;
            itemGo.AddComponent<Toggle>().isOn = false;

            var itemLabelGo = CreateChild(itemGo.GetComponent<RectTransform>(), "Item Label");
            Stretch(itemLabelGo);
            itemLabelGo.GetComponent<RectTransform>().offsetMin = new Vector2(8, 0);
            var itemLabel = itemLabelGo.AddComponent<Text>();
            itemLabel.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            itemLabel.fontSize = 12; itemLabel.color = Color.white;

            templateGo.SetActive(false);

            // Setup ScrollRect
            var scroll = templateGo.GetComponent<ScrollRect>();
            scroll.content = crt;
            scroll.viewport = viewport.GetComponent<RectTransform>();

            // Dropdown component
            var dd = go.AddComponent<Dropdown>();
            dd.captionText = label;
            dd.template = tr;
            dd.itemText = itemLabel;

            dd.ClearOptions();
            var optList = new List<Dropdown.OptionData>();
            foreach (var o in options) optList.Add(new Dropdown.OptionData(o));
            dd.AddOptions(optList);
            int idx = Mathf.Clamp(selected, 0, options.Length - 1);
            dd.value = idx;
            dd.RefreshShownValue();
            // Force label text in case RefreshShownValue doesn't work before Start()
            label.text = options[idx];

            return dd;
        }

        // ═══════════════════════════════════════════════════════
        //  STEP 2: TEAM SELECTION (card grid with HLG cells)
        // ═══════════════════════════════════════════════════════

        private void BuildTeamStep()
        {
            var teams = GameManager.Instance?.AllTeams;
            if (teams == null || teams.Count == 0) return;

            // Left panel: grid
            var leftGo = CreateChild(_stepContainer, "Grid");
            var lr = leftGo.GetComponent<RectTransform>();
            lr.anchorMin = Vector2.zero; lr.anchorMax = new Vector2(0.66f, 1); lr.sizeDelta = Vector2.zero;

            // Right panel: detail
            var rightGo = CreateChild(_stepContainer, "Detail");
            var rr = rightGo.GetComponent<RectTransform>();
            rr.anchorMin = new Vector2(0.68f, 0); rr.anchorMax = Vector2.one; rr.sizeDelta = Vector2.zero;
            rightGo.AddComponent<Image>().color = UITheme.CardBackground;

            // Detail header
            var dHdr = CreateChild(rr, "Header");
            var dhr = dHdr.GetComponent<RectTransform>(); dhr.anchorMin = new Vector2(0, 1); dhr.anchorMax = Vector2.one;
            dhr.pivot = new Vector2(0.5f, 1); dhr.sizeDelta = new Vector2(0, 30);
            dHdr.AddComponent<Image>().color = UITheme.FMCardHeaderBg;
            var dht = MkText(dhr, "TEAM DETAILS", 11, FontStyle.Bold, UITheme.AccentPrimary);
            dht.alignment = TextAnchor.MiddleLeft;
            var dhtr = dht.GetComponent<RectTransform>(); dhtr.anchorMin = Vector2.zero; dhtr.anchorMax = Vector2.one; dhtr.offsetMin = new Vector2(12, 0);
            // Gold accent
            var dacc = CreateChild(rr, "Accent");
            var dacr = dacc.GetComponent<RectTransform>(); dacr.anchorMin = new Vector2(0, 1); dacr.anchorMax = Vector2.one;
            dacr.pivot = new Vector2(0.5f, 1); dacr.anchoredPosition = new Vector2(0, -30); dacr.sizeDelta = new Vector2(0, 2);
            dacc.AddComponent<Image>().color = UITheme.AccentPrimary;

            // Detail logo
            var dLogoGo = CreateChild(rr, "Logo");
            var dlr = dLogoGo.GetComponent<RectTransform>();
            dlr.anchorMin = new Vector2(0.25f, 0.45f); dlr.anchorMax = new Vector2(0.75f, 0.85f); dlr.sizeDelta = Vector2.zero;
            var dLogoImg = dLogoGo.AddComponent<Image>(); dLogoImg.preserveAspect = true; dLogoImg.color = new Color(1, 1, 1, 0.15f);

            // Detail text
            var dText = MkText(rr, "Select a team\nfrom the grid", 13, FontStyle.Normal, UITheme.TextSecondary);
            dText.alignment = TextAnchor.UpperLeft; dText.horizontalOverflow = HorizontalWrapMode.Wrap;
            var dtr = dText.GetComponent<RectTransform>();
            dtr.anchorMin = new Vector2(0, 0.02f); dtr.anchorMax = new Vector2(1, 0.42f);
            dtr.offsetMin = new Vector2(14, 8); dtr.offsetMax = new Vector2(-14, -4);

            // Grid fills entire left panel — 5 cols × 6 rows, no scrolling
            var contentGo = CreateChild(lr, "TeamGrid"); Stretch(contentGo);
            var ctr = contentGo.GetComponent<RectTransform>();

            var grid = contentGo.AddComponent<GridLayoutGroup>();
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = 5;
            int cols = 5, rows = 6, sp = 4, pad = 2;
            grid.spacing = new Vector2(sp, sp);
            grid.padding = new RectOffset(pad, pad, pad, pad);
            // Defer cell size calculation to next frame when rect is laid out
            grid.cellSize = new Vector2(100, 80); // temp
            StartCoroutine(ResizeGridNextFrame(grid, ctr, cols, rows, sp, pad));

            var sorted = new List<Team>(teams);
            sorted.Sort((a, b) => string.Compare($"{a.Conference}{a.City}", $"{b.Conference}{b.City}", StringComparison.Ordinal));

            List<Image> highlights = new List<Image>();
            List<Image> accentLines = new List<Image>();

            foreach (var team in sorted)
            {
                var t = team;
                var cardGo = CreateChild(ctr, $"T_{team.TeamId}");
                var cardImg = cardGo.AddComponent<Image>();
                cardImg.color = team.TeamId == _selectedTeamId ? UITheme.FMNavActive : UITheme.PanelSurface;
                highlights.Add(cardImg);

                // Gold accent line at top of card (hidden unless selected)
                var cacc = CreateChild(cardGo.GetComponent<RectTransform>(), "Acc");
                var cacr = cacc.GetComponent<RectTransform>(); cacr.anchorMin = new Vector2(0, 1); cacr.anchorMax = Vector2.one;
                cacr.pivot = new Vector2(0.5f, 1); cacr.sizeDelta = new Vector2(0, 2);
                var caccImg = cacc.AddComponent<Image>();
                caccImg.color = team.TeamId == _selectedTeamId ? UITheme.AccentPrimary : Color.clear;
                accentLines.Add(caccImg);

                // VerticalLayoutGroup inside cell — logo on top, name below
                var vlg2 = cardGo.AddComponent<VerticalLayoutGroup>();
                vlg2.spacing = 1; vlg2.padding = new RectOffset(4, 4, 3, 2);
                vlg2.childControlWidth = true; vlg2.childControlHeight = false;
                vlg2.childForceExpandWidth = true; vlg2.childForceExpandHeight = false;
                vlg2.childAlignment = TextAnchor.UpperCenter;

                // Logo — centered, fixed height
                var logoGo = CreateChild(cardGo.GetComponent<RectTransform>(), "Logo");
                var logoLE = logoGo.AddComponent<LayoutElement>();
                logoLE.preferredHeight = 46; logoLE.preferredWidth = 46;
                var logoImg = logoGo.AddComponent<Image>(); logoImg.preserveAspect = true;
                var sprite = ArtManager.GetTeamLogo(team.TeamId);
                if (sprite != null) logoImg.sprite = sprite;

                // Team name below logo
                var txtGo = CreateChild(cardGo.GetComponent<RectTransform>(), "Txt");
                var txtLE = txtGo.AddComponent<LayoutElement>(); txtLE.preferredHeight = 30;
                var txt = txtGo.AddComponent<Text>();
                txt.text = $"{team.City} {team.Name}";
                txt.verticalOverflow = VerticalWrapMode.Truncate;
                txt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                txt.fontSize = 10; txt.color = Color.white; txt.alignment = TextAnchor.UpperCenter;
                txt.horizontalOverflow = HorizontalWrapMode.Wrap;

                var btn = cardGo.AddComponent<Button>();
                int idx = highlights.Count - 1;
                btn.onClick.AddListener(() => {
                    _selectedTeamId = t.TeamId; _selectedTeam = t;
                    for (int j = 0; j < highlights.Count; j++) {
                        highlights[j].color = j == idx ? UITheme.FMNavActive : UITheme.PanelSurface;
                        accentLines[j].color = j == idx ? UITheme.AccentPrimary : Color.clear;
                    }
                    Color tc = UITheme.ParseTeamColor(t.PrimaryColor, UITheme.AccentPrimary);
                    dText.text = $"<b>{t.City} {t.Name}</b>\n\nConference: {t.Conference}\nDivision: {t.Division}\nArena: {t.ArenaName}\nRecord: {t.Wins}-{t.Losses}";
                    var s = ArtManager.GetTeamLogo(t.TeamId);
                    if (s != null) { dLogoImg.sprite = s; dLogoImg.color = Color.white; }
                });
            }
        }

        // ═══════════════════════════════════════════════════════
        //  STEP 3: ROLE SELECTION
        // ═══════════════════════════════════════════════════════

        private void BuildRoleStep()
        {
            var hlg = _stepContainer.gameObject.AddComponent<HorizontalLayoutGroup>();
            hlg.spacing = 16; hlg.padding = new RectOffset(16, 16, 0, 0);
            hlg.childControlWidth = true; hlg.childControlHeight = true;
            hlg.childForceExpandWidth = true; hlg.childForceExpandHeight = true;

            var roles = new[] {
                (role: UserRole.GMOnly, title: "GENERAL MANAGER", desc:
                    "\u2022 Control all roster decisions\n\u2022 Trades, free agency, draft\n\u2022 Hire/fire coaching staff\n\u2022 AI coach runs games\n\u2022 View game summaries"),
                (role: UserRole.HeadCoachOnly, title: "HEAD COACH", desc:
                    "\u2022 Control in-game decisions\n\u2022 Plays, subs, timeouts\n\u2022 Player development\n\u2022 Request roster moves\n\u2022 GM may approve or deny"),
                (role: UserRole.Both, title: "GM & HEAD COACH", desc:
                    "\u2022 Full control over everything\n\u2022 Build roster AND coach\n\u2022 Complete experience\n\u2022 Traditional game mode\n\u2022 Maximum control")
            };

            List<Image> bgs = new List<Image>(), accs = new List<Image>();

            for (int i = 0; i < roles.Length; i++)
            {
                int idx = i; var rd = roles[i];
                var cardGo = CreateChild(_stepContainer, $"Role_{rd.title}");
                var ci = cardGo.AddComponent<Image>(); ci.color = rd.role == _selectedRole ? UITheme.FMNavActive : UITheme.PanelSurface;
                bgs.Add(ci);

                var cvlg = cardGo.AddComponent<VerticalLayoutGroup>();
                cvlg.spacing = 8; cvlg.padding = new RectOffset(14, 14, 8, 14);
                cvlg.childControlWidth = true; cvlg.childControlHeight = false; cvlg.childForceExpandWidth = true; cvlg.childForceExpandHeight = false;

                // Gold accent
                var a = CreateChild(cardGo.GetComponent<RectTransform>(), "Acc");
                a.AddComponent<LayoutElement>().preferredHeight = 3;
                var ai = a.AddComponent<Image>(); ai.color = rd.role == _selectedRole ? UITheme.AccentPrimary : UITheme.FMDivider;
                accs.Add(ai);

                var tt = MkText(cardGo.GetComponent<RectTransform>(), rd.title, 16, FontStyle.Bold, Color.white);
                tt.alignment = TextAnchor.MiddleCenter; tt.gameObject.AddComponent<LayoutElement>().preferredHeight = 28;

                var dt = MkText(cardGo.GetComponent<RectTransform>(), rd.desc, 12, FontStyle.Normal, UITheme.TextSecondary);
                dt.alignment = TextAnchor.UpperLeft; dt.horizontalOverflow = HorizontalWrapMode.Wrap;
                dt.gameObject.AddComponent<LayoutElement>().flexibleHeight = 1;

                cardGo.AddComponent<Button>().onClick.AddListener(() => {
                    _selectedRole = rd.role;
                    for (int j = 0; j < bgs.Count; j++) {
                        bgs[j].color = j == idx ? UITheme.FMNavActive : UITheme.PanelSurface;
                        accs[j].color = j == idx ? UITheme.AccentPrimary : UITheme.FMDivider;
                    }
                });
            }
        }

        // ═══════════════════════════════════════════════════════
        //  STEP 4: DIFFICULTY
        // ═══════════════════════════════════════════════════════

        private void BuildDifficultyStep()
        {
            var card = CreateChild(_stepContainer, "Card"); Stretch(card); card.AddComponent<Image>().color = UITheme.CardBackground;
            var cr = card.GetComponent<RectTransform>();

            // FM Header
            var hdr = CreateChild(cr, "Hdr");
            var hr = hdr.GetComponent<RectTransform>(); hr.anchorMin = new Vector2(0, 1); hr.anchorMax = Vector2.one;
            hr.pivot = new Vector2(0.5f, 1); hr.sizeDelta = new Vector2(0, 30);
            hdr.AddComponent<Image>().color = UITheme.FMCardHeaderBg;
            var ht = MkText(hr, "DIFFICULTY", 11, FontStyle.Bold, UITheme.AccentPrimary);
            ht.alignment = TextAnchor.MiddleLeft;
            var htr = ht.GetComponent<RectTransform>(); htr.anchorMin = Vector2.zero; htr.anchorMax = Vector2.one; htr.offsetMin = new Vector2(14, 0);
            var hacc = CreateChild(cr, "Acc");
            var hacr = hacc.GetComponent<RectTransform>(); hacr.anchorMin = new Vector2(0, 1); hacr.anchorMax = Vector2.one;
            hacr.pivot = new Vector2(0.5f, 1); hacr.anchoredPosition = new Vector2(0, -30); hacr.sizeDelta = new Vector2(0, 2);
            hacc.AddComponent<Image>().color = UITheme.AccentPrimary;

            // Content area below header
            var contentGo = CreateChild(cr, "Content");
            var contr = contentGo.GetComponent<RectTransform>();
            contr.anchorMin = Vector2.zero; contr.anchorMax = new Vector2(1, 1);
            contr.offsetMin = new Vector2(40, 20); contr.offsetMax = new Vector2(-40, -40);

            var vlg = contentGo.AddComponent<VerticalLayoutGroup>();
            vlg.spacing = 10; vlg.childControlWidth = true; vlg.childControlHeight = false;
            vlg.childForceExpandWidth = true; vlg.childForceExpandHeight = false;

            var diffs = new[] {
                (p: DifficultyPreset.Easy, n: "EASY", d: "Forgiving owner, weaker AI trades, fewer injuries. Great for learning."),
                (p: DifficultyPreset.Normal, n: "NORMAL", d: "Balanced experience. Fair AI, realistic injuries, moderate expectations."),
                (p: DifficultyPreset.Hard, n: "HARD", d: "Aggressive AI, impatient owner, tougher negotiations. For veterans."),
                (p: DifficultyPreset.Legendary, n: "LEGENDARY", d: "Ruthless AI, demanding owner, frequent injuries. Ultimate challenge.")
            };

            List<Image> dhl = new List<Image>(); List<Image> dacl = new List<Image>();
            Text descLabel = null;

            foreach (var d in diffs)
            {
                var dd = d;
                var row = CreateChild(contr, $"D_{d.n}"); row.AddComponent<LayoutElement>().preferredHeight = 44;
                var ri = row.AddComponent<Image>(); ri.color = d.p == _selectedDifficulty ? UITheme.FMNavActive : UITheme.PanelSurface;
                dhl.Add(ri);

                // Gold left accent
                var la = CreateChild(row.GetComponent<RectTransform>(), "Acc");
                var lar = la.GetComponent<RectTransform>(); lar.anchorMin = Vector2.zero; lar.anchorMax = new Vector2(0, 1); lar.sizeDelta = new Vector2(3, 0);
                var lai = la.AddComponent<Image>(); lai.color = d.p == _selectedDifficulty ? UITheme.AccentPrimary : Color.clear;
                dacl.Add(lai);

                var txt = MkText(row.GetComponent<RectTransform>(), d.n, 15, FontStyle.Bold, Color.white);
                txt.alignment = TextAnchor.MiddleCenter; Stretch(txt.gameObject);
                row.AddComponent<Button>().onClick.AddListener(() => {
                    _selectedDifficulty = dd.p;
                    for (int j = 0; j < dhl.Count; j++) {
                        dhl[j].color = diffs[j].p == dd.p ? UITheme.FMNavActive : UITheme.PanelSurface;
                        dacl[j].color = diffs[j].p == dd.p ? UITheme.AccentPrimary : Color.clear;
                    }
                    if (descLabel != null) descLabel.text = dd.d;
                });
            }

            var dg = CreateChild(contr, "Desc"); dg.AddComponent<LayoutElement>().preferredHeight = 36;
            string initDesc = ""; foreach (var d in diffs) { if (d.p == _selectedDifficulty) initDesc = d.d; }
            descLabel = MkText(dg.GetComponent<RectTransform>(), initDesc, 13, FontStyle.Italic, UITheme.TextSecondary);
            descLabel.alignment = TextAnchor.MiddleCenter; descLabel.horizontalOverflow = HorizontalWrapMode.Wrap; Stretch(descLabel.gameObject);
        }

        // ═══════════════════════════════════════════════════════
        //  STEP 5: CONFIRMATION
        // ═══════════════════════════════════════════════════════

        private void BuildConfirmStep()
        {
            var card = CreateChild(_stepContainer, "Card"); Stretch(card); card.AddComponent<Image>().color = UITheme.CardBackground;
            var cr = card.GetComponent<RectTransform>();

            // Header
            var hdr = CreateChild(cr, "Hdr");
            var hr = hdr.GetComponent<RectTransform>(); hr.anchorMin = new Vector2(0, 1); hr.anchorMax = Vector2.one;
            hr.pivot = new Vector2(0.5f, 1); hr.sizeDelta = new Vector2(0, 30);
            hdr.AddComponent<Image>().color = UITheme.FMCardHeaderBg;
            var ht = MkText(hr, "CAREER SUMMARY", 11, FontStyle.Bold, UITheme.AccentPrimary);
            ht.alignment = TextAnchor.MiddleLeft;
            var htr = ht.GetComponent<RectTransform>(); htr.anchorMin = Vector2.zero; htr.anchorMax = Vector2.one; htr.offsetMin = new Vector2(14, 0);
            var hacc = CreateChild(cr, "Acc");
            var hacr = hacc.GetComponent<RectTransform>(); hacr.anchorMin = new Vector2(0, 1); hacr.anchorMax = Vector2.one;
            hacr.pivot = new Vector2(0.5f, 1); hacr.anchoredPosition = new Vector2(0, -30); hacr.sizeDelta = new Vector2(0, 2);
            hacc.AddComponent<Image>().color = UITheme.AccentPrimary;

            // Content
            var contentGo = CreateChild(cr, "Content");
            var contr = contentGo.GetComponent<RectTransform>();
            contr.anchorMin = Vector2.zero; contr.anchorMax = Vector2.one;
            contr.offsetMin = new Vector2(30, 20); contr.offsetMax = new Vector2(-30, -40);
            var vlg = contentGo.AddComponent<VerticalLayoutGroup>();
            vlg.spacing = 8; vlg.childControlWidth = true; vlg.childControlHeight = false;
            vlg.childForceExpandWidth = true; vlg.childForceExpandHeight = false;

            var bg = _backgrounds[_backgroundIndex];
            string roleName = _selectedRole switch { UserRole.GMOnly => "General Manager", UserRole.HeadCoachOnly => "Head Coach", _ => "GM & Head Coach" };
            long salary = 3_000_000 + (bg.Rep - 50) * 50_000;
            string teamName = _selectedTeam != null ? $"{_selectedTeam.City} {_selectedTeam.Name}" : _selectedTeamId;

            MkSummaryRow(contr, "COACH", $"{_firstName} {_lastName}, Age {_age}");
            MkSummaryRow(contr, "BACKGROUND", bg.Name);
            MkSummaryRow(contr, "ROLE", roleName);
            MkSummaryRow(contr, "TEAM", teamName);
            MkSummaryRow(contr, "DIFFICULTY", _selectedDifficulty.ToString());
            MkSummaryRow(contr, "CONTRACT", $"4 Years, ${salary / 1000000.0:F1}M/year");

            // Team logo
            if (_selectedTeam != null)
            {
                var lg = CreateChild(contr, "Logo"); lg.AddComponent<LayoutElement>().preferredHeight = 80;
                var li = lg.AddComponent<Image>(); li.preserveAspect = true;
                var sp = ArtManager.GetTeamLogo(_selectedTeam.TeamId);
                if (sp != null) li.sprite = sp;
            }
        }

        private void MkSummaryRow(RectTransform parent, string label, string value)
        {
            var row = CreateChild(parent, $"R_{label}"); row.AddComponent<LayoutElement>().preferredHeight = 26;
            var h = row.AddComponent<HorizontalLayoutGroup>();
            h.childControlWidth = true; h.childControlHeight = true; h.childForceExpandWidth = true; h.childForceExpandHeight = true;
            var rr = row.GetComponent<RectTransform>();
            var l = MkText(rr, label, 11, FontStyle.Bold, UITheme.AccentPrimary);
            l.gameObject.AddComponent<LayoutElement>().preferredWidth = 110;
            var v = MkText(rr, value, 13, FontStyle.Normal, Color.white);
            v.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1;
        }

        private void StartGame()
        {
            if (GameManager.Instance == null) return;
            var bg = _backgrounds[_backgroundIndex];
            var diff = DifficultySettings.CreateFromPreset(_selectedDifficulty);
            int clampedDay = Mathf.Min(_birthDay, DateTime.DaysInMonth(_birthYear, _birthMonth));
            var birthDate = new DateTime(_birthYear, _birthMonth, clampedDay);
            Debug.Log($"[MenuInjector] Starting: {_firstName} {_lastName}, DOB: {birthDate:MMM dd, yyyy}, Team: {_selectedTeamId}, Role: {_selectedRole}");
            GameManager.Instance.StartNewGame(_firstName, _lastName, _age, _selectedTeamId, diff, bg.Tactical, bg.Dev, bg.Rep, _selectedRole, birthDate);
        }

        // ═══════════════════════════════════════════════════════
        //  LOAD GAME
        // ═══════════════════════════════════════════════════════

        private void ShowLoadGame()
        {
            if (_loadRoot != null) Destroy(_loadRoot);
            _loadRoot = CreateChild(_menuRoot.GetComponent<RectTransform>(), "LoadGame");
            Stretch(_loadRoot); _loadRoot.AddComponent<Image>().color = UITheme.Background;
            var root = _loadRoot.GetComponent<RectTransform>();
            _selectedSaveSlot = null; _saveSlotHighlights.Clear();

            var tb = CreateChild(root, "Title");
            var tbr = tb.GetComponent<RectTransform>(); tbr.anchorMin = new Vector2(0, 0.9f); tbr.anchorMax = Vector2.one; tbr.sizeDelta = Vector2.zero;
            var tt = MkText(tbr, "LOAD GAME", 24, FontStyle.Bold, UITheme.AccentPrimary);
            tt.alignment = TextAnchor.MiddleCenter; Stretch(tt.gameObject);

            // Nav
            var nav = CreateChild(root, "Nav");
            var nr = nav.GetComponent<RectTransform>(); nr.anchorMin = Vector2.zero; nr.anchorMax = new Vector2(1, 0.08f); nr.sizeDelta = Vector2.zero;
            var nh = nav.AddComponent<HorizontalLayoutGroup>();
            nh.spacing = 20; nh.childAlignment = TextAnchor.MiddleCenter; nh.childControlWidth = false; nh.childControlHeight = true;
            nh.padding = new RectOffset(40, 40, 5, 5);

            var bk = CreateChild(nr, "Back"); bk.AddComponent<LayoutElement>().preferredWidth = 120;
            bk.AddComponent<Image>().color = UITheme.PanelSurface;
            bk.AddComponent<Button>().onClick.AddListener(() => { Destroy(_loadRoot); BuildMainMenu(_menuRoot.GetComponent<RectTransform>()); });
            var bkt = MkText(bk.GetComponent<RectTransform>(), "BACK", 14, FontStyle.Bold, UITheme.TextSecondary);
            bkt.alignment = TextAnchor.MiddleCenter; Stretch(bkt.gameObject);

            CreateChild(nr, "F").AddComponent<LayoutElement>().flexibleWidth = 1;

            var dg = CreateChild(nr, "Del"); dg.AddComponent<LayoutElement>().preferredWidth = 120;
            dg.AddComponent<Image>().color = new Color32(60, 20, 20, 255);
            _deleteBtn = dg.AddComponent<Button>(); _deleteBtn.interactable = false; _deleteBtn.onClick.AddListener(OnDeleteSave);
            var dt = MkText(dg.GetComponent<RectTransform>(), "DELETE", 14, FontStyle.Bold, UITheme.Danger);
            dt.alignment = TextAnchor.MiddleCenter; Stretch(dt.gameObject);

            var lg = CreateChild(nr, "Load"); lg.AddComponent<LayoutElement>().preferredWidth = 140;
            lg.AddComponent<Image>().color = new Color32(20, 40, 60, 255);
            _loadBtn = lg.AddComponent<Button>(); _loadBtn.interactable = false; _loadBtn.onClick.AddListener(OnLoadSave);
            var lt = MkText(lg.GetComponent<RectTransform>(), "LOAD", 14, FontStyle.Bold, UITheme.AccentSecondary);
            lt.alignment = TextAnchor.MiddleCenter; Stretch(lt.gameObject);

            var la = CreateChild(root, "List");
            var lar = la.GetComponent<RectTransform>(); lar.anchorMin = new Vector2(0.1f, 0.1f); lar.anchorMax = new Vector2(0.9f, 0.88f); lar.sizeDelta = Vector2.zero;

            var saves = GameManager.Instance?.SaveLoad?.GetAllSaves();
            if (saves == null || saves.Count == 0) {
                var et = MkText(lar, "No saved games found.\n\nStart a new game to begin your career.", 16, FontStyle.Normal, UITheme.TextSecondary);
                et.alignment = TextAnchor.MiddleCenter; Stretch(et.gameObject); return;
            }

            var sg = CreateChild(lar, "Scroll"); Stretch(sg);
            var scroll = sg.AddComponent<ScrollRect>(); scroll.horizontal = false;
            var vp = CreateChild(sg.GetComponent<RectTransform>(), "VP"); Stretch(vp); vp.AddComponent<RectMask2D>();
            var cg = CreateChild(vp.GetComponent<RectTransform>(), "Content");
            var cgr = cg.GetComponent<RectTransform>(); cgr.anchorMin = new Vector2(0, 1); cgr.anchorMax = Vector2.one; cgr.pivot = new Vector2(0.5f, 1);
            var vlg = cg.AddComponent<VerticalLayoutGroup>();
            vlg.spacing = 6; vlg.childControlWidth = true; vlg.childControlHeight = false; vlg.childForceExpandWidth = true;
            cg.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            scroll.content = cgr; scroll.viewport = vp.GetComponent<RectTransform>();

            foreach (var save in saves)
            {
                var s = save;
                var rg = CreateChild(cgr, $"S_{save.SlotName}"); rg.AddComponent<LayoutElement>().preferredHeight = 70;
                var ri = rg.AddComponent<Image>(); ri.color = UITheme.PanelSurface; _saveSlotHighlights.Add(ri);

                // Use HLG for logo + text
                var rhlg = rg.AddComponent<HorizontalLayoutGroup>();
                rhlg.spacing = 10; rhlg.padding = new RectOffset(10, 10, 8, 8);
                rhlg.childControlWidth = false; rhlg.childControlHeight = false; rhlg.childForceExpandWidth = false;
                rhlg.childAlignment = TextAnchor.MiddleLeft;

                if (!string.IsNullOrEmpty(save.TeamId)) {
                    var slg = CreateChild(rg.GetComponent<RectTransform>(), "Logo");
                    var sle = slg.AddComponent<LayoutElement>(); sle.preferredWidth = 44; sle.preferredHeight = 44;
                    var sli = slg.AddComponent<Image>(); sli.preserveAspect = true;
                    var sp = ArtManager.GetTeamLogo(save.TeamId); if (sp != null) sli.sprite = sp;
                }

                string badge = save.IsIronman ? " [IRONMAN]" : "";
                var tg = CreateChild(rg.GetComponent<RectTransform>(), "Txt");
                tg.AddComponent<LayoutElement>().flexibleWidth = 1;
                var txt = tg.AddComponent<Text>();
                txt.text = $"<b>{save.CoachName}</b> - {save.TeamId}{badge}\nSeason {save.Season} | {save.Record} | {save.SaveTimestamp:MMM dd, yyyy HH:mm}";
                txt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                txt.fontSize = 13; txt.color = Color.white; txt.supportRichText = true; txt.horizontalOverflow = HorizontalWrapMode.Wrap;

                int ri2 = _saveSlotHighlights.Count - 1;
                rg.AddComponent<Button>().onClick.AddListener(() => {
                    _selectedSaveSlot = s.SlotName;
                    for (int j = 0; j < _saveSlotHighlights.Count; j++) _saveSlotHighlights[j].color = j == ri2 ? UITheme.FMNavActive : UITheme.PanelSurface;
                    _loadBtn.interactable = true; _deleteBtn.interactable = true;
                });
            }
        }

        private void OnLoadSave() { if (!string.IsNullOrEmpty(_selectedSaveSlot)) GameManager.Instance?.LoadGame(_selectedSaveSlot); }
        private void OnDeleteSave() { if (!string.IsNullOrEmpty(_selectedSaveSlot)) { GameManager.Instance?.SaveLoad?.DeleteSave(_selectedSaveSlot); _selectedSaveSlot = null; ShowLoadGame(); } }

        // ═══════════════════════════════════════════════════════
        //  HELPERS
        // ═══════════════════════════════════════════════════════

        private static GameObject CreateChild(RectTransform parent, string name)
        { var go = new GameObject(name, typeof(RectTransform)); go.transform.SetParent(parent, false); return go; }
        private static GameObject CreateChild(Transform parent, string name)
        { var go = new GameObject(name, typeof(RectTransform)); go.transform.SetParent(parent, false); return go; }

        private static RectTransform Stretch(GameObject go)
        { var rt = go.GetComponent<RectTransform>(); rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.sizeDelta = Vector2.zero; rt.anchoredPosition = Vector2.zero; return rt; }

        private static Text MkText(RectTransform parent, string content, int fontSize, FontStyle style, Color color)
        {
            var go = new GameObject("Text", typeof(RectTransform)); go.transform.SetParent(parent, false);
            var t = go.AddComponent<Text>(); t.text = content; t.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            t.fontSize = fontSize; t.fontStyle = style; t.color = color; t.supportRichText = true;
            t.horizontalOverflow = HorizontalWrapMode.Overflow; t.verticalOverflow = VerticalWrapMode.Overflow;
            return t;
        }

        // Absolute positioning helpers for Step 1
        private Text PlaceLabel(RectTransform parent, string text, float padX, ref float y)
        {
            var container = CreateChild(parent, "Lbl_" + text);
            var cr = container.GetComponent<RectTransform>();
            cr.anchorMin = new Vector2(0, 1); cr.anchorMax = new Vector2(1, 1);
            cr.pivot = new Vector2(0.5f, 1);
            cr.offsetMin = new Vector2(padX, 0); cr.offsetMax = new Vector2(-padX, 0);
            cr.anchoredPosition = new Vector2(0, y); cr.sizeDelta = new Vector2(cr.sizeDelta.x, 18);
            var t = MkText(cr, text, 11, FontStyle.Bold, UITheme.AccentPrimary);
            t.alignment = TextAnchor.MiddleLeft;
            Stretch(t.gameObject);
            y -= 20;
            return t;
        }

        private InputField PlaceInput(RectTransform parent, string value, string placeholder, float padX, ref float y)
        {
            var go = CreateChild(parent, "Input");
            var gr = go.GetComponent<RectTransform>(); gr.anchorMin = new Vector2(0, 1); gr.anchorMax = new Vector2(1, 1);
            gr.pivot = new Vector2(0.5f, 1); gr.offsetMin = new Vector2(padX, 0); gr.offsetMax = new Vector2(-padX, 0);
            gr.anchoredPosition = new Vector2(0, y); gr.sizeDelta = new Vector2(gr.sizeDelta.x, 32);
            go.AddComponent<Image>().color = UITheme.PanelSurface;

            var tg = CreateChild(gr, "Text"); Stretch(tg);
            tg.GetComponent<RectTransform>().offsetMin = new Vector2(10, 2); tg.GetComponent<RectTransform>().offsetMax = new Vector2(-10, -2);
            var t = tg.AddComponent<Text>(); t.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            t.fontSize = 14; t.color = Color.white; t.supportRichText = false;

            var pg = CreateChild(gr, "PH"); Stretch(pg);
            pg.GetComponent<RectTransform>().offsetMin = new Vector2(10, 2); pg.GetComponent<RectTransform>().offsetMax = new Vector2(-10, -2);
            var pt = pg.AddComponent<Text>(); pt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            pt.fontSize = 14; pt.fontStyle = FontStyle.Italic; pt.color = new Color(1, 1, 1, 0.3f); pt.text = placeholder;

            var input = go.AddComponent<InputField>(); input.textComponent = t; input.placeholder = pt; input.text = value;
            y -= 36;
            return input;
        }

        private Text PlaceText(RectTransform parent, string text, int size, FontStyle style, Color color, float padX, ref float y, float height)
        {
            var t = MkText(parent, text, size, style, color);
            t.horizontalOverflow = HorizontalWrapMode.Wrap; t.verticalOverflow = VerticalWrapMode.Overflow;
            var r = t.GetComponent<RectTransform>(); r.anchorMin = new Vector2(0, 1); r.anchorMax = new Vector2(1, 1);
            r.pivot = new Vector2(0.5f, 1); r.offsetMin = new Vector2(padX, 0); r.offsetMax = new Vector2(-padX, 0);
            r.anchoredPosition = new Vector2(0, y); r.sizeDelta = new Vector2(r.sizeDelta.x, height);
            y -= height + 2;
            return t;
        }
    }
}

using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using NBAHeadCoach.Core;
using NBAHeadCoach.Core.Data;
using NBAHeadCoach.Core.Util;
using static NBAHeadCoach.UI.Menu.MenuUI;

namespace NBAHeadCoach.UI.Menu
{
    public class NewGameWizard
    {
        private readonly MonoBehaviour _host;
        private readonly RectTransform _menuRoot;
        private readonly Action _onBack;

        private GameObject _wizardRoot;
        private int _currentStep;
        private RectTransform _stepContainer;
        private Text _stepIndicatorText;
        private Button _nextBtn, _backBtn;
        private Text _nextBtnText;

        private string _firstName = "", _lastName = "";
        private int _age = 45, _birthMonth = 6, _birthDay = 15, _birthYear = 1980, _backgroundIndex;
        private string _selectedTeamId;
        private Team _selectedTeam;
        private UserRole _selectedRole = UserRole.Both;
        private DifficultyPreset _selectedDifficulty = DifficultyPreset.Normal;

        private static readonly CoachBg[] _backgrounds = {
            new CoachBg("Former Player", "High reputation from playing career, still learning the coaching craft.", 70, 45, 60),
            new CoachBg("College Coach", "Balanced experience with proven track record at the collegiate level.", 55, 60, 55),
            new CoachBg("Assistant Coach", "Years of NBA experience as an assistant, strong tactical knowledge.", 50, 75, 50),
            new CoachBg("Int'l Coach", "Unique perspective from European or international leagues.", 45, 65, 70),
            new CoachBg("Analytics", "Data-driven approach, innovative but unproven at NBA level.", 40, 55, 80)
        };

        private struct CoachBg
        {
            public string Name, Desc; public int Rep, Tactical, Dev;
            public CoachBg(string n, string d, int r, int t, int dv) { Name = n; Desc = d; Rep = r; Tactical = t; Dev = dv; }
        }

        public NewGameWizard(MonoBehaviour host, RectTransform menuRoot, Action onBack)
        { _host = host; _menuRoot = menuRoot; _onBack = onBack; }

        public void Show()
        {
            _currentStep = 0; _firstName = ""; _lastName = ""; _age = 45;
            _birthMonth = 6; _birthDay = 15; _birthYear = 1980; _backgroundIndex = 0;
            _selectedTeamId = null; _selectedTeam = null; _selectedRole = UserRole.Both; _selectedDifficulty = DifficultyPreset.Normal;

            if (_wizardRoot != null) UnityEngine.Object.Destroy(_wizardRoot);
            _wizardRoot = CreateChild(_menuRoot, "Wizard"); Stretch(_wizardRoot);
            _wizardRoot.AddComponent<Image>().color = UITheme.Background;
            var root = _wizardRoot.GetComponent<RectTransform>();

            var stepGo = CreateChild(root, "StepContainer");
            _stepContainer = stepGo.GetComponent<RectTransform>();
            _stepContainer.anchorMin = new Vector2(0.03f, 0.09f); _stepContainer.anchorMax = new Vector2(0.97f, 0.88f); _stepContainer.sizeDelta = Vector2.zero;

            var top = CreateChild(root, "TopBar");
            var tr = top.GetComponent<RectTransform>(); tr.anchorMin = new Vector2(0, 0.9f); tr.anchorMax = Vector2.one; tr.sizeDelta = Vector2.zero;
            _stepIndicatorText = MkText(tr, "", 14, FontStyle.Bold, UITheme.TextSecondary);
            _stepIndicatorText.alignment = TextAnchor.MiddleCenter; Stretch(_stepIndicatorText.gameObject);

            var nav = CreateChild(root, "NavBar");
            var nr = nav.GetComponent<RectTransform>(); nr.anchorMin = Vector2.zero; nr.anchorMax = new Vector2(1, 0.07f); nr.sizeDelta = Vector2.zero;
            var navHlg = nav.AddComponent<HorizontalLayoutGroup>();
            navHlg.spacing = 20; navHlg.childAlignment = TextAnchor.MiddleCenter;
            navHlg.childControlWidth = false; navHlg.childControlHeight = true; navHlg.padding = new RectOffset(40, 40, 4, 4);

            var backGo = CreateChild(nr, "Back"); backGo.AddComponent<LayoutElement>().preferredWidth = 120;
            backGo.AddComponent<Image>().color = UITheme.PanelSurface;
            _backBtn = backGo.AddComponent<Button>(); _backBtn.onClick.AddListener(OnBack);
            var bt = MkText(backGo.GetComponent<RectTransform>(), "BACK", 13, FontStyle.Bold, UITheme.TextSecondary);
            bt.alignment = TextAnchor.MiddleCenter; Stretch(bt.gameObject);

            CreateChild(nr, "Flex").AddComponent<LayoutElement>().flexibleWidth = 1;

            var nextGo = CreateChild(nr, "Next"); nextGo.AddComponent<LayoutElement>().preferredWidth = 160;
            nextGo.AddComponent<Image>().color = new Color32(40, 50, 70, 255);
            var na = CreateChild(nextGo.GetComponent<RectTransform>(), "Accent");
            var nar = na.GetComponent<RectTransform>(); nar.anchorMin = Vector2.zero; nar.anchorMax = new Vector2(0, 1); nar.sizeDelta = new Vector2(3, 0);
            na.AddComponent<Image>().color = UITheme.AccentPrimary;
            _nextBtn = nextGo.AddComponent<Button>(); _nextBtn.onClick.AddListener(OnNext);
            _nextBtnText = MkText(nextGo.GetComponent<RectTransform>(), "NEXT", 14, FontStyle.Bold, UITheme.AccentPrimary);
            _nextBtnText.alignment = TextAnchor.MiddleCenter; Stretch(_nextBtnText.gameObject);

            ShowStep();
        }

        public void Destroy() { if (_wizardRoot != null) UnityEngine.Object.Destroy(_wizardRoot); }

        private void ShowStep()
        {
            foreach (Transform child in _stepContainer) UnityEngine.Object.Destroy(child.gameObject);
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
            _currentStep++; ShowStep();
        }

        private void OnBack()
        {
            if (_currentStep == 0) { Destroy(); _onBack(); return; }
            _currentStep--; ShowStep();
        }

        // ── STEP 1: COACH CREATION ──

        private void BuildCoachStep()
        {
            var card = CreateChild(_stepContainer, "Card");
            var cr = card.GetComponent<RectTransform>();
            cr.anchorMin = new Vector2(0.18f, 0); cr.anchorMax = new Vector2(0.82f, 1); cr.sizeDelta = Vector2.zero;
            card.AddComponent<Image>().color = UITheme.CardBackground;

            var hdr = CreateChild(cr, "Header");
            var hr = hdr.GetComponent<RectTransform>(); hr.anchorMin = new Vector2(0, 1); hr.anchorMax = Vector2.one;
            hr.pivot = new Vector2(0.5f, 1); hr.sizeDelta = new Vector2(0, 32);
            hdr.AddComponent<Image>().color = UITheme.FMCardHeaderBg;
            var acc = CreateChild(cr, "Accent");
            var acr = acc.GetComponent<RectTransform>(); acr.anchorMin = new Vector2(0, 1); acr.anchorMax = Vector2.one;
            acr.pivot = new Vector2(0.5f, 1); acr.anchoredPosition = new Vector2(0, -32); acr.sizeDelta = new Vector2(0, 3);
            acc.AddComponent<Image>().color = UITheme.AccentPrimary;
            var ht = MkText(hr, "CREATE YOUR COACH", 11, FontStyle.Bold, UITheme.AccentPrimary);
            ht.alignment = TextAnchor.MiddleLeft;
            var htr = ht.GetComponent<RectTransform>(); htr.anchorMin = Vector2.zero; htr.anchorMax = Vector2.one; htr.offsetMin = new Vector2(14, 0);

            float y = -44; float pad = 28;
            PlaceLabel(cr, "FIRST NAME", pad, ref y);
            var fn = PlaceInput(cr, _firstName, "Enter first name...", pad, ref y);
            fn.onValueChanged.AddListener(v => _firstName = v);

            y -= 6; PlaceLabel(cr, "LAST NAME", pad, ref y);
            var ln = PlaceInput(cr, _lastName, "Enter last name...", pad, ref y);
            ln.onValueChanged.AddListener(v => _lastName = v);

            y -= 6; PlaceLabel(cr, "DATE OF BIRTH", pad, ref y);
            int currentYear = DateTime.Now.Year;
            int minYear = currentYear - 70, maxYear = currentYear - 30;
            string[] months = { "Jan", "Feb", "Mar", "Apr", "May", "Jun", "Jul", "Aug", "Sep", "Oct", "Nov", "Dec" };

            var dobRow = CreateChild(cr, "DOBRow");
            var dobr = dobRow.GetComponent<RectTransform>();
            dobr.anchorMin = new Vector2(0, 1); dobr.anchorMax = new Vector2(1, 1); dobr.pivot = new Vector2(0.5f, 1);
            dobr.offsetMin = new Vector2(pad, 0); dobr.offsetMax = new Vector2(-pad, 0);
            dobr.anchoredPosition = new Vector2(0, y); dobr.sizeDelta = new Vector2(dobr.sizeDelta.x, 32);
            var dobHlg = dobRow.AddComponent<HorizontalLayoutGroup>();
            dobHlg.spacing = 6; dobHlg.childControlWidth = true; dobHlg.childControlHeight = true; dobHlg.childForceExpandWidth = false;

            Text monthText, dayText, yearText;
            var monthSpinner = PlaceSpinner(dobr, months[_birthMonth - 1], 90, out monthText);
            var daySpinner = PlaceSpinner(dobr, _birthDay.ToString(), 60, out dayText);
            var yearSpinner = PlaceSpinner(dobr, _birthYear.ToString(), 90, out yearText);
            y -= 38;

            RecalcAge();
            var ageDisplay = PlaceLabel(cr, $"Age: {_age}", pad, ref y);
            ageDisplay.color = UITheme.TextSecondary; ageDisplay.fontStyle = FontStyle.Normal;

            Action updateDOB = () => {
                int maxDay = DateTime.DaysInMonth(_birthYear, _birthMonth);
                if (_birthDay > maxDay) _birthDay = maxDay;
                monthText.text = months[_birthMonth - 1]; dayText.text = _birthDay.ToString(); yearText.text = _birthYear.ToString();
                RecalcAge(); ageDisplay.text = $"Age: {_age}";
            };

            AddHoldRepeat(monthSpinner.Item1.gameObject, () => { _birthMonth = _birthMonth <= 1 ? 12 : _birthMonth - 1; updateDOB(); }, 1f);
            AddHoldRepeat(monthSpinner.Item2.gameObject, () => { _birthMonth = _birthMonth >= 12 ? 1 : _birthMonth + 1; updateDOB(); }, 1f);
            AddHoldRepeat(daySpinner.Item1.gameObject, () => { int m = DateTime.DaysInMonth(_birthYear, _birthMonth); _birthDay = _birthDay <= 1 ? m : _birthDay - 1; updateDOB(); }, 1f);
            AddHoldRepeat(daySpinner.Item2.gameObject, () => { int m = DateTime.DaysInMonth(_birthYear, _birthMonth); _birthDay = _birthDay >= m ? 1 : _birthDay + 1; updateDOB(); }, 1f);
            AddHoldRepeat(yearSpinner.Item1.gameObject, () => { _birthYear = _birthYear <= minYear ? maxYear : _birthYear - 1; updateDOB(); }, 2f);
            AddHoldRepeat(yearSpinner.Item2.gameObject, () => { _birthYear = _birthYear >= maxYear ? minYear : _birthYear + 1; updateDOB(); }, 2f);

            y -= 4; y -= 6; PlaceLabel(cr, "BACKGROUND", pad, ref y);
            var bgRow = CreateChild(cr, "BgRow"); var bgr = bgRow.GetComponent<RectTransform>();
            bgr.anchorMin = new Vector2(0, 1); bgr.anchorMax = new Vector2(1, 1); bgr.pivot = new Vector2(0.5f, 1);
            bgr.offsetMin = new Vector2(pad, 0); bgr.offsetMax = new Vector2(-pad, 0);
            bgr.anchoredPosition = new Vector2(0, y); bgr.sizeDelta = new Vector2(bgr.sizeDelta.x, 28);
            var bgHlg = bgRow.AddComponent<HorizontalLayoutGroup>();
            bgHlg.spacing = 4; bgHlg.childControlWidth = true; bgHlg.childControlHeight = true; bgHlg.childForceExpandWidth = true;
            y -= 34;

            var desc = PlaceText(cr, _backgrounds[_backgroundIndex].Desc, 12, FontStyle.Italic, UITheme.TextSecondary, pad, ref y, 32);
            y -= 4; var bgd = _backgrounds[_backgroundIndex];
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

        private void RecalcAge()
        {
            var dob = new DateTime(_birthYear, _birthMonth, Mathf.Min(_birthDay, DateTime.DaysInMonth(_birthYear, _birthMonth)));
            var now = DateTime.Now; _age = now.Year - dob.Year; if (now < dob.AddYears(_age)) _age--;
        }

        private Tuple<Button, Button> PlaceSpinner(RectTransform parent, string initialValue, float width, out Text valueText)
        {
            var go = CreateChild(parent, "Spinner"); go.AddComponent<LayoutElement>().preferredWidth = width;
            go.AddComponent<Image>().color = UITheme.PanelSurface;
            var hlg = go.AddComponent<HorizontalLayoutGroup>();
            hlg.childControlWidth = true; hlg.childControlHeight = true; hlg.childForceExpandWidth = false; hlg.childForceExpandHeight = true;
            hlg.padding = new RectOffset(2, 2, 0, 0);

            var leftGo = CreateChild(go.GetComponent<RectTransform>(), "Left"); leftGo.AddComponent<LayoutElement>().preferredWidth = 20;
            leftGo.AddComponent<Image>().color = UITheme.CardBackground; var leftBtn = leftGo.AddComponent<Button>();
            var lt = MkText(leftGo.GetComponent<RectTransform>(), "\u25C0", 9, FontStyle.Normal, UITheme.TextSecondary);
            lt.alignment = TextAnchor.MiddleCenter; Stretch(lt.gameObject);

            var valGo = CreateChild(go.GetComponent<RectTransform>(), "Value"); valGo.AddComponent<LayoutElement>().flexibleWidth = 1;
            var vt = MkText(valGo.GetComponent<RectTransform>(), initialValue, 12, FontStyle.Normal, Color.white);
            vt.alignment = TextAnchor.MiddleCenter; Stretch(vt.gameObject); valueText = vt;

            var rightGo = CreateChild(go.GetComponent<RectTransform>(), "Right"); rightGo.AddComponent<LayoutElement>().preferredWidth = 20;
            rightGo.AddComponent<Image>().color = UITheme.CardBackground; var rightBtn = rightGo.AddComponent<Button>();
            var rt2 = MkText(rightGo.GetComponent<RectTransform>(), "\u25B6", 9, FontStyle.Normal, UITheme.TextSecondary);
            rt2.alignment = TextAnchor.MiddleCenter; Stretch(rt2.gameObject);

            return new Tuple<Button, Button>(leftBtn, rightBtn);
        }

        private void AddHoldRepeat(GameObject go, Action onTick, float speed)
        { var h = go.AddComponent<HoldRepeatButton>(); h.OnTick = onTick; h.SpeedMultiplier = speed; }

        private IEnumerator ResizeGridNextFrame(GridLayoutGroup grid, RectTransform rect, int cols, int rows, int spacing, int padding)
        {
            yield return null; if (grid == null || rect == null) yield break;
            float w = rect.rect.width, h = rect.rect.height;
            grid.cellSize = new Vector2(Mathf.Floor((w - padding * 2 - spacing * (cols - 1)) / cols), Mathf.Floor((h - padding * 2 - spacing * (rows - 1)) / rows));
        }

        // ── STEP 2: TEAM SELECTION ──

        private void BuildTeamStep()
        {
            var teams = GameManager.Instance?.AllTeams; if (teams == null || teams.Count == 0) return;

            var leftGo = CreateChild(_stepContainer, "Grid"); var lr = leftGo.GetComponent<RectTransform>();
            lr.anchorMin = Vector2.zero; lr.anchorMax = new Vector2(0.66f, 1); lr.sizeDelta = Vector2.zero;

            var rightGo = CreateChild(_stepContainer, "Detail"); var rr = rightGo.GetComponent<RectTransform>();
            rr.anchorMin = new Vector2(0.68f, 0); rr.anchorMax = Vector2.one; rr.sizeDelta = Vector2.zero;
            rightGo.AddComponent<Image>().color = UITheme.CardBackground;

            var dHdr = CreateChild(rr, "Header"); var dhr = dHdr.GetComponent<RectTransform>();
            dhr.anchorMin = new Vector2(0, 1); dhr.anchorMax = Vector2.one; dhr.pivot = new Vector2(0.5f, 1); dhr.sizeDelta = new Vector2(0, 30);
            dHdr.AddComponent<Image>().color = UITheme.FMCardHeaderBg;
            var dht = MkText(dhr, "TEAM DETAILS", 11, FontStyle.Bold, UITheme.AccentPrimary);
            dht.alignment = TextAnchor.MiddleLeft; var dhtr = dht.GetComponent<RectTransform>();
            dhtr.anchorMin = Vector2.zero; dhtr.anchorMax = Vector2.one; dhtr.offsetMin = new Vector2(12, 0);
            var dacc = CreateChild(rr, "Accent"); var dacr = dacc.GetComponent<RectTransform>();
            dacr.anchorMin = new Vector2(0, 1); dacr.anchorMax = Vector2.one; dacr.pivot = new Vector2(0.5f, 1);
            dacr.anchoredPosition = new Vector2(0, -30); dacr.sizeDelta = new Vector2(0, 2);
            dacc.AddComponent<Image>().color = UITheme.AccentPrimary;

            var dLogoGo = CreateChild(rr, "Logo"); var dlr = dLogoGo.GetComponent<RectTransform>();
            dlr.anchorMin = new Vector2(0.25f, 0.45f); dlr.anchorMax = new Vector2(0.75f, 0.85f); dlr.sizeDelta = Vector2.zero;
            var dLogoImg = dLogoGo.AddComponent<Image>(); dLogoImg.preserveAspect = true; dLogoImg.color = new Color(1, 1, 1, 0.15f);

            var dText = MkText(rr, "Select a team\nfrom the grid", 13, FontStyle.Normal, UITheme.TextSecondary);
            dText.alignment = TextAnchor.UpperLeft; dText.horizontalOverflow = HorizontalWrapMode.Wrap;
            var dtr = dText.GetComponent<RectTransform>(); dtr.anchorMin = new Vector2(0, 0.02f); dtr.anchorMax = new Vector2(1, 0.42f);
            dtr.offsetMin = new Vector2(14, 8); dtr.offsetMax = new Vector2(-14, -4);

            var contentGo = CreateChild(lr, "TeamGrid"); Stretch(contentGo); var ctr = contentGo.GetComponent<RectTransform>();
            var grid = contentGo.AddComponent<GridLayoutGroup>();
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount; grid.constraintCount = 5;
            int cols = 5, rows = 6, sp = 4, pd = 2;
            grid.spacing = new Vector2(sp, sp); grid.padding = new RectOffset(pd, pd, pd, pd); grid.cellSize = new Vector2(100, 80);
            _host.StartCoroutine(ResizeGridNextFrame(grid, ctr, cols, rows, sp, pd));

            var sorted = new List<Team>(teams);
            sorted.Sort((a, b) => string.Compare($"{a.Conference}{a.City}", $"{b.Conference}{b.City}", StringComparison.Ordinal));
            List<Image> highlights = new List<Image>(), accentLines = new List<Image>();

            foreach (var team in sorted)
            {
                var t = team;
                var cardGo = CreateChild(ctr, $"T_{team.TeamId}");
                var cardImg = cardGo.AddComponent<Image>(); cardImg.color = team.TeamId == _selectedTeamId ? UITheme.FMNavActive : UITheme.PanelSurface;
                highlights.Add(cardImg);
                var cacc = CreateChild(cardGo.GetComponent<RectTransform>(), "Acc"); var cacr = cacc.GetComponent<RectTransform>();
                cacr.anchorMin = new Vector2(0, 1); cacr.anchorMax = Vector2.one; cacr.pivot = new Vector2(0.5f, 1); cacr.sizeDelta = new Vector2(0, 2);
                var caccImg = cacc.AddComponent<Image>(); caccImg.color = team.TeamId == _selectedTeamId ? UITheme.AccentPrimary : Color.clear;
                accentLines.Add(caccImg);

                var vlg2 = cardGo.AddComponent<VerticalLayoutGroup>();
                vlg2.spacing = 1; vlg2.padding = new RectOffset(4, 4, 3, 2);
                vlg2.childControlWidth = true; vlg2.childControlHeight = false; vlg2.childForceExpandWidth = true; vlg2.childAlignment = TextAnchor.UpperCenter;

                var logoGo = CreateChild(cardGo.GetComponent<RectTransform>(), "Logo");
                var logoLE = logoGo.AddComponent<LayoutElement>(); logoLE.preferredHeight = 46; logoLE.preferredWidth = 46;
                var logoImg = logoGo.AddComponent<Image>(); logoImg.preserveAspect = true;
                var sprite = ArtManager.GetTeamLogo(team.TeamId); if (sprite != null) logoImg.sprite = sprite;

                var txtGo = CreateChild(cardGo.GetComponent<RectTransform>(), "Txt"); txtGo.AddComponent<LayoutElement>().preferredHeight = 30;
                var txt = txtGo.AddComponent<Text>(); txt.text = $"{team.City} {team.Name}";
                txt.verticalOverflow = VerticalWrapMode.Truncate; txt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                txt.fontSize = 10; txt.color = Color.white; txt.alignment = TextAnchor.UpperCenter; txt.horizontalOverflow = HorizontalWrapMode.Wrap;

                int idx = highlights.Count - 1;
                cardGo.AddComponent<Button>().onClick.AddListener(() => {
                    _selectedTeamId = t.TeamId; _selectedTeam = t;
                    for (int j = 0; j < highlights.Count; j++) { highlights[j].color = j == idx ? UITheme.FMNavActive : UITheme.PanelSurface; accentLines[j].color = j == idx ? UITheme.AccentPrimary : Color.clear; }
                    dText.text = $"<b>{t.City} {t.Name}</b>\n\nConference: {t.Conference}\nDivision: {t.Division}\nArena: {t.ArenaName}\nRecord: {t.Wins}-{t.Losses}";
                    var s = ArtManager.GetTeamLogo(t.TeamId); if (s != null) { dLogoImg.sprite = s; dLogoImg.color = Color.white; }
                });
            }
        }

        // ── STEP 3: ROLE SELECTION ──

        private void BuildRoleStep()
        {
            var hlg = _stepContainer.gameObject.AddComponent<HorizontalLayoutGroup>();
            hlg.spacing = 16; hlg.padding = new RectOffset(16, 16, 0, 0);
            hlg.childControlWidth = true; hlg.childControlHeight = true; hlg.childForceExpandWidth = true; hlg.childForceExpandHeight = true;

            var roles = new[] {
                (role: UserRole.GMOnly, title: "GENERAL MANAGER", desc: "\u2022 Control all roster decisions\n\u2022 Trades, free agency, draft\n\u2022 Hire/fire coaching staff\n\u2022 AI coach runs games\n\u2022 View game summaries"),
                (role: UserRole.HeadCoachOnly, title: "HEAD COACH", desc: "\u2022 Control in-game decisions\n\u2022 Plays, subs, timeouts\n\u2022 Player development\n\u2022 Request roster moves\n\u2022 GM may approve or deny"),
                (role: UserRole.Both, title: "GM & HEAD COACH", desc: "\u2022 Full control over everything\n\u2022 Build roster AND coach\n\u2022 Complete experience\n\u2022 Traditional game mode\n\u2022 Maximum control")
            };
            List<Image> bgs = new List<Image>(), accs = new List<Image>();
            for (int i = 0; i < roles.Length; i++)
            {
                int idx = i; var rd = roles[i];
                var cardGo = CreateChild(_stepContainer, $"Role_{rd.title}");
                var ci = cardGo.AddComponent<Image>(); ci.color = rd.role == _selectedRole ? UITheme.FMNavActive : UITheme.PanelSurface; bgs.Add(ci);
                var cvlg = cardGo.AddComponent<VerticalLayoutGroup>();
                cvlg.spacing = 8; cvlg.padding = new RectOffset(14, 14, 8, 14);
                cvlg.childControlWidth = true; cvlg.childControlHeight = false; cvlg.childForceExpandWidth = true;
                var a = CreateChild(cardGo.GetComponent<RectTransform>(), "Acc"); a.AddComponent<LayoutElement>().preferredHeight = 3;
                var ai = a.AddComponent<Image>(); ai.color = rd.role == _selectedRole ? UITheme.AccentPrimary : UITheme.FMDivider; accs.Add(ai);
                var tt = MkText(cardGo.GetComponent<RectTransform>(), rd.title, 16, FontStyle.Bold, Color.white);
                tt.alignment = TextAnchor.MiddleCenter; tt.gameObject.AddComponent<LayoutElement>().preferredHeight = 28;
                var dt = MkText(cardGo.GetComponent<RectTransform>(), rd.desc, 12, FontStyle.Normal, UITheme.TextSecondary);
                dt.alignment = TextAnchor.UpperLeft; dt.horizontalOverflow = HorizontalWrapMode.Wrap; dt.gameObject.AddComponent<LayoutElement>().flexibleHeight = 1;
                cardGo.AddComponent<Button>().onClick.AddListener(() => {
                    _selectedRole = rd.role;
                    for (int j = 0; j < bgs.Count; j++) { bgs[j].color = j == idx ? UITheme.FMNavActive : UITheme.PanelSurface; accs[j].color = j == idx ? UITheme.AccentPrimary : UITheme.FMDivider; }
                });
            }
        }

        // ── STEP 4: DIFFICULTY ──

        private void BuildDifficultyStep()
        {
            var card = CreateChild(_stepContainer, "Card"); Stretch(card); card.AddComponent<Image>().color = UITheme.CardBackground;
            var cr = card.GetComponent<RectTransform>();
            var hdr = CreateChild(cr, "Hdr"); var hr = hdr.GetComponent<RectTransform>();
            hr.anchorMin = new Vector2(0, 1); hr.anchorMax = Vector2.one; hr.pivot = new Vector2(0.5f, 1); hr.sizeDelta = new Vector2(0, 30);
            hdr.AddComponent<Image>().color = UITheme.FMCardHeaderBg;
            var ht = MkText(hr, "DIFFICULTY", 11, FontStyle.Bold, UITheme.AccentPrimary); ht.alignment = TextAnchor.MiddleLeft;
            var htr = ht.GetComponent<RectTransform>(); htr.anchorMin = Vector2.zero; htr.anchorMax = Vector2.one; htr.offsetMin = new Vector2(14, 0);
            var hacc = CreateChild(cr, "Acc"); var hacr = hacc.GetComponent<RectTransform>();
            hacr.anchorMin = new Vector2(0, 1); hacr.anchorMax = Vector2.one; hacr.pivot = new Vector2(0.5f, 1);
            hacr.anchoredPosition = new Vector2(0, -30); hacr.sizeDelta = new Vector2(0, 2);
            hacc.AddComponent<Image>().color = UITheme.AccentPrimary;

            var contentGo = CreateChild(cr, "Content"); var contr = contentGo.GetComponent<RectTransform>();
            contr.anchorMin = Vector2.zero; contr.anchorMax = Vector2.one; contr.offsetMin = new Vector2(40, 20); contr.offsetMax = new Vector2(-40, -40);
            var vlg = contentGo.AddComponent<VerticalLayoutGroup>();
            vlg.spacing = 10; vlg.childControlWidth = true; vlg.childControlHeight = false; vlg.childForceExpandWidth = true;

            var diffs = new[] {
                (p: DifficultyPreset.Easy, n: "EASY", d: "Forgiving owner, weaker AI trades, fewer injuries. Great for learning."),
                (p: DifficultyPreset.Normal, n: "NORMAL", d: "Balanced experience. Fair AI, realistic injuries, moderate expectations."),
                (p: DifficultyPreset.Hard, n: "HARD", d: "Aggressive AI, impatient owner, tougher negotiations. For veterans."),
                (p: DifficultyPreset.Legendary, n: "LEGENDARY", d: "Ruthless AI, demanding owner, frequent injuries. Ultimate challenge.")
            };
            List<Image> dhl = new List<Image>(), dacl = new List<Image>(); Text descLabel = null;
            foreach (var d in diffs)
            {
                var dd = d;
                var row = CreateChild(contr, $"D_{d.n}"); row.AddComponent<LayoutElement>().preferredHeight = 44;
                var ri = row.AddComponent<Image>(); ri.color = d.p == _selectedDifficulty ? UITheme.FMNavActive : UITheme.PanelSurface; dhl.Add(ri);
                var la = CreateChild(row.GetComponent<RectTransform>(), "Acc"); var lar = la.GetComponent<RectTransform>();
                lar.anchorMin = Vector2.zero; lar.anchorMax = new Vector2(0, 1); lar.sizeDelta = new Vector2(3, 0);
                var lai = la.AddComponent<Image>(); lai.color = d.p == _selectedDifficulty ? UITheme.AccentPrimary : Color.clear; dacl.Add(lai);
                var txt = MkText(row.GetComponent<RectTransform>(), d.n, 15, FontStyle.Bold, Color.white);
                txt.alignment = TextAnchor.MiddleCenter; Stretch(txt.gameObject);
                row.AddComponent<Button>().onClick.AddListener(() => {
                    _selectedDifficulty = dd.p;
                    for (int j = 0; j < dhl.Count; j++) { dhl[j].color = diffs[j].p == dd.p ? UITheme.FMNavActive : UITheme.PanelSurface; dacl[j].color = diffs[j].p == dd.p ? UITheme.AccentPrimary : Color.clear; }
                    if (descLabel != null) descLabel.text = dd.d;
                });
            }
            var dg = CreateChild(contr, "Desc"); dg.AddComponent<LayoutElement>().preferredHeight = 36;
            string initDesc = ""; foreach (var d in diffs) if (d.p == _selectedDifficulty) initDesc = d.d;
            descLabel = MkText(dg.GetComponent<RectTransform>(), initDesc, 13, FontStyle.Italic, UITheme.TextSecondary);
            descLabel.alignment = TextAnchor.MiddleCenter; descLabel.horizontalOverflow = HorizontalWrapMode.Wrap; Stretch(descLabel.gameObject);
        }

        // ── STEP 5: CONFIRMATION ──

        private void BuildConfirmStep()
        {
            var card = CreateChild(_stepContainer, "Card"); Stretch(card); card.AddComponent<Image>().color = UITheme.CardBackground;
            var cr = card.GetComponent<RectTransform>();
            var hdr = CreateChild(cr, "Hdr"); var hr = hdr.GetComponent<RectTransform>();
            hr.anchorMin = new Vector2(0, 1); hr.anchorMax = Vector2.one; hr.pivot = new Vector2(0.5f, 1); hr.sizeDelta = new Vector2(0, 30);
            hdr.AddComponent<Image>().color = UITheme.FMCardHeaderBg;
            var ht = MkText(hr, "CAREER SUMMARY", 11, FontStyle.Bold, UITheme.AccentPrimary); ht.alignment = TextAnchor.MiddleLeft;
            var htr = ht.GetComponent<RectTransform>(); htr.anchorMin = Vector2.zero; htr.anchorMax = Vector2.one; htr.offsetMin = new Vector2(14, 0);
            var hacc = CreateChild(cr, "Acc"); var hacr = hacc.GetComponent<RectTransform>();
            hacr.anchorMin = new Vector2(0, 1); hacr.anchorMax = Vector2.one; hacr.pivot = new Vector2(0.5f, 1);
            hacr.anchoredPosition = new Vector2(0, -30); hacr.sizeDelta = new Vector2(0, 2);
            hacc.AddComponent<Image>().color = UITheme.AccentPrimary;

            var contentGo = CreateChild(cr, "Content"); var contr = contentGo.GetComponent<RectTransform>();
            contr.anchorMin = Vector2.zero; contr.anchorMax = Vector2.one; contr.offsetMin = new Vector2(30, 20); contr.offsetMax = new Vector2(-30, -40);
            var vlg = contentGo.AddComponent<VerticalLayoutGroup>();
            vlg.spacing = 8; vlg.childControlWidth = true; vlg.childControlHeight = false; vlg.childForceExpandWidth = true;

            var bg = _backgrounds[_backgroundIndex];
            string roleName = _selectedRole switch { UserRole.GMOnly => "General Manager", UserRole.HeadCoachOnly => "Head Coach", _ => "GM & Head Coach" };
            long salary = 3_000_000 + (bg.Rep - 50) * 50_000;
            string teamName = _selectedTeam != null ? $"{_selectedTeam.City} {_selectedTeam.Name}" : _selectedTeamId;
            MkSummaryRow(contr, "COACH", $"{_firstName} {_lastName}, Age {_age}");
            MkSummaryRow(contr, "BACKGROUND", bg.Name); MkSummaryRow(contr, "ROLE", roleName);
            MkSummaryRow(contr, "TEAM", teamName); MkSummaryRow(contr, "DIFFICULTY", _selectedDifficulty.ToString());
            MkSummaryRow(contr, "CONTRACT", $"4 Years, ${salary / 1000000.0:F1}M/year");
            if (_selectedTeam != null)
            {
                var lg = CreateChild(contr, "Logo"); lg.AddComponent<LayoutElement>().preferredHeight = 80;
                var li = lg.AddComponent<Image>(); li.preserveAspect = true;
                var sp = ArtManager.GetTeamLogo(_selectedTeam.TeamId); if (sp != null) li.sprite = sp;
            }
        }

        private void MkSummaryRow(RectTransform parent, string label, string value)
        {
            var row = CreateChild(parent, $"R_{label}"); row.AddComponent<LayoutElement>().preferredHeight = 26;
            var h = row.AddComponent<HorizontalLayoutGroup>();
            h.childControlWidth = true; h.childControlHeight = true; h.childForceExpandWidth = true; h.childForceExpandHeight = true;
            var rr = row.GetComponent<RectTransform>();
            MkText(rr, label, 11, FontStyle.Bold, UITheme.AccentPrimary).gameObject.AddComponent<LayoutElement>().preferredWidth = 110;
            MkText(rr, value, 13, FontStyle.Normal, Color.white).gameObject.AddComponent<LayoutElement>().flexibleWidth = 1;
        }

        private void StartGame()
        {
            if (GameManager.Instance == null) return;
            var bg = _backgrounds[_backgroundIndex];
            var diff = DifficultySettings.CreateFromPreset(_selectedDifficulty);
            int clampedDay = Mathf.Min(_birthDay, DateTime.DaysInMonth(_birthYear, _birthMonth));
            var birthDate = new DateTime(_birthYear, _birthMonth, clampedDay);
            Debug.Log($"[NewGameWizard] Starting: {_firstName} {_lastName}, DOB: {birthDate:MMM dd, yyyy}, Team: {_selectedTeamId}, Role: {_selectedRole}");
            GameManager.Instance.StartNewGame(_firstName, _lastName, _age, _selectedTeamId, diff, bg.Tactical, bg.Dev, bg.Rep, _selectedRole, birthDate);
        }
    }
}

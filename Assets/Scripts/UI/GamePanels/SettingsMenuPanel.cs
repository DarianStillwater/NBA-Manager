using System;
using UnityEngine;
using UnityEngine.UI;
using NBAHeadCoach.Core;
using NBAHeadCoach.Core.Data;
using NBAHeadCoach.UI.Shell;

namespace NBAHeadCoach.UI.GamePanels
{
    public class SettingsMenuPanel : IGamePanel
    {
        private readonly GameShell _shell;
        private GamePreferences _prefs;

        public SettingsMenuPanel(GameShell shell)
        {
            _shell = shell;
        }

        public void Build(RectTransform contentArea, Team team, Color teamColor)
        {
            _prefs = GameManager.Instance?.Preferences ?? new GamePreferences();

            // Card fills content area
            var card = UIBuilder.Card(contentArea, "MENU", teamColor);
            card.anchorMin = Vector2.zero; card.anchorMax = Vector2.one; card.sizeDelta = Vector2.zero;

            // Body area below the header (32px header)
            var body = UIBuilder.Child(card, "Body");
            var br = body.GetComponent<RectTransform>();
            br.anchorMin = Vector2.zero; br.anchorMax = Vector2.one;
            br.offsetMin = new Vector2(0, 0); br.offsetMax = new Vector2(0, -UITheme.FMCardHeaderHeight);

            var vlg = body.AddComponent<VerticalLayoutGroup>();
            vlg.childControlWidth = true; vlg.childControlHeight = true;
            vlg.childForceExpandWidth = true; vlg.childForceExpandHeight = false;
            vlg.spacing = 4; vlg.padding = new RectOffset(16, 16, 8, 8);

            var bodyRT = body.GetComponent<RectTransform>();

            // ── Save / Load ──
            SectionLabel(bodyRT, "SAVE & LOAD");
            var saveLoadRow = HRow(bodyRT);
            ActionBtn(saveLoadRow, "Save Game", UITheme.FMNavHover, () => _shell.ShowPanel("SaveGame"));
            ActionBtn(saveLoadRow, "Load Game", UITheme.AccentSecondary, () => _shell.ShowPanel("LoadGame"));

            Spacer(bodyRT, 6);

            // ── Preferences ──
            SectionLabel(bodyRT, "PREFERENCES");
            CyclePref(bodyRT, "Auto-Save", new[] { "Off", "Every Game", "Every Day" }, (int)_prefs.AutoSave,
                v => { _prefs.AutoSave = (AutoSaveFrequency)v; Save(); });
            CyclePref(bodyRT, "Auto-Save Slots", new[] { "1", "3", "5" }, _prefs.AutoSaveSlots == 1 ? 0 : _prefs.AutoSaveSlots == 5 ? 2 : 1,
                v => { _prefs.AutoSaveSlots = v == 0 ? 1 : v == 2 ? 5 : 3; Save(); });
            CyclePref(bodyRT, "Sim Speed", new[] { "Instant", "Fast", "Normal", "Detailed" }, (int)_prefs.SimSpeed,
                v => { _prefs.SimSpeed = (SimSpeedSetting)v; Save(); });
            TogglePref(bodyRT, "Auto-Sim Rest Days", _prefs.AutoSimRestDays,
                v => { _prefs.AutoSimRestDays = v; Save(); });
            TogglePref(bodyRT, "Stat Decimals", _prefs.StatDecimals,
                v => { _prefs.StatDecimals = v; Save(); });
            CyclePref(bodyRT, "Currency", new[] { "$M", "Full" }, (int)_prefs.Currency,
                v => { _prefs.Currency = (CurrencyFormat)v; Save(); });
            SliderPref(bodyRT, "Volume", _prefs.Volume, 0, 100,
                v => { _prefs.Volume = v; PlayerPrefs.SetInt("Volume", v); Save(); });

            Spacer(bodyRT, 6);

            // ── Game ──
            SectionLabel(bodyRT, "GAME");
            ActionRow(bodyRT, "Help / Controls", UITheme.PanelSurface, ShowHelp);
            ActionRow(bodyRT, "Exit to Main Menu", UITheme.PanelSurface, () => ConfirmAction(contentArea, "Exit to Main Menu?", "Unsaved progress will be auto-saved.", () => GameManager.Instance?.ReturnToMainMenu()));
            ActionRow(bodyRT, "Retire from Career", new Color(0.4f, 0.2f, 0.2f), () => ConfirmAction(contentArea, "Retire from Career?", "This ends your career permanently. No save will be created.", () => GameManager.Instance?.RetireFromCareer()));
            ActionRow(bodyRT, "Exit to Desktop", new Color(0.4f, 0.2f, 0.2f), () => ConfirmAction(contentArea, "Exit to Desktop?", "Unsaved progress will be lost.",
                () => {
#if UNITY_EDITOR
                    UnityEditor.EditorApplication.isPlaying = false;
#else
                    Application.Quit();
#endif
                }));
        }

        private void Save()
        {
            if (GameManager.Instance != null)
                GameManager.Instance.Preferences = _prefs;
        }

        // ── UI Builders ──

        private void SectionLabel(RectTransform parent, string text)
        {
            var row = UIBuilder.Child(parent, "Section");
            row.AddComponent<LayoutElement>().preferredHeight = 22;
            var t = UIBuilder.Text(row.GetComponent<RectTransform>(), "Lbl", text, 10, FontStyle.Bold, UITheme.AccentPrimary);
            UIBuilder.Stretch(t.gameObject);
            t.alignment = TextAnchor.MiddleLeft;
        }

        private RectTransform HRow(RectTransform parent)
        {
            var row = UIBuilder.Child(parent, "HRow");
            row.AddComponent<LayoutElement>().preferredHeight = 34;
            var hlg = row.AddComponent<HorizontalLayoutGroup>();
            hlg.spacing = 8; hlg.childControlWidth = true; hlg.childControlHeight = true; hlg.childForceExpandWidth = true;
            return row.GetComponent<RectTransform>();
        }

        private void ActionBtn(RectTransform parent, string label, Color color, Action onClick)
        {
            var go = UIBuilder.Child(parent, label);
            go.AddComponent<Image>().color = UITheme.DarkenColor(color, 0.5f);
            UIBuilder.ApplyOutline(go, UITheme.DarkenColor(color, 0.3f), 1f);
            var btn = go.AddComponent<Button>();
            btn.onClick.AddListener(() => onClick?.Invoke());
            var t = UIBuilder.Text(go.GetComponent<RectTransform>(), "T", label, 12, FontStyle.Bold, Color.white);
            UIBuilder.Stretch(t.gameObject);
            t.alignment = TextAnchor.MiddleCenter;
        }

        private void ActionRow(RectTransform parent, string label, Color bgColor, Action onClick)
        {
            var row = UIBuilder.Child(parent, label);
            row.AddComponent<LayoutElement>().preferredHeight = 30;
            row.AddComponent<Image>().color = bgColor;
            row.AddComponent<Button>().onClick.AddListener(() => onClick?.Invoke());
            var t = UIBuilder.Text(row.GetComponent<RectTransform>(), "T", label, 11, FontStyle.Normal, Color.white);
            UIBuilder.Stretch(t.gameObject);
            t.alignment = TextAnchor.MiddleLeft;
            var rt = t.GetComponent<RectTransform>();
            rt.offsetMin = new Vector2(12, 0);
        }

        private void CyclePref(RectTransform parent, string label, string[] options, int currentIndex, Action<int> onChange)
        {
            var row = UIBuilder.Child(parent, "Pref_" + label);
            row.AddComponent<LayoutElement>().preferredHeight = 28;
            row.AddComponent<Image>().color = UITheme.PanelSurface;
            var hlg = row.AddComponent<HorizontalLayoutGroup>();
            hlg.childControlWidth = true; hlg.childControlHeight = true;
            hlg.childForceExpandHeight = true; hlg.padding = new RectOffset(12, 8, 0, 0);

            // Label
            var lblGo = UIBuilder.Child(row.GetComponent<RectTransform>(), "Lbl");
            lblGo.AddComponent<LayoutElement>().flexibleWidth = 1;
            var lbl = UIBuilder.Text(lblGo.GetComponent<RectTransform>(), "T", label, 11, FontStyle.Normal, UITheme.TextSecondary);
            UIBuilder.Stretch(lbl.gameObject);
            lbl.alignment = TextAnchor.MiddleLeft;

            // Spinner: < value >
            int idx = Mathf.Clamp(currentIndex, 0, options.Length - 1);

            var leftGo = UIBuilder.Child(row.GetComponent<RectTransform>(), "L");
            leftGo.AddComponent<LayoutElement>().preferredWidth = 32;
            leftGo.AddComponent<Image>().color = UITheme.CardBackground;
            var lt = UIBuilder.Text(leftGo.GetComponent<RectTransform>(), "T", "<", 14, FontStyle.Bold, UITheme.AccentPrimary);
            UIBuilder.Stretch(lt.gameObject); lt.alignment = TextAnchor.MiddleCenter;

            var valGo = UIBuilder.Child(row.GetComponent<RectTransform>(), "V");
            valGo.AddComponent<LayoutElement>().preferredWidth = 90;
            valGo.AddComponent<Image>().color = UITheme.CardBackground;
            var val = UIBuilder.Text(valGo.GetComponent<RectTransform>(), "T", options[idx], 11, FontStyle.Normal, Color.white);
            UIBuilder.Stretch(val.gameObject); val.alignment = TextAnchor.MiddleCenter;

            var rightGo = UIBuilder.Child(row.GetComponent<RectTransform>(), "R");
            rightGo.AddComponent<LayoutElement>().preferredWidth = 32;
            rightGo.AddComponent<Image>().color = UITheme.CardBackground;
            var rt2 = UIBuilder.Text(rightGo.GetComponent<RectTransform>(), "T", ">", 14, FontStyle.Bold, UITheme.AccentPrimary);
            UIBuilder.Stretch(rt2.gameObject); rt2.alignment = TextAnchor.MiddleCenter;

            leftGo.AddComponent<Button>().onClick.AddListener(() => { idx = (idx - 1 + options.Length) % options.Length; val.text = options[idx]; onChange(idx); });
            rightGo.AddComponent<Button>().onClick.AddListener(() => { idx = (idx + 1) % options.Length; val.text = options[idx]; onChange(idx); });
        }

        private void TogglePref(RectTransform parent, string label, bool currentValue, Action<bool> onChange)
        {
            var row = UIBuilder.Child(parent, "Pref_" + label);
            row.AddComponent<LayoutElement>().preferredHeight = 28;
            row.AddComponent<Image>().color = UITheme.PanelSurface;
            var hlg = row.AddComponent<HorizontalLayoutGroup>();
            hlg.childControlWidth = true; hlg.childControlHeight = true;
            hlg.childForceExpandHeight = true; hlg.padding = new RectOffset(12, 8, 0, 0);

            var lblGo = UIBuilder.Child(row.GetComponent<RectTransform>(), "Lbl");
            lblGo.AddComponent<LayoutElement>().flexibleWidth = 1;
            var lbl = UIBuilder.Text(lblGo.GetComponent<RectTransform>(), "T", label, 11, FontStyle.Normal, UITheme.TextSecondary);
            UIBuilder.Stretch(lbl.gameObject); lbl.alignment = TextAnchor.MiddleLeft;

            bool val = currentValue;
            var toggleGo = UIBuilder.Child(row.GetComponent<RectTransform>(), "Tog");
            toggleGo.AddComponent<LayoutElement>().preferredWidth = 48;
            var togBg = toggleGo.AddComponent<Image>();
            togBg.color = val ? UITheme.Success : UITheme.CardBackground;
            var togText = UIBuilder.Text(toggleGo.GetComponent<RectTransform>(), "T", val ? "ON" : "OFF", 10, FontStyle.Bold, Color.white);
            UIBuilder.Stretch(togText.gameObject); togText.alignment = TextAnchor.MiddleCenter;

            toggleGo.AddComponent<Button>().onClick.AddListener(() =>
            {
                val = !val;
                togBg.color = val ? UITheme.Success : UITheme.CardBackground;
                togText.text = val ? "ON" : "OFF";
                onChange(val);
            });
        }

        private void SliderPref(RectTransform parent, string label, int currentValue, int min, int max, Action<int> onChange)
        {
            var row = UIBuilder.Child(parent, "Pref_" + label);
            row.AddComponent<LayoutElement>().preferredHeight = 28;
            row.AddComponent<Image>().color = UITheme.PanelSurface;
            var hlg = row.AddComponent<HorizontalLayoutGroup>();
            hlg.childControlWidth = true; hlg.childControlHeight = true;
            hlg.childForceExpandHeight = true; hlg.padding = new RectOffset(12, 8, 0, 0);

            var lblGo = UIBuilder.Child(row.GetComponent<RectTransform>(), "Lbl");
            lblGo.AddComponent<LayoutElement>().flexibleWidth = 1;
            var lbl = UIBuilder.Text(lblGo.GetComponent<RectTransform>(), "T", label, 11, FontStyle.Normal, UITheme.TextSecondary);
            UIBuilder.Stretch(lbl.gameObject); lbl.alignment = TextAnchor.MiddleLeft;

            int val = Mathf.Clamp(currentValue, min, max);

            var leftGo = UIBuilder.Child(row.GetComponent<RectTransform>(), "L");
            leftGo.AddComponent<LayoutElement>().preferredWidth = 32;
            leftGo.AddComponent<Image>().color = UITheme.CardBackground;
            var lt = UIBuilder.Text(leftGo.GetComponent<RectTransform>(), "T", "-", 14, FontStyle.Bold, UITheme.AccentPrimary);
            UIBuilder.Stretch(lt.gameObject); lt.alignment = TextAnchor.MiddleCenter;

            var valGo = UIBuilder.Child(row.GetComponent<RectTransform>(), "V");
            valGo.AddComponent<LayoutElement>().preferredWidth = 56;
            valGo.AddComponent<Image>().color = UITheme.CardBackground;
            var valText = UIBuilder.Text(valGo.GetComponent<RectTransform>(), "T", val.ToString(), 11, FontStyle.Normal, Color.white);
            UIBuilder.Stretch(valText.gameObject); valText.alignment = TextAnchor.MiddleCenter;

            var rightGo = UIBuilder.Child(row.GetComponent<RectTransform>(), "R");
            rightGo.AddComponent<LayoutElement>().preferredWidth = 32;
            rightGo.AddComponent<Image>().color = UITheme.CardBackground;
            var rt2 = UIBuilder.Text(rightGo.GetComponent<RectTransform>(), "T", "+", 14, FontStyle.Bold, UITheme.AccentPrimary);
            UIBuilder.Stretch(rt2.gameObject); rt2.alignment = TextAnchor.MiddleCenter;

            leftGo.AddComponent<Button>().onClick.AddListener(() => { val = Mathf.Max(min, val - 10); valText.text = val.ToString(); onChange(val); });
            rightGo.AddComponent<Button>().onClick.AddListener(() => { val = Mathf.Min(max, val + 10); valText.text = val.ToString(); onChange(val); });
        }

        private void Spacer(RectTransform parent, float height)
        {
            var sp = UIBuilder.Child(parent, "Spacer");
            sp.AddComponent<LayoutElement>().preferredHeight = height;
        }

        private void ConfirmAction(RectTransform contentArea, string title, string message, Action onConfirm)
        {
            // Overlay confirm dialog
            var overlay = new GameObject("ConfirmOverlay", typeof(RectTransform));
            overlay.transform.SetParent(contentArea, false);
            UIBuilder.Stretch(overlay);
            overlay.AddComponent<Image>().color = new Color(0, 0, 0, 0.7f);

            var dialog = UIBuilder.Child(overlay.GetComponent<RectTransform>(), "Dialog");
            var dr = dialog.GetComponent<RectTransform>();
            dr.anchorMin = new Vector2(0.3f, 0.35f); dr.anchorMax = new Vector2(0.7f, 0.65f); dr.sizeDelta = Vector2.zero;
            dialog.AddComponent<Image>().color = UITheme.CardBackground;
            UIBuilder.ApplyOutline(dialog, UITheme.AccentPrimary, 1f);

            var dvlg = dialog.AddComponent<VerticalLayoutGroup>();
            dvlg.childControlWidth = true; dvlg.childControlHeight = true;
            dvlg.childForceExpandWidth = true; dvlg.childForceExpandHeight = false;
            dvlg.spacing = 8; dvlg.padding = new RectOffset(20, 20, 16, 16);

            // Title
            var titleGo = UIBuilder.Child(dr, "Title");
            titleGo.AddComponent<LayoutElement>().preferredHeight = 24;
            var tt = UIBuilder.Text(titleGo.GetComponent<RectTransform>(), "T", title, 14, FontStyle.Bold, UITheme.AccentPrimary);
            UIBuilder.Stretch(tt.gameObject); tt.alignment = TextAnchor.MiddleCenter;

            // Message
            var msgGo = UIBuilder.Child(dr, "Msg");
            msgGo.AddComponent<LayoutElement>().preferredHeight = 30;
            var mt = UIBuilder.Text(msgGo.GetComponent<RectTransform>(), "T", message, 11, FontStyle.Normal, UITheme.TextSecondary);
            UIBuilder.Stretch(mt.gameObject); mt.alignment = TextAnchor.MiddleCenter;

            // Buttons
            var btnRow = UIBuilder.Child(dr, "Btns");
            btnRow.AddComponent<LayoutElement>().preferredHeight = 32;
            var bhlg = btnRow.AddComponent<HorizontalLayoutGroup>();
            bhlg.spacing = 12; bhlg.childControlWidth = true; bhlg.childControlHeight = true; bhlg.childForceExpandWidth = true;

            // Cancel
            var cancelGo = UIBuilder.Child(btnRow.GetComponent<RectTransform>(), "Cancel");
            cancelGo.AddComponent<Image>().color = UITheme.PanelSurface;
            cancelGo.AddComponent<Button>().onClick.AddListener(() => UnityEngine.Object.Destroy(overlay));
            var ct = UIBuilder.Text(cancelGo.GetComponent<RectTransform>(), "T", "Cancel", 12, FontStyle.Bold, UITheme.TextSecondary);
            UIBuilder.Stretch(ct.gameObject); ct.alignment = TextAnchor.MiddleCenter;

            // Confirm
            var confirmGo = UIBuilder.Child(btnRow.GetComponent<RectTransform>(), "Confirm");
            confirmGo.AddComponent<Image>().color = new Color(0.6f, 0.15f, 0.15f);
            confirmGo.AddComponent<Button>().onClick.AddListener(() => { UnityEngine.Object.Destroy(overlay); onConfirm?.Invoke(); });
            var cft = UIBuilder.Text(confirmGo.GetComponent<RectTransform>(), "T", "Confirm", 12, FontStyle.Bold, Color.white);
            UIBuilder.Stretch(cft.gameObject); cft.alignment = TextAnchor.MiddleCenter;
        }

        private void ShowHelp()
        {
            // Simple help text - shown inline by rebuilding the panel content wouldn't work well
            // So we just log for now and can expand later
            Debug.Log("[Settings] Help requested — future: show help overlay");
        }
    }
}

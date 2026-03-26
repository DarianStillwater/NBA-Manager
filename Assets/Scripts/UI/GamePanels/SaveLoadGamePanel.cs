using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using NBAHeadCoach.Core;
using NBAHeadCoach.Core.Data;
using NBAHeadCoach.Core.Util;
using NBAHeadCoach.UI.Shell;

namespace NBAHeadCoach.UI.GamePanels
{
    public class SaveLoadGamePanel : IGamePanel
    {
        private bool _isLoadMode;
        private InputField _nameInput;
        private string _selectedSlot;
        private List<Image> _rowHighlights = new List<Image>();

        public SaveLoadGamePanel(bool loadMode = false) { _isLoadMode = loadMode; }

        public void Build(RectTransform parent, Team team, Color teamColor)
        {
            _selectedSlot = null;
            _rowHighlights.Clear();

            var scrollContent = UIBuilder.FixedArea(parent);

            string title = _isLoadMode ? "LOAD GAME" : "SAVE GAME";
            var titleText = UIBuilder.Text(scrollContent, "Title", title, 18, FontStyle.Bold, UITheme.AccentPrimary);
            titleText.gameObject.AddComponent<LayoutElement>().preferredHeight = 28;

            // Action bar at top
            var actionBar = UIBuilder.Child(scrollContent, "ActionBar");
            actionBar.AddComponent<LayoutElement>().preferredHeight = 40;
            var abHlg = actionBar.AddComponent<HorizontalLayoutGroup>();
            abHlg.spacing = 8; abHlg.padding = new RectOffset(0, 0, 4, 4);
            abHlg.childControlWidth = true; abHlg.childControlHeight = true;
            abHlg.childForceExpandWidth = false; abHlg.childForceExpandHeight = true;

            if (!_isLoadMode)
            {
                // Save name input
                var inputGo = UIBuilder.Child(actionBar.GetComponent<RectTransform>(), "NameInput");
                var inputLE = inputGo.AddComponent<LayoutElement>();
                inputLE.flexibleWidth = 1; inputLE.minWidth = 200;
                inputGo.AddComponent<Image>().color = UITheme.PanelSurface;

                var inputTextGo = UIBuilder.Child(inputGo.GetComponent<RectTransform>(), "Text");
                UIBuilder.Stretch(inputTextGo);
                inputTextGo.GetComponent<RectTransform>().offsetMin = new Vector2(8, 2);
                inputTextGo.GetComponent<RectTransform>().offsetMax = new Vector2(-8, -2);
                var inputText = inputTextGo.AddComponent<Text>();
                inputText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                inputText.fontSize = 13; inputText.color = Color.white;
                inputText.supportRichText = false;
                inputText.alignment = TextAnchor.MiddleLeft;

                var placeholderGo = UIBuilder.Child(inputGo.GetComponent<RectTransform>(), "Placeholder");
                UIBuilder.Stretch(placeholderGo);
                placeholderGo.GetComponent<RectTransform>().offsetMin = new Vector2(8, 2);
                placeholderGo.GetComponent<RectTransform>().offsetMax = new Vector2(-8, -2);
                var placeholder = placeholderGo.AddComponent<Text>();
                placeholder.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                placeholder.fontSize = 13; placeholder.fontStyle = FontStyle.Italic;
                placeholder.color = new Color(1, 1, 1, 0.3f);
                placeholder.text = "Enter save name...";
                placeholder.alignment = TextAnchor.MiddleLeft;

                _nameInput = inputGo.AddComponent<InputField>();
                _nameInput.textComponent = inputText;
                _nameInput.placeholder = placeholder;
                _nameInput.text = $"{team.Abbreviation} {GameManager.Instance?.CurrentDate.ToString("MMM dd")}";

                ActionBtn(actionBar.GetComponent<RectTransform>(), "SAVE NEW", UITheme.Success, 120, () =>
                {
                    string saveName = _nameInput?.text ?? "Quick Save";
                    var gm = GameManager.Instance;
                    if (gm?.SaveLoad != null)
                    {
                        var saveData = gm.CreateSaveData();
                        saveData.SaveName = saveName;
                        gm.SaveLoad.SaveGame(saveData, gm.SaveLoad.GetNextSaveSlotName());
                        gm.GetComponent<GameShell>()?.ShowPanel("SaveGame");
                    }
                });

                ActionBtn(actionBar.GetComponent<RectTransform>(), "OVERWRITE", new Color32(200, 150, 30, 255), 120, () =>
                {
                    if (string.IsNullOrEmpty(_selectedSlot)) return;
                    string saveName = _nameInput?.text ?? "Quick Save";
                    var gm = GameManager.Instance;
                    if (gm?.SaveLoad != null)
                    {
                        var saveData = gm.CreateSaveData();
                        saveData.SaveName = saveName;
                        gm.SaveLoad.SaveGame(saveData, _selectedSlot);
                        gm.GetComponent<GameShell>()?.ShowPanel("SaveGame");
                    }
                });
            }
            else
            {
                // Flex spacer
                var spacer = UIBuilder.Child(actionBar.GetComponent<RectTransform>(), "Spacer");
                spacer.AddComponent<LayoutElement>().flexibleWidth = 1;

                ActionBtn(actionBar.GetComponent<RectTransform>(), "LOAD", UITheme.Success, 120, () =>
                {
                    if (string.IsNullOrEmpty(_selectedSlot)) return;
                    GameManager.Instance?.SaveLoad?.LoadGame(_selectedSlot);
                });
            }

            ActionBtn(actionBar.GetComponent<RectTransform>(), "DELETE", UITheme.Danger, 100, () =>
            {
                if (string.IsNullOrEmpty(_selectedSlot)) return;
                var gm = GameManager.Instance;
                gm?.SaveLoad?.DeleteSave(_selectedSlot);
                _selectedSlot = null;
                gm?.GetComponent<GameShell>()?.ShowPanel(_isLoadMode ? "LoadGame" : "SaveGame");
            });

            ActionBtn(actionBar.GetComponent<RectTransform>(), "\u25C0 BACK", UITheme.FMNavHover, 80, () =>
            {
                GameManager.Instance?.GetComponent<GameShell>()?.ShowPanel("Dashboard");
            });

            // Header row
            var headerRow = UIBuilder.TableRow(scrollContent, 28, UITheme.FMCardHeaderBg);
            UIBuilder.TableCell(headerRow, "Save Name", 200, FontStyle.Bold, UITheme.AccentPrimary);
            UIBuilder.TableCell(headerRow, "Team", 50, FontStyle.Bold, UITheme.AccentPrimary);
            UIBuilder.TableCell(headerRow, "W-L", 45, FontStyle.Bold, UITheme.AccentPrimary);
            UIBuilder.TableCell(headerRow, "Date", 90, FontStyle.Bold, UITheme.AccentPrimary);
            UIBuilder.TableCell(headerRow, "Saved", 100, FontStyle.Bold, UITheme.AccentPrimary);

            // Save rows
            var saveLoad = GameManager.Instance?.SaveLoad;
            var saves = saveLoad?.GetAllSaves() ?? new List<SaveSlotInfo>();

            for (int i = 0; i < saves.Count; i++)
            {
                var save = saves[i];
                var bgColor = i % 2 == 0 ? UITheme.CardBackground : UITheme.FMCardHeaderBg;
                var row = UIBuilder.TableRow(scrollContent, 32, bgColor);
                var rowImg = row.gameObject.GetComponent<Image>();
                _rowHighlights.Add(rowImg);

                string displayName = !string.IsNullOrEmpty(save.SaveName) ? save.SaveName : save.SlotName;
                UIBuilder.TableCell(row, displayName, 200, FontStyle.Normal, Color.white);
                var teamObj = GameManager.Instance?.GetTeam(save.TeamId);
                UIBuilder.TableCell(row, teamObj?.Abbreviation ?? save.TeamId ?? "—", 50, FontStyle.Normal, UITheme.TextSecondary);
                UIBuilder.TableCell(row, save.Record ?? "0-0", 45, FontStyle.Normal, UITheme.TextSecondary);
                UIBuilder.TableCell(row, save.Date.Year > 1 ? save.Date.ToString("MMM dd, yyyy") : "—", 90, FontStyle.Normal, UITheme.TextSecondary);
                UIBuilder.TableCell(row, save.SaveTimestamp.Year > 1 ? save.SaveTimestamp.ToString("MMM dd HH:mm") : "—", 100, FontStyle.Normal, UITheme.TextSecondary);

                int idx = i;
                string slot = save.SlotName;
                string sName = displayName;
                row.gameObject.AddComponent<Button>().onClick.AddListener(() => SelectSlot(slot, sName, idx));
            }

            if (saves.Count == 0)
            {
                var emptyRow = UIBuilder.TableRow(scrollContent, 40, UITheme.CardBackground);
                UIBuilder.TableCell(emptyRow, "No saves found", 400, FontStyle.Italic, UITheme.TextSecondary);
            }
        }

        private void SelectSlot(string slotName, string displayName, int idx)
        {
            _selectedSlot = slotName;
            if (_nameInput != null) _nameInput.text = displayName;
            for (int i = 0; i < _rowHighlights.Count; i++)
                if (_rowHighlights[i] != null)
                    _rowHighlights[i].color = i == idx ? UITheme.FMNavActive
                        : (i % 2 == 0 ? UITheme.CardBackground : UITheme.FMCardHeaderBg);
        }

        private void ActionBtn(RectTransform parent, string label, Color color, float width, UnityEngine.Events.UnityAction action)
        {
            var go = UIBuilder.Child(parent, $"Btn_{label}");
            go.AddComponent<LayoutElement>().preferredWidth = width;
            go.AddComponent<Image>().color = color;
            go.AddComponent<Button>().onClick.AddListener(action);
            var txt = UIBuilder.Text(go.GetComponent<RectTransform>(), "Label", label, 11, FontStyle.Bold, Color.white);
            txt.alignment = TextAnchor.MiddleCenter;
            UIBuilder.Stretch(txt.gameObject);
        }
    }
}

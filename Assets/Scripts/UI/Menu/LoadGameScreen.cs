using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using NBAHeadCoach.Core;
using NBAHeadCoach.Core.Util;
using static NBAHeadCoach.UI.Menu.MenuUI;

namespace NBAHeadCoach.UI.Menu
{
    /// <summary>
    /// Load game screen shown from the main menu.
    /// </summary>
    public class LoadGameScreen
    {
        private readonly RectTransform _menuRoot;
        private readonly Action _onBack;

        private GameObject _root;
        private string _selectedSaveSlot;
        private Button _loadBtn, _deleteBtn;
        private List<Image> _saveSlotHighlights = new List<Image>();

        public LoadGameScreen(RectTransform menuRoot, Action onBack)
        { _menuRoot = menuRoot; _onBack = onBack; }

        public void Show()
        {
            if (_root != null) UnityEngine.Object.Destroy(_root);
            _root = CreateChild(_menuRoot, "LoadGame"); Stretch(_root);
            _root.AddComponent<Image>().color = UITheme.Background;
            var root = _root.GetComponent<RectTransform>();
            _selectedSaveSlot = null; _saveSlotHighlights.Clear();

            var tb = CreateChild(root, "Title"); var tbr = tb.GetComponent<RectTransform>();
            tbr.anchorMin = new Vector2(0, 0.9f); tbr.anchorMax = Vector2.one; tbr.sizeDelta = Vector2.zero;
            var tt = MkText(tbr, "LOAD GAME", 24, FontStyle.Bold, UITheme.AccentPrimary);
            tt.alignment = TextAnchor.MiddleCenter; Stretch(tt.gameObject);

            var nav = CreateChild(root, "Nav"); var nr = nav.GetComponent<RectTransform>();
            nr.anchorMin = Vector2.zero; nr.anchorMax = new Vector2(1, 0.08f); nr.sizeDelta = Vector2.zero;
            var nh = nav.AddComponent<HorizontalLayoutGroup>();
            nh.spacing = 20; nh.childAlignment = TextAnchor.MiddleCenter; nh.childControlWidth = false; nh.childControlHeight = true;
            nh.padding = new RectOffset(40, 40, 5, 5);

            var bk = CreateChild(nr, "Back"); bk.AddComponent<LayoutElement>().preferredWidth = 120;
            bk.AddComponent<Image>().color = UITheme.PanelSurface;
            bk.AddComponent<Button>().onClick.AddListener(() => { Destroy(); _onBack(); });
            var bkt = MkText(bk.GetComponent<RectTransform>(), "BACK", 14, FontStyle.Bold, UITheme.TextSecondary);
            bkt.alignment = TextAnchor.MiddleCenter; Stretch(bkt.gameObject);

            CreateChild(nr, "F").AddComponent<LayoutElement>().flexibleWidth = 1;

            var dg = CreateChild(nr, "Del"); dg.AddComponent<LayoutElement>().preferredWidth = 120;
            dg.AddComponent<Image>().color = new Color32(60, 20, 20, 255);
            _deleteBtn = dg.AddComponent<Button>(); _deleteBtn.interactable = false; _deleteBtn.onClick.AddListener(OnDelete);
            var dt = MkText(dg.GetComponent<RectTransform>(), "DELETE", 14, FontStyle.Bold, UITheme.Danger);
            dt.alignment = TextAnchor.MiddleCenter; Stretch(dt.gameObject);

            var lg = CreateChild(nr, "Load"); lg.AddComponent<LayoutElement>().preferredWidth = 140;
            lg.AddComponent<Image>().color = new Color32(20, 40, 60, 255);
            _loadBtn = lg.AddComponent<Button>(); _loadBtn.interactable = false; _loadBtn.onClick.AddListener(OnLoad);
            var lt = MkText(lg.GetComponent<RectTransform>(), "LOAD", 14, FontStyle.Bold, UITheme.AccentSecondary);
            lt.alignment = TextAnchor.MiddleCenter; Stretch(lt.gameObject);

            var la = CreateChild(root, "List"); var lar = la.GetComponent<RectTransform>();
            lar.anchorMin = new Vector2(0.1f, 0.1f); lar.anchorMax = new Vector2(0.9f, 0.88f); lar.sizeDelta = Vector2.zero;

            var saves = GameManager.Instance?.SaveLoad?.GetAllSaves();
            if (saves == null || saves.Count == 0)
            {
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

                var rhlg = rg.AddComponent<HorizontalLayoutGroup>();
                rhlg.spacing = 10; rhlg.padding = new RectOffset(10, 10, 8, 8);
                rhlg.childControlWidth = false; rhlg.childControlHeight = false; rhlg.childForceExpandWidth = false; rhlg.childAlignment = TextAnchor.MiddleLeft;

                if (!string.IsNullOrEmpty(save.TeamId))
                {
                    var slg = CreateChild(rg.GetComponent<RectTransform>(), "Logo");
                    var sle = slg.AddComponent<LayoutElement>(); sle.preferredWidth = 44; sle.preferredHeight = 44;
                    var sli = slg.AddComponent<Image>(); sli.preserveAspect = true;
                    var sp = ArtManager.GetTeamLogo(save.TeamId); if (sp != null) sli.sprite = sp;
                }

                string badge = save.IsIronman ? " [IRONMAN]" : "";
                var tg = CreateChild(rg.GetComponent<RectTransform>(), "Txt"); tg.AddComponent<LayoutElement>().flexibleWidth = 1;
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

        public void Destroy() { if (_root != null) UnityEngine.Object.Destroy(_root); }

        private void OnLoad() { if (!string.IsNullOrEmpty(_selectedSaveSlot)) GameManager.Instance?.LoadGame(_selectedSaveSlot); }
        private void OnDelete() { if (!string.IsNullOrEmpty(_selectedSaveSlot)) { GameManager.Instance?.SaveLoad?.DeleteSave(_selectedSaveSlot); _selectedSaveSlot = null; Show(); } }
    }
}

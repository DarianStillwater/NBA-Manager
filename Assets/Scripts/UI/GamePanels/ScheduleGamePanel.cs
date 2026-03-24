using System;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using NBAHeadCoach.Core;
using NBAHeadCoach.Core.Data;
using NBAHeadCoach.Core.Util;
using NBAHeadCoach.UI.Shell;
using B = NBAHeadCoach.UI.Shell.UIBuilder;

namespace NBAHeadCoach.UI.GamePanels
{
    public class ScheduleGamePanel : IGamePanel
    {
        private Text _detailText;
        private Image _detailLogo;

        public void Build(RectTransform parent, Team team, Color teamColor)
        {
            // Title bar
            var titleBar = B.Child(parent, "TitleBar");
            var tbr = titleBar.GetComponent<RectTransform>();
            tbr.anchorMin = new Vector2(0, 1); tbr.anchorMax = Vector2.one;
            tbr.pivot = new Vector2(0.5f, 1); tbr.sizeDelta = new Vector2(0, 36);
            titleBar.AddComponent<Image>().color = UITheme.FMCardHeaderBg;
            var titleText = B.Text(tbr, "Title", "SCHEDULE", 16, FontStyle.Bold, UITheme.AccentPrimary);
            B.Stretch(titleText.gameObject);
            titleText.GetComponent<RectTransform>().offsetMin = new Vector2(16, 0);
            titleText.alignment = TextAnchor.MiddleLeft;

            // Split
            var split = B.Child(parent, "Split");
            var sr = split.GetComponent<RectTransform>();
            sr.anchorMin = Vector2.zero; sr.anchorMax = Vector2.one;
            sr.offsetMin = Vector2.zero; sr.offsetMax = new Vector2(0, -36);

            // Left: game list
            var left = B.Child(sr, "GameList");
            var lr = left.GetComponent<RectTransform>();
            lr.anchorMin = Vector2.zero; lr.anchorMax = new Vector2(0.55f, 1); lr.sizeDelta = Vector2.zero;

            var leftScroll = left.AddComponent<ScrollRect>();
            var leftVP = B.Child(lr, "Viewport"); B.Stretch(leftVP); leftVP.AddComponent<RectMask2D>();
            var leftContent = B.Child(leftVP.GetComponent<RectTransform>(), "Content");
            var lcr = B.Stretch(leftContent);
            leftContent.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            var vlg = leftContent.AddComponent<VerticalLayoutGroup>();
            vlg.spacing = 1; vlg.padding = new RectOffset(4, 4, 4, 4);
            vlg.childControlWidth = true; vlg.childControlHeight = false; vlg.childForceExpandWidth = true;
            leftScroll.content = lcr; leftScroll.viewport = leftVP.GetComponent<RectTransform>();
            leftScroll.horizontal = false; leftScroll.scrollSensitivity = 30;

            // Right: detail
            var right = B.Child(sr, "GameDetail");
            var rr = right.GetComponent<RectTransform>();
            rr.anchorMin = new Vector2(0.56f, 0); rr.anchorMax = Vector2.one; rr.sizeDelta = Vector2.zero;
            right.AddComponent<Image>().color = UITheme.CardBackground;

            var logoGo = B.Child(rr, "DetailLogo");
            var dlr = logoGo.GetComponent<RectTransform>();
            dlr.anchorMin = new Vector2(0.5f, 0.55f); dlr.anchorMax = new Vector2(0.5f, 0.55f);
            dlr.pivot = new Vector2(0.5f, 0.5f); dlr.sizeDelta = new Vector2(80, 80);
            _detailLogo = logoGo.AddComponent<Image>();
            _detailLogo.preserveAspect = true; _detailLogo.color = new Color(1, 1, 1, 0.3f);

            _detailText = B.Text(rr, "DetailText", "Select a game", 14, FontStyle.Normal, UITheme.TextSecondary);
            var dtr = _detailText.GetComponent<RectTransform>();
            dtr.anchorMin = new Vector2(0, 0.05f); dtr.anchorMax = new Vector2(1, 0.45f);
            dtr.offsetMin = new Vector2(16, 0); dtr.offsetMax = new Vector2(-16, 0);
            _detailText.alignment = TextAnchor.UpperCenter;
            _detailText.horizontalOverflow = HorizontalWrapMode.Wrap;
            _detailText.verticalOverflow = VerticalWrapMode.Truncate;

            // Header row
            var headerRow = B.TableRow(lcr, 24, UITheme.FMCardHeaderBg);
            B.TableCell(headerRow, "Date", 65, FontStyle.Bold, UITheme.AccentPrimary);
            B.TableCell(headerRow, "", 20);
            B.TableCell(headerRow, "Opponent", 130, FontStyle.Bold, UITheme.AccentPrimary);
            B.TableCell(headerRow, "H/A", 45, FontStyle.Bold, UITheme.AccentPrimary);

            var sc = GameManager.Instance?.SeasonController;
            if (sc?.Schedule == null) return;

            var games = sc.Schedule.OrderBy(g => g.Date).ToList();
            for (int i = 0; i < games.Count; i++)
            {
                var game = games[i];
                bool isHome = game.HomeTeamId == team.TeamId;
                string oppId = isHome ? game.AwayTeamId : game.HomeTeamId;
                var opp = GameManager.Instance?.GetTeam(oppId);

                var bgColor = i % 2 == 0 ? UITheme.CardBackground : UITheme.FMCardHeaderBg;
                bool isToday = game.Date.Date == (GameManager.Instance?.CurrentDate.Date ?? DateTime.MinValue);
                if (isToday) bgColor = UITheme.TeamTintedCard(teamColor, 0.2f);

                var row = B.TableRow(lcr, 26, bgColor);
                var btn = row.gameObject.AddComponent<Button>();
                var bc = btn.colors; bc.highlightedColor = UITheme.FMNavHover; btn.colors = bc;

                string cOppId = oppId; var cGame = game; var cOpp = opp;
                btn.onClick.AddListener(() => ShowDetail(cGame, cOppId, cOpp, team, isHome));

                B.TableCell(row, game.Date.ToString("MMM dd"), 65, FontStyle.Normal,
                    isToday ? Color.white : UITheme.TextSecondary);

                var lgo = new GameObject("Logo", typeof(RectTransform));
                lgo.transform.SetParent(row.transform, false);
                var limg = lgo.AddComponent<Image>(); limg.preserveAspect = true; limg.raycastTarget = false;
                var sp = ArtManager.GetTeamLogo(oppId);
                if (sp != null) limg.sprite = sp;
                lgo.AddComponent<LayoutElement>().preferredWidth = 20;

                B.TableCell(row, opp?.Abbreviation ?? oppId, 130, FontStyle.Normal, Color.white);
                B.TableCell(row, isHome ? "HOME" : "AWAY", 45, FontStyle.Normal,
                    isHome ? UITheme.Success : UITheme.TextSecondary);
            }
        }

        private void ShowDetail(CalendarEvent game, string oppId, Team opp, Team team, bool isHome)
        {
            if (_detailLogo != null)
            {
                var logo = ArtManager.GetTeamLogo(oppId);
                if (logo != null) { _detailLogo.sprite = logo; _detailLogo.color = Color.white; }
            }
            if (_detailText != null)
            {
                string oppName = opp != null ? $"{opp.City} {opp.Name}" : oppId;
                string oppRecord = opp != null ? $"{opp.Wins}-{opp.Losses}" : "";
                string venue = isHome ? $"Home  •  {team.ArenaName ?? "Arena"}" : $"Away  •  {opp?.ArenaName ?? "Arena"}";
                string status = game.IsCompleted ? "FINAL" : "UPCOMING";
                _detailText.text = $"{(isHome ? "vs" : "@")} {oppName}\nRecord: {oppRecord}\n\n" +
                    $"{game.Date:dddd, MMMM dd yyyy}\n{venue}\n\nStatus: {status}";
            }
        }
    }
}

using System;
using System.Linq;
using System.Collections.Generic;
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
        private int _activeMonth = -1; // auto-detect current month
        private Text _detailText;
        private Image _detailLogo;

        public void Build(RectTransform parent, Team team, Color teamColor)
        {
            var sc = GameManager.Instance?.SeasonController;
            if (sc?.Schedule == null) return;

            string pid = team.TeamId;
            var allGames = sc.Schedule
                .Where(g => g.HomeTeamId == pid || g.AwayTeamId == pid)
                .OrderBy(g => g.Date).ToList();

            // Determine available months and auto-select current
            var months = allGames.Select(g => g.Date.Month).Distinct().OrderBy(m => m).ToList();
            if (_activeMonth < 0 || !months.Contains(_activeMonth))
            {
                var curDate = GameManager.Instance?.CurrentDate ?? DateTime.Now;
                _activeMonth = months.Contains(curDate.Month) ? curDate.Month : (months.Count > 0 ? months[0] : 10);
            }

            // Month tab bar
            var tabBar = B.Child(parent, "MonthTabs");
            var tbr = tabBar.GetComponent<RectTransform>();
            tbr.anchorMin = new Vector2(0, 1); tbr.anchorMax = Vector2.one;
            tbr.pivot = new Vector2(0.5f, 1); tbr.sizeDelta = new Vector2(0, 28);
            var tabHlg = tabBar.AddComponent<HorizontalLayoutGroup>();
            tabHlg.spacing = 0; tabHlg.childControlWidth = true; tabHlg.childControlHeight = true;
            tabHlg.childForceExpandWidth = true;

            string[] monthNames = { "", "JAN", "FEB", "MAR", "APR", "MAY", "JUN", "JUL", "AUG", "SEP", "OCT", "NOV", "DEC" };
            foreach (var m in months)
            {
                var mm = m;
                var tGo = B.Child(tbr, $"Month_{m}");
                tGo.AddComponent<Image>().color = m == _activeMonth ? UITheme.FMNavActive : Color.clear;
                var tText = B.Text(tGo.GetComponent<RectTransform>(), "T", monthNames[m],
                    10, FontStyle.Bold, m == _activeMonth ? UITheme.AccentPrimary : UITheme.TextSecondary);
                tText.alignment = TextAnchor.MiddleCenter; B.Stretch(tText.gameObject);
                tGo.AddComponent<Button>().onClick.AddListener(() =>
                {
                    _activeMonth = mm;
                    var shell = GameManager.Instance?.GetComponent<GameShell>();
                    shell?.RegisterPanel("Schedule", this);
                    shell?.ShowPanel("Schedule");
                });
            }

            // Split: left game list, right detail
            var split = B.Child(parent, "Split");
            var sr = split.GetComponent<RectTransform>();
            sr.anchorMin = Vector2.zero; sr.anchorMax = Vector2.one;
            sr.offsetMin = Vector2.zero; sr.offsetMax = new Vector2(0, -28);

            // Left: game list (no scroll)
            var left = B.Child(sr, "GameList");
            var lr = left.GetComponent<RectTransform>();
            lr.anchorMin = Vector2.zero; lr.anchorMax = new Vector2(0.55f, 1); lr.sizeDelta = Vector2.zero;

            var leftArea = B.FixedArea(lr, spacing: 0, padding: 2);

            // Header row
            var headerRow = B.TableRow(leftArea, 20, UITheme.FMCardHeaderBg);
            B.TableCell(headerRow, "Date", 60, FontStyle.Bold, UITheme.AccentPrimary);
            B.TableCell(headerRow, "", 18);
            B.TableCell(headerRow, "Opponent", 120, FontStyle.Bold, UITheme.AccentPrimary);
            B.TableCell(headerRow, "H/A", 40, FontStyle.Bold, UITheme.AccentPrimary);
            B.TableCell(headerRow, "Result", 55, FontStyle.Bold, UITheme.AccentPrimary);

            // Filter games for active month
            var monthGames = allGames.Where(g => g.Date.Month == _activeMonth).ToList();

            for (int i = 0; i < monthGames.Count; i++)
            {
                var game = monthGames[i];
                bool isHome = game.HomeTeamId == team.TeamId;
                string oppId = isHome ? game.AwayTeamId : game.HomeTeamId;
                var opp = GameManager.Instance?.GetTeam(oppId);

                var bgColor = i % 2 == 0 ? UITheme.CardBackground : UITheme.FMCardHeaderBg;
                bool isToday = game.Date.Date == (GameManager.Instance?.CurrentDate.Date ?? DateTime.MinValue);
                if (isToday) bgColor = UITheme.TeamTintedCard(teamColor, 0.2f);
                if (game.IsCompleted) bgColor = UITheme.CardHeaderFrosted;

                var row = B.TableRow(leftArea, 20, bgColor);
                var btn = row.gameObject.AddComponent<Button>();
                var bc = btn.colors; bc.highlightedColor = UITheme.FMNavHover; btn.colors = bc;

                string cOppId = oppId; var cGame = game; var cOpp = opp;
                btn.onClick.AddListener(() => ShowDetail(cGame, cOppId, cOpp, team, isHome));

                B.TableCell(row, game.Date.ToString("MMM dd"), 60, FontStyle.Normal,
                    isToday ? Color.white : UITheme.TextSecondary);

                var lgo = new GameObject("Logo", typeof(RectTransform));
                lgo.transform.SetParent(row.transform, false);
                var limg = lgo.AddComponent<Image>(); limg.preserveAspect = true; limg.raycastTarget = false;
                var sp = ArtManager.GetTeamLogo(oppId);
                if (sp != null) limg.sprite = sp;
                lgo.AddComponent<LayoutElement>().preferredWidth = 18;

                B.TableCell(row, opp?.Abbreviation ?? oppId, 120, FontStyle.Normal, Color.white);
                B.TableCell(row, isHome ? "HOME" : "AWAY", 40, FontStyle.Normal,
                    isHome ? UITheme.Success : UITheme.TextSecondary);

                if (game.IsCompleted)
                {
                    bool won = (isHome && game.HomeScore > game.AwayScore) || (!isHome && game.AwayScore > game.HomeScore);
                    string resultStr = isHome ? $"{game.HomeScore}-{game.AwayScore}" : $"{game.AwayScore}-{game.HomeScore}";
                    B.TableCell(row, won ? $"W {resultStr}" : $"L {resultStr}", 55, FontStyle.Bold,
                        won ? UITheme.Success : UITheme.Danger);
                }
                else
                {
                    B.TableCell(row, "—", 55, FontStyle.Normal, UITheme.TextSecondary);
                }
            }

            // Right: detail panel
            var right = B.Child(sr, "GameDetail");
            var rr = right.GetComponent<RectTransform>();
            rr.anchorMin = new Vector2(0.56f, 0); rr.anchorMax = Vector2.one; rr.sizeDelta = Vector2.zero;
            right.AddComponent<Image>().color = UITheme.CardFrosted;

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
                string scoreStr = game.IsCompleted ? $"\n\nScore: {game.HomeScore}-{game.AwayScore}" : "";
                _detailText.text = $"{(isHome ? "vs" : "@")} {oppName}\nRecord: {oppRecord}\n\n" +
                    $"{game.Date:dddd, MMMM dd yyyy}\n{venue}\n\nStatus: {status}{scoreStr}";
            }
        }
    }
}

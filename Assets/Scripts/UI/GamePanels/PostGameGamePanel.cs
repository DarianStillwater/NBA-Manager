using System;
using UnityEngine;
using UnityEngine.UI;
using NBAHeadCoach.Core;
using NBAHeadCoach.Core.Data;
using NBAHeadCoach.UI.Shell;
using Sim = NBAHeadCoach.Core.Simulation;

namespace NBAHeadCoach.UI.GamePanels
{
    public class PostGameGamePanel : IGamePanel
    {
        private readonly GameShell _shell;
        private Sim.GameResult _result;
        private bool _wasHome;
        private string _activeTab = "player";

        public PostGameGamePanel(GameShell shell) { _shell = shell; }

        public void SetResult(Sim.GameResult result, bool wasHome)
        {
            _result = result;
            _wasHome = wasHome;
            _activeTab = "player";
        }

        public void Build(RectTransform parent, Team team, Color teamColor)
        {
            if (_result == null) return;
            var bs = _result.BoxScore;

            bool playerWon = (_wasHome && _result.HomeScore > _result.AwayScore) || (!_wasHome && _result.AwayScore > _result.HomeScore);
            string resultText = playerWon ? "VICTORY!" : "DEFEAT";
            Color resultColor = playerWon ? UITheme.Success : UITheme.Danger;

            // Header area
            var headerArea = UIBuilder.Child(parent, "HeaderArea");
            var har = headerArea.GetComponent<RectTransform>();
            har.anchorMin = new Vector2(0, 1); har.anchorMax = Vector2.one;
            har.pivot = new Vector2(0.5f, 1); har.sizeDelta = new Vector2(0, 100);

            var resultLabel = UIBuilder.Text(har, "Result", resultText, 24, FontStyle.Bold, resultColor);
            var rlr = resultLabel.GetComponent<RectTransform>();
            rlr.anchorMin = new Vector2(0, 0.55f); rlr.anchorMax = Vector2.one;
            rlr.sizeDelta = Vector2.zero; resultLabel.alignment = TextAnchor.MiddleCenter;

            string homeName = bs?.HomeTeam != null ? $"{bs.HomeTeam.City} {bs.HomeTeam.Name}" : _result.HomeTeamId;
            string awayName = bs?.AwayTeam != null ? $"{bs.AwayTeam.City} {bs.AwayTeam.Name}" : _result.AwayTeamId;
            string otText = _result.WentToOvertime ? $"  ({_result.Quarters - 4}OT)" : "";
            var scoreLabel = UIBuilder.Text(har, "Score",
                $"{awayName}  {_result.AwayScore}  —  {_result.HomeScore}  {homeName}{otText}",
                18, FontStyle.Bold, Color.white);
            var slr = scoreLabel.GetComponent<RectTransform>();
            slr.anchorMin = new Vector2(0, 0.2f); slr.anchorMax = new Vector2(1, 0.55f);
            slr.sizeDelta = Vector2.zero; scoreLabel.alignment = TextAnchor.MiddleCenter;

            // Tab bar
            string playerTeamId = GameManager.Instance?.PlayerTeamId;
            var playerTeam = GameManager.Instance?.GetTeam(playerTeamId);
            string oppId = _wasHome ? bs?.AwayTeamId : bs?.HomeTeamId;
            var oppTeam = oppId != null ? GameManager.Instance?.GetTeam(oppId) : null;

            string playerTabLabel = playerTeam != null ? $"{playerTeam.City} {playerTeam.Name}" : "Your Team";
            string oppTabLabel = oppTeam != null ? $"{oppTeam.City} {oppTeam.Name}" : "Opponent";

            var tabBar = UIBuilder.Child(har, "TabBar");
            var tbr = tabBar.GetComponent<RectTransform>();
            tbr.anchorMin = Vector2.zero; tbr.anchorMax = new Vector2(1, 0.2f);
            tbr.sizeDelta = Vector2.zero;
            var tabHlg = tabBar.AddComponent<HorizontalLayoutGroup>();
            tabHlg.spacing = 0; tabHlg.childControlWidth = true; tabHlg.childControlHeight = true;
            tabHlg.childForceExpandWidth = true;

            var tabPlayerGo = UIBuilder.Child(tbr, "TabPlayer");
            tabPlayerGo.AddComponent<Image>().color = _activeTab == "player" ? UITheme.CardHeaderFrosted : Color.clear;
            var tabPlayerText = UIBuilder.Text(tabPlayerGo.GetComponent<RectTransform>(), "T", playerTabLabel, 12, FontStyle.Bold,
                _activeTab == "player" ? UITheme.AccentPrimary : UITheme.TextSecondary);
            tabPlayerText.alignment = TextAnchor.MiddleCenter; UIBuilder.Stretch(tabPlayerText.gameObject);
            tabPlayerGo.AddComponent<Button>().onClick.AddListener(() => { _activeTab = "player"; RebuildPostGame(); });

            var tabOppGo = UIBuilder.Child(tbr, "TabOpp");
            tabOppGo.AddComponent<Image>().color = _activeTab == "opponent" ? UITheme.CardHeaderFrosted : Color.clear;
            var tabOppText = UIBuilder.Text(tabOppGo.GetComponent<RectTransform>(), "T", oppTabLabel, 12, FontStyle.Bold,
                _activeTab == "opponent" ? UITheme.AccentSecondary : UITheme.TextSecondary);
            tabOppText.alignment = TextAnchor.MiddleCenter; UIBuilder.Stretch(tabOppText.gameObject);
            tabOppGo.AddComponent<Button>().onClick.AddListener(() => { _activeTab = "opponent"; RebuildPostGame(); });

            // Box score area
            var boxArea = UIBuilder.Child(parent, "BoxArea");
            var bar = boxArea.GetComponent<RectTransform>();
            bar.anchorMin = Vector2.zero; bar.anchorMax = Vector2.one;
            bar.offsetMin = Vector2.zero; bar.offsetMax = new Vector2(0, -100);

            var scroll = UIBuilder.FixedArea(bar, spacing: 0, padding: 2);
            var vlg = scroll.GetComponent<VerticalLayoutGroup>();
            if (vlg != null) { vlg.spacing = 0; vlg.padding = new RectOffset(4, 4, 2, 2); }

            Team activeTeam = _activeTab == "player" ? playerTeam : oppTeam;
            bool isPlayerTab = _activeTab == "player";
            if (activeTeam?.Roster != null && bs != null)
                BuildTeamBoxScore(scroll, activeTeam, bs, isPlayerTab);

            // Continue button
            var btnGo = UIBuilder.Child(scroll, "ContinueBtn");
            btnGo.AddComponent<LayoutElement>().preferredHeight = 40;
            btnGo.AddComponent<Image>().color = UITheme.DarkenColor(UITheme.Success, 0.5f);
            UIBuilder.ApplyOutline(btnGo, UITheme.DarkenColor(UITheme.Success, 0.3f), 1f);
            btnGo.AddComponent<Button>().onClick.AddListener(() =>
            {
                GameManager.Instance?.AdvanceDay();
                _shell?.ShowPanel("Dashboard");
            });
            var btnText = UIBuilder.Text(btnGo.GetComponent<RectTransform>(), "BT", "CONTINUE", 14, FontStyle.Bold, Color.white);
            btnText.alignment = TextAnchor.MiddleCenter; UIBuilder.Stretch(btnText.gameObject);
        }

        private void RebuildPostGame()
        {
            _shell?.RegisterPanel("PostGame", this);
            _shell?.ShowPanel("PostGame");
        }

        private void BuildTeamBoxScore(RectTransform scroll, Team team, Sim.BoxScore bs, bool isPlayerTeam)
        {
            Color titleColor = isPlayerTeam ? UITheme.AccentPrimary : UITheme.AccentSecondary;

            var hr = UIBuilder.TableRow(scroll, 18, UITheme.CardHeaderFrosted);
            UIBuilder.TableCell(hr, "Player", 120, FontStyle.Bold, titleColor);
            UIBuilder.TableCell(hr, "MIN", 30, FontStyle.Bold, titleColor);
            UIBuilder.TableCell(hr, "PTS", 30, FontStyle.Bold, titleColor);
            UIBuilder.TableCell(hr, "FG", 42, FontStyle.Bold, titleColor);
            UIBuilder.TableCell(hr, "3PT", 38, FontStyle.Bold, titleColor);
            UIBuilder.TableCell(hr, "FT", 38, FontStyle.Bold, titleColor);
            UIBuilder.TableCell(hr, "OREB", 32, FontStyle.Bold, titleColor);
            UIBuilder.TableCell(hr, "DREB", 32, FontStyle.Bold, titleColor);
            UIBuilder.TableCell(hr, "AST", 28, FontStyle.Bold, titleColor);
            UIBuilder.TableCell(hr, "STL", 28, FontStyle.Bold, titleColor);
            UIBuilder.TableCell(hr, "BLK", 28, FontStyle.Bold, titleColor);
            UIBuilder.TableCell(hr, "TO", 28, FontStyle.Bold, titleColor);
            UIBuilder.TableCell(hr, "PF", 24, FontStyle.Bold, titleColor);
            UIBuilder.TableCell(hr, "+/-", 30, FontStyle.Bold, titleColor);

            int tPts=0, tFgm=0, tFga=0, t3m=0, t3a=0, tFtm=0, tFta=0;
            int tOreb=0, tDreb=0, tAst=0, tStl=0, tBlk=0, tTo=0, tPf=0;

            int rowIdx = 0;
            foreach (var player in team.Roster)
            {
                var s = bs.GetPlayerStats(player.PlayerId);
                if (s == null || s.Minutes == 0) continue;
                var bgColor = rowIdx % 2 == 0 ? UITheme.CardFrosted : UITheme.CardHeaderFrosted;
                var row = UIBuilder.TableRow(scroll, 18, bgColor);

                UIBuilder.TableCell(row, $"{player.FirstName[0]}. {player.LastName}", 120, FontStyle.Normal, Color.white);
                UIBuilder.TableCell(row, s.Minutes.ToString(), 30, FontStyle.Normal, UITheme.TextSecondary);
                UIBuilder.TableCell(row, s.Points.ToString(), 30, FontStyle.Bold, Color.white);
                UIBuilder.TableCell(row, $"{s.FieldGoalsMade}-{s.FieldGoalAttempts}", 42, FontStyle.Normal, UITheme.TextSecondary);
                UIBuilder.TableCell(row, $"{s.ThreePointMade}-{s.ThreePointAttempts}", 38, FontStyle.Normal, UITheme.TextSecondary);
                UIBuilder.TableCell(row, $"{s.FreeThrowsMade}-{s.FreeThrowAttempts}", 38, FontStyle.Normal, UITheme.TextSecondary);
                UIBuilder.TableCell(row, s.OffensiveRebounds.ToString(), 32, FontStyle.Normal, UITheme.TextSecondary);
                UIBuilder.TableCell(row, s.DefensiveRebounds.ToString(), 32, FontStyle.Normal, UITheme.TextSecondary);
                UIBuilder.TableCell(row, s.Assists.ToString(), 28, FontStyle.Normal, UITheme.TextSecondary);
                UIBuilder.TableCell(row, s.Steals.ToString(), 28, FontStyle.Normal, UITheme.TextSecondary);
                UIBuilder.TableCell(row, s.Blocks.ToString(), 28, FontStyle.Normal, UITheme.TextSecondary);
                UIBuilder.TableCell(row, s.Turnovers.ToString(), 28, FontStyle.Normal, UITheme.TextSecondary);
                UIBuilder.TableCell(row, s.PersonalFouls.ToString(), 24, FontStyle.Normal, UITheme.TextSecondary);
                string pm = s.PlusMinus >= 0 ? $"+{s.PlusMinus}" : s.PlusMinus.ToString();
                UIBuilder.TableCell(row, pm, 30, FontStyle.Normal, s.PlusMinus >= 0 ? UITheme.Success : UITheme.Danger);

                tPts += s.Points; tFgm += s.FieldGoalsMade; tFga += s.FieldGoalAttempts;
                t3m += s.ThreePointMade; t3a += s.ThreePointAttempts;
                tFtm += s.FreeThrowsMade; tFta += s.FreeThrowAttempts;
                tOreb += s.OffensiveRebounds; tDreb += s.DefensiveRebounds;
                tAst += s.Assists; tStl += s.Steals; tBlk += s.Blocks;
                tTo += s.Turnovers; tPf += s.PersonalFouls;
                rowIdx++;
            }

            // Totals row
            var totalsRow = UIBuilder.TableRow(scroll, 20, UITheme.DarkenColor(titleColor, 0.2f));
            UIBuilder.TableCell(totalsRow, "TOTALS", 120, FontStyle.Bold, titleColor);
            UIBuilder.TableCell(totalsRow, "", 30);
            UIBuilder.TableCell(totalsRow, tPts.ToString(), 30, FontStyle.Bold, Color.white);
            UIBuilder.TableCell(totalsRow, $"{tFgm}-{tFga}", 42, FontStyle.Bold, Color.white);
            UIBuilder.TableCell(totalsRow, $"{t3m}-{t3a}", 38, FontStyle.Bold, Color.white);
            UIBuilder.TableCell(totalsRow, $"{tFtm}-{tFta}", 38, FontStyle.Bold, Color.white);
            UIBuilder.TableCell(totalsRow, tOreb.ToString(), 32, FontStyle.Bold, Color.white);
            UIBuilder.TableCell(totalsRow, tDreb.ToString(), 32, FontStyle.Bold, Color.white);
            UIBuilder.TableCell(totalsRow, tAst.ToString(), 28, FontStyle.Bold, Color.white);
            UIBuilder.TableCell(totalsRow, tStl.ToString(), 28, FontStyle.Bold, Color.white);
            UIBuilder.TableCell(totalsRow, tBlk.ToString(), 28, FontStyle.Bold, Color.white);
            UIBuilder.TableCell(totalsRow, tTo.ToString(), 28, FontStyle.Bold, Color.white);
            UIBuilder.TableCell(totalsRow, tPf.ToString(), 24, FontStyle.Bold, Color.white);

            // Shooting %
            string fgPct = tFga > 0 ? $"{(float)tFgm/tFga*100:0.0}%" : "—";
            string tpPct = t3a > 0 ? $"{(float)t3m/t3a*100:0.0}%" : "—";
            string ftPct = tFta > 0 ? $"{(float)tFtm/tFta*100:0.0}%" : "—";
            var pctRow = UIBuilder.TableRow(scroll, 18, UITheme.CardHeaderFrosted);
            UIBuilder.TableCell(pctRow, "Shooting %", 120, FontStyle.Italic, UITheme.TextSecondary);
            UIBuilder.TableCell(pctRow, "", 30);
            UIBuilder.TableCell(pctRow, "", 30);
            UIBuilder.TableCell(pctRow, fgPct, 42, FontStyle.Bold, UITheme.AccentPrimary);
            UIBuilder.TableCell(pctRow, tpPct, 38, FontStyle.Bold, UITheme.AccentPrimary);
            UIBuilder.TableCell(pctRow, ftPct, 38, FontStyle.Bold, UITheme.AccentPrimary);
        }
    }
}

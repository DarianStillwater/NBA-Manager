using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using NBAHeadCoach.Core;
using NBAHeadCoach.Core.Data;
using NBAHeadCoach.Core.Manager;
using NBAHeadCoach.UI.Shell;
using B = NBAHeadCoach.UI.Shell.UIBuilder;

namespace NBAHeadCoach.UI.GamePanels
{
    /// <summary>
    /// The playoff picture: seeding race before April, then play-in results, the
    /// full bracket round by round, and the champion banner.
    /// </summary>
    public class PlayoffBracketPanel : IGamePanel
    {
        public void Build(RectTransform parent, Team team, Color teamColor)
        {
            var scroll = B.FixedArea(parent);
            var title = B.Text(scroll, "Title", "PLAYOFFS", 18, FontStyle.Bold, UITheme.AccentPrimary);
            var titleLE = title.gameObject.AddComponent<LayoutElement>(); titleLE.preferredHeight = 24; titleLE.flexibleHeight = 0;

            var pm = PlayoffManager.Instance;
            var bracket = pm?.CurrentBracket;

            if (bracket == null || pm.CurrentPhase == PlayoffPhase.NotStarted)
            {
                BuildSeedingPreview(scroll, team, teamColor);
                return;
            }

            if (bracket.IsComplete && !string.IsNullOrEmpty(bracket.ChampionTeamId))
                BuildChampionBanner(scroll, bracket, teamColor);

            if (!string.IsNullOrEmpty(team?.TeamId))
            {
                var status = B.Text(scroll, "Status",
                    $"Your team:  {pm.GetTeamStatus(team.TeamId)}", 13, FontStyle.Bold,
                    pm.IsTeamAlive(team.TeamId) ? UITheme.Success : UITheme.TextSecondary);
                status.gameObject.AddComponent<LayoutElement>().preferredHeight = 22;
            }

            if (bracket.Finals != null)
                BuildSeriesCard(scroll, "NBA FINALS", new[] { bracket.Finals }, team, teamColor);

            BuildConferenceRounds(scroll, "EASTERN CONFERENCE", bracket.Eastern, team, teamColor);
            BuildConferenceRounds(scroll, "WESTERN CONFERENCE", bracket.Western, team, teamColor);
            BuildPlayInCard(scroll, bracket.PlayIn, team, teamColor);
        }

        private void BuildChampionBanner(RectTransform scroll, PlayoffBracket bracket, Color teamColor)
        {
            var card = B.Card(scroll, "NBA CHAMPIONS", UITheme.AccentPrimary);
            card.gameObject.AddComponent<LayoutElement>().preferredHeight = 76;

            string champ = TeamName(bracket.ChampionTeamId);
            string mvp = PlayerName(GameManager.Instance?.Awards?.GetForSeason(bracket.Season)?.FinalsMvpId);
            string body = $"{champ}";
            if (!string.IsNullOrEmpty(mvp)) body += $"\nFinals MVP: {mvp}";

            var text = B.Text(card, "Champ", body, 16, FontStyle.Bold, UITheme.AccentPrimary);
            B.FillCard(text); text.alignment = TextAnchor.MiddleCenter;
        }

        private void BuildConferenceRounds(RectTransform scroll, string header, ConferenceBracket conf, Team team, Color teamColor)
        {
            if (conf == null) return;

            if (conf.ConferenceFinals != null)
                BuildSeriesCard(scroll, $"{header} — FINALS", new[] { conf.ConferenceFinals }, team, teamColor);
            if (conf.ConferenceSemis != null && conf.ConferenceSemis.Any(s => s != null))
                BuildSeriesCard(scroll, $"{header} — SEMIFINALS", conf.ConferenceSemis, team, teamColor);
            if (conf.FirstRound != null && conf.FirstRound.Any(s => s != null))
                BuildSeriesCard(scroll, $"{header} — FIRST ROUND", conf.FirstRound, team, teamColor);
        }

        private void BuildSeriesCard(RectTransform scroll, string header, IEnumerable<PlayoffSeries> seriesList, Team team, Color teamColor)
        {
            var valid = seriesList?.Where(s => s != null).ToList();
            if (valid == null || valid.Count == 0) return;

            var card = B.Card(scroll, header, teamColor);
            card.gameObject.AddComponent<LayoutElement>().preferredHeight = 40 + valid.Count * 40;

            var body = B.Child(card, "Body");
            var rt = body.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(12, 8); rt.offsetMax = new Vector2(-12, -36);
            var vlg = body.AddComponent<VerticalLayoutGroup>();
            vlg.childControlWidth = true; vlg.childControlHeight = false;
            vlg.childForceExpandWidth = true; vlg.spacing = 2;

            foreach (var s in valid)
            {
                bool involvesPlayer = team != null &&
                    (s.HigherSeedTeamId == team.TeamId || s.LowerSeedTeamId == team.TeamId);

                string matchup = $"#{s.HigherSeed} {TeamAbbr(s.HigherSeedTeamId)}  vs  #{s.LowerSeed} {TeamAbbr(s.LowerSeedTeamId)}";
                string status = s.GetStatusString(TeamAbbr);
                string games = string.Join("  ", s.Games
                    .Where(g => g.IsComplete)
                    .OrderBy(g => g.GameNumber)
                    .Select(g => $"G{g.GameNumber} {g.HomeScore}-{g.AwayScore}"));

                var line = B.Text(rt, "Series",
                    $"{matchup}   <b>{status}</b>" + (games.Length > 0 ? $"\n<size=11>{games}</size>" : ""),
                    13, involvesPlayer ? FontStyle.Bold : FontStyle.Normal,
                    involvesPlayer ? Color.white : UITheme.TextSecondary);
                line.gameObject.AddComponent<LayoutElement>().preferredHeight = 36;
                line.alignment = TextAnchor.UpperLeft;
            }
        }

        private void BuildPlayInCard(RectTransform scroll, PlayInTournament playIn, Team team, Color teamColor)
        {
            if (playIn == null) return;

            var card = B.Card(scroll, "PLAY-IN TOURNAMENT", teamColor);
            card.gameObject.AddComponent<LayoutElement>().preferredHeight = 200;

            var lines = new List<string>();
            foreach (var (label, bracket) in new[] { ("EAST", playIn.East), ("WEST", playIn.West) })
            {
                if (bracket == null) continue;
                lines.Add($"<b>{label}</b>");
                lines.Add(PlayInLine("7 vs 8", bracket.SevenVsEight));
                lines.Add(PlayInLine("9 vs 10", bracket.NineVsTen));
                lines.Add(PlayInLine("8-seed game", bracket.EightSeedGame));
                if (bracket.IsComplete)
                    lines.Add($"<color=#{ColorUtility.ToHtmlStringRGB(UITheme.Success)}>7-seed: {TeamAbbr(bracket.SevenSeedTeamId)}   8-seed: {TeamAbbr(bracket.EightSeedTeamId)}</color>");
            }

            var text = B.Text(card, "PlayIn", string.Join("\n", lines), 12, FontStyle.Normal, UITheme.TextSecondary);
            B.FillCard(text); text.alignment = TextAnchor.UpperLeft; text.lineSpacing = 1.25f;
        }

        private string PlayInLine(string label, PlayInGame game)
        {
            if (game == null || string.IsNullOrEmpty(game.HigherSeedTeamId)) return $"{label}:  TBD";
            if (!game.IsComplete)
                return $"{label}:  {TeamAbbr(game.HigherSeedTeamId)} vs {TeamAbbr(game.LowerSeedTeamId)}";
            return $"{label}:  {TeamAbbr(game.HigherSeedTeamId)} {game.HigherSeedScore}-{game.LowerSeedScore} {TeamAbbr(game.LowerSeedTeamId)}  → {TeamAbbr(game.WinnerTeamId)}";
        }

        private void BuildSeedingPreview(RectTransform scroll, Team team, Color teamColor)
        {
            var sc = GameManager.Instance?.SeasonController;

            var info = B.Text(scroll, "Info",
                "The playoffs begin April 19 — play-in tournament, then four best-of-seven rounds.",
                13, FontStyle.Italic, UITheme.TextSecondary);
            info.gameObject.AddComponent<LayoutElement>().preferredHeight = 24;

            foreach (var conference in new[] { "Eastern", "Western" })
            {
                var standings = sc?.GetConferenceStandings(conference);
                if (standings == null || standings.Count == 0) continue;

                var card = B.Card(scroll, $"{conference.ToUpper()} SEEDING RACE", teamColor);
                card.gameObject.AddComponent<LayoutElement>().preferredHeight = 190;

                var lines = standings.Take(10).Select((s, i) =>
                {
                    string tag = i < 6 ? "PLAYOFF" : "PLAY-IN";
                    bool mine = team != null && s.TeamId == team.TeamId;
                    string row = $"#{i + 1}  {TeamAbbr(s.TeamId)}  {s.Wins}-{s.Losses}   <size=10>{tag}</size>";
                    return mine ? $"<b><color=white>{row}</color></b>" : row;
                });

                var text = B.Text(card, "Seeds", string.Join("\n", lines), 12, FontStyle.Normal, UITheme.TextSecondary);
                B.FillCard(text); text.alignment = TextAnchor.UpperLeft; text.lineSpacing = 1.2f;
            }
        }

        private static string TeamAbbr(string teamId) =>
            GameManager.Instance?.GetTeam(teamId)?.Abbreviation ?? teamId ?? "TBD";

        private static string TeamName(string teamId)
        {
            var t = GameManager.Instance?.GetTeam(teamId);
            return t != null ? $"{t.City} {t.Name}" : teamId;
        }

        private static string PlayerName(string playerId) =>
            string.IsNullOrEmpty(playerId) ? null
            : GameManager.Instance?.PlayerDatabase?.GetPlayer(playerId)?.FullName;
    }
}

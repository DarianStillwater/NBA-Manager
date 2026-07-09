using System;
using UnityEngine;
using NBAHeadCoach.Core.Data;
using NBAHeadCoach.UI.Shell;
using Sim = NBAHeadCoach.Core.Simulation;

namespace NBAHeadCoach.UI.Components
{
    /// <summary>
    /// Shared full box-score table (MIN PTS FG 3PT FT OREB DREB AST STL BLK TO PF +/-,
    /// zebra rows, totals, shooting %). Used by the post-game report AND the in-match
    /// BOX SCORE overlay so both render identically from the same BoxScore data.
    /// </summary>
    public static class BoxScoreTable
    {
        /// <summary>
        /// Build one team's table into a vertical-layout parent. includeRow defaults to
        /// "played at least a minute" (the post-game rule); the live overlay widens it so
        /// the current five show up at 0:00.
        /// </summary>
        public static void Build(RectTransform scroll, Team team, Sim.BoxScore bs, Color titleColor,
            Func<Sim.PlayerGameStats, bool> includeRow = null)
        {
            includeRow ??= s => s != null && s.Minutes > 0;

            var hr = UIBuilder.TableRow(scroll, 0, UITheme.CardHeaderFrosted);
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
                if (!includeRow(s)) continue;
                var bgColor = rowIdx % 2 == 0 ? UITheme.CardFrosted : UITheme.CardHeaderFrosted;
                var row = UIBuilder.TableRow(scroll, 0, bgColor);

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
            var totalsRow = UIBuilder.TableRow(scroll, 0, UITheme.DarkenColor(titleColor, 0.2f));
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
            var pctRow = UIBuilder.TableRow(scroll, 0, UITheme.CardHeaderFrosted);
            UIBuilder.TableCell(pctRow, "Shooting %", 120, FontStyle.Italic, UITheme.TextSecondary);
            UIBuilder.TableCell(pctRow, "", 30);
            UIBuilder.TableCell(pctRow, "", 30);
            UIBuilder.TableCell(pctRow, fgPct, 42, FontStyle.Bold, UITheme.AccentPrimary);
            UIBuilder.TableCell(pctRow, tpPct, 38, FontStyle.Bold, UITheme.AccentPrimary);
            UIBuilder.TableCell(pctRow, ftPct, 38, FontStyle.Bold, UITheme.AccentPrimary);
        }
    }
}

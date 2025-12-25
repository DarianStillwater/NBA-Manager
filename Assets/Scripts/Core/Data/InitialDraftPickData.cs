using System;
using System.Collections.Generic;

namespace NBAHeadCoach.Core.Data
{
    /// <summary>
    /// Data structure for loading initial draft pick ownership from JSON.
    /// Represents the real-world draft pick trades as of a specific date.
    /// </summary>
    [Serializable]
    public class InitialDraftPickData
    {
        /// <summary>Date this data is accurate as of (e.g., "2025-12-01")</summary>
        public string dataAsOf;

        /// <summary>All picks that have been traded (not owned by original team)</summary>
        public List<TradedPickEntry> tradedPicks;

        /// <summary>All swap rights (where a team can swap picks with another)</summary>
        public List<SwapRightEntry> swapRights;
    }

    /// <summary>
    /// Represents a draft pick that has been traded to another team.
    /// </summary>
    [Serializable]
    public class TradedPickEntry
    {
        /// <summary>Team that originally owned this pick</summary>
        public string originalTeamId;

        /// <summary>Draft year (e.g., 2026)</summary>
        public int year;

        /// <summary>Round (1 or 2)</summary>
        public int round;

        /// <summary>Team that currently owns this pick</summary>
        public string currentOwnerId;

        /// <summary>Protection conditions on the pick</summary>
        public List<ProtectionEntry> protections;

        /// <summary>What happens if protections never allow conveyance: "BecomeSecondRound", "ConveyUnprotected", "Void"</summary>
        public string finalConveyance;
    }

    /// <summary>
    /// Represents protection conditions for a specific year.
    /// </summary>
    [Serializable]
    public class ProtectionEntry
    {
        /// <summary>Year this protection applies</summary>
        public int year;

        /// <summary>Type: "TopN", "Lottery", "Range", "Unprotected"</summary>
        public string type;

        /// <summary>For TopN: the threshold (e.g., 10 for top-10 protected)</summary>
        public int threshold;

        /// <summary>For Range: minimum pick number</summary>
        public int rangeMin;

        /// <summary>For Range: maximum pick number</summary>
        public int rangeMax;

        /// <summary>
        /// Convert to the game's DraftProtection type.
        /// </summary>
        public DraftProtection ToDraftProtection()
        {
            return type?.ToLower() switch
            {
                "topn" => DraftProtection.TopN(year, threshold),
                "lottery" => DraftProtection.Lottery(year),
                "range" => DraftProtection.Range(year, rangeMin, rangeMax),
                "unprotected" => DraftProtection.Unprotected(year),
                _ => DraftProtection.Unprotected(year)
            };
        }
    }

    /// <summary>
    /// Represents a swap right where one team can swap picks with another.
    /// </summary>
    [Serializable]
    public class SwapRightEntry
    {
        /// <summary>Team whose pick position is being used for the swap</summary>
        public string originalTeamId;

        /// <summary>Draft year</summary>
        public int year;

        /// <summary>Team that benefits from the swap right (can choose better pick)</summary>
        public string beneficiaryTeamId;
    }

    /// <summary>
    /// Helper extension methods for converting JSON data to game types.
    /// </summary>
    public static class InitialDraftPickDataExtensions
    {
        /// <summary>
        /// Convert string to ConveyanceType enum.
        /// </summary>
        public static ConveyanceType ParseConveyanceType(string value)
        {
            if (string.IsNullOrEmpty(value))
                return ConveyanceType.BecomeSecondRound;

            return value.ToLower() switch
            {
                "becomesecondround" => ConveyanceType.BecomeSecondRound,
                "conveyunprotected" => ConveyanceType.ConveyUnprotected,
                "void" => ConveyanceType.Void,
                _ => ConveyanceType.BecomeSecondRound
            };
        }
    }
}

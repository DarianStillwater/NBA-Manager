using System;
using System.Collections.Generic;
using UnityEngine;

namespace NBAHeadCoach.Core.Data
{
    /// <summary>
    /// Represents a draft pick with ownership and protections.
    /// </summary>
    [Serializable]
    public class DraftPick
    {
        // ==================== BASIC INFO ====================
        
        /// <summary>Draft year (e.g., 2025)</summary>
        public int Year;
        
        /// <summary>First or second round</summary>
        public int Round; // 1 or 2
        
        /// <summary>Team that originally owned the pick</summary>
        public string OriginalTeamId;
        
        /// <summary>Team that currently owns the pick</summary>
        public string CurrentOwnerId;
        
        /// <summary>Is this a pick swap right instead of outright ownership?</summary>
        public bool IsSwapRight;
        
        /// <summary>For swap rights: team with the swap option</summary>
        public string SwapBeneficiaryTeamId;

        // ==================== PROTECTIONS ====================
        
        /// <summary>List of protections on this pick</summary>
        public List<DraftProtection> Protections = new List<DraftProtection>();
        
        /// <summary>What happens if all protections trigger and never convey</summary>
        public ConveyanceType FinalConveyance = ConveyanceType.BecomeSecondRound;

        // ==================== COMPUTED PROPERTIES ====================
        
        public bool IsFirstRound => Round == 1;
        public bool IsProtected => Protections.Count > 0;
        
        /// <summary>
        /// Checks if the pick conveys to the new owner based on where it lands.
        /// </summary>
        public bool DoesConvey(int actualPickPosition)
        {
            foreach (var protection in Protections)
            {
                if (protection.Year != Year)
                    continue;
                    
                if (protection.IsProtected(actualPickPosition))
                {
                    // Pick is protected - does NOT convey
                    return false;
                }
            }
            
            // No active protection - pick conveys
            return true;
        }
        
        /// <summary>
        /// Gets what happens this year if the pick doesn't convey.
        /// </summary>
        public ConveyanceResult GetConveyanceResult(int actualPickPosition)
        {
            var result = new ConveyanceResult { Year = Year };
            
            // Check if pick conveys
            if (DoesConvey(actualPickPosition))
            {
                result.Conveys = true;
                result.ConveyingAsRound = Round;
                return result;
            }
            
            // Pick doesn't convey - check for next year's protection
            var nextYearProtection = Protections.Find(p => p.Year == Year + 1);
            
            if (nextYearProtection != null)
            {
                result.Conveys = false;
                result.DefersToYear = Year + 1;
                result.NextYearProtection = nextYearProtection.GetProtectionDescription();
            }
            else
            {
                // No more protections - final conveyance applies
                result.Conveys = true;
                result.ConveyingAsRound = FinalConveyance == ConveyanceType.BecomeSecondRound ? 2 : 1;
                result.IsFinalConveyance = true;
            }
            
            return result;
        }

        // ==================== FACTORY METHODS ====================
        
        /// <summary>
        /// Creates an unprotected first round pick.
        /// </summary>
        public static DraftPick CreateFirstRound(int year, string originalTeam, string currentOwner)
        {
            return new DraftPick
            {
                Year = year,
                Round = 1,
                OriginalTeamId = originalTeam,
                CurrentOwnerId = currentOwner
            };
        }
        
        /// <summary>
        /// Creates a protected first round pick.
        /// </summary>
        public static DraftPick CreateProtectedFirstRound(
            int year, 
            string originalTeam, 
            string currentOwner,
            params DraftProtection[] protections)
        {
            var pick = CreateFirstRound(year, originalTeam, currentOwner);
            pick.Protections.AddRange(protections);
            return pick;
        }
        
        /// <summary>
        /// Creates a second round pick.
        /// </summary>
        public static DraftPick CreateSecondRound(int year, string originalTeam, string currentOwner)
        {
            return new DraftPick
            {
                Year = year,
                Round = 2,
                OriginalTeamId = originalTeam,
                CurrentOwnerId = currentOwner
            };
        }
        
        /// <summary>
        /// Creates a pick swap right.
        /// </summary>
        public static DraftPick CreateSwapRight(int year, string originalTeam, string beneficiary)
        {
            return new DraftPick
            {
                Year = year,
                Round = 1,
                OriginalTeamId = originalTeam,
                CurrentOwnerId = originalTeam,
                IsSwapRight = true,
                SwapBeneficiaryTeamId = beneficiary
            };
        }

        // ==================== UTILITY ====================
        
        public string GetDescription()
        {
            string desc = $"{Year} {(IsFirstRound ? "1st" : "2nd")} Round ({OriginalTeamId})";
            
            if (IsSwapRight)
                desc = $"{Year} Pick Swap ({OriginalTeamId} to {SwapBeneficiaryTeamId})";
            
            if (IsProtected)
            {
                var currentProtection = Protections.Find(p => p.Year == Year);
                if (currentProtection != null)
                    desc += $" - {currentProtection.GetProtectionDescription()}";
            }
            
            return desc;
        }
    }

    /// <summary>
    /// Represents protection conditions on a draft pick for a specific year.
    /// </summary>
    [Serializable]
    public class DraftProtection
    {
        /// <summary>Year this protection applies to</summary>
        public int Year;
        
        /// <summary>Type of protection</summary>
        public ProtectionType Type;
        
        /// <summary>For TopN protection: the threshold (e.g., 10 for top-10 protected)</summary>
        public int TopNThreshold;
        
        /// <summary>For Range protection: minimum pick number</summary>
        public int RangeMin;
        
        /// <summary>For Range protection: maximum pick number</summary>
        public int RangeMax;

        // ==================== PROTECTION CHECK ====================
        
        /// <summary>
        /// Returns true if the pick is protected at the given position.
        /// </summary>
        public bool IsProtected(int pickPosition)
        {
            return Type switch
            {
                ProtectionType.TopN => pickPosition <= TopNThreshold,
                ProtectionType.Lottery => pickPosition <= 14,
                ProtectionType.Range => pickPosition >= RangeMin && pickPosition <= RangeMax,
                ProtectionType.Unprotected => false,
                _ => false
            };
        }
        
        /// <summary>
        /// Gets a human-readable description of the protection.
        /// </summary>
        public string GetProtectionDescription()
        {
            return Type switch
            {
                ProtectionType.TopN => $"Top {TopNThreshold} protected",
                ProtectionType.Lottery => "Lottery protected",
                ProtectionType.Range => $"Protected {RangeMin}-{RangeMax}",
                ProtectionType.Unprotected => "Unprotected",
                _ => "Unknown"
            };
        }

        // ==================== FACTORY METHODS ====================
        
        /// <summary>Top N protected (e.g., Top 10 protected)</summary>
        public static DraftProtection TopN(int year, int n) => new DraftProtection
        {
            Year = year,
            Type = ProtectionType.TopN,
            TopNThreshold = n
        };
        
        /// <summary>Lottery protected (picks 1-14)</summary>
        public static DraftProtection Lottery(int year) => new DraftProtection
        {
            Year = year,
            Type = ProtectionType.Lottery
        };
        
        /// <summary>Protected within a range (e.g., 1-4)</summary>
        public static DraftProtection Range(int year, int min, int max) => new DraftProtection
        {
            Year = year,
            Type = ProtectionType.Range,
            RangeMin = min,
            RangeMax = max
        };
        
        /// <summary>Unprotected</summary>
        public static DraftProtection Unprotected(int year) => new DraftProtection
        {
            Year = year,
            Type = ProtectionType.Unprotected
        };
    }

    public enum ProtectionType
    {
        Unprotected,    // No protection
        TopN,           // Top 1, Top 3, Top 5, Top 10, etc.
        Lottery,        // Protected if in lottery (1-14)
        Range           // Protected within specific range
    }

    public enum ConveyanceType
    {
        BecomeSecondRound,  // If never conveys, becomes 2nd rounder
        ConveyUnprotected,  // Eventually conveys unprotected
        Void                // Trade protection expires, pick returns to original owner
    }

    /// <summary>
    /// Result of conveyance check for a specific year.
    /// </summary>
    public class ConveyanceResult
    {
        public int Year;
        public bool Conveys;
        public int ConveyingAsRound;
        public int DefersToYear;
        public string NextYearProtection;
        public bool IsFinalConveyance;
        
        public override string ToString()
        {
            if (Conveys)
            {
                string round = ConveyingAsRound == 1 ? "1st" : "2nd";
                return IsFinalConveyance 
                    ? $"Conveys as {round} rounder (final)" 
                    : $"Conveys as {round} rounder";
            }
            return $"Defers to {DefersToYear} ({NextYearProtection})";
        }
    }
}

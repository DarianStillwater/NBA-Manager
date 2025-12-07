using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using NBAHeadCoach.Core.Data;

namespace NBAHeadCoach.Core.Manager
{
    /// <summary>
    /// Manages the NBA Draft including lottery, draft order, and pick execution.
    /// </summary>
    public class DraftSystem
    {
        private ProspectGenerator _prospectGenerator;
        private SalaryCapManager _capManager;
        
        // Current draft class
        private List<DraftProspect> _prospects = new List<DraftProspect>();
        private List<DraftSelection> _draftResults = new List<DraftSelection>();
        
        // Draft order (teamIds in pick order)
        private List<string> _firstRoundOrder = new List<string>();
        private List<string> _secondRoundOrder = new List<string>();
        
        // Lottery odds (for non-playoff teams, 14 teams)
        private static readonly float[] LotteryOdds = {
            14.0f, 14.0f, 14.0f, 12.5f, 10.5f, 9.0f, 7.5f,
            6.0f, 4.5f, 3.0f, 2.0f, 1.5f, 1.0f, 0.5f
        };

        public DraftSystem(SalaryCapManager capManager, int? seed = null)
        {
            _capManager = capManager;
            _prospectGenerator = new ProspectGenerator(seed);
        }

        // ==================== DRAFT CLASS ====================

        /// <summary>
        /// Generates a new draft class of 120 prospects.
        /// </summary>
        public List<DraftProspect> GenerateDraftClass(int year)
        {
            _prospects = _prospectGenerator.GenerateDraftClass(year);
            _draftResults.Clear();
            return _prospects;
        }

        public List<DraftProspect> GetProspects() => _prospects.ToList();
        
        public DraftProspect GetProspect(string prospectId) => 
            _prospects.FirstOrDefault(p => p.ProspectId == prospectId);

        // ==================== LOTTERY ====================

        /// <summary>
        /// Runs the draft lottery for the 14 non-playoff teams.
        /// Returns the lottery order (team IDs for picks 1-14).
        /// </summary>
        public List<LotteryResult> RunLottery(List<string> nonPlayoffTeams, System.Random rng = null)
        {
            rng ??= new System.Random();
            
            if (nonPlayoffTeams.Count != 14)
            {
                Debug.LogWarning($"Lottery expects 14 teams, got {nonPlayoffTeams.Count}");
            }
            
            var results = new List<LotteryResult>();
            var remainingTeams = nonPlayoffTeams.ToList();
            var originalOdds = LotteryOdds.Take(remainingTeams.Count).ToList();
            
            // Draw top 4 picks via lottery
            for (int pick = 1; pick <= 4 && remainingTeams.Count > 0; pick++)
            {
                // Calculate remaining odds
                float totalOdds = 0;
                var teamOdds = new List<(string team, float odds, int originalPosition)>();
                
                for (int i = 0; i < remainingTeams.Count; i++)
                {
                    int originalIdx = nonPlayoffTeams.IndexOf(remainingTeams[i]);
                    float odds = originalIdx < originalOdds.Count ? originalOdds[originalIdx] : 0.5f;
                    teamOdds.Add((remainingTeams[i], odds, originalIdx + 1));
                    totalOdds += odds;
                }
                
                // Random selection weighted by odds
                float roll = (float)rng.NextDouble() * totalOdds;
                float cumulative = 0;
                string selectedTeam = remainingTeams[0];
                int originalPos = 1;
                
                foreach (var (team, odds, origPos) in teamOdds)
                {
                    cumulative += odds;
                    if (roll <= cumulative)
                    {
                        selectedTeam = team;
                        originalPos = origPos;
                        break;
                    }
                }
                
                results.Add(new LotteryResult
                {
                    Pick = pick,
                    TeamId = selectedTeam,
                    OriginalPosition = originalPos,
                    MovedUp = originalPos > pick
                });
                
                remainingTeams.Remove(selectedTeam);
            }
            
            // Remaining picks 5-14 go in reverse standings order
            int currentPick = 5;
            foreach (var team in nonPlayoffTeams)
            {
                if (!results.Any(r => r.TeamId == team))
                {
                    int originalPos = nonPlayoffTeams.IndexOf(team) + 1;
                    results.Add(new LotteryResult
                    {
                        Pick = currentPick,
                        TeamId = team,
                        OriginalPosition = originalPos,
                        MovedUp = false
                    });
                    currentPick++;
                }
            }
            
            return results.OrderBy(r => r.Pick).ToList();
        }

        // ==================== DRAFT ORDER ====================

        /// <summary>
        /// Sets the full draft order for both rounds.
        /// </summary>
        public void SetDraftOrder(List<string> firstRound, List<string> secondRound)
        {
            _firstRoundOrder = firstRound.ToList();
            _secondRoundOrder = secondRound.ToList();
        }

        /// <summary>
        /// Gets the team picking at a specific position.
        /// </summary>
        public string GetTeamAtPick(int pickNumber)
        {
            if (pickNumber <= 30)
                return pickNumber <= _firstRoundOrder.Count ? _firstRoundOrder[pickNumber - 1] : null;
            
            int secondRoundPick = pickNumber - 30;
            return secondRoundPick <= _secondRoundOrder.Count ? _secondRoundOrder[secondRoundPick - 1] : null;
        }

        // ==================== DRAFTING ====================

        /// <summary>
        /// Executes a draft pick.
        /// </summary>
        public DraftSelection MakePick(int pickNumber, string teamId, string prospectId)
        {
            var prospect = GetProspect(prospectId);
            if (prospect == null)
            {
                Debug.LogError($"Prospect {prospectId} not found");
                return null;
            }
            
            // Remove from available prospects
            _prospects.RemoveAll(p => p.ProspectId == prospectId);
            
            // Create selection record
            var selection = new DraftSelection
            {
                PickNumber = pickNumber,
                Round = pickNumber <= 30 ? 1 : 2,
                TeamId = teamId,
                Prospect = prospect
            };
            
            _draftResults.Add(selection);
            
            // Create rookie contract
            var contract = Contract.CreateRookieScale(prospectId, teamId, pickNumber);
            _capManager?.RegisterContract(contract);
            
            Debug.Log($"Pick {pickNumber}: {teamId} selects {prospect.FullName} ({prospect.Position})");
            
            return selection;
        }

        /// <summary>
        /// AI makes a pick based on team needs and BPA.
        /// </summary>
        public DraftSelection AISelectPick(int pickNumber, string teamId, TeamNeeds needs = null)
        {
            if (_prospects.Count == 0)
                return null;
            
            // Score each prospect
            var scoredProspects = new List<(DraftProspect prospect, float score)>();
            
            foreach (var prospect in _prospects)
            {
                float score = prospect.ProjectedOverall + prospect.Potential * 0.3f;
                
                // Boost for team needs
                if (needs != null && needs.PositionNeeds.Contains(prospect.Position))
                    score += 5;
                
                // Penalize for high bust probability
                score -= prospect.BustProbability * 10;
                
                // Add some randomness
                score += (float)new System.Random().NextDouble() * 5 - 2.5f;
                
                scoredProspects.Add((prospect, score));
            }
            
            var bestProspect = scoredProspects.OrderByDescending(p => p.score).First().prospect;
            return MakePick(pickNumber, teamId, bestProspect.ProspectId);
        }

        // ==================== COMBINE ====================

        /// <summary>
        /// Simulates draft combine results for a prospect.
        /// </summary>
        public CombineResults SimulateCombine(DraftProspect prospect)
        {
            var rng = new System.Random();
            
            return new CombineResults
            {
                ProspectId = prospect.ProspectId,
                
                // Measurements (slight variance from generated)
                HeightWithShoes = prospect.Height + 1,
                HeightWithoutShoes = prospect.Height,
                Wingspan = prospect.Wingspan + rng.Next(-1, 2),
                StandingReach = prospect.Height + (prospect.Wingspan - prospect.Height) / 2 + 5,
                Weight = prospect.Weight + rng.Next(-5, 6),
                BodyFat = 5f + (float)rng.NextDouble() * 10f,
                
                // Athletic testing
                VerticalLeapMax = 25 + (prospect.Athleticism / 3) + rng.Next(-3, 4),
                VerticalLeapStanding = 20 + (prospect.Athleticism / 4) + rng.Next(-2, 3),
                LaneAgility = 12.0f - (prospect.Athleticism / 20f) + (float)rng.NextDouble() * 0.5f,
                ThreeQuarterSprint = 3.5f - (prospect.Athleticism / 100f) + (float)rng.NextDouble() * 0.2f,
                BenchPress = 5 + rng.Next(15),
                
                // Scrimmage performance
                ScrimmageRating = prospect.ProjectedOverall + rng.Next(-10, 11),
                
                // Interview
                InterviewScore = 50 + rng.Next(50)
            };
        }

        // ==================== RESULTS ====================

        public List<DraftSelection> GetDraftResults() => _draftResults.ToList();

        public DraftSelection GetSelection(int pickNumber) =>
            _draftResults.FirstOrDefault(s => s.PickNumber == pickNumber);
    }

    // ==================== DATA CLASSES ====================

    [Serializable]
    public class DraftSelection
    {
        public int PickNumber;
        public int Round;
        public string TeamId;
        public DraftProspect Prospect;
    }

    [Serializable]
    public class LotteryResult
    {
        public int Pick;
        public string TeamId;
        public int OriginalPosition;
        public bool MovedUp;
    }

    [Serializable]
    public class CombineResults
    {
        public string ProspectId;
        
        // Measurements
        public int HeightWithShoes;
        public int HeightWithoutShoes;
        public int Wingspan;
        public int StandingReach;
        public int Weight;
        public float BodyFat;
        
        // Athletic Tests
        public int VerticalLeapMax;
        public int VerticalLeapStanding;
        public float LaneAgility;      // seconds (lower = better)
        public float ThreeQuarterSprint; // seconds (lower = better)
        public int BenchPress;         // reps of 185 lbs
        
        // Performance
        public int ScrimmageRating;
        public int InterviewScore;
    }

    public class TeamNeeds
    {
        public List<Position> PositionNeeds = new List<Position>();
        public bool NeedsScoring;
        public bool NeedsDefense;
        public bool NeedsPlaymaking;
    }
}

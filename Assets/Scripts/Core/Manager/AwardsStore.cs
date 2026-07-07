using System.Collections.Generic;
using System.Linq;
using NBAHeadCoach.Core.Data;

namespace NBAHeadCoach.Core.Manager
{
    /// <summary>
    /// Persistent record of season awards — MVP through All-Defense, All-Star
    /// selections, Finals MVP, and the champion. Written when milestones occur
    /// (All-Star break, champion crowned); read by the League panel and, later,
    /// the history system.
    /// </summary>
    public class AwardsStore : IGameSystem, ISaveSection
    {
        public static AwardsStore Instance { get; private set; }

        public string SystemId => "Awards";

        private readonly List<SeasonAwards> _history = new List<SeasonAwards>();

        public AwardsStore()
        {
            Instance = this;
        }

        public IReadOnlyList<SeasonAwards> History => _history;

        public SeasonAwards Latest => _history.Count > 0 ? _history[_history.Count - 1] : null;

        public SeasonAwards GetForSeason(int season) =>
            _history.FirstOrDefault(a => a.Season == season);

        public SeasonAwards GetOrCreate(int season)
        {
            var entry = GetForSeason(season);
            if (entry == null)
            {
                entry = new SeasonAwards { Season = season };
                _history.Add(entry);
                _history.Sort((a, b) => a.Season.CompareTo(b.Season));
            }
            return entry;
        }

        /// <summary>Record the end-of-season voting results.</summary>
        public SeasonAwards RecordSeasonAwards(int season, AwardVotingResults results,
            string cotyTeamId, string finalsMvpId, string championId, string runnerUpId)
        {
            var entry = GetOrCreate(season);
            entry.MvpId = results?.MVP?.PlayerId;
            entry.DpoyId = results?.DPOY?.PlayerId;
            entry.RoyId = results?.ROTY?.PlayerId;
            entry.SixthManId = results?.SixthMan?.PlayerId;
            entry.MipId = results?.MIP?.PlayerId;
            entry.CotyId = cotyTeamId;
            entry.AllNbaFirst = results?.AllNBAFirst?.Select(p => p.PlayerId).ToList() ?? new List<string>();
            entry.AllNbaSecond = results?.AllNBASecond?.Select(p => p.PlayerId).ToList() ?? new List<string>();
            entry.AllNbaThird = results?.AllNBAThird?.Select(p => p.PlayerId).ToList() ?? new List<string>();
            entry.AllDefenseFirst = results?.AllDefenseFirst?.Select(p => p.PlayerId).ToList() ?? new List<string>();
            entry.AllDefenseSecond = results?.AllDefenseSecond?.Select(p => p.PlayerId).ToList() ?? new List<string>();
            entry.FinalsMvpId = finalsMvpId;
            entry.ChampionTeamId = championId;
            entry.RunnerUpTeamId = runnerUpId;
            return entry;
        }

        /// <summary>Record All-Star selections when the break begins.</summary>
        public void RecordAllStars(int season, IEnumerable<string> playerIds)
        {
            var entry = GetOrCreate(season);
            entry.AllStars = playerIds?.Where(id => !string.IsNullOrEmpty(id)).ToList()
                             ?? new List<string>();
        }

        public void WriteSave(SaveData data)
        {
            data.AwardsHistoryList = new List<SeasonAwards>(_history);
        }

        public void ReadSave(SaveData data, in SaveReadContext ctx)
        {
            _history.Clear();
            if (data.AwardsHistoryList != null)
                _history.AddRange(data.AwardsHistoryList.Where(a => a != null));
        }
    }
}

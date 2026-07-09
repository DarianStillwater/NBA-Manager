using System.Collections.Generic;

namespace NBAHeadCoach.Core
{
    /// <summary>
    /// Tracks coaching technicals per team during a game and reports an ejection on the
    /// second technical (NBA rule: two technicals = automatic ejection). Pure and
    /// deterministic — no RNG, no Unity types — so it's unit-testable in isolation.
    /// </summary>
    public class CoachEjectionTracker
    {
        private const int TechnicalsForEjection = 2;

        private readonly Dictionary<string, int> _technicals = new Dictionary<string, int>();
        private readonly HashSet<string> _ejected = new HashSet<string>();

        /// <summary>Charge a coach technical. Returns true exactly once — on the technical that
        /// ejects him. Further technicals for an already-ejected coach return false.</summary>
        public bool RegisterTechnical(string teamId)
        {
            if (string.IsNullOrEmpty(teamId) || _ejected.Contains(teamId)) return false;

            int count = _technicals.TryGetValue(teamId, out var c) ? c + 1 : 1;
            _technicals[teamId] = count;

            if (count >= TechnicalsForEjection)
            {
                _ejected.Add(teamId);
                return true;
            }
            return false;
        }

        public bool IsEjected(string teamId) => teamId != null && _ejected.Contains(teamId);

        public int TechnicalCount(string teamId) => _technicals.TryGetValue(teamId, out var c) ? c : 0;

        public void Reset()
        {
            _technicals.Clear();
            _ejected.Clear();
        }
    }
}

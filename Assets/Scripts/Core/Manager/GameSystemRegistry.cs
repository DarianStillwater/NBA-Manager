using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace NBAHeadCoach.Core.Manager
{
    /// <summary>
    /// The manifest of live game systems. GameManager registers every manager here after
    /// construction; the daily loop, save pipeline, and phase fan-out iterate the registry
    /// instead of hand-maintained call lists. Registration order is preserved and used as
    /// the tiebreaker for equal TickOrders and as the read order for save sections.
    /// </summary>
    public sealed class GameSystemRegistry
    {
        private readonly List<IGameSystem> _all = new List<IGameSystem>();
        private readonly HashSet<string> _ids = new HashSet<string>();
        private List<IDailyTickable> _dailySorted; // invalidated on Register

        public void Register(IGameSystem system)
        {
            if (system == null) throw new ArgumentNullException(nameof(system));
            if (string.IsNullOrEmpty(system.SystemId))
                throw new ArgumentException($"{system.GetType().Name} has an empty SystemId");
            if (!_ids.Add(system.SystemId))
                throw new InvalidOperationException($"Duplicate SystemId '{system.SystemId}'");
            _all.Add(system);
            _dailySorted = null;
        }

        public IReadOnlyList<IGameSystem> All => _all;

        /// <summary>Sorted by (TickOrder, registration index).</summary>
        public IReadOnlyList<IDailyTickable> DailyTickables
        {
            get
            {
                if (_dailySorted == null)
                {
                    _dailySorted = _all.OfType<IDailyTickable>()
                        .Select((t, i) => (t, i))
                        .OrderBy(p => p.t.TickOrder)
                        .ThenBy(p => p.i)
                        .Select(p => p.t)
                        .ToList();
                }
                return _dailySorted;
            }
        }

        /// <summary>Registration order — also the save read/write order.</summary>
        public IReadOnlyList<ISaveSection> SaveSections =>
            _all.OfType<ISaveSection>().ToList();

        public IReadOnlyList<ISeasonPhaseListener> PhaseListeners =>
            _all.OfType<ISeasonPhaseListener>().ToList();

        public IReadOnlyList<INewGameInitializable> NewGameInitializables =>
            _all.OfType<INewGameInitializable>().ToList();

        /// <summary>
        /// Run one game day across all tickables. A throwing system logs and is skipped —
        /// one broken manager must not abort the rest of the day.
        /// </summary>
        public void TickDay(in DailyTickContext ctx)
        {
            var tickables = DailyTickables;
            for (int i = 0; i < tickables.Count; i++)
            {
                try
                {
                    tickables[i].DailyTick(ctx);
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[GameSystemRegistry] {tickables[i].SystemId} DailyTick failed: {ex}");
                }
            }
        }
    }

    /// <summary>
    /// Bridges an existing manager call into the registry without touching the manager.
    /// Migration scaffolding: replaced by native IDailyTickable implementations as each
    /// manager adopts the lifecycle contract.
    /// </summary>
    public sealed class DailyTickAdapter : IDailyTickable
    {
        private readonly Action<DailyTickContext> _tick;

        public DailyTickAdapter(string systemId, int tickOrder, Action<DailyTickContext> tick)
        {
            SystemId = systemId;
            TickOrder = tickOrder;
            _tick = tick ?? throw new ArgumentNullException(nameof(tick));
        }

        public string SystemId { get; }
        public int TickOrder { get; }
        public void DailyTick(in DailyTickContext ctx) => _tick(ctx);
    }
}

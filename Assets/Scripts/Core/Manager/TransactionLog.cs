using System;
using System.Collections.Generic;
using NBAHeadCoach.Core.Data;

namespace NBAHeadCoach.Core.Manager
{
    /// <summary>
    /// League transaction history — trades, signings, waivers, draft picks.
    /// Producers append as features wire up (trades today; signings/waives/draft
    /// when those systems go live). Persisted via SaveData.Transactions.
    /// </summary>
    public class TransactionLog : IGameSystem, ISaveSection
    {
        public static TransactionLog Instance { get; private set; }

        public string SystemId => "Transactions";

        private readonly List<TransactionRecord> _records = new List<TransactionRecord>();

        public event Action<TransactionRecord> OnTransactionRecorded;

        public TransactionLog()
        {
            Instance = this;
        }

        public IReadOnlyList<TransactionRecord> Records => _records;

        public void Add(TransactionType type, DateTime date, List<string> teamIds,
            List<string> playerIds, string description)
        {
            var record = new TransactionRecord
            {
                TransactionId = Guid.NewGuid().ToString("N"),
                Date = date,
                DateStr = date.ToString("o"),
                Type = type,
                TeamIds = teamIds ?? new List<string>(),
                PlayerIds = playerIds ?? new List<string>(),
                Description = description ?? ""
            };
            _records.Add(record);
            OnTransactionRecorded?.Invoke(record);
        }

        public void WriteSave(SaveData data)
        {
            data.Transactions = new List<TransactionRecord>(_records);
        }

        public void ReadSave(SaveData data, in SaveReadContext ctx)
        {
            _records.Clear();
            if (data.Transactions == null) return;
            foreach (var r in data.Transactions)
            {
                if (r == null) continue;
                // Rehydrate the DateTime from its ISO backup
                if (!string.IsNullOrEmpty(r.DateStr) &&
                    DateTime.TryParse(r.DateStr, null, System.Globalization.DateTimeStyles.RoundtripKind, out var d))
                    r.Date = d;
                _records.Add(r);
            }
        }
    }
}

using System;
using System.Collections.Generic;
using NBAHeadCoach.Core.Data;

namespace NBAHeadCoach.Core.Manager
{
    public enum InboxMessageType
    {
        System,
        OwnerMessage,
        Finance,
        Trade,
        Media,
        Injury,
        Scouting,
        JobMarket,
        League
    }

    /// <summary>
    /// One inbox message. JsonUtility-safe: dates as ISO strings, no dictionaries.
    /// </summary>
    [Serializable]
    public class InboxMessage
    {
        public string MessageId;
        public string DateStr; // ISO
        public InboxMessageType Type;
        public string Sender;
        public string Title;
        public string Body;
        public bool IsRead;
        public bool IsHighPriority;
        public string DeepLinkPanelId;  // GameShell panel id; "" = none
        public string DeepLinkPayload;  // entity id (playerId/tradeId/...); "" = none

        public DateTime Date =>
            DateTime.TryParse(DateStr, null, System.Globalization.DateTimeStyles.RoundtripKind, out var d)
                ? d : DateTime.MinValue;
    }

    /// <summary>
    /// THE message bus. Systems publish; the Inbox panel and the sidebar badge
    /// consume. Persisted as a save section (newest-first, capped).
    /// </summary>
    public class InboxService : IGameSystem, ISaveSection
    {
        public static InboxService Instance { get; private set; }

        public string SystemId => "Inbox";

        private const int MAX_MESSAGES = 500;
        private readonly List<InboxMessage> _messages = new List<InboxMessage>(); // newest first

        public event Action<InboxMessage> OnMessagePublished;
        public event Action OnUnreadCountChanged;

        public InboxService()
        {
            Instance = this;
        }

        public InboxMessage Publish(InboxMessageType type, string sender, string title, string body,
            bool highPriority = false, string deepLinkPanelId = null, string deepLinkPayload = null)
        {
            var stamp = GameManager.Instance?.CurrentDate ?? DateTime.Now;
            if (stamp.Year <= 1) stamp = DateTime.Now;

            var msg = new InboxMessage
            {
                MessageId = Guid.NewGuid().ToString("N"),
                DateStr = stamp.ToString("o"),
                Type = type,
                Sender = sender ?? "",
                Title = title ?? "",
                Body = body ?? "",
                IsRead = false,
                IsHighPriority = highPriority,
                DeepLinkPanelId = deepLinkPanelId ?? "",
                DeepLinkPayload = deepLinkPayload ?? ""
            };

            _messages.Insert(0, msg);
            if (_messages.Count > MAX_MESSAGES)
                _messages.RemoveAt(_messages.Count - 1);

            OnMessagePublished?.Invoke(msg);
            OnUnreadCountChanged?.Invoke();
            return msg;
        }

        public IReadOnlyList<InboxMessage> GetAll() => _messages;

        public List<InboxMessage> GetByType(InboxMessageType type) =>
            _messages.FindAll(m => m.Type == type);

        public int UnreadCount => _messages.FindAll(m => !m.IsRead).Count;

        public void MarkRead(string messageId)
        {
            var msg = _messages.Find(m => m.MessageId == messageId);
            if (msg != null && !msg.IsRead)
            {
                msg.IsRead = true;
                OnUnreadCountChanged?.Invoke();
            }
        }

        public void MarkAllRead()
        {
            bool changed = false;
            foreach (var m in _messages)
            {
                if (!m.IsRead) { m.IsRead = true; changed = true; }
            }
            if (changed) OnUnreadCountChanged?.Invoke();
        }

        public void WriteSave(SaveData data)
        {
            data.Inbox = new List<InboxMessage>(_messages);
        }

        public void ReadSave(SaveData data, in SaveReadContext ctx)
        {
            _messages.Clear();
            if (data.Inbox != null) _messages.AddRange(data.Inbox);
            OnUnreadCountChanged?.Invoke();
        }
    }
}

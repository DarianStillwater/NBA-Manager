using System.Linq;
using UnityEngine;
using NBAHeadCoach.Core.Data;
using NBAHeadCoach.Core.Manager;

namespace NBAHeadCoach.Tests
{
    /// <summary>
    /// Guards the InboxService contract: publish ordering, unread tracking,
    /// mark-read events, capacity pruning, and the save-section round trip.
    /// </summary>
    public class InboxServiceTest : BaseTest
    {
        public override (int passed, int failed) RunAndReport()
        {
            _passed = 0; _failed = 0;

            TestPublishAndOrder();
            TestUnreadTracking();
            TestSaveRoundTrip();
            TestCapacityPrune();

            return (_passed, _failed);
        }

        private void TestPublishAndOrder()
        {
            var inbox = new InboxService();
            inbox.Publish(InboxMessageType.System, "League Office", "First", "body1");
            inbox.Publish(InboxMessageType.Trade, "League Office", "Second", "body2", highPriority: true);

            AssertEqual(2, inbox.GetAll().Count, "Two messages stored");
            AssertEqual("Second", inbox.GetAll()[0].Title, "Newest message first");
            Assert(inbox.GetAll()[0].IsHighPriority, "Priority flag stored");
            AssertEqual(1, inbox.GetByType(InboxMessageType.Trade).Count, "Type filter works");
            Assert(!string.IsNullOrEmpty(inbox.GetAll()[0].MessageId), "Message id assigned");
        }

        private void TestUnreadTracking()
        {
            var inbox = new InboxService();
            int badgeEvents = 0;
            inbox.OnUnreadCountChanged += () => badgeEvents++;

            var m1 = inbox.Publish(InboxMessageType.Injury, "Medical Staff", "Hurt", "out 10 days");
            var m2 = inbox.Publish(InboxMessageType.Media, "Media Relations", "Story", "");
            AssertEqual(2, inbox.UnreadCount, "Both messages unread");

            inbox.MarkRead(m1.MessageId);
            AssertEqual(1, inbox.UnreadCount, "MarkRead decrements unread");
            inbox.MarkRead(m1.MessageId);
            AssertEqual(1, inbox.UnreadCount, "Re-marking read is idempotent");

            inbox.MarkAllRead();
            AssertEqual(0, inbox.UnreadCount, "MarkAllRead clears unread");
            AssertGreaterThan(badgeEvents, 3, "Unread-changed event fires on publish and reads");
        }

        private void TestSaveRoundTrip()
        {
            var a = new InboxService();
            var msg = a.Publish(InboxMessageType.OwnerMessage, "Team Owner", "Expectations", "Win now",
                highPriority: true, deepLinkPanelId: "Dashboard", deepLinkPayload: "x1");
            a.MarkRead(msg.MessageId);
            a.Publish(InboxMessageType.Trade, "League Office", "Deal", "Details");

            var data = new SaveData();
            a.WriteSave(data);
            var parsed = JsonUtility.FromJson<SaveData>(JsonUtility.ToJson(data));

            var b = new InboxService();
            b.ReadSave(parsed, new SaveReadContext(SaveData.CURRENT_VERSION, false, 2025));

            AssertEqual(2, b.GetAll().Count, "Messages survive save round trip");
            AssertEqual(1, b.UnreadCount, "Read flags survive round trip");
            var restored = b.GetAll().First(m => m.Type == InboxMessageType.OwnerMessage);
            AssertEqual("Dashboard", restored.DeepLinkPanelId, "Deep link survives round trip");
            Assert(restored.Date.Year > 2000, "Date rehydrates from ISO string");
        }

        private void TestCapacityPrune()
        {
            var inbox = new InboxService();
            for (int i = 0; i < 520; i++)
                inbox.Publish(InboxMessageType.League, "League Office", $"msg{i}", "");

            AssertEqual(500, inbox.GetAll().Count, "Inbox capped at 500 messages");
            AssertEqual("msg519", inbox.GetAll()[0].Title, "Newest kept after prune");
        }
    }
}

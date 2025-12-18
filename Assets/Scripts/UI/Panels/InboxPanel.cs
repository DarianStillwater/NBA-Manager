using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using NBAHeadCoach.Core;
using NBAHeadCoach.Core.Data;

namespace NBAHeadCoach.UI.Panels
{
    /// <summary>
    /// Inbox panel for viewing messages from owner, media, agents, etc.
    /// </summary>
    public class InboxPanel : BasePanel
    {
        [Header("Message List")]
        [SerializeField] private Transform _messageListContainer;
        [SerializeField] private GameObject _messageRowPrefab;

        [Header("Filter Tabs")]
        [SerializeField] private Button _allTabButton;
        [SerializeField] private Button _ownerTabButton;
        [SerializeField] private Button _mediaTabButton;
        [SerializeField] private Button _agentTabButton;
        [SerializeField] private Button _leagueTabButton;

        [Header("Unread Counter")]
        [SerializeField] private Text _unreadCountText;

        [Header("Message Detail")]
        [SerializeField] private GameObject _messageDetailPanel;
        [SerializeField] private Text _senderNameText;
        [SerializeField] private Text _senderTitleText;
        [SerializeField] private Text _subjectText;
        [SerializeField] private Text _dateText;
        [SerializeField] private Text _messageBodyText;
        [SerializeField] private Image _senderAvatar;

        [Header("Response Options")]
        [SerializeField] private Transform _responseOptionsContainer;
        [SerializeField] private Button _deleteButton;
        [SerializeField] private Button _archiveButton;

        // State
        private List<InboxMessage> _allMessages = new List<InboxMessage>();
        private List<MessageRowUI> _messageRows = new List<MessageRowUI>();
        private InboxMessage _selectedMessage;
        private MessageCategory _currentFilter = MessageCategory.All;

        // Events
        public event Action<InboxMessage, int> OnMessageResponse;

        protected override void Awake()
        {
            base.Awake();
            SetupTabs();
            SetupButtons();
        }

        private void SetupTabs()
        {
            if (_allTabButton != null)
                _allTabButton.onClick.AddListener(() => FilterMessages(MessageCategory.All));

            if (_ownerTabButton != null)
                _ownerTabButton.onClick.AddListener(() => FilterMessages(MessageCategory.Owner));

            if (_mediaTabButton != null)
                _mediaTabButton.onClick.AddListener(() => FilterMessages(MessageCategory.Media));

            if (_agentTabButton != null)
                _agentTabButton.onClick.AddListener(() => FilterMessages(MessageCategory.Agent));

            if (_leagueTabButton != null)
                _leagueTabButton.onClick.AddListener(() => FilterMessages(MessageCategory.League));
        }

        private void SetupButtons()
        {
            if (_deleteButton != null)
                _deleteButton.onClick.AddListener(DeleteSelectedMessage);

            if (_archiveButton != null)
                _archiveButton.onClick.AddListener(ArchiveSelectedMessage);
        }

        protected override void OnShown()
        {
            base.OnShown();
            LoadMessages();
            RefreshMessageList();
            ClearMessageDetail();
            UpdateTabVisuals();
        }

        private void LoadMessages()
        {
            _allMessages.Clear();

            // Load messages from GameManager or generate sample messages
            if (GameManager.Instance != null)
            {
                // Would load from a message system
                // For now, generate contextual messages based on game state
                GenerateContextualMessages();
            }
        }

        private void GenerateContextualMessages()
        {
            var gm = GameManager.Instance;
            if (gm == null) return;

            var currentDate = gm.SeasonController?.CurrentDate ?? DateTime.Now;
            var team = gm.GetPlayerTeam();

            // Welcome message (first time)
            _allMessages.Add(new InboxMessage
            {
                Id = "msg_welcome",
                Category = MessageCategory.Owner,
                SenderName = "Team Owner",
                SenderTitle = $"{team?.City ?? "Team"} {team?.Name ?? ""} Owner",
                Subject = "Welcome to the Organization",
                Body = $"Welcome to the {team?.City ?? ""} {team?.Name ?? ""}, Coach!\n\n" +
                       "I'm excited to have you leading our franchise. We have high expectations this season, " +
                       "and I know you're the right person for the job.\n\n" +
                       "Remember, my door is always open if you need to discuss anything.\n\n" +
                       "Best of luck,\nThe Owner",
                Date = currentDate.AddDays(-1),
                IsRead = false,
                Priority = MessagePriority.High
            });

            // Season expectations message
            string expectations = GetOwnerExpectations(team);
            _allMessages.Add(new InboxMessage
            {
                Id = "msg_expectations",
                Category = MessageCategory.Owner,
                SenderName = "Team Owner",
                SenderTitle = "Franchise Owner",
                Subject = "Season Expectations",
                Body = $"Coach,\n\n{expectations}\n\nI'll be watching closely.\n\nThe Owner",
                Date = currentDate,
                IsRead = false,
                Priority = MessagePriority.High,
                ResponseOptions = new List<string> { "I understand", "We'll exceed expectations", "Let's discuss further" }
            });

            // League office message
            _allMessages.Add(new InboxMessage
            {
                Id = "msg_league",
                Category = MessageCategory.League,
                SenderName = "NBA Commissioner's Office",
                SenderTitle = "League Office",
                Subject = "Season Schedule Released",
                Body = "Coach,\n\nThe official NBA schedule has been released. You can view your team's " +
                       "complete 82-game schedule in the Calendar section.\n\n" +
                       "Key dates to remember:\n" +
                       "- Trade Deadline: February 6\n" +
                       "- All-Star Break: February 14-19\n" +
                       "- Playoffs Begin: April 19\n\n" +
                       "Good luck this season!\n\nNBA League Office",
                Date = currentDate.AddDays(-2),
                IsRead = true,
                Priority = MessagePriority.Normal
            });

            // Media request
            _allMessages.Add(new InboxMessage
            {
                Id = "msg_media_1",
                Category = MessageCategory.Media,
                SenderName = "ESPN",
                SenderTitle = "Sports Media",
                Subject = "Interview Request",
                Body = "Coach,\n\nWe'd like to schedule an interview to discuss your plans for the upcoming season " +
                       "and your coaching philosophy.\n\n" +
                       "Would you be available for a 15-minute segment this week?\n\n" +
                       "Best regards,\nESPN Sports",
                Date = currentDate,
                IsRead = false,
                Priority = MessagePriority.Low,
                ResponseOptions = new List<string> { "Accept interview", "Decline politely", "Reschedule" }
            });

            // Sort by date descending
            _allMessages = _allMessages.OrderByDescending(m => m.Date).ToList();
        }

        private string GetOwnerExpectations(Team team)
        {
            if (team == null) return "We expect you to do your best.";

            float avgOvr = team.Roster?.Count > 0 ? (float)team.Roster.Average(p => p.Overall) : 70;

            if (avgOvr >= 80)
                return "We have a championship-caliber roster. Anything less than a deep playoff run will be a disappointment. " +
                       "I expect us to be a top seed in the conference.";
            if (avgOvr >= 75)
                return "We should be competing for a playoff spot this season. I expect us to make the playoffs " +
                       "and be competitive in the first round.";
            if (avgOvr >= 70)
                return "We're in a rebuilding phase. I want to see development from our young players and " +
                       "competitive basketball. A playoff appearance would exceed expectations.";

            return "We're in a full rebuild. Focus on developing our young talent and building for the future. " +
                   "Wins aren't the priority - player growth is.";
        }

        #region Message List

        private void FilterMessages(MessageCategory category)
        {
            _currentFilter = category;
            RefreshMessageList();
            UpdateTabVisuals();
        }

        private void UpdateTabVisuals()
        {
            SetTabActive(_allTabButton, _currentFilter == MessageCategory.All);
            SetTabActive(_ownerTabButton, _currentFilter == MessageCategory.Owner);
            SetTabActive(_mediaTabButton, _currentFilter == MessageCategory.Media);
            SetTabActive(_agentTabButton, _currentFilter == MessageCategory.Agent);
            SetTabActive(_leagueTabButton, _currentFilter == MessageCategory.League);
        }

        private void SetTabActive(Button button, bool active)
        {
            if (button == null) return;

            var colors = button.colors;
            colors.normalColor = active ? new Color(0.3f, 0.3f, 0.5f) : new Color(0.2f, 0.2f, 0.3f);
            button.colors = colors;
        }

        private void RefreshMessageList()
        {
            // Clear existing
            foreach (var row in _messageRows)
            {
                if (row != null) Destroy(row.gameObject);
            }
            _messageRows.Clear();

            // Get filtered messages
            var filteredMessages = _currentFilter == MessageCategory.All
                ? _allMessages
                : _allMessages.Where(m => m.Category == _currentFilter).ToList();

            // Update unread count
            int unreadCount = _allMessages.Count(m => !m.IsRead);
            if (_unreadCountText != null)
            {
                _unreadCountText.text = unreadCount > 0 ? $"{unreadCount} unread" : "All read";
            }

            // Create rows
            foreach (var message in filteredMessages)
            {
                CreateMessageRow(message);
            }
        }

        private void CreateMessageRow(InboxMessage message)
        {
            GameObject rowGO;
            if (_messageRowPrefab != null && _messageListContainer != null)
            {
                rowGO = Instantiate(_messageRowPrefab, _messageListContainer);
            }
            else
            {
                rowGO = CreateDefaultMessageRow(message);
            }

            var rowUI = rowGO.GetComponent<MessageRowUI>();
            if (rowUI == null)
            {
                rowUI = rowGO.AddComponent<MessageRowUI>();
            }

            rowUI.Setup(message, OnMessageClicked);
            _messageRows.Add(rowUI);
        }

        private GameObject CreateDefaultMessageRow(InboxMessage message)
        {
            var rowGO = new GameObject($"Message_{message.Id}");
            if (_messageListContainer != null)
                rowGO.transform.SetParent(_messageListContainer, false);

            var bg = rowGO.AddComponent<Image>();
            bg.color = message.IsRead
                ? new Color(0.15f, 0.15f, 0.2f)
                : new Color(0.2f, 0.2f, 0.3f);

            var button = rowGO.AddComponent<Button>();

            var layout = rowGO.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 10;
            layout.childAlignment = TextAnchor.MiddleLeft;
            layout.padding = new RectOffset(10, 10, 8, 8);

            var layoutElement = rowGO.AddComponent<LayoutElement>();
            layoutElement.preferredHeight = 60;
            layoutElement.flexibleWidth = 1;

            // Priority indicator
            if (message.Priority == MessagePriority.High)
            {
                var priorityGO = new GameObject("Priority");
                priorityGO.transform.SetParent(rowGO.transform, false);
                var priorityText = priorityGO.AddComponent<Text>();
                priorityText.text = "!";
                priorityText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                priorityText.fontSize = 18;
                priorityText.fontStyle = FontStyle.Bold;
                priorityText.color = Color.red;
                var priorityLayout = priorityGO.AddComponent<LayoutElement>();
                priorityLayout.preferredWidth = 20;
            }

            // Content container
            var contentGO = new GameObject("Content");
            contentGO.transform.SetParent(rowGO.transform, false);
            var contentLayout = contentGO.AddComponent<VerticalLayoutGroup>();
            contentLayout.spacing = 2;
            var contentLayoutElement = contentGO.AddComponent<LayoutElement>();
            contentLayoutElement.flexibleWidth = 1;

            // Sender
            var senderGO = new GameObject("Sender");
            senderGO.transform.SetParent(contentGO.transform, false);
            var senderText = senderGO.AddComponent<Text>();
            senderText.text = message.SenderName;
            senderText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            senderText.fontSize = 14;
            senderText.fontStyle = message.IsRead ? FontStyle.Normal : FontStyle.Bold;
            senderText.color = Color.white;

            // Subject
            var subjectGO = new GameObject("Subject");
            subjectGO.transform.SetParent(contentGO.transform, false);
            var subjectText = subjectGO.AddComponent<Text>();
            subjectText.text = message.Subject;
            subjectText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            subjectText.fontSize = 12;
            subjectText.color = new Color(0.8f, 0.8f, 0.8f);

            // Date
            var dateGO = new GameObject("Date");
            dateGO.transform.SetParent(rowGO.transform, false);
            var dateText = dateGO.AddComponent<Text>();
            dateText.text = message.Date.ToString("MMM d");
            dateText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            dateText.fontSize = 11;
            dateText.color = new Color(0.6f, 0.6f, 0.6f);
            dateText.alignment = TextAnchor.MiddleRight;
            var dateLayout = dateGO.AddComponent<LayoutElement>();
            dateLayout.preferredWidth = 50;

            return rowGO;
        }

        private void OnMessageClicked(InboxMessage message)
        {
            _selectedMessage = message;
            message.IsRead = true;

            // Refresh list to update read status
            RefreshMessageList();

            // Show detail
            ShowMessageDetail(message);
        }

        #endregion

        #region Message Detail

        private void ClearMessageDetail()
        {
            _selectedMessage = null;
            if (_messageDetailPanel != null)
                _messageDetailPanel.SetActive(false);
        }

        private void ShowMessageDetail(InboxMessage message)
        {
            if (_messageDetailPanel != null)
                _messageDetailPanel.SetActive(true);

            if (_senderNameText != null)
                _senderNameText.text = message.SenderName;

            if (_senderTitleText != null)
                _senderTitleText.text = message.SenderTitle;

            if (_subjectText != null)
                _subjectText.text = message.Subject;

            if (_dateText != null)
                _dateText.text = message.Date.ToString("MMMM d, yyyy");

            if (_messageBodyText != null)
                _messageBodyText.text = message.Body;

            // Show response options if available
            RefreshResponseOptions(message);
        }

        private void RefreshResponseOptions(InboxMessage message)
        {
            if (_responseOptionsContainer == null) return;

            // Clear existing
            foreach (Transform child in _responseOptionsContainer)
            {
                Destroy(child.gameObject);
            }

            if (message.ResponseOptions == null || message.ResponseOptions.Count == 0)
            {
                _responseOptionsContainer.gameObject.SetActive(false);
                return;
            }

            _responseOptionsContainer.gameObject.SetActive(true);

            for (int i = 0; i < message.ResponseOptions.Count; i++)
            {
                CreateResponseButton(message, i, message.ResponseOptions[i]);
            }
        }

        private void CreateResponseButton(InboxMessage message, int index, string responseText)
        {
            var buttonGO = new GameObject($"Response_{index}");
            buttonGO.transform.SetParent(_responseOptionsContainer, false);

            var bg = buttonGO.AddComponent<Image>();
            bg.color = new Color(0.25f, 0.25f, 0.35f);

            var button = buttonGO.AddComponent<Button>();
            var colors = button.colors;
            colors.highlightedColor = new Color(0.35f, 0.35f, 0.5f);
            button.colors = colors;

            var layout = buttonGO.AddComponent<LayoutElement>();
            layout.preferredHeight = 35;
            layout.flexibleWidth = 1;

            int capturedIndex = index;
            button.onClick.AddListener(() => SelectResponse(message, capturedIndex));

            // Text
            var textGO = new GameObject("Text");
            textGO.transform.SetParent(buttonGO.transform, false);

            var text = textGO.AddComponent<Text>();
            text.text = responseText;
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = 13;
            text.color = Color.white;
            text.alignment = TextAnchor.MiddleCenter;

            var textRect = text.GetComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.sizeDelta = Vector2.zero;
        }

        private void SelectResponse(InboxMessage message, int responseIndex)
        {
            // Clear response options after selection
            message.ResponseOptions = null;
            message.HasResponded = true;

            OnMessageResponse?.Invoke(message, responseIndex);

            // Refresh detail view
            ShowMessageDetail(message);
        }

        #endregion

        #region Actions

        private void DeleteSelectedMessage()
        {
            if (_selectedMessage != null)
            {
                _allMessages.Remove(_selectedMessage);
                ClearMessageDetail();
                RefreshMessageList();
            }
        }

        private void ArchiveSelectedMessage()
        {
            if (_selectedMessage != null)
            {
                _selectedMessage.IsArchived = true;
                ClearMessageDetail();
                RefreshMessageList();
            }
        }

        /// <summary>
        /// Add a new message to the inbox
        /// </summary>
        public void AddMessage(InboxMessage message)
        {
            _allMessages.Insert(0, message);
            RefreshMessageList();
        }

        /// <summary>
        /// Get unread message count
        /// </summary>
        public int GetUnreadCount()
        {
            return _allMessages.Count(m => !m.IsRead);
        }

        #endregion
    }

    /// <summary>
    /// Inbox message data
    /// </summary>
    [Serializable]
    public class InboxMessage
    {
        public string Id;
        public MessageCategory Category;
        public string SenderName;
        public string SenderTitle;
        public string Subject;
        public string Body;
        public DateTime Date;
        public bool IsRead;
        public bool IsArchived;
        public bool HasResponded;
        public MessagePriority Priority;
        public List<string> ResponseOptions;
    }

    public enum MessageCategory
    {
        All,
        Owner,
        Media,
        Agent,
        League,
        Team
    }

    public enum MessagePriority
    {
        Low,
        Normal,
        High,
        Urgent
    }

    /// <summary>
    /// UI component for message rows
    /// </summary>
    public class MessageRowUI : MonoBehaviour
    {
        public InboxMessage Message { get; private set; }

        private Button _button;
        private Action<InboxMessage> _onClick;

        public void Setup(InboxMessage message, Action<InboxMessage> onClick)
        {
            Message = message;
            _onClick = onClick;

            _button = GetComponent<Button>();
            if (_button != null)
            {
                _button.onClick.RemoveAllListeners();
                _button.onClick.AddListener(() => _onClick?.Invoke(Message));
            }
        }
    }
}

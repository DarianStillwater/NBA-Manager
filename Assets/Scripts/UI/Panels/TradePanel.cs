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
    /// Trade panel for proposing and reviewing trades
    /// </summary>
    public class TradePanel : BasePanel
    {
        [Header("Team Selection")]
        [SerializeField] private Dropdown _tradePartnerDropdown;
        [SerializeField] private Text _partnerTeamInfoText;

        [Header("Your Team Assets")]
        [SerializeField] private Transform _yourPlayersContainer;
        [SerializeField] private Transform _yourPicksContainer;
        [SerializeField] private Text _yourTeamNameText;

        [Header("Their Team Assets")]
        [SerializeField] private Transform _theirPlayersContainer;
        [SerializeField] private Transform _theirPicksContainer;
        [SerializeField] private Text _theirTeamNameText;

        [Header("Trade Package - Sending")]
        [SerializeField] private Transform _sendingContainer;
        [SerializeField] private Text _sendingValueText;

        [Header("Trade Package - Receiving")]
        [SerializeField] private Transform _receivingContainer;
        [SerializeField] private Text _receivingValueText;

        [Header("Trade Evaluation")]
        [SerializeField] private Text _tradeStatusText;
        [SerializeField] private Slider _tradeBalanceSlider;
        [SerializeField] private Text _salaryImpactText;
        [SerializeField] private Text _aiAssessmentText;

        [Header("Buttons")]
        [SerializeField] private Button _proposeTradeButton;
        [SerializeField] private Button _clearTradeButton;
        [SerializeField] private Button _findTradesButton;
        [SerializeField] private Button _backButton;

        [Header("Trade Finder Results")]
        [SerializeField] private GameObject _tradeFinderPanel;
        [SerializeField] private Transform _suggestedTradesContainer;

        // State
        private Team _playerTeam;
        private Team _selectedPartnerTeam;
        private List<Player> _sendingPlayers = new List<Player>();
        private List<DraftPick> _sendingPicks = new List<DraftPick>();
        private List<Player> _receivingPlayers = new List<Player>();
        private List<DraftPick> _receivingPicks = new List<DraftPick>();
        private List<TradeAssetUI> _assetUIs = new List<TradeAssetUI>();

        // Events
        public event Action OnBackRequested;
        public event Action<TradeProposal> OnTradeProposed;

        protected override void Awake()
        {
            base.Awake();
            SetupControls();
        }

        private void SetupControls()
        {
            if (_tradePartnerDropdown != null)
            {
                _tradePartnerDropdown.onValueChanged.AddListener(OnPartnerTeamChanged);
            }

            if (_proposeTradeButton != null)
                _proposeTradeButton.onClick.AddListener(ProposeTrade);

            if (_clearTradeButton != null)
                _clearTradeButton.onClick.AddListener(ClearTrade);

            if (_findTradesButton != null)
                _findTradesButton.onClick.AddListener(FindTrades);

            if (_backButton != null)
                _backButton.onClick.AddListener(() => OnBackRequested?.Invoke());
        }

        protected override void OnShown()
        {
            base.OnShown();
            LoadTeams();
            ClearTrade();
        }

        private void LoadTeams()
        {
            _playerTeam = GameManager.Instance?.GetPlayerTeam();

            if (_yourTeamNameText != null && _playerTeam != null)
            {
                _yourTeamNameText.text = $"{_playerTeam.City} {_playerTeam.Name}";
            }

            // Populate trade partner dropdown
            PopulatePartnerDropdown();

            // Populate your assets
            PopulateYourAssets();
        }

        private void PopulatePartnerDropdown()
        {
            if (_tradePartnerDropdown == null) return;

            _tradePartnerDropdown.ClearOptions();

            var allTeams = GameManager.Instance?.AllTeams;
            if (allTeams == null) return;

            var options = new List<string>();
            foreach (var team in allTeams.Where(t => t.TeamId != _playerTeam?.TeamId))
            {
                options.Add($"{team.City} {team.Name}");
            }

            _tradePartnerDropdown.AddOptions(options);

            if (options.Count > 0)
            {
                OnPartnerTeamChanged(0);
            }
        }

        private void OnPartnerTeamChanged(int index)
        {
            var allTeams = GameManager.Instance?.AllTeams;
            if (allTeams == null) return;

            var partnerTeams = allTeams.Where(t => t.TeamId != _playerTeam?.TeamId).ToList();
            if (index >= 0 && index < partnerTeams.Count)
            {
                _selectedPartnerTeam = partnerTeams[index];

                if (_theirTeamNameText != null)
                {
                    _theirTeamNameText.text = $"{_selectedPartnerTeam.City} {_selectedPartnerTeam.Name}";
                }

                if (_partnerTeamInfoText != null)
                {
                    _partnerTeamInfoText.text = $"Record: {_selectedPartnerTeam.Wins}-{_selectedPartnerTeam.Losses}";
                }

                PopulateTheirAssets();
                ClearTradePackage();
            }
        }

        #region Asset Display

        private void PopulateYourAssets()
        {
            ClearContainer(_yourPlayersContainer);
            ClearContainer(_yourPicksContainer);

            if (_playerTeam?.Roster == null) return;

            foreach (var player in _playerTeam.Roster.OrderByDescending(p => p.Overall))
            {
                CreatePlayerAsset(player, _yourPlayersContainer, true);
            }

            // Add draft picks (would come from team data)
            CreateSampleDraftPicks(_yourPicksContainer, true);
        }

        private void PopulateTheirAssets()
        {
            ClearContainer(_theirPlayersContainer);
            ClearContainer(_theirPicksContainer);

            if (_selectedPartnerTeam?.Roster == null) return;

            foreach (var player in _selectedPartnerTeam.Roster.OrderByDescending(p => p.Overall))
            {
                CreatePlayerAsset(player, _theirPlayersContainer, false);
            }

            CreateSampleDraftPicks(_theirPicksContainer, false);
        }

        private void CreatePlayerAsset(Player player, Transform container, bool isYourTeam)
        {
            var assetGO = new GameObject($"Player_{player.PlayerId}");
            assetGO.transform.SetParent(container, false);

            var bg = assetGO.AddComponent<Image>();
            bg.color = new Color(0.2f, 0.2f, 0.25f);

            var button = assetGO.AddComponent<Button>();
            var colors = button.colors;
            colors.highlightedColor = new Color(0.3f, 0.3f, 0.4f);
            button.colors = colors;

            var layout = assetGO.AddComponent<LayoutElement>();
            layout.preferredHeight = 35;
            layout.flexibleWidth = 1;

            // Content
            var contentGO = new GameObject("Content");
            contentGO.transform.SetParent(assetGO.transform, false);
            var contentLayout = contentGO.AddComponent<HorizontalLayoutGroup>();
            contentLayout.spacing = 5;
            contentLayout.padding = new RectOffset(5, 5, 2, 2);
            contentLayout.childAlignment = TextAnchor.MiddleLeft;

            var contentRect = contentGO.GetComponent<RectTransform>();
            contentRect.anchorMin = Vector2.zero;
            contentRect.anchorMax = Vector2.one;
            contentRect.sizeDelta = Vector2.zero;

            // Position
            AddText(contentGO.transform, player.PositionString, 30);

            // Name
            AddText(contentGO.transform, $"{player.FirstName[0]}. {player.LastName}", 120, TextAnchor.MiddleLeft);

            // Overall
            var ovrText = AddText(contentGO.transform, player.Overall.ToString(), 35);
            ovrText.color = GetOverallColor(player.Overall);

            // Salary
            long salary = player.AnnualSalary;
            AddText(contentGO.transform, $"${salary / 1000000f:F1}M", 55, TextAnchor.MiddleRight);

            // Click handler
            var capturedPlayer = player;
            var capturedIsYour = isYourTeam;
            button.onClick.AddListener(() => OnAssetClicked(capturedPlayer, capturedIsYour));

            var assetUI = assetGO.AddComponent<TradeAssetUI>();
            assetUI.Player = player;
            assetUI.IsYourTeam = isYourTeam;
            _assetUIs.Add(assetUI);
        }

        private void CreateSampleDraftPicks(Transform container, bool isYourTeam)
        {
            // Create sample picks for current year
            int year = GameManager.Instance?.SeasonController?.CurrentSeason ?? 2024;

            CreateDraftPickAsset(new DraftPick { Year = year + 1, Round = 1, OriginalTeam = isYourTeam ? _playerTeam?.TeamId : _selectedPartnerTeam?.TeamId },
                container, isYourTeam);
            CreateDraftPickAsset(new DraftPick { Year = year + 1, Round = 2, OriginalTeam = isYourTeam ? _playerTeam?.TeamId : _selectedPartnerTeam?.TeamId },
                container, isYourTeam);
        }

        private void CreateDraftPickAsset(DraftPick pick, Transform container, bool isYourTeam)
        {
            var assetGO = new GameObject($"Pick_{pick.Year}_{pick.Round}");
            assetGO.transform.SetParent(container, false);

            var bg = assetGO.AddComponent<Image>();
            bg.color = new Color(0.25f, 0.2f, 0.2f);

            var button = assetGO.AddComponent<Button>();

            var layout = assetGO.AddComponent<LayoutElement>();
            layout.preferredHeight = 30;
            layout.flexibleWidth = 1;

            // Text
            var textGO = new GameObject("Text");
            textGO.transform.SetParent(assetGO.transform, false);
            var text = textGO.AddComponent<Text>();
            text.text = $"{pick.Year} Round {pick.Round} Pick";
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = 12;
            text.color = Color.white;
            text.alignment = TextAnchor.MiddleCenter;

            var textRect = text.GetComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.sizeDelta = Vector2.zero;

            var capturedPick = pick;
            var capturedIsYour = isYourTeam;
            button.onClick.AddListener(() => OnPickClicked(capturedPick, capturedIsYour));
        }

        private Text AddText(Transform parent, string text, float width, TextAnchor align = TextAnchor.MiddleCenter)
        {
            var textGO = new GameObject("Text");
            textGO.transform.SetParent(parent, false);
            var textComp = textGO.AddComponent<Text>();
            textComp.text = text;
            textComp.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            textComp.fontSize = 12;
            textComp.color = Color.white;
            textComp.alignment = align;

            var layout = textGO.AddComponent<LayoutElement>();
            layout.preferredWidth = width;

            return textComp;
        }

        private void ClearContainer(Transform container)
        {
            if (container == null) return;

            foreach (Transform child in container)
            {
                Destroy(child.gameObject);
            }
        }

        private Color GetOverallColor(int overall)
        {
            if (overall >= 85) return new Color(0.3f, 0.8f, 0.3f);
            if (overall >= 75) return new Color(0.5f, 0.7f, 0.9f);
            if (overall >= 65) return new Color(0.9f, 0.9f, 0.5f);
            return new Color(0.7f, 0.7f, 0.7f);
        }

        #endregion

        #region Trade Building

        private void OnAssetClicked(Player player, bool isYourTeam)
        {
            if (isYourTeam)
            {
                if (_sendingPlayers.Contains(player))
                {
                    _sendingPlayers.Remove(player);
                }
                else
                {
                    _sendingPlayers.Add(player);
                }
            }
            else
            {
                if (_receivingPlayers.Contains(player))
                {
                    _receivingPlayers.Remove(player);
                }
                else
                {
                    _receivingPlayers.Add(player);
                }
            }

            RefreshTradePackage();
        }

        private void OnPickClicked(DraftPick pick, bool isYourTeam)
        {
            if (isYourTeam)
            {
                if (_sendingPicks.Contains(pick))
                    _sendingPicks.Remove(pick);
                else
                    _sendingPicks.Add(pick);
            }
            else
            {
                if (_receivingPicks.Contains(pick))
                    _receivingPicks.Remove(pick);
                else
                    _receivingPicks.Add(pick);
            }

            RefreshTradePackage();
        }

        private void RefreshTradePackage()
        {
            // Update sending display
            RefreshPackageDisplay(_sendingContainer, _sendingPlayers, _sendingPicks);

            // Update receiving display
            RefreshPackageDisplay(_receivingContainer, _receivingPlayers, _receivingPicks);

            // Calculate values
            float sendingValue = CalculateTradeValue(_sendingPlayers, _sendingPicks);
            float receivingValue = CalculateTradeValue(_receivingPlayers, _receivingPicks);

            if (_sendingValueText != null)
                _sendingValueText.text = $"Value: {sendingValue:F0}";

            if (_receivingValueText != null)
                _receivingValueText.text = $"Value: {receivingValue:F0}";

            // Update balance slider
            if (_tradeBalanceSlider != null)
            {
                float total = sendingValue + receivingValue;
                if (total > 0)
                {
                    _tradeBalanceSlider.value = receivingValue / total;
                }
            }

            // Update trade status
            UpdateTradeStatus(sendingValue, receivingValue);

            // Update salary impact
            UpdateSalaryImpact();

            // Highlight selected assets
            UpdateAssetHighlights();
        }

        private void RefreshPackageDisplay(Transform container, List<Player> players, List<DraftPick> picks)
        {
            ClearContainer(container);

            foreach (var player in players)
            {
                var entryGO = new GameObject(player.LastName);
                entryGO.transform.SetParent(container, false);

                var text = entryGO.AddComponent<Text>();
                text.text = $"{player.PositionString} {player.FirstName[0]}. {player.LastName} ({player.Overall})";
                text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                text.fontSize = 12;
                text.color = Color.white;

                var layout = entryGO.AddComponent<LayoutElement>();
                layout.preferredHeight = 20;
            }

            foreach (var pick in picks)
            {
                var entryGO = new GameObject($"Pick_{pick.Year}");
                entryGO.transform.SetParent(container, false);

                var text = entryGO.AddComponent<Text>();
                text.text = $"{pick.Year} R{pick.Round} Pick";
                text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                text.fontSize = 12;
                text.color = new Color(0.9f, 0.8f, 0.5f);

                var layout = entryGO.AddComponent<LayoutElement>();
                layout.preferredHeight = 20;
            }
        }

        private float CalculateTradeValue(List<Player> players, List<DraftPick> picks)
        {
            float value = 0;

            foreach (var player in players)
            {
                // Base value from overall
                value += player.Overall * 10;

                // Age factor
                if (player.Age < 25) value += 200;
                else if (player.Age > 32) value -= 100;

                // Contract value (cheaper is better for receiving)
                long salary = player.AnnualSalary;
                if (salary < 10000000) value += 50;
            }

            foreach (var pick in picks)
            {
                // First round picks are more valuable
                value += pick.Round == 1 ? 300 : 100;
            }

            return value;
        }

        private void UpdateTradeStatus(float sendingValue, float receivingValue)
        {
            if (_tradeStatusText == null) return;

            float ratio = sendingValue > 0 ? receivingValue / sendingValue : 0;

            if (_sendingPlayers.Count == 0 && _receivingPlayers.Count == 0)
            {
                _tradeStatusText.text = "Select players to trade";
                _tradeStatusText.color = Color.gray;
            }
            else if (ratio < 0.7f)
            {
                _tradeStatusText.text = "Trade unlikely - you're giving up too much";
                _tradeStatusText.color = Color.red;
            }
            else if (ratio > 1.3f)
            {
                _tradeStatusText.text = "Trade unlikely - asking for too much";
                _tradeStatusText.color = new Color(1f, 0.5f, 0f);
            }
            else
            {
                _tradeStatusText.text = "Trade appears fair - likely to be accepted";
                _tradeStatusText.color = Color.green;
            }

            // AI assessment
            if (_aiAssessmentText != null)
            {
                _aiAssessmentText.text = GenerateAIAssessment(ratio);
            }
        }

        private string GenerateAIAssessment(float ratio)
        {
            if (_receivingPlayers.Count == 0 && _sendingPlayers.Count == 0)
                return "";

            var assessment = new List<string>();

            if (_receivingPlayers.Any(p => p.Overall >= 85))
                assessment.Add("Acquiring a star player");

            if (_sendingPlayers.Any(p => p.Age < 24 && p.Overall >= 70))
                assessment.Add("Giving up young talent");

            if (_receivingPlayers.Sum(p => p.AnnualSalary) >
                _sendingPlayers.Sum(p => p.AnnualSalary))
                assessment.Add("Increasing salary");

            if (ratio > 1.1f)
                assessment.Add("May need to sweeten deal");

            return string.Join(" | ", assessment);
        }

        private void UpdateSalaryImpact()
        {
            if (_salaryImpactText == null) return;

            long outgoing = _sendingPlayers.Sum(p => p.AnnualSalary);
            long incoming = _receivingPlayers.Sum(p => p.AnnualSalary);
            long net = incoming - outgoing;

            _salaryImpactText.text = net >= 0
                ? $"Salary Impact: +${net / 1000000f:F1}M"
                : $"Salary Impact: -${Math.Abs(net) / 1000000f:F1}M";

            _salaryImpactText.color = net <= 0 ? Color.green : Color.yellow;
        }

        private void UpdateAssetHighlights()
        {
            foreach (var assetUI in _assetUIs)
            {
                if (assetUI == null || assetUI.Player == null) continue;

                bool isSelected = assetUI.IsYourTeam
                    ? _sendingPlayers.Contains(assetUI.Player)
                    : _receivingPlayers.Contains(assetUI.Player);

                var bg = assetUI.GetComponent<Image>();
                if (bg != null)
                {
                    bg.color = isSelected
                        ? new Color(0.3f, 0.4f, 0.3f)
                        : new Color(0.2f, 0.2f, 0.25f);
                }
            }
        }

        #endregion

        #region Actions

        private void ClearTrade()
        {
            _sendingPlayers.Clear();
            _sendingPicks.Clear();
            _receivingPlayers.Clear();
            _receivingPicks.Clear();

            ClearTradePackage();
        }

        private void ClearTradePackage()
        {
            ClearContainer(_sendingContainer);
            ClearContainer(_receivingContainer);

            if (_sendingValueText != null) _sendingValueText.text = "Value: 0";
            if (_receivingValueText != null) _receivingValueText.text = "Value: 0";
            if (_tradeStatusText != null)
            {
                _tradeStatusText.text = "Select players to trade";
                _tradeStatusText.color = Color.gray;
            }

            UpdateAssetHighlights();
        }

        private void ProposeTrade()
        {
            if (_sendingPlayers.Count == 0 && _receivingPlayers.Count == 0)
            {
                Debug.Log("[TradePanel] Cannot propose empty trade");
                return;
            }

            var proposal = new TradeProposal
            {
                ProposingTeamId = _playerTeam?.TeamId,
                ReceivingTeamId = _selectedPartnerTeam?.TeamId,
                PlayersToSend = _sendingPlayers.Select(p => p.PlayerId).ToList(),
                PlayersToReceive = _receivingPlayers.Select(p => p.PlayerId).ToList(),
                PicksToSend = _sendingPicks,
                PicksToReceive = _receivingPicks
            };

            OnTradeProposed?.Invoke(proposal);

            // Simulate AI response
            SimulateTradeResponse(proposal);
        }

        private void SimulateTradeResponse(TradeProposal proposal)
        {
            float sendingValue = CalculateTradeValue(_sendingPlayers, _sendingPicks);
            float receivingValue = CalculateTradeValue(_receivingPlayers, _receivingPicks);
            float ratio = sendingValue > 0 ? receivingValue / sendingValue : 0;

            bool accepted = ratio >= 0.8f && ratio <= 1.2f;

            if (accepted)
            {
                Debug.Log("[TradePanel] Trade ACCEPTED!");
                // Would execute trade through TradeSystem
            }
            else
            {
                Debug.Log($"[TradePanel] Trade REJECTED - ratio: {ratio:F2}");
            }
        }

        private void FindTrades()
        {
            if (_tradeFinderPanel != null)
            {
                _tradeFinderPanel.SetActive(!_tradeFinderPanel.activeSelf);
            }

            // Would use TradeFinder to suggest trades
            Debug.Log("[TradePanel] Finding suggested trades...");
        }

        /// <summary>
        /// Pre-select a player for trading (from roster panel)
        /// </summary>
        public void PreSelectPlayer(Player player)
        {
            if (player != null && !_sendingPlayers.Contains(player))
            {
                _sendingPlayers.Add(player);
                RefreshTradePackage();
            }
        }

        #endregion
    }

    /// <summary>
    /// Trade proposal data
    /// </summary>
    [Serializable]
    public class TradeProposal
    {
        public string ProposingTeamId;
        public string ReceivingTeamId;
        public List<string> PlayersToSend = new List<string>();
        public List<string> PlayersToReceive = new List<string>();
        public List<DraftPick> PicksToSend = new List<DraftPick>();
        public List<DraftPick> PicksToReceive = new List<DraftPick>();
    }

    /// <summary>
    /// Draft pick data
    /// </summary>
    [Serializable]
    public class DraftPick
    {
        public int Year;
        public int Round;
        public string OriginalTeam;
        public string CurrentOwner;
        public bool IsProtected;
        public string ProtectionDetails;
    }

    /// <summary>
    /// UI component for trade assets
    /// </summary>
    public class TradeAssetUI : MonoBehaviour
    {
        public Player Player;
        public DraftPick Pick;
        public bool IsYourTeam;
    }
}

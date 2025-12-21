using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using NBAHeadCoach.Core;
using NBAHeadCoach.Core.Data;
using NBAHeadCoach.Core.AI;

namespace NBAHeadCoach.UI.Panels
{
    /// <summary>
    /// UI panel for coaches to submit roster requests to the AI GM.
    /// Only available in Coach-Only mode.
    /// </summary>
    public class RosterRequestPanel : BasePanel
    {
        [Header("Request Type Selection")]
        [SerializeField] private Dropdown _requestTypeDropdown;
        [SerializeField] private Dropdown _priorityDropdown;

        [Header("Player Selection")]
        [SerializeField] private GameObject _playerSelectionGroup;
        [SerializeField] private Dropdown _targetPlayerDropdown;
        [SerializeField] private Dropdown _tradeAwayDropdown;
        [SerializeField] private Text _targetPlayerLabel;
        [SerializeField] private Text _tradeAwayLabel;

        [Header("Request Details")]
        [SerializeField] private InputField _reasoningInput;
        [SerializeField] private Text _requestSummaryText;

        [Header("GM Info")]
        [SerializeField] private Text _gmNameText;
        [SerializeField] private Text _gmPersonalityText;
        [SerializeField] private Text _approvalRateText;

        [Header("Request History")]
        [SerializeField] private Transform _historyContainer;
        [SerializeField] private GameObject _historyRowPrefab;
        [SerializeField] private Text _pendingCountText;

        [Header("Buttons")]
        [SerializeField] private Button _submitButton;
        [SerializeField] private Button _closeButton;
        [SerializeField] private Button _viewHistoryButton;

        [Header("Response Popup")]
        [SerializeField] private GameObject _responsePopup;
        [SerializeField] private Text _responseText;
        [SerializeField] private Text _revealedTraitText;
        [SerializeField] private Button _acknowledgeButton;

        // Data
        private List<Player> _currentRoster;
        private List<Player> _availablePlayers;
        private RosterRequestType _selectedType;
        private RequestPriority _selectedPriority;
        private AIGMController _gmController;

        public override void Initialize()
        {
            base.Initialize();

            SetupDropdowns();
            SetupButtons();

            if (_responsePopup != null)
                _responsePopup.SetActive(false);
        }

        protected override void OnShown()
        {
            base.OnShown();
            RefreshData();
            UpdateUI();
        }

        private void SetupDropdowns()
        {
            // Request type dropdown
            if (_requestTypeDropdown != null)
            {
                _requestTypeDropdown.ClearOptions();
                _requestTypeDropdown.AddOptions(new List<string>
                {
                    "Trade for Player",
                    "Sign Free Agent",
                    "Waive Player",
                    "Extend Contract",
                    "Need: Big Man",
                    "Need: Guard",
                    "Need: Shooter",
                    "Need: Defender",
                    "Need: Veteran",
                    "Request Budget Increase",
                    "Trade for Draft Pick"
                });
                _requestTypeDropdown.onValueChanged.AddListener(OnRequestTypeChanged);
            }

            // Priority dropdown
            if (_priorityDropdown != null)
            {
                _priorityDropdown.ClearOptions();
                _priorityDropdown.AddOptions(new List<string>
                {
                    "Low - Nice to have",
                    "Medium - Would help",
                    "High - Significant need",
                    "Critical - Urgent"
                });
                _priorityDropdown.value = 1;  // Default to Medium
            }
        }

        private void SetupButtons()
        {
            if (_submitButton != null)
                _submitButton.onClick.AddListener(OnSubmitClicked);

            if (_closeButton != null)
                _closeButton.onClick.AddListener(Hide);

            if (_viewHistoryButton != null)
                _viewHistoryButton.onClick.AddListener(ShowHistory);

            if (_acknowledgeButton != null)
                _acknowledgeButton.onClick.AddListener(OnAcknowledgeClicked);
        }

        private void RefreshData()
        {
            // Get GM controller
            _gmController = AIGMController.Instance;

            // Get roster data
            var team = GameManager.Instance?.GetPlayerTeam();
            _currentRoster = team?.Roster ?? new List<Player>();

            // TODO: Get available free agents
            _availablePlayers = new List<Player>();

            // Update player dropdowns
            UpdatePlayerDropdowns();
        }

        private void UpdateUI()
        {
            UpdateGMInfo();
            UpdateRequestSummary();
            UpdatePendingCount();
            OnRequestTypeChanged(0);
        }

        private void UpdateGMInfo()
        {
            var aiGM = GameManager.Instance?.GetAIGM();

            if (_gmNameText != null)
            {
                _gmNameText.text = aiGM?.FullName ?? "General Manager";
            }

            if (_gmPersonalityText != null && _gmController != null)
            {
                _gmPersonalityText.text = _gmController.GetKnownPersonalityDescription();
            }

            if (_approvalRateText != null && _gmController != null)
            {
                float rate = _gmController.RequestHistory.GetApprovalRate();
                _approvalRateText.text = $"Approval Rate: {rate:P0}";
            }
        }

        private void UpdatePlayerDropdowns()
        {
            // Target player dropdown (current roster for trades/waives, or free agents)
            if (_targetPlayerDropdown != null)
            {
                _targetPlayerDropdown.ClearOptions();
                var options = new List<string> { "Select Player..." };

                foreach (var player in _currentRoster)
                {
                    options.Add($"{player.FirstName} {player.LastName} ({player.Position})");
                }

                _targetPlayerDropdown.AddOptions(options);
            }

            // Trade away dropdown (our players to offer)
            if (_tradeAwayDropdown != null)
            {
                _tradeAwayDropdown.ClearOptions();
                var options = new List<string> { "Select Player..." };

                foreach (var player in _currentRoster)
                {
                    options.Add($"{player.FirstName} {player.LastName} (OVR: {player.Overall})");
                }

                _tradeAwayDropdown.AddOptions(options);
            }
        }

        private void OnRequestTypeChanged(int index)
        {
            _selectedType = index switch
            {
                0 => RosterRequestType.TradePlayer,
                1 => RosterRequestType.SignFreeAgent,
                2 => RosterRequestType.WaivePlayer,
                3 => RosterRequestType.ExtendContract,
                4 => RosterRequestType.AcquireBigMan,
                5 => RosterRequestType.AcquireGuard,
                6 => RosterRequestType.AcquireShooter,
                7 => RosterRequestType.AcquireDefender,
                8 => RosterRequestType.AcquireVeteran,
                9 => RosterRequestType.IncreaseBudget,
                10 => RosterRequestType.TradeForPick,
                _ => RosterRequestType.TradePlayer
            };

            // Show/hide player selection based on request type
            bool showPlayerSelection = _selectedType switch
            {
                RosterRequestType.TradePlayer => true,
                RosterRequestType.SignFreeAgent => true,
                RosterRequestType.WaivePlayer => true,
                RosterRequestType.ExtendContract => true,
                _ => false
            };

            bool showTradeAway = _selectedType == RosterRequestType.TradePlayer;

            if (_playerSelectionGroup != null)
                _playerSelectionGroup.SetActive(showPlayerSelection);

            if (_tradeAwayDropdown != null)
                _tradeAwayDropdown.gameObject.SetActive(showTradeAway);

            if (_tradeAwayLabel != null)
                _tradeAwayLabel.gameObject.SetActive(showTradeAway);

            // Update labels
            if (_targetPlayerLabel != null)
            {
                _targetPlayerLabel.text = _selectedType switch
                {
                    RosterRequestType.TradePlayer => "Player to Acquire:",
                    RosterRequestType.SignFreeAgent => "Free Agent:",
                    RosterRequestType.WaivePlayer => "Player to Waive:",
                    RosterRequestType.ExtendContract => "Player to Extend:",
                    _ => "Player:"
                };
            }

            UpdateRequestSummary();
        }

        private void UpdateRequestSummary()
        {
            if (_requestSummaryText == null) return;

            string summary = _selectedType switch
            {
                RosterRequestType.TradePlayer => "Request the GM to acquire a player via trade.",
                RosterRequestType.SignFreeAgent => "Request to sign a free agent to the roster.",
                RosterRequestType.WaivePlayer => "Request to release a player from the roster.",
                RosterRequestType.ExtendContract => "Request to extend a player's contract.",
                RosterRequestType.AcquireBigMan => "Express need for frontcourt help (C/PF).",
                RosterRequestType.AcquireGuard => "Express need for backcourt help (PG/SG).",
                RosterRequestType.AcquireShooter => "Express need for perimeter shooting.",
                RosterRequestType.AcquireDefender => "Express need for defensive help.",
                RosterRequestType.AcquireVeteran => "Express need for veteran leadership.",
                RosterRequestType.IncreaseBudget => "Request additional spending flexibility.",
                RosterRequestType.TradeForPick => "Request to acquire draft picks.",
                _ => ""
            };

            _requestSummaryText.text = summary;
        }

        private void UpdatePendingCount()
        {
            if (_pendingCountText == null || _gmController == null) return;

            int pending = _gmController.RequestHistory.PendingRequests;
            _pendingCountText.text = pending > 0 ? $"Pending: {pending}" : "";
        }

        private void OnSubmitClicked()
        {
            if (_gmController == null)
            {
                Debug.LogError("[RosterRequestPanel] No GM controller available");
                return;
            }

            // Get selected priority
            _selectedPriority = _priorityDropdown?.value switch
            {
                0 => RequestPriority.Low,
                1 => RequestPriority.Medium,
                2 => RequestPriority.High,
                3 => RequestPriority.Critical,
                _ => RequestPriority.Medium
            };

            // Create request based on type
            RosterRequest request = CreateRequest();
            if (request == null)
            {
                Debug.LogWarning("[RosterRequestPanel] Failed to create request");
                return;
            }

            // Submit to GM
            var result = _gmController.ProcessRequest(request);

            // Show response
            ShowResponse(result);

            // Update UI
            UpdateGMInfo();
            UpdatePendingCount();
        }

        private RosterRequest CreateRequest()
        {
            string reasoning = _reasoningInput?.text ?? "Team needs";

            switch (_selectedType)
            {
                case RosterRequestType.TradePlayer:
                    {
                        int targetIdx = (_targetPlayerDropdown?.value ?? 0) - 1;
                        int tradeIdx = (_tradeAwayDropdown?.value ?? 0) - 1;

                        if (targetIdx < 0 || tradeIdx < 0 || tradeIdx >= _currentRoster.Count)
                            return null;

                        var tradeAway = _currentRoster[tradeIdx];
                        return RosterRequest.CreateTradeRequest(
                            $"target_{targetIdx}",
                            "Target Player",
                            tradeAway.PlayerId,
                            $"{tradeAway.FirstName} {tradeAway.LastName}",
                            reasoning
                        );
                    }

                case RosterRequestType.SignFreeAgent:
                    return RosterRequest.CreateSigningRequest(
                        "fa_target",
                        "Free Agent",
                        reasoning
                    );

                case RosterRequestType.WaivePlayer:
                    {
                        int idx = (_targetPlayerDropdown?.value ?? 0) - 1;
                        if (idx < 0 || idx >= _currentRoster.Count) return null;

                        var player = _currentRoster[idx];
                        return RosterRequest.CreateWaiveRequest(
                            player.PlayerId,
                            $"{player.FirstName} {player.LastName}",
                            reasoning
                        );
                    }

                case RosterRequestType.ExtendContract:
                    {
                        int idx = (_targetPlayerDropdown?.value ?? 0) - 1;
                        if (idx < 0 || idx >= _currentRoster.Count) return null;

                        var player = _currentRoster[idx];
                        return new RosterRequest
                        {
                            Type = RosterRequestType.ExtendContract,
                            Priority = _selectedPriority,
                            TargetPlayerId = player.PlayerId,
                            TargetPlayerName = $"{player.FirstName} {player.LastName}",
                            CoachReasoning = reasoning
                        };
                    }

                default:
                    return RosterRequest.CreateNeedRequest(_selectedType, _selectedPriority, reasoning);
            }
        }

        private void ShowResponse(RosterRequestResult result)
        {
            if (_responsePopup == null || result == null) return;

            _responsePopup.SetActive(true);

            if (_responseText != null)
            {
                string statusText = result.IsApproved ? "[APPROVED]" : "[DENIED]";
                Color statusColor = result.IsApproved ? new Color(0.2f, 0.7f, 0.2f) : new Color(0.8f, 0.3f, 0.3f);

                _responseText.text = $"{statusText}\n\n\"{result.GMResponse}\"";
                _responseText.color = statusColor;

                if (result.IsApproved && !string.IsNullOrEmpty(result.ActionTaken))
                {
                    _responseText.text += $"\n\n{result.ActionTaken}";
                }
            }

            if (_revealedTraitText != null)
            {
                if (!string.IsNullOrEmpty(result.RevealedTrait))
                {
                    _revealedTraitText.gameObject.SetActive(true);
                    _revealedTraitText.text = $"[INSIGHT] {result.RevealedTrait}";
                }
                else
                {
                    _revealedTraitText.gameObject.SetActive(false);
                }
            }
        }

        private void OnAcknowledgeClicked()
        {
            if (_responsePopup != null)
                _responsePopup.SetActive(false);

            // Clear input
            if (_reasoningInput != null)
                _reasoningInput.text = "";

            // Reset dropdowns
            if (_targetPlayerDropdown != null)
                _targetPlayerDropdown.value = 0;
            if (_tradeAwayDropdown != null)
                _tradeAwayDropdown.value = 0;
        }

        private void ShowHistory()
        {
            if (_historyContainer == null || _gmController == null) return;

            // Clear existing
            for (int i = _historyContainer.childCount - 1; i >= 0; i--)
            {
                Destroy(_historyContainer.GetChild(i).gameObject);
            }

            // Show recent requests
            var requests = _gmController.RequestHistory.AllRequests;
            int shown = 0;

            for (int i = requests.Count - 1; i >= 0 && shown < 10; i--, shown++)
            {
                CreateHistoryRow(requests[i]);
            }
        }

        private void CreateHistoryRow(RosterRequest request)
        {
            var rowGO = new GameObject($"Request_{request.RequestId}");
            rowGO.transform.SetParent(_historyContainer, false);

            var layoutElement = rowGO.AddComponent<LayoutElement>();
            layoutElement.preferredHeight = 35;

            var textGO = new GameObject("Text");
            textGO.transform.SetParent(rowGO.transform, false);

            var text = textGO.AddComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = 12;

            string status = request.Status switch
            {
                RequestStatus.Approved => "[OK]",
                RequestStatus.Denied => "[NO]",
                RequestStatus.Pending => "[...]",
                _ => ""
            };

            text.text = $"{request.RequestDate:MM/dd} {status} {request.Type}: {request.CoachReasoning}";
            text.color = request.Status switch
            {
                RequestStatus.Approved => new Color(0.5f, 0.8f, 0.5f),
                RequestStatus.Denied => new Color(0.8f, 0.5f, 0.5f),
                _ => Color.white
            };

            var rect = text.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }
    }
}

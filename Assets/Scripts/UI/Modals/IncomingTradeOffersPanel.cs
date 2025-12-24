using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using NBAHeadCoach.Core.Data;
using NBAHeadCoach.Core.Manager;

namespace NBAHeadCoach.UI.Modals
{
    /// <summary>
    /// SlidePanel showing incoming trade offers from AI teams.
    /// Allows user to accept, reject, or counter offers.
    /// </summary>
    public class IncomingTradeOffersPanel : SlidePanel
    {
        [Header("Header")]
        [SerializeField] private Text _titleText;
        [SerializeField] private Text _offerCountText;

        [Header("Offer List")]
        [SerializeField] private Transform _offerListContainer;
        [SerializeField] private GameObject _offerRowPrefab;
        [SerializeField] private ScrollRect _listScrollRect;

        [Header("Offer Detail")]
        [SerializeField] private GameObject _detailPanel;
        [SerializeField] private Text _offeringTeamText;
        [SerializeField] private Text _offerMessageText;
        [SerializeField] private Text _expiresText;
        [SerializeField] private Transform _theyOfferContainer;
        [SerializeField] private Transform _theyWantContainer;
        [SerializeField] private GameObject _assetItemPrefab;

        [Header("Evaluation")]
        [SerializeField] private Text _evaluationText;
        [SerializeField] private Image _valueIndicator;
        [SerializeField] private Color _goodDealColor = new Color(0.2f, 0.7f, 0.2f);
        [SerializeField] private Color _fairDealColor = new Color(0.7f, 0.7f, 0.2f);
        [SerializeField] private Color _badDealColor = new Color(0.7f, 0.2f, 0.2f);

        [Header("Actions")]
        [SerializeField] private Button _acceptButton;
        [SerializeField] private Button _rejectButton;
        [SerializeField] private Button _counterButton;
        [SerializeField] private Button _closeButton;

        // State
        private AITradeOfferGenerator _offerGenerator;
        private List<IncomingOfferRow> _activeRows = new List<IncomingOfferRow>();
        private IncomingTradeOffer _selectedOffer;
        private List<GameObject> _assetItems = new List<GameObject>();

        // Events
        public event Action<IncomingTradeOffer, IncomingOfferResponse> OnOfferResponded;
        public event Action<IncomingTradeOffer> OnCounterRequested;

        protected override void Awake()
        {
            base.Awake();
            SetupControls();
        }

        private void SetupControls()
        {
            if (_acceptButton != null)
                _acceptButton.onClick.AddListener(OnAcceptClicked);

            if (_rejectButton != null)
                _rejectButton.onClick.AddListener(OnRejectClicked);

            if (_counterButton != null)
                _counterButton.onClick.AddListener(OnCounterClicked);

            if (_closeButton != null)
                _closeButton.onClick.AddListener(() => HideSlide());

            // Initially hide detail panel
            if (_detailPanel != null)
                _detailPanel.SetActive(false);
        }

        /// <summary>
        /// Show the panel with offers from the generator.
        /// </summary>
        public void ShowWithOffers(AITradeOfferGenerator offerGenerator)
        {
            _offerGenerator = offerGenerator;
            _selectedOffer = null;

            if (_titleText != null)
                _titleText.text = "Incoming Trade Offers";

            PopulateOfferList();
            UpdateOfferCount();

            if (_detailPanel != null)
                _detailPanel.SetActive(false);

            ShowSlide();
        }

        private void PopulateOfferList()
        {
            ClearOfferList();

            if (_offerGenerator == null) return;

            var offers = _offerGenerator.GetPendingOffers();

            foreach (var offer in offers)
            {
                if (_offerRowPrefab != null && _offerListContainer != null)
                {
                    var rowObj = Instantiate(_offerRowPrefab, _offerListContainer);
                    var row = rowObj.GetComponent<IncomingOfferRow>();

                    if (row == null)
                        row = rowObj.AddComponent<IncomingOfferRow>();

                    row.Setup(offer, offer == _selectedOffer);
                    row.OnSelected += OnOfferSelected;
                    _activeRows.Add(row);
                }
            }

            // Show empty state if no offers
            if (offers.Count == 0 && _detailPanel != null)
            {
                // Could show an empty state message here
            }
        }

        private void ClearOfferList()
        {
            foreach (var row in _activeRows)
            {
                row.OnSelected -= OnOfferSelected;
                Destroy(row.gameObject);
            }
            _activeRows.Clear();
        }

        private void UpdateOfferCount()
        {
            if (_offerCountText != null && _offerGenerator != null)
            {
                int count = _offerGenerator.GetPendingOfferCount();
                _offerCountText.text = count == 1
                    ? "1 pending offer"
                    : $"{count} pending offers";
            }
        }

        private void OnOfferSelected(IncomingTradeOffer offer)
        {
            _selectedOffer = offer;

            // Update row selection states
            foreach (var row in _activeRows)
            {
                row.SetSelected(row.Offer == offer);
            }

            ShowOfferDetails(offer);
        }

        private void ShowOfferDetails(IncomingTradeOffer offer)
        {
            if (_detailPanel == null) return;

            _detailPanel.SetActive(true);

            // Header
            if (_offeringTeamText != null)
                _offeringTeamText.text = $"Offer from {offer.OfferingTeamId}";

            if (_offerMessageText != null)
                _offerMessageText.text = $"\"{offer.OfferMessage}\"";

            if (_expiresText != null)
            {
                int days = offer.DaysUntilExpiry;
                _expiresText.text = days == 1
                    ? "Expires in 1 day"
                    : $"Expires in {days} days";
                _expiresText.color = days <= 1 ? _badDealColor : Color.white;
            }

            // Clear existing asset items
            ClearAssetItems();

            // Populate what they're offering
            PopulateAssets(offer.Proposal, offer.OfferingTeamId, _theyOfferContainer, true);

            // Populate what they want
            string playerTeamId = GameManager.Instance?.PlayerTeamId;
            PopulateAssets(offer.Proposal, playerTeamId, _theyWantContainer, false);

            // Show evaluation
            ShowTradeEvaluation(offer);

            // Update button states
            UpdateActionButtons();
        }

        private void PopulateAssets(TradeProposal proposal, string teamId, Transform container, bool isReceiving)
        {
            if (container == null || proposal == null) return;

            var assets = isReceiving
                ? proposal.AllAssets.FindAll(a => a.SendingTeamId == teamId)
                : proposal.AllAssets.FindAll(a => a.ReceivingTeamId != teamId && a.SendingTeamId != teamId);

            // Actually, let's get the right assets
            if (isReceiving)
            {
                // What they offer = assets where sending team is the offering team
                assets = proposal.AllAssets.FindAll(a => a.SendingTeamId == teamId);
            }
            else
            {
                // What they want = assets where receiving team is the offering team
                assets = proposal.AllAssets.FindAll(a => a.ReceivingTeamId == teamId);
            }

            foreach (var asset in assets)
            {
                CreateAssetItem(container, asset);
            }
        }

        private void CreateAssetItem(Transform container, TradeAsset asset)
        {
            if (_assetItemPrefab != null)
            {
                var itemObj = Instantiate(_assetItemPrefab, container);
                _assetItems.Add(itemObj);

                var text = itemObj.GetComponentInChildren<Text>();
                if (text != null)
                {
                    text.text = GetAssetDisplayText(asset);
                }
            }
            else
            {
                // Fallback: create simple text
                var textObj = new GameObject("AssetText");
                textObj.transform.SetParent(container);
                var text = textObj.AddComponent<Text>();
                text.text = GetAssetDisplayText(asset);
                text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                text.fontSize = 14;
                text.color = Color.white;
                _assetItems.Add(textObj);
            }
        }

        private string GetAssetDisplayText(TradeAsset asset)
        {
            switch (asset.Type)
            {
                case TradeAssetType.Player:
                    var player = GameManager.Instance?.GetPlayer(asset.PlayerId);
                    string name = player?.FullName ?? asset.PlayerId;
                    string salary = asset.Salary >= 1_000_000
                        ? $"${asset.Salary / 1_000_000f:F1}M"
                        : $"${asset.Salary:N0}";
                    return $"{name} ({salary})";

                case TradeAssetType.DraftPick:
                    string round = asset.IsFirstRound ? "1st" : "2nd";
                    string orig = asset.OriginalTeamId != asset.SendingTeamId
                        ? $" via {asset.OriginalTeamId}"
                        : "";
                    return $"{asset.Year} {round} Round Pick{orig}";

                case TradeAssetType.Cash:
                    return $"${asset.CashAmount:N0} Cash";

                default:
                    return "Unknown Asset";
            }
        }

        private void ClearAssetItems()
        {
            foreach (var item in _assetItems)
            {
                Destroy(item);
            }
            _assetItems.Clear();
        }

        private void ShowTradeEvaluation(IncomingTradeOffer offer)
        {
            if (_evaluationText == null) return;

            // Simple evaluation based on proposal value difference
            float valueReceiving = 0f;
            float valueGiving = 0f;
            string playerTeamId = GameManager.Instance?.PlayerTeamId;

            foreach (var asset in offer.Proposal.AllAssets)
            {
                float value = EstimateAssetValue(asset);

                if (asset.ReceivingTeamId == playerTeamId)
                    valueReceiving += value;
                else if (asset.SendingTeamId == playerTeamId)
                    valueGiving += value;
            }

            float netValue = valueReceiving - valueGiving;

            // Set evaluation text and color
            if (netValue > 10f)
            {
                _evaluationText.text = "Excellent deal for us";
                if (_valueIndicator != null) _valueIndicator.color = _goodDealColor;
            }
            else if (netValue > 0f)
            {
                _evaluationText.text = "Good deal";
                if (_valueIndicator != null) _valueIndicator.color = _goodDealColor;
            }
            else if (netValue > -10f)
            {
                _evaluationText.text = "Fair trade";
                if (_valueIndicator != null) _valueIndicator.color = _fairDealColor;
            }
            else
            {
                _evaluationText.text = "We're giving up more value";
                if (_valueIndicator != null) _valueIndicator.color = _badDealColor;
            }
        }

        private float EstimateAssetValue(TradeAsset asset)
        {
            switch (asset.Type)
            {
                case TradeAssetType.Player:
                    return asset.Salary / 2_000_000f;
                case TradeAssetType.DraftPick:
                    float baseVal = asset.IsFirstRound ? 25f : 5f;
                    int yearsAway = asset.Year - DateTime.Now.Year;
                    return baseVal * Math.Max(0.3f, 1f - yearsAway * 0.1f);
                case TradeAssetType.Cash:
                    return asset.CashAmount / 1_000_000f;
                default:
                    return 0f;
            }
        }

        private void UpdateActionButtons()
        {
            bool hasSelection = _selectedOffer != null;

            if (_acceptButton != null)
                _acceptButton.interactable = hasSelection;

            if (_rejectButton != null)
                _rejectButton.interactable = hasSelection;

            if (_counterButton != null)
                _counterButton.interactable = hasSelection;
        }

        private void OnAcceptClicked()
        {
            if (_selectedOffer == null) return;

            var offer = _selectedOffer;
            _offerGenerator?.RespondToOffer(offer.OfferId, IncomingOfferResponse.Accept);
            OnOfferResponded?.Invoke(offer, IncomingOfferResponse.Accept);

            // Refresh the list
            _selectedOffer = null;
            PopulateOfferList();
            UpdateOfferCount();

            if (_detailPanel != null)
                _detailPanel.SetActive(false);
        }

        private void OnRejectClicked()
        {
            if (_selectedOffer == null) return;

            var offer = _selectedOffer;
            _offerGenerator?.RespondToOffer(offer.OfferId, IncomingOfferResponse.Reject);
            OnOfferResponded?.Invoke(offer, IncomingOfferResponse.Reject);

            // Refresh the list
            _selectedOffer = null;
            PopulateOfferList();
            UpdateOfferCount();

            if (_detailPanel != null)
                _detailPanel.SetActive(false);
        }

        private void OnCounterClicked()
        {
            if (_selectedOffer == null) return;

            OnCounterRequested?.Invoke(_selectedOffer);

            // The counter flow would open the trade panel with the offer pre-loaded
            // For now, mark as countered
            _offerGenerator?.RespondToOffer(_selectedOffer.OfferId, IncomingOfferResponse.Counter);
        }

        protected override void OnHidden()
        {
            base.OnHidden();
            ClearOfferList();
            ClearAssetItems();
            _selectedOffer = null;
        }
    }

    /// <summary>
    /// Row in the incoming offers list.
    /// </summary>
    public class IncomingOfferRow : MonoBehaviour
    {
        private Text _teamText;
        private Text _summaryText;
        private Text _expiryText;
        private Button _selectButton;
        private Image _backgroundImage;

        private IncomingTradeOffer _offer;
        private bool _isSetup;

        public IncomingTradeOffer Offer => _offer;
        public event Action<IncomingTradeOffer> OnSelected;

        public void Setup(IncomingTradeOffer offer, bool isSelected)
        {
            if (!_isSetup)
            {
                _selectButton = GetComponentInChildren<Button>();
                var texts = GetComponentsInChildren<Text>();
                if (texts.Length >= 1) _teamText = texts[0];
                if (texts.Length >= 2) _summaryText = texts[1];
                if (texts.Length >= 3) _expiryText = texts[2];
                _backgroundImage = GetComponent<Image>();

                if (_selectButton != null)
                    _selectButton.onClick.AddListener(OnButtonClicked);

                _isSetup = true;
            }

            _offer = offer;

            if (_teamText != null)
                _teamText.text = offer.OfferingTeamId;

            if (_summaryText != null)
            {
                var playersWanted = offer.Proposal.AllAssets
                    .Where(a => a.Type == TradeAssetType.Player && a.ReceivingTeamId == offer.OfferingTeamId)
                    .ToList();
                if (playersWanted.Count > 0)
                {
                    var player = GameManager.Instance?.GetPlayer(playersWanted[0].PlayerId);
                    _summaryText.text = $"Wants: {player?.FullName ?? playersWanted[0].PlayerId}";
                }
                else
                {
                    _summaryText.text = "View details";
                }
            }

            if (_expiryText != null)
            {
                int days = offer.DaysUntilExpiry;
                _expiryText.text = days == 1 ? "1 day" : $"{days} days";
                _expiryText.color = days <= 1 ? Color.red : Color.white;
            }

            SetSelected(isSelected);
        }

        public void SetSelected(bool selected)
        {
            if (_backgroundImage != null)
            {
                _backgroundImage.color = selected
                    ? new Color(0.2f, 0.4f, 0.6f, 0.6f)
                    : new Color(0.1f, 0.1f, 0.1f, 0.5f);
            }
        }

        private void OnButtonClicked()
        {
            OnSelected?.Invoke(_offer);
        }

        private void OnDestroy()
        {
            if (_selectButton != null)
                _selectButton.onClick.RemoveListener(OnButtonClicked);
        }
    }
}

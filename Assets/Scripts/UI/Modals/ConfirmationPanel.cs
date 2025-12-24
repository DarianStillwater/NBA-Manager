using System;
using UnityEngine;
using UnityEngine.UI;

namespace NBAHeadCoach.UI.Modals
{
    /// <summary>
    /// Reusable confirmation dialog panel with yes/no actions.
    /// Supports normal and destructive (red) confirmation styles.
    /// </summary>
    public class ConfirmationPanel : SlidePanel
    {
        [Header("Content")]
        [SerializeField] private Text _titleText;
        [SerializeField] private Text _messageText;
        [SerializeField] private Text _detailText;

        [Header("Buttons")]
        [SerializeField] private Button _confirmButton;
        [SerializeField] private Button _cancelButton;
        [SerializeField] private Text _confirmButtonText;
        [SerializeField] private Text _cancelButtonText;
        [SerializeField] private Image _confirmButtonImage;

        [Header("Colors")]
        [SerializeField] private Color _normalConfirmColor = new Color(0.2f, 0.6f, 0.9f);
        [SerializeField] private Color _destructiveConfirmColor = new Color(0.9f, 0.3f, 0.3f);
        [SerializeField] private Color _cancelColor = new Color(0.4f, 0.4f, 0.4f);

        private Action _onConfirm;
        private Action _onCancel;

        protected override void Awake()
        {
            base.Awake();

            if (_confirmButton != null)
                _confirmButton.onClick.AddListener(OnConfirmClicked);

            if (_cancelButton != null)
                _cancelButton.onClick.AddListener(OnCancelClicked);
        }

        /// <summary>
        /// Show a standard confirmation dialog.
        /// </summary>
        /// <param name="title">Dialog title</param>
        /// <param name="message">Main message</param>
        /// <param name="onConfirm">Called when user confirms</param>
        /// <param name="onCancel">Called when user cancels (optional)</param>
        /// <param name="confirmText">Custom confirm button text (default: "Confirm")</param>
        /// <param name="cancelText">Custom cancel button text (default: "Cancel")</param>
        public void Show(string title, string message, Action onConfirm, Action onCancel = null,
            string confirmText = "Confirm", string cancelText = "Cancel")
        {
            SetContent(title, message, null);
            SetButtonText(confirmText, cancelText);
            SetConfirmStyle(false);

            _onConfirm = onConfirm;
            _onCancel = onCancel;

            ShowSlide();
        }

        /// <summary>
        /// Show a confirmation with additional detail text.
        /// </summary>
        public void ShowWithDetail(string title, string message, string detail, Action onConfirm, Action onCancel = null,
            string confirmText = "Confirm", string cancelText = "Cancel")
        {
            SetContent(title, message, detail);
            SetButtonText(confirmText, cancelText);
            SetConfirmStyle(false);

            _onConfirm = onConfirm;
            _onCancel = onCancel;

            ShowSlide();
        }

        /// <summary>
        /// Show a destructive action confirmation (red confirm button).
        /// Use for actions like firing, deleting, etc.
        /// </summary>
        /// <param name="title">Dialog title</param>
        /// <param name="message">Warning message</param>
        /// <param name="onConfirm">Called when user confirms</param>
        /// <param name="confirmText">Custom confirm button text (default: "Delete")</param>
        public void ShowDestructive(string title, string message, Action onConfirm,
            string confirmText = "Delete", string cancelText = "Cancel")
        {
            SetContent(title, message, null);
            SetButtonText(confirmText, cancelText);
            SetConfirmStyle(true);

            _onConfirm = onConfirm;
            _onCancel = null;

            ShowSlide();
        }

        /// <summary>
        /// Show a destructive confirmation with detail text.
        /// </summary>
        public void ShowDestructiveWithDetail(string title, string message, string detail, Action onConfirm,
            string confirmText = "Delete", string cancelText = "Cancel")
        {
            SetContent(title, message, detail);
            SetButtonText(confirmText, cancelText);
            SetConfirmStyle(true);

            _onConfirm = onConfirm;
            _onCancel = null;

            ShowSlide();
        }

        /// <summary>
        /// Show an info/alert dialog (confirm only, no cancel).
        /// </summary>
        public void ShowAlert(string title, string message, Action onDismiss = null, string buttonText = "OK")
        {
            SetContent(title, message, null);
            SetText(_confirmButtonText, buttonText);
            SetConfirmStyle(false);

            // Hide cancel button
            if (_cancelButton != null)
                _cancelButton.gameObject.SetActive(false);

            _onConfirm = onDismiss;
            _onCancel = null;

            ShowSlide();
        }

        private void SetContent(string title, string message, string detail)
        {
            SetText(_titleText, title);
            SetText(_messageText, message);

            if (_detailText != null)
            {
                if (string.IsNullOrEmpty(detail))
                {
                    _detailText.gameObject.SetActive(false);
                }
                else
                {
                    _detailText.gameObject.SetActive(true);
                    _detailText.text = detail;
                }
            }
        }

        private void SetButtonText(string confirmText, string cancelText)
        {
            SetText(_confirmButtonText, confirmText);
            SetText(_cancelButtonText, cancelText);

            // Ensure cancel button is visible
            if (_cancelButton != null)
                _cancelButton.gameObject.SetActive(true);
        }

        private void SetConfirmStyle(bool destructive)
        {
            if (_confirmButtonImage != null)
            {
                _confirmButtonImage.color = destructive ? _destructiveConfirmColor : _normalConfirmColor;
            }
        }

        private void OnConfirmClicked()
        {
            var callback = _onConfirm;
            _onConfirm = null;
            _onCancel = null;

            HideSlide(() => callback?.Invoke());
        }

        private void OnCancelClicked()
        {
            var callback = _onCancel;
            _onConfirm = null;
            _onCancel = null;

            HideSlide(() => callback?.Invoke());
        }

        private void SetText(Text textComponent, string value)
        {
            if (textComponent != null)
                textComponent.text = value ?? "";
        }

        protected override void OnHidden()
        {
            base.OnHidden();
            // Reset state
            _onConfirm = null;
            _onCancel = null;
        }

        private void OnDestroy()
        {
            if (_confirmButton != null)
                _confirmButton.onClick.RemoveListener(OnConfirmClicked);

            if (_cancelButton != null)
                _cancelButton.onClick.RemoveListener(OnCancelClicked);
        }
    }
}

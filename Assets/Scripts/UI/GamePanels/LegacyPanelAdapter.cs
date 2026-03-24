using System;
using UnityEngine;
using NBAHeadCoach.Core.Data;

namespace NBAHeadCoach.UI.GamePanels
{
    /// <summary>
    /// Adapter that wraps a delegate into an IGamePanel.
    /// Used during migration to wrap ArtInjector's existing Build methods.
    /// </summary>
    public class LegacyPanelAdapter : IGamePanel
    {
        private readonly Action<RectTransform, Team, Color> _buildAction;

        public LegacyPanelAdapter(Action<RectTransform, Team, Color> buildAction)
        {
            _buildAction = buildAction;
        }

        public void Build(RectTransform contentArea, Team team, Color teamColor)
        {
            _buildAction?.Invoke(contentArea, team, teamColor);
        }
    }
}

using UnityEngine;
using NBAHeadCoach.Core.Data;

namespace NBAHeadCoach.UI.GamePanels
{
    /// <summary>
    /// Interface for all game scene panels that build their UI programmatically.
    /// </summary>
    public interface IGamePanel
    {
        void Build(RectTransform contentArea, Team team, Color teamColor);
    }
}

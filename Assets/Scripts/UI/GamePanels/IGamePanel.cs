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

    /// <summary>
    /// Panels that accept a deep-link payload (an entity id) before being shown —
    /// e.g. an inbox message opening the Roster panel focused on a player.
    /// </summary>
    public interface IDeepLinkPanel
    {
        void SetDeepLinkPayload(string payload);
    }
}

using System.Collections.Generic;
using UnityEngine;
using NBAHeadCoach.Core.Data;

namespace NBAHeadCoach.UI.Panels
{
    public class RosterPanel : BasePanel
    {
        [Header("List configuration")]
        public Transform ListContent;
        public GameObject RosterRowPrefab;
        
        private List<GameObject> _spawnedRows = new List<GameObject>();

        protected override void OnShown()
        {
            RefreshList();
        }

        public void RefreshList()
        {
            // Clear old
            foreach(var go in _spawnedRows) Destroy(go);
            _spawnedRows.Clear();
            
            // Get Data (Placeholder access)
            // In real app, access UserTeam from GameManager
            // var team = GameManager.Instance.UserTeam;
            
            // For now, finding a team via PlayerDatabase or stub
            // List<Player> players = ...
            // We need a way to get the USER team.
            
            // Placeholder:
            Debug.Log("RosterPanel: Refreshing List (Connection to Team Data pending)");
        }
        
        public void Populate(List<Player> players)
        {
            foreach(var go in _spawnedRows) Destroy(go);
            _spawnedRows.Clear();

            if (RosterRowPrefab == null || ListContent == null) return;

            foreach(var p in players)
            {
                var go = Instantiate(RosterRowPrefab, ListContent);
                var row = go.GetComponent<Components.RosterRow>();
                if (row != null) row.Setup(p);
                _spawnedRows.Add(go);
            }
        }
    }
}

using UnityEngine;
using UnityEngine.UI;
using NBAHeadCoach.UI;
using NBAHeadCoach.UI.Components;
using NBAHeadCoach.UI.Panels;

namespace NBAHeadCoach.Tools
{
    public class UIBuilder : MonoBehaviour
    {
        [Header("Settings")]
        public bool BuildOnStart = true;

        // Colors matching reference
        private static readonly Color32 TabRed = new Color32(180, 40, 40, 255);
        private static readonly Color32 TabYellow = new Color32(200, 160, 40, 255);
        private static readonly Color32 TabGray = new Color32(60, 65, 75, 255);
        private static readonly Color32 CardBg = new Color32(25, 28, 38, 240);
        private static readonly Color32 DarkBg = new Color32(15, 18, 25, 255);
        private static readonly Color32 GoldText = new Color32(255, 200, 80, 255);
        private static readonly Color32 GreenStatus = new Color32(80, 200, 80, 255);
        private static readonly Color32 RedStatus = new Color32(220, 80, 80, 255);
        private static readonly Color32 RadarRed = new Color32(200, 50, 50, 200);

        private void Start()
        {
            if (BuildOnStart) BuildFranchiseUI();
        }

        [ContextMenu("Build Franchise UI")]
        public void BuildFranchiseUI()
        {
            // Cleanup
            var old = GameObject.Find("MainCanvas");
            if (old != null) DestroyImmediate(old);
            old = GameObject.Find("UIManager");
            if (old != null) DestroyImmediate(old);

            // Canvas
            GameObject canvasObj = new GameObject("MainCanvas", typeof(RectTransform));
            canvasObj.layer = LayerMask.NameToLayer("UI");
            Canvas canvas = canvasObj.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            CanvasScaler scaler = canvasObj.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;
            canvasObj.AddComponent<GraphicRaycaster>();

            // EventSystem
            if (Object.FindFirstObjectByType<UnityEngine.EventSystems.EventSystem>() == null)
            {
                var es = new GameObject("EventSystem");
                es.AddComponent<UnityEngine.EventSystems.EventSystem>();
                es.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
            }

            // UIManager
            var mgrObj = new GameObject("UIManager");
            UIManager mgr = mgrObj.AddComponent<UIManager>();
            mgr.MainCanvas = canvas.transform;

            // ========== BACKGROUND ==========
            GameObject bg = CreatePanel("Background", canvas.transform, V2(0, 0), V2(1, 1));
            bg.AddComponent<Image>().color = DarkBg;

            // ========== TOP NAV BAR ==========
            GameObject navBar = CreatePanel("NavBar", canvas.transform, V2(0.15f, 0.92f), V2(0.85f, 0.98f));
            HorizontalLayoutGroup navLayout = navBar.AddComponent<HorizontalLayoutGroup>();
            navLayout.childControlWidth = false;
            navLayout.childForceExpandWidth = false;
            navLayout.spacing = 15;
            navLayout.childAlignment = TextAnchor.MiddleCenter;

            CreateNavTab(navBar.transform, "🏀 Team", TabRed, true);
            CreateNavTab(navBar.transform, "🏆 League", TabYellow, false);
            CreateNavTab(navBar.transform, "👤 Scouting", TabGray, false);
            CreateNavTab(navBar.transform, "🏢 Front Office", TabGray, false);

            // ========== HERO CARD - NEXT MATCH ==========
            GameObject heroCard = CreateCard("HeroCard", canvas.transform, V2(0.02f, 0.42f), V2(0.98f, 0.90f));
            
            // Red glow accent on left side
            GameObject redGlow = CreatePanel("RedGlow", heroCard.transform, V2(0, 0), V2(0.03f, 1));
            Image glowImg = redGlow.AddComponent<Image>();
            glowImg.color = new Color32(180, 40, 40, 180);

            // "NEXT MATCH" header
            CreateText(heroCard.transform, "NEXT MATCH", V2(0.04f, 0.82f), V2(0.4f, 0.95f), 
                Color.white, 28, FontStyle.Bold, TextAnchor.MiddleLeft);

            // Team matchup: MIAMI HEAT vs. BOSTON CELTICS
            CreateText(heroCard.transform, "MIAMI HEAT", V2(0.04f, 0.55f), V2(0.25f, 0.75f), 
                Color.white, 24, FontStyle.Bold, TextAnchor.MiddleLeft);
            CreateText(heroCard.transform, "vs.", V2(0.26f, 0.55f), V2(0.30f, 0.75f), 
                GoldText, 20, FontStyle.Normal, TextAnchor.MiddleCenter);
            CreateText(heroCard.transform, "BOSTON CELTICS", V2(0.31f, 0.55f), V2(0.55f, 0.75f), 
                Color.white, 24, FontStyle.Bold, TextAnchor.MiddleLeft);

            // Tip-off badge
            GameObject tipBadge = CreatePanel("TipBadge", heroCard.transform, V2(0.04f, 0.38f), V2(0.28f, 0.50f));
            Image tipBg = tipBadge.AddComponent<Image>();
            tipBg.color = new Color32(35, 40, 50, 255);
            CreateText(tipBadge.transform, "TIP OFF: 7:30 PM ET TODAY", V2(0, 0), V2(1, 1), 
                Color.white, 14, FontStyle.Bold, TextAnchor.MiddleCenter);

            // Play Match button (red)
            CreateButton(heroCard.transform, "PLAY MATCH", V2(0.04f, 0.08f), V2(0.18f, 0.28f), TabRed);
            
            // Simulate button (yellow)
            CreateButton(heroCard.transform, "SIMULATE ▶▶", V2(0.20f, 0.08f), V2(0.36f, 0.28f), TabYellow);

            // ===== OPPOSING STAR SECTION =====
            CreateText(heroCard.transform, "OPPOSING STAR: JAYSON TATUM", V2(0.55f, 0.82f), V2(0.96f, 0.95f), 
                Color.white, 16, FontStyle.Bold, TextAnchor.MiddleCenter);

            // Radar chart
            GameObject radarBg = CreatePanel("RadarBg", heroCard.transform, V2(0.58f, 0.15f), V2(0.92f, 0.80f));
            RadarChart radar = radarBg.AddComponent<RadarChart>();
            radar.Scoring = 92f;
            radar.Playmaking = 85f;
            radar.Defense = 80f;
            radar.Rebounding = 78f;
            radar.Athleticism = 83f;
            radar.FillColor = RadarRed;

            // Radar labels (positioned around chart)
            CreateText(heroCard.transform, "Shooting\n(92)", V2(0.72f, 0.75f), V2(0.82f, 0.82f), GoldText, 10, FontStyle.Normal, TextAnchor.MiddleCenter);
            CreateText(heroCard.transform, "Finishing\n(88)", V2(0.88f, 0.55f), V2(0.98f, 0.65f), GoldText, 10, FontStyle.Normal, TextAnchor.MiddleCenter);
            CreateText(heroCard.transform, "Playmaking\n(85)", V2(0.85f, 0.25f), V2(0.98f, 0.35f), GoldText, 10, FontStyle.Normal, TextAnchor.MiddleCenter);
            CreateText(heroCard.transform, "Defense\n(80)", V2(0.72f, 0.08f), V2(0.82f, 0.18f), GoldText, 10, FontStyle.Normal, TextAnchor.MiddleCenter);
            CreateText(heroCard.transform, "Rebounding\n(78)", V2(0.58f, 0.25f), V2(0.68f, 0.35f), GoldText, 10, FontStyle.Normal, TextAnchor.MiddleCenter);
            CreateText(heroCard.transform, "Athleticism\n(83)", V2(0.55f, 0.55f), V2(0.68f, 0.65f), GoldText, 10, FontStyle.Normal, TextAnchor.MiddleCenter);
            CreateText(heroCard.transform, "Basketball IQ\n(89)", V2(0.58f, 0.75f), V2(0.72f, 0.82f), GoldText, 10, FontStyle.Normal, TextAnchor.MiddleCenter);

            // ========== BOTTOM ROW - 3 CARDS ==========
            float cardY1 = 0.02f, cardY2 = 0.40f;

            // Card 1: Conference Standing
            GameObject standingsCard = CreateCard("StandingsCard", canvas.transform, V2(0.02f, cardY1), V2(0.33f, cardY2));
            CreateCardHeader(standingsCard.transform, "CONFERENCE STANDING");
            CreateText(standingsCard.transform, "🏆 Eastern Conference", V2(0.05f, 0.70f), V2(0.95f, 0.82f), 
                Color.white, 14, FontStyle.Bold, TextAnchor.MiddleLeft);
            
            // Standings list
            CreateStandingRow(standingsCard.transform, "1.", "Boston Celtics", "(20-5)", 0.58f, false);
            CreateStandingRow(standingsCard.transform, "2.", "Milwaukee Bucks", "(18-7)", 0.46f, false);
            CreateStandingRow(standingsCard.transform, "3.", "MIAMI HEAT", "(17-8)", 0.34f, true); // Highlighted
            CreateStandingRow(standingsCard.transform, "4.", "Philadelphia 76ers", "(16-9)", 0.22f, false);

            // Card 2: Current Roster & Form
            GameObject rosterCard = CreateCard("RosterCard", canvas.transform, V2(0.34f, cardY1), V2(0.66f, cardY2));
            CreateCardHeader(rosterCard.transform, "CURRENT ROSTER & FORM (LAST 5)");
            
            CreateRosterRow(rosterCard.transform, "Jimmy Butler", "SF", new bool[]{true,true,true,true,true}, "Active", 0.68f);
            CreateRosterRow(rosterCard.transform, "Bam Adebayo", "C", new bool[]{true,true,true,true,false}, "Active", 0.54f);
            CreateRosterRow(rosterCard.transform, "Tyler Herro", "SG", new bool[]{true,false,true,false,false}, "Active", 0.40f);
            CreateRosterRow(rosterCard.transform, "Kyle Lowry", "PG", new bool[]{true,true,true,true,true}, "Active", 0.26f);
            CreateRosterRow(rosterCard.transform, "Caleb Martin", "PF", new bool[]{false,false,false,false,false}, "Injured", 0.12f);

            // Card 3: Inbox
            GameObject inboxCard = CreateCard("InboxCard", canvas.transform, V2(0.67f, cardY1), V2(0.98f, cardY2));
            CreateCardHeader(inboxCard.transform, "INBOX");
            
            CreateInboxItem(inboxCard.transform, "🏆", "Trade Offer:", "Receive 2025 1st Round Pick from NYK", 0.68f);
            CreateInboxItem(inboxCard.transform, "📋", "Scouting Report:", "Top Prospect Scouting Update", 0.54f);
            CreateInboxItem(inboxCard.transform, "🏥", "Injury Update:", "Caleb Martin Status Changed to Day-to-Day", 0.40f);
            CreateInboxItem(inboxCard.transform, "🎯", "Owner's Goal:", "Win Next 3 Home Games", 0.26f);
            CreateInboxItem(inboxCard.transform, "⭐", "League News:", "All-Star Voting Opens Soon", 0.12f);

            Debug.Log("✅ Premium Dashboard Built!");
        }

        // ===== HELPER METHODS =====

        private Vector2 V2(float x, float y) => new Vector2(x, y);

        private GameObject CreatePanel(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax)
        {
            GameObject go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            RectTransform rt = go.GetComponent<RectTransform>();
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            return go;
        }

        private GameObject CreateCard(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax)
        {
            GameObject card = CreatePanel(name, parent, anchorMin, anchorMax);
            Image bg = card.AddComponent<Image>();
            bg.color = CardBg;
            card.AddComponent<Outline>().effectColor = new Color(1, 1, 1, 0.05f);
            return card;
        }

        private void CreateCardHeader(Transform card, string title)
        {
            GameObject header = CreatePanel("Header", card, V2(0, 0.85f), V2(1, 1));
            CreateText(header.transform, title, V2(0.05f, 0), V2(0.95f, 1), 
                Color.white, 14, FontStyle.Bold, TextAnchor.MiddleLeft);
        }

        private Text CreateText(Transform parent, string content, Vector2 anchorMin, Vector2 anchorMax, 
            Color color, int fontSize, FontStyle style, TextAnchor alignment)
        {
            GameObject txtObj = CreatePanel("Text", parent, anchorMin, anchorMax);
            Text txt = txtObj.AddComponent<Text>();
            txt.text = content;
            txt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            txt.fontSize = fontSize;
            txt.fontStyle = style;
            txt.alignment = alignment;
            txt.color = color;
            txt.raycastTarget = false;
            return txt;
        }

        private void CreateNavTab(Transform parent, string label, Color32 bgColor, bool isActive)
        {
            GameObject tab = new GameObject(label + "Tab", typeof(RectTransform), typeof(Image), typeof(Button));
            tab.transform.SetParent(parent, false);
            RectTransform rt = tab.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(160, 45);

            Image bg = tab.GetComponent<Image>();
            bg.color = bgColor;

            // Label
            GameObject txtObj = new GameObject("Label", typeof(RectTransform), typeof(Text));
            txtObj.transform.SetParent(tab.transform, false);
            Text txt = txtObj.GetComponent<Text>();
            txt.text = label;
            txt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            txt.fontSize = 16;
            txt.fontStyle = FontStyle.Bold;
            txt.alignment = TextAnchor.MiddleCenter;
            txt.color = Color.white;
            txt.raycastTarget = false;
            Stretch(txtObj.GetComponent<RectTransform>());
        }

        private void CreateButton(Transform parent, string label, Vector2 anchorMin, Vector2 anchorMax, Color32 bgColor)
        {
            GameObject btn = CreatePanel(label + "Btn", parent, anchorMin, anchorMax);
            Image bg = btn.AddComponent<Image>();
            bg.color = bgColor;
            btn.AddComponent<Button>();
            btn.AddComponent<Shadow>().effectDistance = new Vector2(2, -2);

            GameObject txtObj = new GameObject("Label", typeof(RectTransform), typeof(Text));
            txtObj.transform.SetParent(btn.transform, false);
            Text txt = txtObj.GetComponent<Text>();
            txt.text = label;
            txt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            txt.fontSize = 14;
            txt.fontStyle = FontStyle.Bold;
            txt.alignment = TextAnchor.MiddleCenter;
            txt.color = Color.white;
            txt.raycastTarget = false;
            Stretch(txtObj.GetComponent<RectTransform>());
        }

        private void CreateStandingRow(Transform parent, string rank, string team, string record, float yPos, bool highlight)
        {
            GameObject row = CreatePanel("Row" + rank, parent, V2(0.05f, yPos), V2(0.95f, yPos + 0.10f));
            if (highlight)
            {
                Image bg = row.AddComponent<Image>();
                bg.color = new Color32(40, 100, 40, 150); // Green highlight
            }
            
            CreateText(row.transform, rank, V2(0, 0), V2(0.1f, 1), Color.white, 12, FontStyle.Normal, TextAnchor.MiddleLeft);
            CreateText(row.transform, team, V2(0.15f, 0), V2(0.7f, 1), highlight ? GreenStatus : Color.white, 12, FontStyle.Bold, TextAnchor.MiddleLeft);
            CreateText(row.transform, record, V2(0.75f, 0), V2(1, 1), Color.gray, 11, FontStyle.Normal, TextAnchor.MiddleRight);
        }

        private void CreateRosterRow(Transform parent, string name, string pos, bool[] form, string status, float yPos)
        {
            GameObject row = CreatePanel("Row" + name, parent, V2(0.02f, yPos), V2(0.98f, yPos + 0.12f));
            
            // Player name (gold)
            CreateText(row.transform, name, V2(0.08f, 0), V2(0.40f, 1), GoldText, 12, FontStyle.Bold, TextAnchor.MiddleLeft);
            
            // Position
            CreateText(row.transform, "(" + pos + ")", V2(0.40f, 0), V2(0.50f, 1), Color.gray, 10, FontStyle.Normal, TextAnchor.MiddleLeft);
            
            // Form dots
            string formStr = "";
            foreach (bool win in form)
                formStr += win ? "● " : "○ ";
            CreateText(row.transform, formStr, V2(0.52f, 0), V2(0.75f, 1), GreenStatus, 12, FontStyle.Normal, TextAnchor.MiddleLeft);
            
            // Status
            Color statusColor = status == "Active" ? GreenStatus : RedStatus;
            CreateText(row.transform, status, V2(0.76f, 0), V2(1, 1), statusColor, 10, FontStyle.Normal, TextAnchor.MiddleRight);
        }

        private void CreateInboxItem(Transform parent, string icon, string title, string desc, float yPos)
        {
            GameObject row = CreatePanel("Inbox" + title, parent, V2(0.02f, yPos), V2(0.98f, yPos + 0.14f));
            
            CreateText(row.transform, icon, V2(0, 0), V2(0.08f, 1), Color.white, 16, FontStyle.Normal, TextAnchor.MiddleCenter);
            CreateText(row.transform, title, V2(0.10f, 0), V2(0.45f, 1), GoldText, 11, FontStyle.Bold, TextAnchor.MiddleLeft);
            CreateText(row.transform, desc, V2(0.46f, 0), V2(1, 1), Color.white, 10, FontStyle.Normal, TextAnchor.MiddleLeft);
        }

        private void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }
    }
}

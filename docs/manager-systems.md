# Manager Systems Documentation

## Overview

The game architecture uses 45 domain managers in `Assets/Scripts/Core/Manager/` to handle specific game systems. Most managers follow the Singleton pattern and are initialized by GameManager.

## Manager Categories

### Personnel Management

| Manager | Responsibility |
|---------|---------------|
| **PersonnelManager** | Central hub for all personnel operations (coaches, scouts, GMs) |
| AdvanceScoutingManager | Scout assignments and opponent reports |
| FormerPlayerCareerManager | Former player career pipelines (coach/scout/GM transitions) |
| RetirementManager | Player & non-player retirement processing |
| JobSecurityManager | Coach job security and hot seat tracking |
| GMJobSecurityManager | GM job security and performance evaluation |
| StaffEvaluationGenerator | Performance reviews for coaching staff |

### Roster & Contracts

| Manager | Responsibility |
|---------|---------------|
| RosterManager | Roster moves, waiving, signing, **OnPlayerRemoved event** |
| ContractNegotiationManager | Contract offers and negotiation sessions |
| AgentManager | Agent relationship management |
| SalaryCapManager | Cap calculations, exceptions, CBA compliance |
| FreeAgentManager | Free agency operations and AI signings |

### Trading & Transactions

| Manager | Responsibility |
|---------|---------------|
| TradeSystem | Trade proposals and execution |
| TradeNegotiationManager | Multi-step trade negotiations |
| TradeValidator | CBA compliance checking for trades |
| TradeFinder | AI trade partner matching |
| **AITradeOfferGenerator** | AI teams proactively propose trades (~1-2/week) |
| **TradeAnnouncementSystem** | Trade news generation and ticker updates |
| **DraftPickRegistry** | Central draft pick ownership tracking with Stepien Rule |

### Season & Competition

| Manager | Responsibility |
|---------|---------------|
| PlayoffManager | Playoffs and play-in tournament management |
| AllStarManager | All-Star game selection and event |
| AwardManager | MVP, DPOY, ROY, 6MOY, MIP, COY, All-Teams voting |
| HistoryManager | Records tracking, Hall of Fame induction |
| LeagueEventsManager | League-wide events (lockouts, rule changes, etc.) |

### Development & Offseason

| Manager | Responsibility |
|---------|---------------|
| PlayerDevelopmentManager | Attribute progression with mentorship integration |
| **MentorshipManager** | Mentor-mentee relationships, development bonuses (up to 30%) |
| TendencyCoachingManager | Player tendency training system |
| **PracticeManager** | Practice sessions, drills, weekly schedules |
| DraftSystem | Draft lottery and execution |
| DraftClassGenerator | Draft class generation with realistic distributions |
| **DraftPickRegistry** | Draft pick ownership tracking across seasons |
| ProspectGenerator | Procedural prospect generation |
| OffseasonManager | Offseason phase coordination |
| SummerLeagueManager | Summer league simulation |
| TrainingCampManager | Training camp and roster cuts |

### In-Game Coaching & Analytics

| Manager | Responsibility |
|---------|---------------|
| **GameAnalyticsTracker** | Real-time possession-by-possession stats, shot charts, +/- |
| **PlayEffectivenessTracker** | Hot/cold play tracking, opponent adjustment detection |
| **GamePlanBuilder** | Pre-game preparation, matchups, contingency plans |

### Dual-Role & Job Market

| Manager | Responsibility |
|---------|---------------|
| **JobMarketManager** | Job openings, applications, interviews, offers, firing |

### Morale & Chemistry

| Manager | Responsibility |
|---------|---------------|
| **MoraleChemistryManager** | Team meetings, individual conversations, captain effects |
| **PersonalityManager** | Pair/team chemistry, captain influence, escalation processing |

### Team Operations

| Manager | Responsibility |
|---------|---------------|
| FinanceManager | Team finances and budget management |
| RevenueManager | Game-by-game revenue tracking |
| InjuryManager | Injuries and load management |
| MediaManager | Press conferences and media relations |
| ScoutingReportGenerator | Text-based scouting reports |

## Key Manager Details

### PersonnelManager (Central Personnel Hub)

**Pattern**: Singleton
**Responsibilities**:
- All personnel CRUD operations
- Contract negotiations (coaches, scouts, GMs)
- Hiring and firing
- Staff assignments
- Coordinator management
- Staff meetings generation
- Former player career transitions

**Key APIs**:
```csharp
// Query
GetProfile(profileId) → UnifiedCareerProfile
GetTeamStaff(teamId) → List<UnifiedCareerProfile>
GetHeadCoach(teamId) → UnifiedCareerProfile
GetOffensiveCoordinator(teamId) → UnifiedCareerProfile
GetDefensiveCoordinator(teamId) → UnifiedCareerProfile

// Operations
HirePersonnel(profile, teamId, role)
FirePersonnel(teamId, profileId, reason)
StartNegotiation(profileId, teamId) → StaffNegotiationSession
MakeOffer(sessionId, amount, years) → NegotiationResponse

// Staff Meetings
CreatePreGameMeeting(teamId, opponentId, opponentName, gameDate)
CreateHalftimeMeeting(teamId, opponentId, teamScore, oppScore)
```

**Integration**: Replaced 9 legacy managers (CoachingStaffManager, ScoutingManager, UnifiedCareerManager, StaffManagementManager, StaffHiringManager, CoachJobMarketManager, etc.)

### RosterManager

**Pattern**: Singleton
**Key Event**: `OnPlayerRemoved` - Fires when player is traded/waived/cut
**Integration**: Captain system listens to this event for automatic captain replacement

**Key APIs**:
```csharp
AddPlayerToRoster(teamId, playerId)
RemovePlayerFromRoster(teamId, playerId) // Fires OnPlayerRemoved
WaivePlayer(teamId, playerId)
SignPlayer(teamId, playerId, contract)
```

### AITradeOfferGenerator

**Pattern**: Singleton
**Responsibilities**:
- Generate proactive AI trade offers to player
- ~15% daily chance (1-2 offers per week average)
- Load real-world GM profiles from JSON (Dec 2025 data)
- Personality-based offer generation

**Offer Lifecycle**:
1. AI evaluates player's roster for targets
2. Generates offer based on FO personality
3. Offer expires in 3-7 days
4. Player can Accept/Reject/Counter

**Integration**:
- Uses PlayerValueCalculator for player assessment
- Uses DraftPickRegistry for draft pick inclusion
- TradeAnnouncementSystem generates news on execution

### DraftPickRegistry

**Pattern**: Singleton
**Responsibilities**:
- Central registry for all draft pick ownership
- Tracks traded picks across all seasons (2025-2031+)
- Stepien Rule validation (can't trade consecutive 1sts)
- Protection and swap rights tracking
- Loads initial data from JSON (38 traded picks, 14 swaps as of Dec 2025)

**Key APIs**:
```csharp
GetPickOwner(year, round, originalTeam) → string teamId
TransferPick(year, round, originalTeam, fromTeam, toTeam)
CanTradePick(teamId, year, round) → bool (Stepien Rule check)
GetTeamPicks(teamId, year) → List<DraftPick>
```

### MentorshipManager

**Pattern**: Singleton
**Responsibilities**:
- Veteran-rookie mentorship relationships
- Development bonuses (up to 30% boost)
- Organic relationship formation
- Practice session mentorship
- Milestone tracking

**Key APIs**:
```csharp
AssignMentor(mentorId, menteeId, teamId, focusAreas) → (success, message, relationship)
GetMentorshipDevelopmentBonus(menteeId) → float // 0.0 to 0.30
ProcessPracticeMentorshipSessions(teamId, roster, sessionType)
CheckOrganicFormation(teamId, roster) → List<MentorshipRelationship>
```

**Integration**: PlayerDevelopmentManager applies mentorship bonuses during attribute progression

### PracticeManager

**Pattern**: Singleton
**Responsibilities**:
- Schedule practice sessions
- Execute drills with attribute bonuses
- Manage player fatigue
- Install plays into playbook (familiarity system)
- Weekly schedule management (game days, practice days, off days)

**Key APIs**:
```csharp
SchedulePractice(teamId, date, focusArea, intensity)
ExecutePractice(sessionId) → PracticeResults
GetWeeklySchedule(teamId, weekStart) → WeeklySchedule
InstallPlay(teamId, playId) // Requires practice sessions
```

### GameAnalyticsTracker

**Pattern**: Singleton
**Responsibilities**:
- Real-time possession-by-possession tracking
- Shot charts (makes/misses by location)
- Plus-minus tracking per player
- Run detection (8+ unanswered points)
- Matchup quality scoring

**Key APIs**:
```csharp
TrackPossession(possessionData)
GetShotChart(teamId, playerId) → List<ShotData>
GetPlusMinus(playerId) → float
GetCurrentRun(teamId) → int points
```

**Integration**: CoachingAdvisor uses analytics for in-game suggestions

### PlayEffectivenessTracker

**Pattern**: Singleton
**Responsibilities**:
- Track hot/cold plays
- Detect opponent adjustments
- Play success rate analysis
- Recommend play rotations

**Key APIs**:
```csharp
TrackPlayExecution(playId, success, defenseUsed)
GetHotPlays(teamId) → List<string> playIds
GetColdPlays(teamId) → List<string> playIds
DetectOpponentAdjustment(opponentId) → AdjustmentDetected
```

### GamePlanBuilder

**Pattern**: Singleton
**Responsibilities**:
- Pre-game preparation
- Matchup analysis
- Contingency planning
- Integration with OpponentTendencyProfile

**Key APIs**:
```csharp
BuildGamePlan(teamId, opponentId, opponentProfile) → GamePlan
GetMatchupAdvantages(lineup, opponentLineup) → List<Matchup>
GenerateContingencyPlans(gamePlan) → List<ContingencyPlan>
```

### MoraleChemistryManager

**Pattern**: Singleton
**Responsibilities**:
- Process game results for morale impact
- Team meetings (70% success, 14-day cooldown)
- Individual conversations (promise time, praise, etc.)
- Captain morale amplification (+20% happy, -10% unhappy)
- Contract satisfaction tracking
- Escalation processing (5-step discontent ladder)

**Key APIs**:
```csharp
// Captain System
AssignCaptain(playerId, teamId)
GetCaptain(teamId) → Player
GetCaptainMoraleModifier(teamId) → float

// Interventions
CallTeamMeeting(coachId) → MeetingResult
TalkToPlayer(playerId, conversationType) → ConversationResult

// Processing
ProcessGameResult(teamId, won, isHome, opponentStrength)
ProcessDailyMorale(teamId)
GetExpectationBasedMoraleChange(teamId, won) → float
```

### PersonalityManager

**Pattern**: Singleton
**Responsibilities**:
- Pairwise chemistry calculation
- Team chemistry aggregation
- Captain influence on chemistry
- Escalation processing (complaint → holdout)
- Trait-based compatibility

**Key APIs**:
```csharp
GetPairChemistry(player1Id, player2Id) → float (-1.0 to 1.0)
GetTeamChemistry(teamId) → float
ApplyCaptainInfluence(teamId, baseMorale) → adjustedMorale
ProcessDiscontentEscalation(playerId) → EscalationResult
```

### JobMarketManager

**Pattern**: Singleton
**Responsibilities**:
- Handle firing (both GM and Coach roles)
- Generate job openings
- Process applications
- Conduct interviews
- Make offers
- Track job market state

**Key APIs**:
```csharp
// Firing
HandlePlayerFired(reason, publicStatement)
GetFiringDetails() → FiringDetails

// Job Search
GetAvailableJobs(roleFilter) → List<JobOpening>
ApplyForJob(openingId, coverLetter) → JobApplication
AcceptJob(openingId) → bool
```

### TradeAnnouncementSystem

**Pattern**: Singleton
**Responsibilities**:
- Generate trade news on execution
- Headline generation
- Trade breakdown analysis
- Team grades (A+, B-, etc.)
- News ticker updates

**Output**:
- Headline: "BREAKING: Lakers acquire..."
- Summary: Players/picks involved
- Analysis: "Who won" breakdown
- Grades: "LAL: A-", "BOS: B+"

**Integration**:
- TradeSystem calls on trade execution
- DashboardPanel displays ticker
- InboxPanel shows full details

## Manager Initialization

Managers are initialized in GameManager's `Awake()` method in dependency order:

1. Data managers (PlayerDatabase, SalaryCapManager)
2. Personnel systems (PersonnelManager, RosterManager)
3. Trade systems (TradeSystem, DraftPickRegistry, AITradeOfferGenerator)
4. Season managers (SeasonController, PlayoffManager)
5. Development managers (PlayerDevelopmentManager, MentorshipManager, PracticeManager)
6. Morale systems (MoraleChemistryManager, PersonalityManager)
7. Financial managers (FinanceManager, RevenueManager)
8. Job market (JobMarketManager)

## Event System

Managers communicate via C# events to maintain loose coupling:

| Event | Publisher | Subscribers |
|-------|-----------|-------------|
| OnPlayerRemoved | RosterManager | GameManager (captain check), MoraleChemistryManager |
| OnTradeExecuted | TradeSystem | DraftPickRegistry, TradeAnnouncementSystem, GameManager |
| OnPhaseChanged | SeasonController | GameManager (captain check), OffseasonManager |
| OnCaptainSelectionRequired | GameManager | GameSceneController (shows modal) |
| OnTradeAnnounced | TradeAnnouncementSystem | DashboardPanel (ticker), InboxPanel |

## Manager Best Practices

1. **Singleton Pattern**: Most managers use singleton for global access
2. **Event-Driven**: Use events for cross-manager communication
3. **State Persistence**: Implement `SaveState()` and `LoadState()` for save/load
4. **Null Safety**: Always check for null GameManager instance
5. **Dependency Injection**: Pass required dependencies via constructor or Initialize()
6. **Facade Pattern**: PersonnelManager acts as facade for all personnel operations

## Summary

- **45 domain managers** handling all game systems
- **Singleton pattern** for global state management
- **Event-driven architecture** for loose coupling
- **Save/Load support** across all managers
- **Centralized initialization** via GameManager
- **Facade patterns** (PersonnelManager) for complex subsystems

Key subsystems:
- Personnel (7 managers)
- Trading (7 managers)
- Development (11 managers)
- Competition (5 managers)
- Operations (9 managers)
- Morale/Chemistry (2 managers)
- Job Market (1 manager)
- Analytics (3 managers)

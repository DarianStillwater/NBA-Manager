# NBA Head Coach – Complete Design Document

> **Purpose**: This document is the single source of truth for understanding the entire game design. An AI or developer can reference this without scanning all project files.
>
> **Last Updated**: December 2025 (NBA Rules System & Initial Data)

---

## GAME OVERVIEW

**Genre**: Single-player NBA franchise management simulation
**Platform**: Unity (Windows)
**Perspective**: You play as an NBA head coach managing your team through seasons

**Core Loop**:
1. Manage roster (trades, free agency, contracts) - *GM role*
2. Develop players (training, playing time) - *Both roles*
3. Coach games (strategy, play calling, substitutions) - *Coach role*
4. Navigate seasons (82 games → playoffs → offseason)
5. Build career legacy across multiple teams and roles

### Dual-Role System

> **Key Feature**: Players choose their role when starting a new career:
> - **GM Only**: Full roster control, hire/fire coaches, AI coach runs games autonomously
> - **Head Coach Only**: Full game control, submit roster requests to AI GM for approval
> - **Both** (default): Complete control over all aspects
>
> Roles have **linked fates** - getting fired from either role ends the tenure. After firing, enter the **Job Market** to apply for new positions (potentially switching roles).

### Design Philosophy: No Visible Attributes

> **Critical Design Decision**: Player attributes (shooting, defense, speed, etc.) are NEVER shown as numbers to the user. All attributes exist internally to drive simulation, but players learn about talent through:
> - **Statistics**: Box scores, shooting percentages, per-game averages
> - **Scouting Reports**: Text-based descriptions ("Elite three-point shooter", "Struggles against physical defenders")
> - **Game Observation**: Watch how players perform in simulated games
>
> This creates a realistic "eye test" experience where coaches evaluate players like real NBA coaches do - through observation and professional scouting, not spreadsheet numbers.

---

## PROJECT STRUCTURE

### Scenes
| Scene | Path | Purpose |
|-------|------|---------|
| Boot | `Assets/Scenes/Boot.unity` | Load data, initialize GameManager |
| MainMenu | `Assets/Scenes/MainMenu.unity` | New game, load, settings |
| Game | `Assets/Scenes/Game.unity` | Main dashboard/management hub |
| Match | `Assets/Scenes/Match.unity` | Interactive match experience |

### Script Organization
```
Assets/Scripts/
├── Core/
│   ├── AI/                 # AI coach personalities, trade evaluation, coordinators (12 files)
│   ├── Data/               # All data models - 50+ classes (see Data Models section)
│   ├── Gameplay/           # In-game coaching logic (1 file)
│   ├── Manager/            # Domain managers - 38 managers (see Manager Systems)
│   ├── Simulation/         # Game/possession simulation engine (6 files)
│   ├── Util/               # Utilities like NameGenerator
│   ├── GameManager.cs      # Central game orchestrator
│   ├── SeasonController.cs # Season flow and calendar
│   ├── MatchFlowController.cs # Pre/post game flow
│   ├── MatchSimulationController.cs # Match simulation
│   ├── PlayByPlayGenerator.cs # Broadcast-style text
│   └── SaveLoadManager.cs  # Persistence with Ironman mode
├── UI/
│   ├── Panels/             # 15 UI panels (Dashboard, Roster, Trade, etc.)
│   ├── Modals/             # 8 modal dialogs (Captain Selection, Trade Offers, etc.)
│   ├── Components/         # 16 reusable UI components
│   ├── Match/              # Match-specific UI components
│   ├── GameSceneController.cs # Panel navigation
│   └── MainMenuController.cs # Main menu flow
├── View/                   # Camera, visualization, player visuals (5 files)
├── Tools/                  # Scene setup utilities (3 files)
└── Tests/                  # Unit and integration tests (3 files)
```

### Data Storage
- **Game Data**: `Assets/StreamingAssets/Data/{players.json,teams.json}`
- **Mod Support**: `%USERPROFILE%/Documents/NBAHeadCoach/Mods/`
- **Save Files**: `Application.persistentDataPath/Saves/*.nbahc`

---

## CORE ARCHITECTURE

### GameManager (Singleton Orchestrator)
**File**: `Assets/Scripts/Core/GameManager.cs`

Central controller managing game state, scene loading, and manager coordination.

**States** (GameState enum):
- `Booting` → `MainMenu` → `NewGame` → `Playing`
- `Playing` ↔ `PreGame` ↔ `Match` ↔ `PostGame`
- `Offseason` (with sub-states for each phase)
- `JobMarket` (after being fired, job search mode)

**NewGame States** (NewGameState enum):
- `CoachCreation` → `TeamSelection` → `RoleSelection` → `DifficultySettings` → `ContractNegotiation` → `Confirmation`

**Events**:
- `OnStateChanged` - State transitions
- `OnNewGameStarted` - Fresh career started
- `OnGameLoaded` - Save loaded
- `OnDayAdvanced` - Calendar progressed
- `OnSeasonChanged` - New season started
- `OnCaptainSelectionRequired` - **NEW** - Captain needs to be selected

### SeasonController
**File**: `Assets/Scripts/Core/SeasonController.cs`

Manages the 82-game schedule, calendar, standings, and season phases.

**Phases**: TrainingCamp → Preseason → Regular Season → Play-In → Playoffs → Draft → Free Agency → Offseason

**Events**:
- `OnPhaseChanged` - Season phase transitions (triggers captain selection at RegularSeason start)

### MatchFlowController
**File**: `Assets/Scripts/Core/MatchFlowController.cs`

Bridges calendar events to simulation; handles pre-game, match, and post-game flow.

---

## DATA MODELS

### Core Entities

| File | Description | Size |
|------|-------------|------|
| `Player.cs` | Complete player data (attributes, career, personality, IsCaptain) | 45KB |
| `Team.cs` | Team identity, roster, strategy | 8KB |
| `Contract.cs` | CBA-compliant contracts with options | 15KB |
| `UnifiedCareerProfile.cs` | **Unified career system for all personnel** | 45KB |

### Personnel System (Unified Architecture)

> **Recent Refactor**: The personnel system was consolidated from multiple legacy managers into a unified architecture centered on `UnifiedCareerProfile` and `PersonnelManager`.

| File | Description |
|------|-------------|
| `UnifiedCareerProfile.cs` | Central profile for coaches, scouts, GMs - tracks both career tracks |
| `StaffNegotiation.cs` | Contract negotiation sessions, offers, rounds |
| `StaffAssignment.cs` | Task assignments for personnel |
| `StaffRoles.cs` | Role definitions and specializations |
| `FormerPlayerCoach.cs` | Former players entering coaching |
| `FormerPlayerScout.cs` | Former players entering scouting |
| `FormerPlayerGM.cs` | Former players entering front office |
| `FormerPlayerProgressionData.cs` | Progression tracking for former players |
| `NonPlayerRetirementData.cs` | Retirement data for non-player personnel |

### Legacy Files (DELETED)

The following files have been removed as part of the personnel refactor:
- ~~`Coach.cs`~~ - Replaced by `UnifiedCareerProfile`
- ~~`Scout.cs`~~ - Replaced by `UnifiedCareerProfile`
- ~~`CoachCareer.cs`~~ - Merged into `UnifiedCareerProfile`
- ~~`CoachingStaffManager.cs`~~ - Replaced by `PersonnelManager`
- ~~`ScoutingManager.cs`~~ - Replaced by `PersonnelManager`
- ~~`UnifiedCareerManager.cs`~~ - Merged into `PersonnelManager`
- ~~`StaffManagementManager.cs`~~ - Replaced by `PersonnelManager`
- ~~`StaffHiringManager.cs`~~ - Replaced by `PersonnelManager`
- ~~`CoachJobMarketManager.cs`~~ - Merged into `PersonnelManager`

### Game Data

| File | Description |
|------|-------------|
| `SaveData.cs` | Complete save file structure (42KB) |
| `SeasonCalendar.cs` | Schedule, game dates, season phases |
| `SeasonStats.cs` | Player/team statistical records |
| `PlayoffData.cs` | Playoff bracket, series tracking |
| `GameLog.cs` | Game result logging |

### Gameplay Data

| File | Description |
|------|-------------|
| `PlayBook.cs` | Team playbooks with 15-20 plays, familiarity system, practice integration |
| `SetPlay.cs` | Individual play definitions with 20+ action types |
| `TeamStrategy.cs` | 11 offensive + 11 defensive schemes |
| `PlayerGameInstructions.cs` | Per-player game focus and tendencies |
| `CourtPosition.cs` | Spatial positioning system |
| `ShotMarkerData.cs` | Shot attempt event data for court visualization |

### Deep Coaching Strategy System

> **Recent Addition**: Comprehensive coaching strategy simulation with 6 integrated phases.

#### In-Game Analytics (Phase 1)

| File | Description |
|------|-------------|
| `GameAnalyticsTracker.cs` | Real-time possession-by-possession stats, shot charts, +/- tracking |
| `CoachingAdvisor.cs` | AI assistant suggesting adjustments based on game flow |
| `MatchupEvaluator.cs` | Real-time matchup quality scoring and mismatch detection |
| `PlayEffectivenessTracker.cs` | Tracks hot/cold plays, opponent adjustment detection |

#### Player Tendencies (Phase 2)

| File | Description |
|------|-------------|
| `PlayerTendencies.cs` | Innate (hard to change) and coachable behavioral tendencies |
| `TendencyCoachingManager.cs` | Training system for coachable tendencies |

#### Practice & Training System (Phase 3)

| File | Description |
|------|-------------|
| `PracticeSession.cs` | Practice sessions with focus areas, drills, intensity |
| `PracticeDrill.cs` | Individual drill types (shooting, defense, team, conditioning, film) |
| `PracticeManager.cs` | Orchestrates practices, schedules, fatigue, and development |
| `WeeklySchedule.cs` | Game days, practice days, off days, back-to-back handling |

#### Deep Mentorship System (Phase 4)

| File | Description |
|------|-------------|
| `MentorshipRelationship.cs` | Mentor-mentee pairings with strength, compatibility, milestones |
| `MentorshipManager.cs` | Orchestrates assignments, organic formation, development bonuses (up to 30%) |
| `MentorProfile.cs` | Mentor capabilities (teaching ability, patience, specialties) |

#### Opponent Chess Match (Phase 5)

| File | Description |
|------|-------------|
| `OpponentTendencyProfile.cs` | Comprehensive opponent analysis (offensive/defensive tendencies) |
| `OpponentAdjustmentPredictor.cs` | Predicts opponent coach adjustments based on personality/game state |
| `GamePlanBuilder.cs` | Pre-game preparation, matchups, contingency plans |

#### Staff Partnership System (Phase 6)

| File | Description |
|------|-------------|
| `CoordinatorAI.cs` | Enhanced AI for offensive/defensive coordinators, delegation support |
| `StaffMeeting.cs` | Pre-game/halftime meetings, staff contributions, disagreement handling |

### Dual-Role System

> **Recent Addition**: Comprehensive role selection system allowing play as GM, Coach, or Both with AI counterparts.

#### Role Configuration

| File | Description |
|------|-------------|
| `StaffRoles.cs` | UserRoleConfiguration, TeamStaffConfiguration, role helpers |

#### GM-Only Mode (Phase 2)

| File | Description |
|------|-------------|
| `AutonomousGameResult.cs` | Game results when AI coach runs games (box scores, moments, coach performance) |
| `AutonomousGameSimulator.cs` | Simulates games using AI coach personality |

#### Coach-Only Mode (Phase 3)

| File | Description |
|------|-------------|
| `RosterRequest.cs` | Roster request types, history, priorities, results |
| `AIGMController.cs` | AI GM with hidden personality traits, request processing |

#### Job Market System (Phase 4)

| File | Description |
|------|-------------|
| `JobMarketData.cs` | JobOpening, JobApplication, FiringDetails, UnsolicitedOffer, JobMarketState |
| `JobMarketManager.cs` | Firing, job openings, applications, interviews, offers |

#### AI Personality Discovery (Phase 5)

| File | Description |
|------|-------------|
| `AIPersonalityDiscovery.cs` | Trait observation, confidence levels, personality insights, relationship tracking |

### Trade AI System (NEW)

> **Recent Addition**: Intelligent AI trade system with value-based evaluation and proactive offers.

| File | Description |
|------|-------------|
| `PlayerValueCalculator.cs` | **NEW** - Stats-based player value evaluation (production, potential, contract value) |
| `DraftPickRegistry.cs` | **NEW** - Central registry for tracking draft pick ownership and trades |
| `AITradeOfferGenerator.cs` | **NEW** - AI teams proactively propose trades to the player |
| `TradeAnnouncementSystem.cs` | **NEW** - News generation when trades execute |
| `IncomingTradeOffer.cs` | **NEW** - Incoming offer data with expiration |

### Morale & Chemistry System (NEW)

> **Recent Addition**: Enhanced morale system with captain influence, contract satisfaction, and escalating effects.

| File | Description |
|------|-------------|
| `Personality.cs` | 13 personality traits, morale events, trait multipliers, **ContractSatisfaction**, **DiscontentLevel** |
| `MoraleChemistryManager.cs` | Game processing, locker room events, **team meetings**, **individual conversations** |
| `PersonalityManager.cs` | Pair/team chemistry, **captain influence**, **escalation processing** |

### Supporting Data

| File | Description |
|------|-------------|
| `DraftPick.cs` | Draft pick ownership/trading |
| `DevelopmentInstruction.cs` | Player development focus |
| `ScoutingReport.cs` | Text-based scouting |
| `InjuryData.cs` | Injury types, severity, recovery |
| `Personality.cs` | Player/staff personality traits |
| `Agent.cs` | Player agents (26KB) |
| `LeagueCBA.cs` | Salary cap rules |
| `TeamFinances.cs` | Revenue, expenses, budgets |
| `TrainingFacility.cs` | Training infrastructure |
| `AwardTypes.cs` | Award definitions |

---

## MANAGER SYSTEMS

### Personnel Management (Unified)

| Manager | File | Description |
|---------|------|-------------|
| **PersonnelManager** | `PersonnelManager.cs` | **CENTRAL** - All personnel operations |
| AdvanceScoutingManager | `AdvanceScoutingManager.cs` | Scout assignments and reports |
| FormerPlayerCareerManager | `FormerPlayerCareerManager.cs` | Former player pipelines |
| RetirementManager | `RetirementManager.cs` | Player & non-player retirement |
| JobSecurityManager | `JobSecurityManager.cs` | Coach job security |
| GMJobSecurityManager | `GMJobSecurityManager.cs` | GM progression and security |

### Roster & Contracts

| Manager | File | Description |
|---------|------|-------------|
| RosterManager | `RosterManager.cs` | Roster moves, waiving, signing, **OnPlayerRemoved event** |
| ContractNegotiationManager | `ContractNegotiationManager.cs` | Contract offers and negotiation |
| AgentManager | `AgentManager.cs` | Agent relationship management |
| SalaryCapManager | `SalaryCapManager.cs` | Cap calculations, exceptions |
| FreeAgentManager | `FreeAgentManager.cs` | Free agency operations |

### Trading & Transactions

| Manager | File | Description |
|---------|------|-------------|
| TradeSystem | `TradeSystem.cs` | Trade proposals and execution |
| TradeNegotiationManager | `TradeNegotiationManager.cs` | Multi-step negotiations |
| TradeValidator | `TradeValidator.cs` | CBA compliance checking |
| TradeFinder | `TradeFinder.cs` | AI trade partner matching |
| **AITradeEvaluator** | `AITradeEvaluator.cs` | **ENHANCED** - Uses PlayerValueCalculator for stats-based evaluation |
| **PlayerValueCalculator** | `PlayerValueCalculator.cs` | **NEW** - Production, potential, contract value assessment |
| **DraftPickRegistry** | `DraftPickRegistry.cs` | **NEW** - Draft pick ownership tracking |
| **AITradeOfferGenerator** | `AITradeOfferGenerator.cs` | **NEW** - AI proactive trade offers |
| **TradeAnnouncementSystem** | `TradeAnnouncementSystem.cs` | **NEW** - Trade news generation |

### Season & Competition

| Manager | File | Description |
|---------|------|-------------|
| PlayoffManager | `PlayoffManager.cs` | Playoffs and play-in tournament |
| AllStarManager | `AllStarManager.cs` | All-Star game selection |
| AwardManager | `AwardManager.cs` | MVP, DPOY, ROY, 6MOY, MIP, COY, All-Teams |
| HistoryManager | `HistoryManager.cs` | Records, Hall of Fame |
| LeagueEventsManager | `LeagueEventsManager.cs` | League-wide events |

### Development & Offseason

| Manager | File | Description |
|---------|------|-------------|
| PlayerDevelopmentManager | `PlayerDevelopmentManager.cs` | Attribute progression with mentorship integration |
| MentorshipManager | `MentorshipManager.cs` | Mentor-mentee relationships, development bonuses |
| TendencyCoachingManager | `TendencyCoachingManager.cs` | Player tendency training |
| PracticeManager | `PracticeManager.cs` | Practice sessions, drills, schedule |
| DraftSystem | `DraftSystem.cs` | Draft lottery and execution |
| DraftClassGenerator | `DraftClassGenerator.cs` | Prospect generation |
| ProspectGenerator | `ProspectGenerator.cs` | Procedural prospects |
| OffseasonManager | `OffseasonManager.cs` | Offseason phase coordination |
| SummerLeagueManager | `SummerLeagueManager.cs` | Summer league simulation |
| TrainingCampManager | `TrainingCampManager.cs` | Training camp and cuts |

### In-Game Coaching & AI

| Manager/AI | File | Description |
|------------|------|-------------|
| GameCoach | `GameCoach.cs` | Central in-game coaching with coordinator integration |
| CoordinatorAI | `CoordinatorAI.cs` | Offensive/defensive coordinator AI, delegation |
| CoachingAdvisor | `CoachingAdvisor.cs` | AI suggestion system during games |
| GameAnalyticsTracker | `GameAnalyticsTracker.cs` | Real-time game statistics |
| PlayEffectivenessTracker | `PlayEffectivenessTracker.cs` | Hot/cold play tracking |
| MatchupEvaluator | `MatchupEvaluator.cs` | Matchup quality and mismatch detection |
| OpponentAdjustmentPredictor | `OpponentAdjustmentPredictor.cs` | Predict opponent coach moves |
| GamePlanBuilder | `GamePlanBuilder.cs` | Pre-game preparation and contingencies |

### Dual-Role & Job Market

| Manager/AI | File | Description |
|------------|------|-------------|
| JobMarketManager | `JobMarketManager.cs` | Job openings, applications, interviews, offers |
| AIGMController | `AIGMController.cs` | AI GM decision-making with hidden personality |
| AutonomousGameSimulator | `AutonomousGameSimulator.cs` | Simulates games with AI coach |
| PersonalityDiscoveryManager | `AIPersonalityDiscovery.cs` | Tracks AI personality trait discovery |

### Morale & Chemistry

| Manager | File | Description |
|---------|------|-------------|
| **MoraleChemistryManager** | `MoraleChemistryManager.cs` | **ENHANCED** - Team meetings, individual conversations, captain effects |
| **PersonalityManager** | `PersonalityManager.cs` | **ENHANCED** - Captain influence, escalation processing |

### Team Operations

| Manager | File | Description |
|---------|------|-------------|
| FinanceManager | `FinanceManager.cs` | Team finances |
| RevenueManager | `RevenueManager.cs` | Game-by-game revenue |
| InjuryManager | `InjuryManager.cs` | Injuries and load management |
| MoraleChemistryManager | `MoraleChemistryManager.cs` | Team chemistry and morale |
| PersonalityManager | `PersonalityManager.cs` | Personality system |
| MediaManager | `MediaManager.cs` | Press conferences |
| ScoutingReportGenerator | `ScoutingReportGenerator.cs` | Text-based reports |

---

## SIMULATION ENGINE

### Core Files
| File | Description |
|------|-------------|
| `GameSimulator.cs` | Full game simulation via possession loop |
| `PossessionSimulator.cs` | Individual possession resolution |
| `BoxScore.cs` | Team and player statistics |
| `ShotCalculator.cs` | Shot probability formulas |
| `PlaySelector.cs` | AI play selection logic |

### Simulation Features
- 4 quarters + overtime
- Possession-by-possession gameplay
- Energy/fatigue drain
- Box score generation
- Quarter-by-quarter scoring

---

## UI PANELS

### Implemented Panels

| Panel | File | Description |
|-------|------|-------------|
| DashboardPanel | `DashboardPanel.cs` | Main hub with team overview, **trade news ticker** |
| RosterPanel | `RosterPanel.cs` | Team roster management |
| CalendarPanel | `CalendarPanel.cs` | Schedule view |
| StandingsPanel | `StandingsPanel.cs` | League standings |
| TradePanel | `TradePanel.cs` | Trade interface |
| DraftPanel | `DraftPanel.cs` | Draft experience |
| InboxPanel | `InboxPanel.cs` | Messages, notifications, **trade offer notifications** |
| StaffPanel | `StaffPanel.cs` | Staff management |
| StaffHiringPanel | `StaffHiringPanel.cs` | Staff hiring interface |
| MatchPanel | `MatchPanel.cs` | In-game coaching with animated court visualization |
| PreGamePanel | `PreGamePanel.cs` | Pre-game preparation |
| PostGamePanel | `PostGamePanel.cs` | Post-game results |
| PlayoffBracketPanel | `PlayoffBracketPanel.cs` | Playoff bracket view |
| NewGamePanel | `NewGamePanel.cs` | New game wizard (with role selection) |
| TeamSelectionPanel | `TeamSelectionPanel.cs` | Team selection |
| GameSummaryPanel | `GameSummaryPanel.cs` | GM-only game results view |
| RosterRequestPanel | `RosterRequestPanel.cs` | Coach roster requests to AI GM |
| JobMarketPanel | `JobMarketPanel.cs` | Job search when unemployed |

### Modal Dialogs (NEW)

| Modal | File | Description |
|-------|------|-------------|
| **CaptainSelectionPanel** | `CaptainSelectionPanel.cs` | **NEW** - Mandatory captain selection modal |
| **IncomingTradeOffersPanel** | `IncomingTradeOffersPanel.cs` | **NEW** - View and respond to AI trade offers |
| SlidePanel | `SlidePanel.cs` | Base class for animated slide-in modals |
| ConfirmationPanel | `ConfirmationPanel.cs` | Yes/No confirmation dialogs |
| PlayerSelectionPanel | `PlayerSelectionPanel.cs` | Generic player selection |
| ContractDetailPanel | `ContractDetailPanel.cs` | Contract details view |
| ProspectSelectionPanel | `ProspectSelectionPanel.cs` | Draft prospect selection |

### UI Components

| Component | Description |
|-----------|-------------|
| StaffRow | Staff member display row |
| CourtDiagramView | 2D court visualization (static formations) |
| CoachingMenuView | Tabbed coaching interface |
| AttributeDisplayFactory | Rating/attribute display |
| ScrollingTicker | News ticker |
| **AnimatedCourtView** | Animated 2D court with smooth player/ball movement |
| **AnimatedPlayerDot** | Player dot with team color, jersey number, hover tooltip |
| **BallAnimator** | Ball positioning with parabolic arc animations |
| **ShotMarkerUI** | Persistent shot location markers (green make/red miss) |
| **CaptainSelectionRow** | **NEW** - Row component for captain selection list |

---

## CAPTAIN SYSTEM (NEW)

### Overview

The Captain System allows users to designate a team captain who influences team morale and chemistry.

### Captain Selection Timing

| Trigger | Description |
|---------|-------------|
| **Season Start** | When transitioning from Preseason → RegularSeason, if no captain exists |
| **Trade** | If captain is traded away, prompt for new captain immediately |
| **Cut/Release** | If captain is waived/released, prompt for new captain immediately |

### Captain Effects

| Effect | Description |
|--------|-------------|
| **Morale Amplification** | Happy captain: +20% morale boost to teammates |
| **Negative Influence** | Unhappy captain: -10% morale penalty spread to team |
| **Staff Guidance** | Coaching staff recommends candidates based on leadership qualities (no numbers shown) |

### UI: CaptainSelectionPanel

**Features** (No Visible Attributes Philosophy):
- Players sorted by leadership (internally - numbers never shown)
- **Coaching staff provides qualitative recommendations** instead of numerical ratings
- Years of experience displayed (e.g., "7-year veteran")
- Staff feedback when selecting players with low leadership potential
- **MANDATORY**: Cannot be dismissed without selecting (no cancel button)
- Selection blocks game progression until captain is chosen

**Staff Recommendation Tiers** (based on hidden Leadership stat):
- "Staff's Top Choice" (gold) - highest leadership players
- "Recommended by Staff" (green) - strong leadership
- "Shows Leadership Potential" (white) - adequate leadership
- No badge - low leadership players

**Staff Feedback on Selection**:
- Players with concerns: "Your coaching staff has concerns about [Name]'s ability to command the locker room."
- Developing leaders: "Your coaching staff notes that [Name] is still developing as a vocal leader."

### Event Flow

```
SeasonController.OnPhaseChanged(RegularSeason)
    → GameManager checks for captain
    → If none: OnCaptainSelectionRequired event
    → GameSceneController shows CaptainSelectionPanel
    → User selects captain
    → GameManager.AssignCaptain(playerId)

TradeSystem.OnTradeExecuted(proposal)
    → GameManager checks if captain was traded
    → If yes: OnCaptainSelectionRequired(team, isReplacement=true)

RosterManager.OnPlayerRemoved(teamId, playerId)
    → GameManager checks if removed player was captain
    → If yes: OnCaptainSelectionRequired(team, isReplacement=true)
```

---

## TRADE AI SYSTEM (NEW)

### Overview

Intelligent trade AI that evaluates players based on production and potential rather than salary alone.

### PlayerValueCalculator

Stats-based player evaluation with the following factors:

| Factor | Weight | Description |
|--------|--------|-------------|
| **Production Value** | 40% | Based on OverallRating (0-100 scale) |
| **Potential Value** | 25% | HiddenPotential + age adjustment |
| **Contract Value** | 20% | Production vs. salary efficiency |
| **Age Curve** | 10% | Position-specific peak/decline curves |
| **Position Scarcity** | 5% | Rare positions worth more |

**Age Curves by Position**:
- Guards: Peak 27-30, decline at 31
- Wings: Peak 26-30, decline at 32
- Bigs: Peak 26-29, decline at 30

**FO Personality Modifiers**:
- Aggressive FOs: Higher value on young players
- Conservative FOs: Prefer proven veterans
- Analytics-focused: Weight contract efficiency higher

### DraftPickRegistry

Central registry tracking draft pick ownership across all teams.

**Features**:
- Initialize picks for each season
- Transfer picks between teams
- Stepien Rule validation (can't trade consecutive first-rounders)
- Save/load support

### AITradeOfferGenerator

AI teams proactively propose trades to the player.

**Frequency**: ~15% daily chance (1-2 offers per week average)

**Offer Lifecycle**:
1. AI evaluates player team roster for desirable targets
2. Generates offer based on FO personality
3. Offer expires in 3-7 days if no response
4. Player can Accept, Reject, or Counter

### TradeAnnouncementSystem

Generates news when trades execute.

**Output**:
- Headline: "BREAKING: Lakers acquire..."
- Summary: Trade breakdown
- Analysis: "Who won" breakdown
- Team grades: "LAL: A-", "BOS: B+"

**Display**:
- News ticker on Dashboard (scrolling headlines)
- Full details in Inbox
- Player team trades get priority treatment

---

## NBA RULES SYSTEM (NEW)

### Overview

Full NBA rules implementation with fouls, free throws, violations, timeouts, and substitution opportunities at dead balls.

### Core Files

| File | Description |
|------|-------------|
| `FoulSystem.cs` | **NEW** - Foul determination, team foul tracking, bonus/double bonus logic |
| `FreeThrowHandler.cs` | **NEW** - Quick-result free throw calculation with clutch modifiers |
| `ViolationChecker.cs` | **NEW** - Attribute-based violation detection (traveling, backcourt, 3-second) |
| `TimeoutIntelligence.cs` | **NEW** - AI timeout decision logic with priority system |
| `RulesEnums.cs` | **NEW** - FoulType, ViolationType, FreeThrowScenario enums |

### Foul System

**Foul Types**:
- Personal fouls (shooting vs non-shooting)
- Loose ball fouls
- Offensive fouls
- Technical fouls (volatile players, low composure)
- Flagrant 1 & 2 (clutch time only, 2% review rate)

**Team Foul Tracking**:
- 5+ team fouls: BONUS (2 free throws on non-shooting fouls)
- 10+ team fouls: DOUBLE BONUS
- Team fouls reset each quarter

**Foul Probability Formula**:
```
Base: 18% (~22 fouls/team/game)
Modifiers:
  - DefensiveIQ: ±10%
  - Composure: ±10%
  - CloseoutControl: ±12.5%
  - DefensiveGambling: +20%
  - Aggression: ±12.5%
  - Rim attacks: +10%
```

### Free Throw System

**Quick Result Mode**:
- Uses shooter's FreeThrow attribute (0-100)
- Clutch modifier from Clutch attribute
- "Icing" penalty if timeout just called
- Returns made/attempts + play-by-play text

**Free Throw Scenarios**:
- Two shots (non-shooting bonus foul)
- Two shots (shooting foul on 2-point attempt)
- Three shots (shooting foul on 3-point attempt)
- One shot (and-one after made basket)
- Technical (1 shot, retain possession)
- Flagrant (2 shots, retain possession)

### Violation System

**Attribute-Based Probabilities**:
| Violation | Base Chance | Key Attribute |
|-----------|-------------|---------------|
| Traveling | ~0.125% | BallHandling |
| Backcourt | ~0.0625% | BasketballIQ |
| 3-Second | ~0.1% | BasketballIQ (centers/PFs) |

Violations count as turnovers in statistics.

### AI Timeout Intelligence

**Priority System**:
| Priority | Trigger | Reason |
|----------|---------|--------|
| 100 | 8+ unanswered points | StopRun |
| 90 | Clutch FTs, close game | IcingShooter |
| 80 | Last 2 min, trailing | AdvanceBall |
| 70 | Clutch, opponent just scored | DrawUpPlay |
| 60 | 2+ starters below 55% energy | RestPlayers |
| 50 | End of half, use-or-lose | Mandatory |

### Dead Ball Detection

Dead balls trigger at:
- Fouls (any type)
- Timeouts
- Out of bounds turnovers
- Made baskets (before inbound)

At dead balls:
- AI timeout check runs
- Substitution opportunity offered to player
- Foul trouble/fatigue alerts displayed

### Foul Trouble Indicators (No Visible Attributes)

| Situation | Display Text |
|-----------|--------------|
| 5 fouls | "FOUL TROUBLE: One more and he's out!" |
| 3+ fouls in 1st half | "FOUL TROUBLE: Heavy foul load early" |
| 4 fouls in Q3 | "FOUL TROUBLE: Walking a tightrope" |
| 4 fouls in Q4 | "FOUL TROUBLE: In danger of fouling out" |

---

## INITIAL DATA SYSTEM (NEW)

### Overview

Real-world NBA data as of December 2025 for GM profiles and draft pick ownership, loaded at game initialization.

### GM Profiles

**File**: `Assets/Resources/Data/initial_front_offices.json`

Contains all 30 NBA GM profiles with:
- Name, title, years in position
- Competence rating (Elite, Good, Average, Poor, Terrible)
- Trade evaluation/negotiation/scouting skills (0-100)
- Trade aggression and team situation
- Behavioral traits (patience, risk tolerance, leak tendency)

**Competence Distribution**:
| Rating | Count | Example GMs |
|--------|-------|-------------|
| Elite | 5 | Sam Presti (OKC), Brad Stevens (BOS), Trajan Langdon (DET) |
| Good | 10 | Mike Dunleavy Jr. (GSW), Jon Horst (MIL), Zach Kleiman (MEM) |
| Average | 10 | Various new/unproven GMs |
| Poor | 4 | Teams with recent struggles |
| Terrible | 1 | Reserved for historically bad decisions |

**Team Situations** (December 2025):
| Situation | Teams |
|-----------|-------|
| Championship | OKC, BOS, CLE, DEN |
| Contending | MIL, NYK, GSW, LAL, MEM, IND, ORL, MIN |
| PlayoffBubble | MIA, PHI, PHX, DAL, SAC, ATL, CHI |
| Rebuilding | DET, HOU, SAS, UTA, POR, WAS, CHA |
| StuckInMiddle | TOR, BKN, NOP, LAC |

### Draft Pick Registry

**File**: `Assets/Resources/Data/initial_draft_picks.json`

Contains all traded first-round picks with protections and swap rights.

**Statistics** (December 2025):
- **38 traded first-round picks** (2025-2031)
- **14 swap rights** (2026-2030)

**Pick-Rich Teams**:
| Team | Incoming Picks | Notable Assets |
|------|----------------|----------------|
| OKC | ~12 first-rounders | PHI 2026, LAC 2026/2028/2030, HOU 2027, DEN 2027/2029 |
| UTA | ~8 first-rounders | CLE 2027/2029, LAL 2027, MIN 2027/2029 |
| BKN | ~8 first-rounders | NYK 2027/2029/2031, PHX 2027/2029, DAL 2029 |
| MEM | ~5 first-rounders | ORL 2028/2030, PHX 2026 swap |

**Pick-Poor Teams**:
- Cleveland (multiple to Utah from Mitchell trade)
- Denver (picks to OKC)
- LA Clippers (to OKC)
- New York (to Brooklyn from Bridges trade)

### Protection Types

| Type | Description | Example |
|------|-------------|---------|
| TopN | Protected if pick is in top N | Top-4 protected |
| Lottery | Protected if in lottery (1-14) | Lottery protected |
| Range | Protected if in specific range | 15-30 protected |

### Final Conveyance Types

| Type | Description |
|------|-------------|
| ConveyUnprotected | Becomes unprotected in final year |
| BecomeSecondRound | Converts to second-round pick if never conveys |
| Void | Pick disappears if never conveys |

### Data Classes

| File | Description |
|------|-------------|
| `InitialFrontOfficeData.cs` | Serialization classes for GM profiles |
| `InitialDraftPickData.cs` | Serialization classes for draft picks |

### Integration

**GM Data Loading**:
```csharp
// In AITradeOfferGenerator.LoadInitialFrontOfficeData()
var data = JsonUtility.FromJson<InitialFrontOfficeData>(jsonAsset.text);
foreach (var entry in data.frontOffices)
    RegisterFrontOffice(entry.ToFrontOfficeProfile());
```

**Draft Pick Loading**:
```csharp
// In DraftPickRegistry.InitializeForSeason() → LoadInitialDraftData()
// Applies traded picks, protections, and swap rights from JSON
```

---

## MORALE & CHEMISTRY SYSTEM (ENHANCED)

### Overview

Enhanced morale system with captain influence, contract satisfaction, expectation-based effects, and player intervention options.

### Morale Factors

| Factor | Impact | Description |
|--------|--------|-------------|
| **Playing Time** | High | Minutes played vs expected (based on salary/rating) |
| **Team Performance** | Medium | Win/loss effects based on team expectations |
| **Personal Performance** | Medium | Stats vs career averages, awards |
| **Contract Satisfaction** | Medium | New contract boost, underpaid penalty |
| **Captain Influence** | High | Captain morale amplifies/spreads to team |

### Expectation-Based Win/Loss Effects

| Team Type | Win Effect | Loss Effect |
|-----------|------------|-------------|
| Contenders (>60% expected) | +1 morale | -5 morale |
| Playoff teams (40-60%) | +3 morale | -3 morale |
| Lottery teams (<40%) | +5 morale | -1 morale |

### Contract Satisfaction

| Situation | Effect |
|-----------|--------|
| New contract signed | +30 boost, decays over time |
| Underpaid (salary < market) | -20 ongoing |
| Contract year | ±10 mixed anxiety/motivation |
| Passed over for extension | -25 immediate |

### Escalating Behavioral Effects

Players who remain unhappy escalate through a 5-step ladder:

| Level | Effect | Description |
|-------|--------|-------------|
| 1 | Private Complaint | Inbox message from player |
| 2 | Reduced Effort | Mental stats -5% |
| 3 | Media Comments | News ticker comment |
| 4 | Trade Request | Formal demand to be traded |
| 5 | Holdout | Refuses to play |

**Trigger**: Morale below 30 for 7+ days → escalate
**De-escalate**: Morale above 50 for 14+ days → reduce level

### Player Intervention Options

#### Team Meetings
```
Success (70%): All players +5 morale
Backfire (30%): Volatile players -5, others +2
Cooldown: 14 days between meetings
Coach Leadership improves success odds
```

#### Individual Conversations
| Type | Effect |
|------|--------|
| Promise Playing Time | +10 morale, creates expectation |
| Explain Role | +5 morale, reduces expected role |
| Offer Trade | Variable based on destination |
| Praise | +8 morale (Sensitive players: +12) |
| Constructive Criticism | ±0 morale, improves development |

### Enhanced Pairwise Chemistry

High chemistry pairs (+0.5 or higher):
- +15% assist success rate when passing to each other
- +10% screen effectiveness
- +5% defensive rotation timing

Low chemistry pairs (-0.3 or lower):
- -10% pass success rate
- Reluctant to pass (lower pass frequency)
- -5% defensive communication

---

## FORMER PLAYER CAREER SYSTEM

### Overview

Former players can enter coaching, scouting, or front office careers after retirement.

### Career Pipelines

| Pipeline | Entry Point | Requirements |
|----------|-------------|--------------|
| **Coaching** | Assistant Coach | 5+ NBA seasons, Leadership ≥60 |
| **Scouting** | Regional Scout | 3+ NBA seasons, BasketballIQ ≥65 |
| **Front Office** | Scout/Assistant | 8+ NBA seasons, Leadership ≥70 |

### Progression

**Coaching Track**:
Assistant Coach → Position Coach → Coordinator → Head Coach

**Scouting Track**:
Regional Scout → National Scout → Director of Scouting

**Front Office Track**:
Scout → Assistant GM → General Manager

### Cross-Track Transitions

| Transition | Requirements |
|------------|--------------|
| HC → GM | 5+ years as HC, 60%+ win rate |
| GM → HC | 3+ years as GM, playoff success |
| Scout → Coach | 3+ years scouting, recommendation |
| Coordinator → AGM | 4+ years as coordinator |

---

## UTILITIES

### NameGenerator (`Core/Util/NameGenerator.cs`)

Procedural name generation using Markov chains with realistic NBA demographics.

**Features**:
- 12 nationality profiles (American, African-American, European, Latin American, etc.)
- Character-level Markov chain for novel but plausible names
- Separate tracking for player vs. staff names to prevent overlap
- NBA-realistic demographic weighting (~75% American/African-American)

**Integration Points**:
- `PersonnelManager.CreateRandomProfile()` - Staff names
- `ProspectGenerator.GenerateProspect()` - Player names

---

## IMPLEMENTATION STATUS

### Fully Implemented ✅

| Feature | Status |
|---------|--------|
| Match Simulator | ESPN Gamecast-style simulation |
| Playoff System | Play-in + bracket |
| Injury System | Generation, recovery, load management |
| Awards System | MVP, DPOY, ROY, 6MOY, MIP, COY, All-Teams |
| Job Security | Coach hot seat with owner meetings |
| Media System | Press conferences with consequences |
| Financial System | Revenue model, projections |
| Scouting | Text-based reports only |
| History & Records | Season archives, franchise records, HoF |
| Save System | Complete with Ironman mode |
| Unified Career System | Coach↔GM transitions, cross-track careers |
| Personnel Refactor | Single PersonnelManager architecture |
| Name Generation | Markov chain for player/staff names |
| **Deep Coaching Strategy** | **6-phase comprehensive coaching simulation** |
| In-Game Analytics | Real-time stats, shot charts, +/-, run detection |
| Coaching Advisor | AI suggestions for adjustments during games |
| Play Effectiveness | Hot/cold play tracking, opponent adjustment detection |
| Matchup Evaluation | Real-time matchup scoring, hunt/hide recommendations |
| Player Tendencies | Innate + coachable behavioral tendencies |
| Practice System | Sessions, drills, weekly schedules, fatigue management |
| Mentorship System | Veteran-rookie relationships, up to 30% dev bonus |
| Opponent Prediction | Coach personality-based adjustment prediction |
| Game Plan Builder | Pre-game prep, matchups, contingency plans |
| Staff Partnership | Coordinator AI, delegation, staff meetings |
| **Dual-Role System** | **5-phase role selection and AI counterpart system** |
| Role Selection | Choose GM, Coach, or Both when starting new career |
| GM-Only Mode | AI coach runs games autonomously, view summaries |
| Coach-Only Mode | Submit roster requests to AI GM for approval |
| Job Market | After firing, search and apply for new positions |
| AI Personality Discovery | Learn AI traits through interactions over time |
| **Animated Court Visualization** | Real-time court visualization with moving players and ball |
| Animated Player Dots | Team-colored dots with jersey numbers, smooth interpolation |
| Ball Animation | Parabolic arc passes and shots with shadow effects |
| Shot Markers | Persistent make/miss markers on court with auto-fade |
| Spatial State Events | Real-time position updates from possession simulator |
| **Trade AI System** | **NEW** - Stats-based player evaluation, proactive AI offers |
| PlayerValueCalculator | Production, potential, contract value assessment |
| DraftPickRegistry | Central pick ownership tracking with Stepien Rule |
| AITradeOfferGenerator | AI teams propose trades to player (~1-2/week) |
| TradeAnnouncementSystem | News generation for executed trades |
| IncomingTradeOffersPanel | UI for viewing/responding to AI offers |
| **Captain System** | **NEW** - Captain designation with morale influence |
| Captain Selection Modal | Mandatory selection at season start |
| Captain Replacement | Automatic prompt when captain traded/cut |
| Captain Morale Effects | +20%/-10% morale amplification |
| **Enhanced Morale System** | **NEW** - Contract satisfaction, escalation, interventions |
| Contract Satisfaction | Morale effects from contract situations |
| Expectation-Based Effects | Win/loss morale scaled to team expectations |
| Escalating Behavior | 5-step discontent ladder (complaint → holdout) |
| Team Meetings | Coach intervention with success/backfire odds |
| Individual Conversations | One-on-one talks with various approaches |
| Enhanced Pair Chemistry | On-court bonuses for high/low chemistry pairs |
| **Former Player Careers** | **NEW** - Coaching/scouting/GM career paths |
| **NBA Rules System** | **NEW** - Full foul, free throw, violation, timeout implementation |
| FoulSystem | Personal, shooting, loose ball, offensive, technical, flagrant fouls |
| FreeThrowHandler | Quick-result FT calculation with clutch modifiers |
| ViolationChecker | Traveling, backcourt, 3-second violations |
| TimeoutIntelligence | AI timeout decision with priority system |
| Team Foul Tracking | Bonus/double bonus with quarter reset |
| Dead Ball Detection | Substitution opportunities at fouls/timeouts/scores |
| **Initial Data System** | **NEW** - Real-world December 2025 NBA data |
| GM Profiles | 30 NBA GM profiles with competence, skills, trade tendencies |
| Draft Pick Registry | 38 traded picks, 14 swap rights (2025-2031) |
| Automatic Loading | Data loaded during game initialization |

### Outstanding Issues / TODOs

| Location | Issue | Priority |
|----------|-------|----------|
| `DashboardPanel.cs:271` | "Add highlight player stats" - Feature incomplete | Low |
| `MatchViewer.cs:58` | "Fetch name" - Player names not loaded in match view | Low |
| `PersonnelManager.cs` | Limited contract negotiation UI feedback | Medium |

### Known Gaps

1. **Contract Popup System** - No modal/popup infrastructure; contract details shown in debug log
2. **Summer League UI** - Manager exists but limited UI integration
3. **Training Camp UI** - Manager exists but no dedicated panel

---

## CODING CONVENTIONS

### Namespaces
- `NBAHeadCoach.Core` - Core game logic
- `NBAHeadCoach.Core.Data` - Data models
- `NBAHeadCoach.Core.Manager` - Domain managers
- `NBAHeadCoach.Core.Simulation` - Simulation engine
- `NBAHeadCoach.Core.AI` - AI systems
- `NBAHeadCoach.Core.Util` - Utilities
- `NBAHeadCoach.UI` - UI base classes
- `NBAHeadCoach.UI.Panels` - Panel implementations
- `NBAHeadCoach.UI.Modals` - Modal dialogs
- `NBAHeadCoach.UI.Components` - Reusable components

### Patterns Used
- **Singleton**: GameManager, SeasonController, PersonnelManager
- **Registry**: Panel registration in GameSceneController
- **Event System**: C# events for loose coupling
- **Factory**: AttributeDisplayFactory, name generation
- **Facade**: StaffManagementManager (central staff operations)

### Key APIs

**PersonnelManager** (Central personnel API):
```csharp
// Query
GetProfile(profileId) → UnifiedCareerProfile
GetTeamStaff(teamId) → List<UnifiedCareerProfile>
GetUnemployedPool() → List<UnifiedCareerProfile>
GetHeadCoach(teamId) → UnifiedCareerProfile
GetScouts(teamId) → List<UnifiedCareerProfile>

// Operations
HirePersonnel(profile, teamId, role)
FirePersonnel(teamId, profileId, reason)
StartNegotiation(profileId, teamId) → StaffNegotiationSession
MakeOffer(sessionId, amount, years) → NegotiationResponse

// Assignment
AssignStaffToTask(teamId, taskAssignment)
GetActiveAssignments(teamId) → List<StaffTaskAssignment>
```

**NameGenerator** (Name generation API):
```csharp
GeneratePlayerName(nationality?) → GeneratedName
GenerateStaffName(nationality?) → GeneratedName
ClearTrackedNames() // Call on new game
```

**GameCoach** (In-game coaching with coordinator integration):
```csharp
// Coordinator AI
InitializeCoordinatorAI(teamId, offensiveCoordinator, defensiveCoordinator)
GetCoordinatorSuggestions() → List<CoordinatorGameSuggestion>
AcceptCoordinatorSuggestion(suggestion)
RejectCoordinatorSuggestion(suggestion, reason)

// Delegation
DelegateOffense(delegated)  // Let OC call plays
DelegateDefense(delegated)  // Let DC adjust schemes
DelegateTimeouts(delegated) // Let staff call timeouts
GetDelegatedPlayCall() → DelegatedPlayCall
GetDelegatedDefensiveCall() → DelegatedDefensiveCall

// Staff Meetings
SetPreGameMeeting(meeting)
GetGamePlanBonus() → float
```

**MoraleChemistryManager** (Enhanced morale API):
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

**TradeSystem** (Enhanced trade API):
```csharp
// Incoming offers
GetPendingIncomingOffers() → List<IncomingTradeOffer>
RespondToOffer(offerId, response) → TradeOfferResponse

// Announcements
OnTradeAnnounced → event Action<TradeAnnouncement>

// Execution
ExecuteTrade(proposal) → TradeResult
```

**GameManager** (Captain system additions):
```csharp
// Captain events
OnCaptainSelectionRequired → event Action<Team, bool>  // Team, isReplacement

// Captain operations
AssignCaptain(playerId)
GetPlayerTeamCaptain() → Player
```

**PersonnelManager** (Coordinator management additions):
```csharp
// Coordinator queries
GetOffensiveCoordinator(teamId) → UnifiedCareerProfile
GetDefensiveCoordinator(teamId) → UnifiedCareerProfile
GetCoordinators(teamId) → List<UnifiedCareerProfile>
GetStaffSynergyBonus(teamId) → float

// Staff meetings
CreatePreGameMeeting(teamId, opponentId, opponentName, gameDate) → StaffMeeting
CreateHalftimeMeeting(teamId, opponentId, opponentName, teamScore, oppScore) → StaffMeeting
GenerateMeetingContributions(teamId, opponentProfile) → List<StaffContribution>
```

**MentorshipManager** (Mentorship API):
```csharp
AssignMentor(mentorId, menteeId, teamId, focusAreas) → (success, message, relationship)
GetMentorshipDevelopmentBonus(menteeId) → float  // Up to 0.30 (30%)
ProcessPracticeMentorshipSessions(teamId, roster, sessionType) → List<MentorshipSessionResult>
CheckOrganicFormation(teamId, roster) → List<MentorshipRelationship>
```

**GameManager** (Role system additions):
```csharp
// Role configuration
UserRoleConfig → UserRoleConfiguration
UserControlsRoster → bool  // True if GM or Both
UserControlsGames → bool   // True if Coach or Both
GetAICoach() → UnifiedCareerProfile  // If GM-only mode
GetAIGM() → UnifiedCareerProfile     // If Coach-only mode

// Career transitions
StartNewCareerFromJobMarket(teamId, newRole, salary, contractYears)
```

**JobMarketManager** (Job market API):
```csharp
// Firing
HandlePlayerFired(reason, publicStatement)
GetFiringDetails() → FiringDetails

// Job search
GetAvailableJobs(roleFilter?) → List<JobOpening>
ApplyForJob(openingId, coverLetter) → JobApplication
AcceptJob(openingId) → bool
DeclineJob(openingId)

// Offers
GetUnsolicitedOffers() → List<UnsolicitedOffer>
GetMarketSummary() → JobMarketSummary
```

**AIGMController** (AI GM API for coaches):
```csharp
ProcessRequest(RosterRequest) → RosterRequestResult
GetKnownPersonalityDescription() → string
RequestHistory → RosterRequestHistory
```

**PersonalityDiscoveryManager** (AI trait learning):
```csharp
GetDiscovery(aiProfileId, aiName, isGM) → AIPersonalityDiscovery
RecordObservation(aiProfileId, traitKey, positiveEvidence, context)
GetInsights(aiProfileId) → List<string>
```

---

## GETTING STARTED FOR DEVELOPERS

### Quick Start
1. Open project in Unity 2022.3+
2. Open `Assets/Scenes/Boot.unity`
3. Press Play
4. Boot → MainMenu → New Game

### Key Entry Points
- `GameManager.cs` - Start here for game flow
- `PersonnelManager.cs` - For personnel/staff features
- `SeasonController.cs` - For season/calendar features
- `MatchSimulationController.cs` - For match simulation

### Adding New Features
1. Create data classes in `Core/Data/`
2. Create manager in `Core/Manager/` (register in GameManager if needed)
3. Create UI panel in `UI/Panels/` extending `BasePanel`
4. Register panel in `GameSceneController._panelRegistry`

---

## CHANGE LOG

### December 2025 - NBA Rules System & Initial Data

- **NBA Rules System** - Full NBA rules implementation for match simulation
  - `FoulSystem.cs` - Team foul tracking, bonus/double bonus, all foul types
  - `FreeThrowHandler.cs` - Quick-result FT with clutch/icing modifiers
  - `ViolationChecker.cs` - Attribute-based traveling, backcourt, 3-second
  - `TimeoutIntelligence.cs` - AI timeout decisions (stop run, icing, advance ball)
  - `RulesEnums.cs` - FoulType, ViolationType, FreeThrowScenario enums
  - Dead ball detection with substitution opportunities
  - Enhanced `MatchSimulationController.cs` with foul system integration
  - Enhanced `PossessionSimulator.cs` with foul/violation checks
  - Enhanced `GameSimulator.cs` with team foul reset per quarter
- **Initial Data System** - Real-world NBA data as of December 2025
  - `initial_front_offices.json` - All 30 NBA GM profiles
    - Sam Presti (OKC), Brad Stevens (BOS), Rob Pelinka (LAL) as Elite
    - Competence ratings, trade tendencies, skills (0-100)
    - Team situations (Championship, Contending, Rebuilding, etc.)
  - `initial_draft_picks.json` - Complete traded pick registry
    - 38 traded first-round picks (2025-2031)
    - 14 swap rights (2026-2030)
    - Protection types: TopN, Lottery, Range
    - Conveyance types: ConveyUnprotected, BecomeSecondRound, Void
  - `InitialFrontOfficeData.cs` - GM profile serialization classes
  - `InitialDraftPickData.cs` - Draft pick serialization classes
  - Enhanced `DraftPickRegistry.cs` with `LoadInitialDraftData()`
  - Enhanced `AITradeOfferGenerator.cs` with `LoadInitialFrontOfficeData()`

### December 2024 - Captain System & Trade AI
- **Captain System** - Designate team captain with morale influence
  - Captain selection modal at season start (Preseason → RegularSeason)
  - Automatic replacement prompt when captain traded or cut
  - Leadership-based recommendations (≥70 green, ≥60 yellow, <60 red)
  - Captain morale amplification (+20% happy, -10% unhappy)
  - `CaptainSelectionPanel.cs` - Mandatory selection modal
  - `RosterManager.OnPlayerRemoved` event for cut/waive detection
  - `GameManager.OnCaptainSelectionRequired` event
- **Trade AI System** - Intelligent value-based trade evaluation
  - `PlayerValueCalculator.cs` - Stats-based player value (production, potential, contract)
  - `DraftPickRegistry.cs` - Central pick ownership tracking
  - `AITradeOfferGenerator.cs` - AI proactive trade offers (~1-2/week)
  - `TradeAnnouncementSystem.cs` - Trade news generation
  - `IncomingTradeOffersPanel.cs` - UI for AI trade offers
  - Age curves by position (guards peak 27-30, wings 26-30, bigs 26-29)
  - FO personality affects value assessment
- **Enhanced Morale System** - Contract satisfaction and escalation
  - Contract satisfaction (+30 new contract, -20 underpaid, -25 passed over)
  - Expectation-based win/loss (contenders: +1/-5, lottery: +5/-1)
  - 5-step escalation ladder (complaint → reduced effort → media → trade request → holdout)
  - Team meetings (70% success, 14-day cooldown)
  - Individual conversations (promise time, explain role, praise, constructive)
  - Enhanced pair chemistry on-court bonuses

### December 2024 - Former Player Career System
- **Former Player Careers** - Coaching/scouting/GM career paths for retired players
  - Coaching track: Assistant → Position Coach → Coordinator → Head Coach
  - Scouting track: Regional → National → Director
  - Front Office track: Scout → Assistant GM → GM
  - Cross-track transitions (HC↔GM, Scout→Coach, Coordinator→AGM)
  - `FormerPlayerCareerManager.cs` - Pipeline management
  - `FormerPlayerProgressionData.cs` - Progression tracking

### December 2024 - Animated Court Visualization
- **Animated Court View** - Real-time 2D court visualization with smooth player movement
- **Player Dots** - Team-colored circles with jersey numbers, smooth position interpolation
- **Ball Animator** - Ball attaches to carrier, parabolic arc animations for passes/shots
- **Shot Markers** - Persistent shot location markers (green makes, red misses) with auto-fade
- Added `AnimatedCourtView.cs` - Main court visualization component with state buffer
- Added `AnimatedPlayerDot.cs` - Player dot with tooltip showing name, stats, energy
- Added `BallAnimator.cs` - Ball flight coroutines with shadow depth effect
- Added `ShotMarkerUI.cs` - Shot marker with appear/fade animations
- Added `ShotMarkerData.cs` - Shot event data struct
- Enhanced `MatchSimulationController.cs` with `OnSpatialStateUpdate` and `OnShotAttempt` events
- Enhanced `MatchPanel.cs` with animated court integration and layout support
- Coordinate transformation: Court (X: 0-47, Y: -25 to +25) → UI rect positions

### December 2024 - Dual-Role System
- **Phase 1: Role Selection** - Added role choice (GM/Coach/Both) to new game wizard
- **Phase 2: GM-Only Mode** - AI coach runs games autonomously with `AutonomousGameSimulator`
- **Phase 3: Coach-Only Mode** - Submit roster requests to AI GM via `RosterRequestPanel`
- **Phase 4: Job Market** - After firing, search and apply for positions via `JobMarketPanel`
- **Phase 5: AI Personality Discovery** - Learn AI traits through interactions over time
- Added `UserRoleConfiguration` for role tracking and AI counterpart references
- Added `JobMarketManager` with firing, applications, interviews, offers
- Added `AIGMController` with hidden personality traits
- Added `AIPersonalityDiscovery` system with observation tracking
- Enhanced `GameManager` with role-aware helpers and career transitions
- Enhanced `MatchFlowController` with autonomous game simulation support
- Added `JobMarket` state to `GameState` enum
- Added `RoleSelection` step to new game wizard

### December 2024 - Deep Coaching Strategy Simulation
- **Phase 1: In-Game Analytics** - Added real-time game stats, coaching advisor, matchup evaluation, play effectiveness tracking
- **Phase 2: Player Tendencies** - Added innate/coachable behavioral tendencies with training system
- **Phase 3: Practice System** - Added practice sessions, drills, weekly schedules, familiarity gains
- **Phase 4: Mentorship System** - Added mentor-mentee relationships with up to 30% development bonus
- **Phase 5: Opponent Chess Match** - Added opponent tendency profiles, adjustment prediction, game plan builder
- **Phase 6: Staff Partnership** - Added CoordinatorAI with delegation, staff meetings with contributions/disagreements
- Enhanced `GameCoach.cs` with coordinator integration and delegation controls
- Enhanced `PersonnelManager.cs` with coordinator queries and meeting generation
- Enhanced `Player.cs` with tendencies and mentor profile
- Enhanced `PlayBook.cs` with practice integration and installation tracking
- Added `OffensiveCoordinator` and `DefensiveCoordinator` to `UnifiedRole` enum

### December 2024 - Personnel Refactor
- Consolidated 9 legacy manager classes into `PersonnelManager`
- Created `UnifiedCareerProfile` for all personnel types
- Migrated negotiation system to `StaffNegotiation.cs`
- Added `NameGenerator` with Markov chain
- Fixed API mismatches across managers
- Connected `StaffPanel` to `StaffHiringPanel`

---

*This document should be updated when major architectural changes occur.*

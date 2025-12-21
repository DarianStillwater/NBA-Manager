# NBA Head Coach – Complete Design Document

> **Purpose**: This document is the single source of truth for understanding the entire game design. An AI or developer can reference this without scanning all project files.
> 
> **Last Updated**: December 2024

---

## GAME OVERVIEW

**Genre**: Single-player NBA franchise management simulation  
**Platform**: Unity (Windows)  
**Perspective**: You play as an NBA head coach managing your team through seasons  

**Core Loop**:
1. Manage roster (trades, free agency, contracts)
2. Develop players (training, playing time)
3. Coach games (strategy, play calling, substitutions)
4. Navigate seasons (82 games → playoffs → offseason)
5. Build coaching legacy across multiple careers

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
│   ├── AI/                 # AI coach personalities, trade evaluation (3 files)
│   ├── Data/               # All data models - 30 classes (see Data Models section)
│   ├── Gameplay/           # In-game coaching logic (1 file)
│   ├── Manager/            # Domain managers - 28 managers (see Manager Systems)
│   ├── Simulation/         # Game/possession simulation engine (6 files)
│   ├── Util/               # Utilities like NameGenerator
│   ├── GameManager.cs      # Central game orchestrator
│   ├── SeasonController.cs # Season flow and calendar
│   ├── MatchFlowController.cs # Pre/post game flow
│   ├── MatchSimulationController.cs # Match simulation
│   ├── PlayByPlayGenerator.cs # Broadcast-style text
│   └── SaveLoadManager.cs  # Persistence with Ironman mode
├── UI/
│   ├── Panels/             # 13 UI panels (Dashboard, Roster, Trade, etc.)
│   ├── Components/         # 14 reusable UI components
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

**Events**:
- `OnStateChanged` - State transitions
- `OnNewGameStarted` - Fresh career started
- `OnGameLoaded` - Save loaded
- `OnDayAdvanced` - Calendar progressed
- `OnSeasonChanged` - New season started

### SeasonController
**File**: `Assets/Scripts/Core/SeasonController.cs`

Manages the 82-game schedule, calendar, standings, and season phases.

**Phases**: Preseason → Regular Season → Play-In → Playoffs → Draft → Free Agency → Offseason

### MatchFlowController
**File**: `Assets/Scripts/Core/MatchFlowController.cs`

Bridges calendar events to simulation; handles pre-game, match, and post-game flow.

---

## DATA MODELS

### Core Entities

| File | Description | Size |
|------|-------------|------|
| `Player.cs` | Complete player data (attributes, career, personality) | 42KB |
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
| `SaveData.cs` | Complete save file structure (38KB) |
| `SeasonCalendar.cs` | Schedule, game dates, season phases |
| `SeasonStats.cs` | Player/team statistical records |
| `PlayoffData.cs` | Playoff bracket, series tracking |
| `GameLog.cs` | Game result logging |

### Gameplay Data

| File | Description |
|------|-------------|
| `PlayBook.cs` | Team playbooks with 15-20 plays |
| `SetPlay.cs` | Individual play definitions |
| `TeamStrategy.cs` | Offensive/defensive schemes |
| `PlayerGameInstructions.cs` | Per-player game focus |
| `CourtPosition.cs` | Spatial positioning system |

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
| RosterManager | `RosterManager.cs` | Roster moves, waiving, signing |
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

### Season & Competition

| Manager | File | Description |
|---------|------|-------------|
| PlayoffManager | `PlayoffManager.cs` | Playoffs and play-in tournament |
| AllStarManager | `AllStarManager.cs` | All-Star game selection |
| AwardManager | `AwardManager.cs` | MVP, DPOY, All-NBA voting |
| HistoryManager | `HistoryManager.cs` | Records, Hall of Fame |
| LeagueEventsManager | `LeagueEventsManager.cs` | League-wide events |

### Development & Offseason

| Manager | File | Description |
|---------|------|-------------|
| PlayerDevelopmentManager | `PlayerDevelopmentManager.cs` | Attribute progression |
| DraftSystem | `DraftSystem.cs` | Draft lottery and execution |
| DraftClassGenerator | `DraftClassGenerator.cs` | Prospect generation |
| ProspectGenerator | `ProspectGenerator.cs` | Procedural prospects |
| OffseasonManager | `OffseasonManager.cs` | Offseason phase coordination |
| SummerLeagueManager | `SummerLeagueManager.cs` | Summer league simulation |
| TrainingCampManager | `TrainingCampManager.cs` | Training camp and cuts |

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
| DashboardPanel | `DashboardPanel.cs` | Main hub with team overview |
| RosterPanel | `RosterPanel.cs` | Team roster management |
| CalendarPanel | `CalendarPanel.cs` | Schedule view |
| StandingsPanel | `StandingsPanel.cs` | League standings |
| TradePanel | `TradePanel.cs` | Trade interface |
| DraftPanel | `DraftPanel.cs` | Draft experience |
| InboxPanel | `InboxPanel.cs` | Messages and notifications |
| StaffPanel | `StaffPanel.cs` | Staff management |
| StaffHiringPanel | `StaffHiringPanel.cs` | Staff hiring interface |
| MatchPanel | `MatchPanel.cs` | In-game coaching |
| PreGamePanel | `PreGamePanel.cs` | Pre-game preparation |
| PostGamePanel | `PostGamePanel.cs` | Post-game results |
| PlayoffBracketPanel | `PlayoffBracketPanel.cs` | Playoff bracket view |
| NewGamePanel | `NewGamePanel.cs` | New game wizard |
| TeamSelectionPanel | `TeamSelectionPanel.cs` | Team selection |

### UI Components

| Component | Description |
|-----------|-------------|
| StaffRow | Staff member display row |
| CourtDiagramView | 2D court visualization |
| CoachingMenuView | Tabbed coaching interface |
| AttributeDisplayFactory | Rating/attribute display |
| ScrollingTicker | News ticker |

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
| In-Game Analytics | Real-time stats, coaching advisor, matchup evaluation |

### Outstanding Issues / TODOs

| Location | Issue | Priority |
|----------|-------|----------|
| `DashboardPanel.cs:271` | "Add highlight player stats" - Feature incomplete | Low |
| `MatchViewer.cs:58` | "Fetch name" - Player names not loaded in match view | Low |
| `PersonnelManager.cs` | Limited contract negotiation UI feedback | Medium |

### Known Gaps

1. **Contract Popup System** - No modal/popup infrastructure; contract details shown in debug log
2. **Trade AI** - Basic trade finding; could use smarter partner matching
3. **Summer League UI** - Manager exists but limited UI integration
4. **Training Camp UI** - Manager exists but no dedicated panel

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
- `NBAHeadCoach.UI.Components` - Reusable components

### Patterns Used
- **Singleton**: GameManager, SeasonController, PersonnelManager
- **Registry**: Panel registration in GameSceneController
- **Event System**: C# events for loose coupling
- **Factory**: AttributeDisplayFactory, name generation

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

### December 2024 - Personnel Refactor
- Consolidated 9 legacy manager classes into `PersonnelManager`
- Created `UnifiedCareerProfile` for all personnel types
- Migrated negotiation system to `StaffNegotiation.cs`
- Added `NameGenerator` with Markov chain
- Fixed API mismatches across managers
- Connected `StaffPanel` to `StaffHiringPanel`

---

*This document should be updated when major architectural changes occur.*

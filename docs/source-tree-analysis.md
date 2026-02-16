# Source Tree Analysis

## Complete Directory Structure

```
d:/NBA Head Coach/
│
├── Assets/                                  # Unity assets root
│   │
│   ├── Main Menu.unity                      # Main menu scene
│   │
│   ├── Scenes/                              # Game scenes
│   │   ├── Boot.unity                       # → Boot sequence, GameManager init
│   │   ├── MainMenu.unity                   # → Main menu, new game wizard
│   │   ├── Game.unity                       # → Management dashboard (main scene)
│   │   └── Match.unity                      # → In-game coaching view
│   │
│   ├── Scripts/                             # All C# source code
│   │   │
│   │   ├── Core/                            # Core game logic (singleton managers)
│   │   │   │
│   │   │   ├── AI/                          # **12 AI systems** for opponent simulation
│   │   │   │   ├── AIAdaptationSystem.cs         # Opponent adjustment tracking
│   │   │   │   ├── AICoachPersonality.cs         # Coach personality (977 lines)
│   │   │   │   ├── AIGMController.cs              # AI GM for coach-only mode (516 lines)
│   │   │   │   ├── AIPersonalityDiscovery.cs     # Trait learning system
│   │   │   │   ├── AITradeEvaluator.cs           # Trade evaluation AI (538 lines)
│   │   │   │   ├── AutonomousGameSimulator.cs    # AI coach game simulation (GM mode)
│   │   │   │   ├── CoachingAdvisor.cs            # In-game AI suggestions
│   │   │   │   ├── CoordinatorAI.cs              # OC/DC AI systems
│   │   │   │   ├── MatchupEvaluator.cs           # Matchup quality assessment
│   │   │   │   ├── OpponentAdjustmentPredictor.cs # Predict opponent moves
│   │   │   │   ├── PlayerValueCalculator.cs      # Stats-based player valuation
│   │   │   │   └── StaffAIDecisionMaker.cs       # Staff decision AI
│   │   │   │
│   │   │   ├── Data/                        # **51 data model classes** (24,881 LOC)
│   │   │   │   ├── Agent.cs                      # Player agents (669 lines)
│   │   │   │   ├── AutonomousGameResult.cs       # AI coach game results
│   │   │   │   ├── AwardTypes.cs                 # Award definitions
│   │   │   │   ├── CareerTransitionRequirements.cs # Career transition rules
│   │   │   │   ├── Contract.cs                   # CBA-compliant contracts
│   │   │   │   ├── CourtPosition.cs              # Spatial positioning
│   │   │   │   ├── DevelopmentInstruction.cs     # Player dev focus
│   │   │   │   ├── DraftPick.cs                  # Draft pick data
│   │   │   │   ├── FormerPlayerCoach.cs          # Former player → coach
│   │   │   │   ├── FormerPlayerGM.cs             # Former player → GM (759 lines)
│   │   │   │   ├── FormerPlayerProgressionData.cs # Progression tracking
│   │   │   │   ├── FormerPlayerScout.cs          # Former player → scout
│   │   │   │   ├── FrontOfficeProfile.cs         # GM personality (598 lines)
│   │   │   │   ├── GameLog.cs                    # Game results
│   │   │   │   ├── InitialDraftPickData.cs       # Dec 2025 draft pick data
│   │   │   │   ├── InitialFrontOfficeData.cs     # Dec 2025 GM profiles
│   │   │   │   ├── InjuryData.cs                 # Injury tracking
│   │   │   │   ├── JobMarketData.cs              # Job market structures
│   │   │   │   ├── LeagueCBA.cs                  # Salary cap rules
│   │   │   │   ├── MentorProfile.cs              # Mentor capabilities (570 lines)
│   │   │   │   ├── MentorshipRelationship.cs     # Mentor-mentee pairs (564 lines)
│   │   │   │   ├── NBAPlayerData.cs              # Player database entries
│   │   │   │   ├── NonPlayerRetirementData.cs    # Non-player retirement
│   │   │   │   ├── OpponentTendencyProfile.cs    # Opponent analysis (597 lines)
│   │   │   │   ├── Personality.cs                # Personality traits
│   │   │   │   ├── PlayBook.cs                   # Team playbooks (943 lines)
│   │   │   │   ├── Player.cs                     # **CORE** - Complete player (1,185 lines)
│   │   │   │   ├── PlayerDatabase.cs             # Player registry
│   │   │   │   ├── PlayerGameInstructions.cs     # Per-player tactics (1,051 lines)
│   │   │   │   ├── PlayerTendencies.cs           # Behavioral tendencies (560 lines)
│   │   │   │   ├── PlayoffData.cs                # Playoff bracket (799 lines)
│   │   │   │   ├── PracticeDrill.cs              # Practice drills (839 lines)
│   │   │   │   ├── PracticeSession.cs            # Practice sessions
│   │   │   │   ├── RosterRequest.cs              # Coach roster requests
│   │   │   │   ├── SaveData.cs                   # Save file structure (1,070 lines)
│   │   │   │   ├── ScoutingReport.cs             # Text-based scouting
│   │   │   │   ├── SeasonCalendar.cs             # Schedule, phases (627 lines)
│   │   │   │   ├── SeasonStats.cs                # Season statistics
│   │   │   │   ├── SetPlay.cs                    # Play definitions (794 lines)
│   │   │   │   ├── ShotMarkerData.cs             # Shot visualization data
│   │   │   │   ├── StaffAssignment.cs            # Staff task assignments
│   │   │   │   ├── StaffMeeting.cs               # Pre-game/halftime meetings (725 lines)
│   │   │   │   ├── StaffNegotiation.cs           # Staff contract negotiation
│   │   │   │   ├── StaffRoles.cs                 # Role definitions, user config
│   │   │   │   ├── Team.cs                       # Team identity, roster
│   │   │   │   ├── TeamFinances.cs               # Revenue, expenses (675 lines)
│   │   │   │   ├── TeamStrategy.cs               # Offensive/defensive schemes (677 lines)
│   │   │   │   ├── TrainingFacility.cs           # Training infrastructure
│   │   │   │   ├── UnifiedCareerProfile.cs       # **CORE** - All personnel (1,161 lines)
│   │   │   │   ├── WeeklySchedule.cs             # Practice/game schedule
│   │   │   │   └── ... (additional data models)
│   │   │   │
│   │   │   ├── Gameplay/                    # In-game coaching logic
│   │   │   │   └── GameCoach.cs                  # Central in-game coaching
│   │   │   │
│   │   │   ├── Manager/                     # **45 domain managers** (Singleton pattern)
│   │   │   │   ├── AdvanceScoutingManager.cs     # Scout assignments
│   │   │   │   ├── AgentManager.cs               # Agent relationships
│   │   │   │   ├── AITradeOfferGenerator.cs      # AI proactive trades
│   │   │   │   ├── AllStarManager.cs             # All-Star game
│   │   │   │   ├── AwardManager.cs               # MVP, DPOY, etc.
│   │   │   │   ├── ContractNegotiationManager.cs # Contract offers
│   │   │   │   ├── DraftClassGenerator.cs        # Draft class generation
│   │   │   │   ├── DraftPickRegistry.cs          # **KEY** - Draft pick tracking
│   │   │   │   ├── DraftSystem.cs                # Draft lottery, execution
│   │   │   │   ├── FinanceManager.cs             # Team finances
│   │   │   │   ├── FormerPlayerCareerManager.cs  # Career transitions
│   │   │   │   ├── FreeAgentManager.cs           # Free agency
│   │   │   │   ├── GameAnalyticsTracker.cs       # Real-time analytics
│   │   │   │   ├── GamePlanBuilder.cs            # Pre-game prep
│   │   │   │   ├── GMJobSecurityManager.cs       # GM hot seat
│   │   │   │   ├── HistoryManager.cs             # Records, HoF
│   │   │   │   ├── InjuryManager.cs              # Injuries, load management
│   │   │   │   ├── JobMarketManager.cs           # **KEY** - Job market system
│   │   │   │   ├── JobSecurityManager.cs         # Coach hot seat
│   │   │   │   ├── LeagueEventsManager.cs        # League-wide events
│   │   │   │   ├── MediaManager.cs               # Press conferences
│   │   │   │   ├── MentorshipManager.cs          # **KEY** - Mentorship (30% bonus)
│   │   │   │   ├── MoraleChemistryManager.cs     # **KEY** - Morale, captain, meetings
│   │   │   │   ├── OffseasonManager.cs           # Offseason coordination
│   │   │   │   ├── PersonalityManager.cs         # **KEY** - Chemistry, escalation
│   │   │   │   ├── PersonnelManager.cs           # **CENTRAL** - All personnel ops
│   │   │   │   ├── PlayEffectivenessTracker.cs   # Hot/cold plays
│   │   │   │   ├── PlayerDevelopmentManager.cs   # Attribute progression
│   │   │   │   ├── PlayoffManager.cs             # Playoffs, play-in
│   │   │   │   ├── PracticeManager.cs            # **KEY** - Practice system
│   │   │   │   ├── ProspectGenerator.cs          # Prospect generation
│   │   │   │   ├── RetirementManager.cs          # Player/staff retirement
│   │   │   │   ├── RevenueManager.cs             # Revenue tracking
│   │   │   │   ├── RosterManager.cs              # **KEY** - Roster, OnPlayerRemoved event
│   │   │   │   ├── SalaryCapManager.cs           # Cap calculations
│   │   │   │   ├── ScoutingReportGenerator.cs    # Scouting text
│   │   │   │   ├── StaffEvaluationGenerator.cs   # Staff reviews
│   │   │   │   ├── SummerLeagueManager.cs        # Summer league
│   │   │   │   ├── TendencyCoachingManager.cs    # Tendency training
│   │   │   │   ├── TradeAnnouncementSystem.cs    # **KEY** - Trade news
│   │   │   │   ├── TradeFinder.cs                # AI partner matching
│   │   │   │   ├── TradeNegotiationManager.cs    # Multi-step negotiations
│   │   │   │   ├── TradeSystem.cs                # **KEY** - Trade execution
│   │   │   │   ├── TradeValidator.cs             # CBA compliance
│   │   │   │   └── TrainingCampManager.cs        # Training camp
│   │   │   │
│   │   │   ├── Simulation/                  # Game simulation engine
│   │   │   │   ├── BoxScore.cs                   # Team/player statistics
│   │   │   │   ├── FoulSystem.cs                 # **NEW** - NBA foul system
│   │   │   │   ├── FreeThrowHandler.cs           # **NEW** - Free throw calculation
│   │   │   │   ├── GameSimulator.cs              # Full game sim loop
│   │   │   │   ├── PlaySelector.cs               # AI play selection
│   │   │   │   ├── PossessionSimulator.cs        # Individual possession
│   │   │   │   ├── RulesEnums.cs                 # **NEW** - Foul/violation types
│   │   │   │   ├── ShotCalculator.cs             # Shot probability
│   │   │   │   ├── TimeoutIntelligence.cs        # **NEW** - AI timeout logic
│   │   │   │   └── ViolationChecker.cs           # **NEW** - Violation detection
│   │   │   │
│   │   │   ├── Util/                        # Utilities
│   │   │   │   └── NameGenerator.cs              # Markov chain name generation
│   │   │   │
│   │   │   ├── GameManager.cs               # **CENTRAL** - Game orchestrator, state machine
│   │   │   ├── MatchFlowController.cs       # Pre/match/post-game flow
│   │   │   ├── MatchSimulationController.cs # Match simulation orchestration
│   │   │   ├── PlayByPlayGenerator.cs       # Broadcast-style text
│   │   │   ├── SaveLoadManager.cs           # Persistence, Ironman mode
│   │   │   └── SeasonController.cs          # **KEY** - Season flow, calendar, standings
│   │   │
│   │   ├── UI/                              # User interface
│   │   │   │
│   │   │   ├── Components/                  # **16 reusable UI components**
│   │   │   │   ├── AnimatedCourtView.cs          # Animated 2D court
│   │   │   │   ├── AnimatedPlayerDot.cs          # Player visualization
│   │   │   │   ├── AttributeDisplayFactory.cs    # Rating display
│   │   │   │   ├── BallAnimator.cs               # Ball animations
│   │   │   │   ├── CaptainSelectionRow.cs        # **NEW** - Captain selection row
│   │   │   │   ├── CoachingMenuView.cs           # Tabbed coaching UI
│   │   │   │   ├── CourtDiagramView.cs           # Static court formations
│   │   │   │   ├── ScrollingTicker.cs            # News ticker
│   │   │   │   ├── ShotMarkerUI.cs               # Shot markers (make/miss)
│   │   │   │   ├── StaffRow.cs                   # Staff display row
│   │   │   │   └── Widgets/                      # UI widgets
│   │   │   │
│   │   │   ├── Match/                       # Match-specific UI
│   │   │   │   └── (Match UI components)
│   │   │   │
│   │   │   ├── Modals/                      # **8 modal dialogs**
│   │   │   │   ├── CaptainSelectionPanel.cs      # **NEW** - Captain selection modal
│   │   │   │   ├── ConfirmationPanel.cs          # Yes/No dialogs
│   │   │   │   ├── ContractDetailPanel.cs        # Contract details
│   │   │   │   ├── IncomingTradeOffersPanel.cs   # **NEW** - AI trade offers
│   │   │   │   ├── PlayerSelectionPanel.cs       # Player selection
│   │   │   │   ├── ProspectSelectionPanel.cs     # Draft prospect selection
│   │   │   │   └── SlidePanel.cs                 # Base slide-in modal
│   │   │   │
│   │   │   ├── Panels/                      # **15 UI panels**
│   │   │   │   ├── CalendarPanel.cs              # Schedule view
│   │   │   │   ├── DashboardPanel.cs             # **MAIN** - Dashboard hub, trade ticker
│   │   │   │   ├── DraftPanel.cs                 # Draft experience
│   │   │   │   ├── GameSummaryPanel.cs           # GM-only game results
│   │   │   │   ├── InboxPanel.cs                 # Messages, trade notifications
│   │   │   │   ├── JobMarketPanel.cs             # **NEW** - Job search
│   │   │   │   ├── MatchPanel.cs                 # **KEY** - In-game coaching
│   │   │   │   ├── NewGamePanel.cs               # New game wizard
│   │   │   │   ├── PlayoffBracketPanel.cs        # Playoff bracket
│   │   │   │   ├── PostGamePanel.cs              # Post-game results
│   │   │   │   ├── PreGamePanel.cs               # Pre-game prep
│   │   │   │   ├── RosterPanel.cs                # Roster management
│   │   │   │   ├── RosterRequestPanel.cs         # **NEW** - Coach roster requests
│   │   │   │   ├── StaffHiringPanel.cs           # Staff hiring
│   │   │   │   ├── StaffPanel.cs                 # Staff management
│   │   │   │   ├── StandingsPanel.cs             # League standings
│   │   │   │   ├── TeamSelectionPanel.cs         # Team selection
│   │   │   │   └── TradePanel.cs                 # Trade interface
│   │   │   │
│   │   │   ├── GameSceneController.cs       # **KEY** - Panel navigation registry
│   │   │   └── MainMenuController.cs        # Main menu flow
│   │   │
│   │   ├── View/                            # Camera and visualization
│   │   │   └── (5 visualization files)
│   │   │
│   │   ├── Tools/                           # Scene setup utilities
│   │   │   └── (3 tool scripts)
│   │   │
│   │   └── Tests/                           # Unit/integration tests
│   │       └── (3 test files)
│   │
│   ├── Resources/                           # Unity Resources (loadable at runtime)
│   │   └── Data/
│   │       ├── initial_draft_picks.json          # **38 traded picks** (2025-2031)
│   │       └── initial_front_offices.json        # **30 NBA GMs** (Dec 2025)
│   │
│   └── StreamingAssets/                     # Data files (loaded at build time)
│       └── Data/
│           ├── players.json                      # Player database
│           └── teams.json                        # NBA teams
│
├── Packages/                                # Unity package dependencies
│   ├── manifest.json                        # Package manifest (UGUI 2.0, Multiplayer Center)
│   └── packages-lock.json                   # Locked package versions
│
├── ProjectSettings/                         # Unity project settings
│   ├── AudioManager.asset
│   ├── DynamicsManager.asset
│   ├── EditorBuildSettings.asset
│   ├── GraphicsSettings.asset
│   ├── InputManager.asset
│   ├── ProjectVersion.txt                   # Unity version: 6000.3.0f1
│   └── ... (other Unity settings)
│
├── Library/                                 # Unity-generated (excluded from git)
│
├── docs/                                    # **Generated documentation** (this scan)
│   ├── ai-systems.md                        # AI personality systems
│   ├── data-models.md                       # Data model reference
│   ├── manager-systems.md                   # Manager architecture
│   ├── project-overview.md                  # High-level summary
│   ├── source-tree-analysis.md              # This file
│   └── project-scan-report.json             # Scan state/metadata
│
├── _bmad/                                   # BMAD workflow system (meta-development)
│   ├── core/                                # Core BMAD workflows
│   ├── gds/                                 # Game Dev Studio module
│   └── _config/                             # BMAD configuration
│
├── .claude/                                 # Claude Code settings
├── .gitignore                               # Git exclusions
├── .vscode/                                 # VSCode settings
└── ProjectOutline.md                        # **57KB design document** (single source of truth)
```

## Critical Paths

### Entry Points
- **Boot Flow**: `Boot.unity` → `GameManager.cs` → Initialize managers → `MainMenu.unity`
- **New Game**: `MainMenuController.cs` → `NewGamePanel.cs` → Role selection → `Game.unity`
- **Match**: `MatchFlowController.cs` → `Match.unity` → `MatchSimulationController.cs`

### Core Systems
- **Orchestration**: `GameManager.cs` (singleton, state machine)
- **Season Management**: `SeasonController.cs` (calendar, standings, phases)
- **Personnel**: `PersonnelManager.cs` (central facade for all staff)
- **Roster**: `RosterManager.cs` (OnPlayerRemoved event for captain system)
- **Trading**: `TradeSystem.cs` + `AITradeEvaluator.cs` + `AITradeOfferGenerator.cs`
- **Morale**: `MoraleChemistryManager.cs` + `PersonalityManager.cs`
- **Simulation**: `GameSimulator.cs` → `PossessionSimulator.cs` loop

### UI Navigation
- **Dashboard Hub**: `DashboardPanel.cs` (main navigation, trade ticker)
- **Panel Registry**: `GameSceneController._panelRegistry` (15 panels)
- **Modal System**: `SlidePanel.cs` base class (8 modals)

### Data Persistence
- **Save**: `SaveLoadManager.cs` → `SaveData.cs` → Binary file
- **Load**: Binary → `SaveData.cs` → Restore manager states

## Integration Points

### Cross-System Events
- `RosterManager.OnPlayerRemoved` → GameManager checks captain
- `TradeSystem.OnTradeExecuted` → DraftPickRegistry, TradeAnnouncementSystem
- `SeasonController.OnPhaseChanged` → GameManager (captain check at RegularSeason start)
- `GameManager.OnCaptainSelectionRequired` → GameSceneController (shows modal)

### Manager Dependencies
- **PersonnelManager** needs: Nothing (central facade)
- **RosterManager** needs: SalaryCapManager
- **TradeSystem** needs: AITradeEvaluator, DraftPickRegistry, TradeValidator
- **MoraleChemistryManager** needs: PersonalityManager, RosterManager
- **PlayerDevelopmentManager** needs: MentorshipManager (for dev bonus)

### UI → Manager Flow
- **DashboardPanel** → Multiple managers for overview data
- **TradePanel** → TradeSystem, AITradeEvaluator
- **RosterPanel** → RosterManager, PersonnelManager
- **MatchPanel** → MatchSimulationController, GameCoach

## Key File Sizes

| File | Lines | Purpose |
|------|-------|---------|
| Player.cs | 1,185 | Complete player data model |
| UnifiedCareerProfile.cs | 1,161 | All personnel types |
| SaveData.cs | 1,070 | Save file structure |
| PlayerGameInstructions.cs | 1,051 | Per-player tactics |
| AICoachPersonality.cs | 977 | Coach AI personality |
| PlayBook.cs | 943 | Team playbooks |
| PracticeDrill.cs | 839 | Practice drills |
| PlayoffData.cs | 799 | Playoff bracket |
| SetPlay.cs | 794 | Play definitions |
| FormerPlayerGM.cs | 759 | Former player GM system |

## Dependencies

### Unity Packages
- **UGUI** (2.0.0) - UI framework
- **Multiplayer Center** (1.0.1) - Multiplayer features (unused?)
- **Unity Modules**: AI, Animation, Audio, Physics, UI, etc.

### External Data
- **initial_front_offices.json** - 30 real NBA GM profiles (Dec 2025)
- **initial_draft_picks.json** - 38 traded picks + 14 swaps (2025-2031)
- **players.json** - Player database (procedural + real)
- **teams.json** - NBA team definitions

## Namespaces

```csharp
NBAHeadCoach.Core              // GameManager, SeasonController, etc.
NBAHeadCoach.Core.Data         // Data models (51 classes)
NBAHeadCoach.Core.Manager      // Domain managers (45 classes)
NBAHeadCoach.Core.Simulation   // Simulation engine
NBAHeadCoach.Core.AI           // AI systems (12 classes)
NBAHeadCoach.Core.Gameplay     // In-game coaching
NBAHeadCoach.Core.Util         // Utilities
NBAHeadCoach.UI                // UI base classes
NBAHeadCoach.UI.Panels         // Panel implementations (15)
NBAHeadCoach.UI.Modals         // Modal dialogs (8)
NBAHeadCoach.UI.Components     // Reusable components (16)
```

## Summary

The project follows a **clean layered architecture**:
1. **Data Layer** (51 models) - Pure data structures
2. **Manager Layer** (45 managers) - Business logic, singleton pattern
3. **Simulation Layer** (6 files) - Game simulation engine
4. **UI Layer** (39 files) - Views and user interaction
5. **AI Layer** (12 files) - Opponent behavior and decision-making

**Total Code Organization**:
- **191 C# scripts**
- **~60,000+ lines of code**
- **5 Unity scenes**
- **JSON data files** for NBA data

The architecture demonstrates:
- **Separation of concerns** (clear layer boundaries)
- **Event-driven design** (loose coupling between managers)
- **Singleton pattern** (global state management)
- **Facade pattern** (PersonnelManager)
- **Factory pattern** (Name generation, AI personalities)
- **MVC-like structure** (Panels as views, Managers as controllers, Data as models)

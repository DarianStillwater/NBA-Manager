# NBA Head Coach - Project Overview

## Project Summary

**Name**: NBA Head Coach
**Type**: Unity Game (Single-player NBA Franchise Management Simulation)
**Platform**: Windows
**Unity Version**: 6000.3.0f1 (Unity 6.0)
**Language**: C# (.NET)
**Repository Structure**: Monolith (single cohesive codebase)

## Quick Facts

- **191 C# scripts** (~24,881 LOC in Data/ alone)
- **5 Unity scenes** (Boot, MainMenu, Game, Match)
- **51 data models** covering all game entities
- **45 domain managers** for system orchestration
- **12 AI systems** for opponent simulation
- **15 UI panels** for game navigation
- **8 modal dialogs** for user interactions

## Core Concept

Players manage an NBA franchise as a **Head Coach**, **General Manager**, or **Both**. The game simulates:
- 82-game regular seasons
- Playoffs and championship runs
- Roster management (trades, free agency, draft)
- Player development and mentorship
- In-game coaching with tactical depth
- Career progression across multiple teams

## Design Philosophy: No Visible Attributes

**Critical**: Player attributes (shooting, defense, speed, etc.) are **never shown as numbers**. All attributes exist internally to drive simulation, but players evaluate talent through:
- **Statistics**: Box scores, shooting percentages, per-game averages
- **Scouting Reports**: Text-based descriptions ("Elite three-point shooter")
- **Game Observation**: Watch performance in simulated games

This creates a realistic "eye test" experience like real NBA coaches.

## Technology Stack

| Category | Technology | Version |
|----------|-----------|---------|
| Game Engine | Unity | 6000.3.0f1 (Unity 6.0) |
| Language | C# | .NET Framework |
| UI Framework | Unity UGUI | 2.0.0 |
| Data Format | JSON | (StreamingAssets, Resources) |
| Save System | Custom Binary | Ironman mode support |
| Version Control | Git | (inferred from .gitignore) |

## Architecture Pattern

**Domain-Driven Design** with:
- **GameManager** (Singleton orchestrator)
- **45 Domain Managers** (Singleton pattern)
- **Event-Driven Communication** (C# events)
- **Data-Driven Simulation** (JSON configs, procedural generation)
- **MVC-like UI** (Panels as views, Managers as controllers, Data as models)

## Project Structure

```
d:/NBA Head Coach/
├── Assets/
│   ├── Main Menu.unity                 # Entry scene
│   ├── Scenes/                         # Core game scenes
│   │   ├── Boot.unity                  # Initialize GameManager
│   │   ├── MainMenu.unity              # Main menu & new game
│   │   ├── Game.unity                  # Management dashboard
│   │   └── Match.unity                 # In-game coaching
│   ├── Scripts/
│   │   ├── Core/
│   │   │   ├── AI/                     # 12 AI systems
│   │   │   ├── Data/                   # 51 data models
│   │   │   ├── Manager/                # 45 domain managers
│   │   │   ├── Simulation/             # Game simulation engine
│   │   │   ├── Gameplay/               # In-game coaching logic
│   │   │   └── Util/                   # Name generation, utilities
│   │   ├── UI/
│   │   │   ├── Panels/                 # 15 UI panels
│   │   │   ├── Modals/                 # 8 modal dialogs
│   │   │   ├── Components/             # 16 reusable UI components
│   │   │   └── Match/                  # Match-specific UI
│   │   ├── View/                       # Camera, visualization
│   │   ├── Tools/                      # Scene setup utilities
│   │   └── Tests/                      # Unit/integration tests
│   ├── Resources/Data/                 # JSON data files
│   │   ├── initial_front_offices.json  # 30 NBA GM profiles (Dec 2025)
│   │   └── initial_draft_picks.json    # 38 traded picks (2025-2031)
│   ├── StreamingAssets/Data/           # Game data
│   │   ├── players.json                # Player database
│   │   └── teams.json                  # NBA teams
│   └── ProjectSettings/                # Unity project settings
├── Packages/                           # Unity package dependencies
│   └── manifest.json                   # Package list
├── Library/                            # Unity-generated (excluded from git)
├── ProjectOutline.md                   # 57KB design document
├── docs/                               # Generated documentation
│   ├── project-overview.md             # This file
│   ├── ai-systems.md                   # AI personality systems
│   ├── data-models.md                  # Data model reference
│   ├── manager-systems.md              # Manager architecture
│   ├── source-tree-analysis.md         # Directory structure
│   └── project-scan-report.json        # Scan state
├── _bmad/                              # BMAD workflow system
└── .gitignore                          # Git exclusions
```

## Core Game Loop

1. **Season Management**: 82 games → Playoffs → Offseason
2. **Game Day**:
   - Pre-game: Review matchups, set game plan
   - Match: Real-time coaching (play calls, substitutions, adjustments)
   - Post-game: Review stats, injuries, morale
3. **Roster Management**: Trades, free agency, draft
4. **Player Development**: Practice, mentorship, training
5. **Morale & Chemistry**: Team meetings, conversations, captain influence
6. **Career Progression**: Job market, role transitions

## Key Features

### Dual-Role System
- **GM Only**: AI coach runs games autonomously
- **Coach Only**: Submit roster requests to AI GM
- **Both**: Full control over all aspects
- **Job Market**: After firing, search for new positions with role flexibility

### Captain System
- Designate team captain
- Captain morale amplifies to team (+20% happy, -10% unhappy)
- Automatic replacement when captain traded/cut
- Staff recommendations (no visible leadership numbers)

### Trade AI System
- Stats-based player valuation (production, potential, contract)
- AI teams proactively propose trades (~1-2 per week)
- Real-world GM personalities (Dec 2025 data)
- Counter-offer generation

### Morale & Chemistry
- Contract satisfaction tracking
- Expectation-based win/loss effects
- 5-step escalation (complaint → holdout)
- Team meetings and individual conversations
- On-court chemistry bonuses

### Deep Coaching System
- **Phase 1**: In-game analytics, AI advisor, matchup evaluation
- **Phase 2**: Player tendencies (innate + coachable)
- **Phase 3**: Practice system with drills and weekly schedules
- **Phase 4**: Mentorship (up to 30% dev bonus)
- **Phase 5**: Opponent analysis and adjustment prediction
- **Phase 6**: Staff partnership (coordinators, meetings)

### NBA Rules System
- Full foul system (personal, shooting, technical, flagrant)
- Free throw scenarios (quick-result with clutch modifiers)
- Violations (traveling, backcourt, 3-second)
- AI timeout intelligence (stop run, icing, advance ball)
- Team foul tracking (bonus/double bonus)

### Former Player Careers
- Coaching track: Assistant → Coordinator → Head Coach
- Scouting track: Regional → National → Director
- Front office track: Scout → Assistant GM → GM
- Cross-track transitions (HC ↔ GM, etc.)

### Initial Data System (Dec 2025)
- 30 real NBA GM profiles with competence ratings
- 38 traded first-round picks (2025-2031)
- 14 pick swap rights
- Automatic loading at game start

## Entry Points for Development

### Main Controllers
- **GameManager.cs**: Central game orchestrator, state machine
- **SeasonController.cs**: Season flow, calendar, standings
- **MatchFlowController.cs**: Pre-game → Match → Post-game
- **MatchSimulationController.cs**: Possession-by-possession simulation

### UI Entry
- **GameSceneController.cs**: Panel navigation registry
- **MainMenuController.cs**: Main menu flow

### Key Managers
- **PersonnelManager.cs**: All staff operations (central facade)
- **RosterManager.cs**: Roster moves, captain events
- **TradeSystem.cs**: Trade execution
- **MoraleChemistryManager.cs**: Morale processing, captain system

## Development Workflow

### Running the Game
1. Open project in Unity 2022.3+ (tested with Unity 6.0)
2. Open `Assets/Scenes/Boot.unity`
3. Press Play
4. Boot → MainMenu → New Game → Choose role → Play

### Adding New Features
1. Create data classes in `Core/Data/`
2. Create manager in `Core/Manager/` (if needed)
3. Register manager in `GameManager.Awake()`
4. Create UI panel in `UI/Panels/` extending `BasePanel`
5. Register panel in `GameSceneController._panelRegistry`
6. Wire up events for cross-system communication

### Testing
- Unit tests in `Assets/Scripts/Tests/`
- Integration tests via Play mode
- Save/Load testing with Ironman mode verification

## Data Flow

### Game Boot Sequence
1. **Boot Scene**: Initialize GameManager singleton
2. **Load Config**: Load initial NBA data (GMs, draft picks)
3. **Initialize Managers**: 45 managers in dependency order
4. **Transition**: Load MainMenu scene
5. **New Game**: User creates career → Load Game scene
6. **Play**: GameManager.State = Playing

### Match Flow
1. **Calendar Advance**: SeasonController triggers game day
2. **Pre-Game**: GameSceneController shows PreGamePanel
3. **User Ready**: Transition to Match scene
4. **Simulation**: MatchSimulationController runs possession loop
5. **Post-Game**: Return to Game scene, show PostGamePanel
6. **Morale Processing**: MoraleChemistryManager.ProcessGameResult()

### Save/Load
1. **Save**: GameManager collects state from all managers
2. **Serialize**: SaveData object → JSON → Binary file
3. **Ironman**: If enabled, delete previous save
4. **Load**: Read binary → Deserialize → Restore manager states

## Performance Considerations

- **Simulation Speed**: Possession-by-possession can be CPU-intensive
- **UI Updates**: Animated court updates every frame during match
- **Save File Size**: Complete game state can be several MB
- **Manager Overhead**: 45 singletons in memory at once

## Known Issues / TODOs

Referenced in ProjectOutline.md:
- **DashboardPanel.cs:271**: "Add highlight player stats" - Feature incomplete
- **MatchViewer.cs:58**: "Fetch name" - Player names not loaded
- **PersonnelManager.cs**: Limited contract negotiation UI feedback
- **Contract Popup System**: No modal infrastructure yet
- **Summer League UI**: Manager exists, limited UI integration
- **Training Camp UI**: Manager exists, no dedicated panel

## Documentation Files

After exhaustive scan, the following documentation has been generated:

1. **project-overview.md** (this file) - High-level summary
2. **ai-systems.md** - AI personality and decision-making systems
3. **data-models.md** - Complete data model reference (51 files)
4. **manager-systems.md** - Manager architecture (45 files)
5. **source-tree-analysis.md** - Annotated directory structure
6. **project-scan-report.json** - Scan metadata and state

## Getting Help

- **ProjectOutline.md**: Comprehensive 57KB design document (single source of truth)
- **BMAD Workflows**: Use `/bmad-gds-*` commands for game dev workflows
- **GitHub Issues**: https://github.com/anthropics/claude-code/issues (for Claude Code issues)

## Summary

**NBA Head Coach** is a sophisticated, data-driven NBA franchise management simulation built in Unity 6.0. The codebase demonstrates:
- **Clean architecture** with domain managers and event-driven communication
- **Deep simulation** with AI personalities, trade logic, and morale systems
- **No visible attributes philosophy** for realistic player evaluation
- **Comprehensive career system** with dual roles and job market
- **Real-world data integration** (Dec 2025 GM profiles and draft picks)

**Total Project Size**:
- 191 C# scripts
- 51 data models
- 45 domain managers
- 12 AI systems
- 23 UI components
- ~60,000+ lines of code

The project is **production-ready** with most core features implemented. Outstanding work focuses on UI polish and additional content (more teams, historical seasons, etc.).

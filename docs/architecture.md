# NBA Head Coach - Architecture Documentation

## Executive Summary

**NBA Head Coach** is a comprehensive NBA franchise management simulation game built with Unity 6. The game allows players to take on dual roles (GM + Head Coach or Coach-only) to manage every aspect of an NBA franchise including roster construction, trade negotiation, game coaching, practice planning, player development, and morale management.

**Key Features**:
- Dual-role system (GM + Head Coach, or Coach-only with AI GM)
- Real-time game coaching with tactical play-calling
- Comprehensive AI personality systems for opponent simulation
- Stats-based trade evaluation system
- Mentorship and morale systems with chemistry tracking
- No visible attributes design philosophy (scouting-based evaluation)
- Ironman save system preventing save scumming

**Architecture Type**: Monolithic Unity game with Domain-Driven Design pattern using Singleton managers and event-driven communication.

**Scale**: 191 C# scripts, ~24,881 LOC in data models alone, 45 domain managers, 12 AI systems.

---

## Technology Stack

| Category | Technology | Version | Purpose |
|----------|-----------|---------|---------|
| **Game Engine** | Unity | 6000.3.0f1 (Unity 6) | Core game engine and runtime |
| **Language** | C# | .NET Standard 2.1 | Primary programming language |
| **UI Framework** | Unity UI (UGUI) | 2.0.0 | User interface rendering |
| **Platform** | Windows | 10.0.19045 | Target platform |
| **IDE** | Visual Studio | 2022 | Development environment |
| **Serialization** | Unity JsonUtility | Built-in | Save/load system |
| **Version Control** | Git | Latest | Source control |

**Dependencies** (Unity Packages):
- `com.unity.ugui@2.0.0` - UI system with TextMeshPro
- `com.unity.multiplayer.center@1.0.1` - Multiplayer utilities (unused)

**Architecture Pattern**:
- Domain-Driven Design with Singleton managers
- Event-driven architecture for cross-system communication
- MVC-inspired pattern for UI (View controllers + Data models)

---

## System Architecture

### High-Level Architecture

```
┌─────────────────────────────────────────────────────────────┐
│                         Boot Layer                          │
│  (Boot.cs → Initializes all Singleton managers)           │
└─────────────────────────────────────────────────────────────┘
                              ↓
┌─────────────────────────────────────────────────────────────┐
│                      Manager Layer                          │
│  ┌──────────────┐  ┌──────────────┐  ┌──────────────┐     │
│  │   Personnel  │  │    Trading   │  │  Development │     │
│  │   Managers   │  │   Managers   │  │   Managers   │     │
│  │  (11 files)  │  │  (6 files)   │  │  (8 files)   │     │
│  └──────────────┘  └──────────────┘  └──────────────┘     │
│  ┌──────────────┐  ┌──────────────┐  ┌──────────────┐     │
│  │  Simulation  │  │   Morale &   │  │   Meta Game  │     │
│  │   Managers   │  │   Chemistry  │  │   Managers   │     │
│  │  (7 files)   │  │  (5 files)   │  │  (8 files)   │     │
│  └──────────────┘  └──────────────┘  └──────────────┘     │
└─────────────────────────────────────────────────────────────┘
                              ↓
                    ┌─────────────────┐
                    │   Event Bus     │
                    │  (C# Events)    │
                    └─────────────────┘
                              ↓
┌─────────────────────────────────────────────────────────────┐
│                      Data Layer                             │
│  ┌──────────────┐  ┌──────────────┐  ┌──────────────┐     │
│  │   Player     │  │  Contract    │  │  Schedule    │     │
│  │  (1,185 LOC) │  │   System     │  │   System     │     │
│  └──────────────┘  └──────────────┘  └──────────────┘     │
│         51 data model classes (~24,881 LOC)                │
└─────────────────────────────────────────────────────────────┘
                              ↓
┌─────────────────────────────────────────────────────────────┐
│                      AI Layer                               │
│  ┌──────────────┐  ┌──────────────┐  ┌──────────────┐     │
│  │  AI Coach    │  │   AI GM      │  │   AI Trade   │     │
│  │ Personality  │  │ Controller   │  │  Evaluator   │     │
│  │  (977 LOC)   │  │  (516 LOC)   │  │  (538 LOC)   │     │
│  └──────────────┘  └──────────────┘  └──────────────┘     │
│         12 AI systems for decision-making                  │
└─────────────────────────────────────────────────────────────┘
                              ↓
┌─────────────────────────────────────────────────────────────┐
│                      UI Layer                               │
│  ┌──────────────┐  ┌──────────────┐  ┌──────────────┐     │
│  │  Controllers │  │    Panels    │  │    Views     │     │
│  │   (Main UI   │  │ (Trade, FA,  │  │  (Rosters,   │     │
│  │   screens)   │  │  Draft, etc.)│  │   Stats)     │     │
│  └──────────────┘  └──────────────┘  └──────────────┘     │
└─────────────────────────────────────────────────────────────┘
```

### Key Design Principles

1. **Singleton Pattern for Managers**
   - Each domain manager is a Singleton accessible globally
   - Example: `RosterManager.Instance.GetPlayer(id)`
   - Initialized at boot, persist across scenes

2. **Event-Driven Communication**
   - Managers publish events for state changes
   - Other systems subscribe to relevant events
   - Decouples systems for maintainability
   ```csharp
   // Publishing
   public static event Action<Player, Team> OnPlayerTradedEvent;
   OnPlayerTradedEvent?.Invoke(player, newTeam);

   // Subscribing
   TradeManager.OnPlayerTradedEvent += HandlePlayerTrade;
   ```

3. **No Visible Attributes Philosophy**
   - Player ratings (50+ attributes) never shown to user
   - Evaluation via stats, scouting reports, observation only
   - Simulates real-world GM decision-making

4. **Separation of Concerns**
   - **Data Models**: Pure data structures (`Player`, `Contract`, `Team`)
   - **Managers**: Business logic and state management
   - **Controllers**: UI orchestration and user input
   - **AI Systems**: Autonomous decision-making

---

## Data Architecture

### Core Data Models (51 files, ~24,881 LOC)

#### Player System
- **Player.cs** (1,185 LOC): Complete player profile with 50+ attributes, career stats, personality
- **PlayerTendencies.cs** (560 LOC): Innate and coachable behavior patterns
- **Personality.cs**: 13 personality traits affecting morale and chemistry
- **MentorProfile.cs** (570 LOC): Veteran player mentoring capabilities
- **Contract.cs**: CBA-compliant contract structure with options

#### Personnel System
- **UnifiedCareerProfile.cs** (1,161 LOC): Coaches, scouts, GMs with dual-career tracking
- **FormerPlayerGM.cs** (759 LOC): Former players transitioning to front office
- **FormerPlayerCoach.cs**: Former players becoming coaches
- **Agent.cs** (669 LOC): Player agents with negotiation styles

#### Gameplay System
- **PlayBook.cs** (943 LOC): Team playbooks with 15-20 plays and familiarity tracking
- **SetPlay.cs** (794 LOC): Individual play definitions with 20+ action types
- **TeamStrategy.cs** (677 LOC): Offensive/defensive schemes and pace settings
- **PlayerGameInstructions.cs** (1,051 LOC): Per-player tactical instructions

#### Season & Competition
- **SeasonCalendar.cs** (627 LOC): 82-game schedule with season phases
- **PlayoffData.cs** (799 LOC): Playoff brackets and series tracking
- **SaveData.cs** (1,070 LOC): Complete save file structure with Ironman support

#### Financial System
- **TeamFinances.cs** (675 LOC): Revenue, expenses, luxury tax
- **LeagueCBA.cs**: Salary cap rules and CBA compliance

### Data Persistence
- **Format**: JSON serialization via Unity's `JsonUtility`
- **Location**: `%APPDATA%/../LocalLow/<Company>/<Product>/saves/`
- **Structure**: Single `SaveData.cs` object containing entire game state
- **Ironman Mode**: Flag prevents save scumming

**Save Data Contents**:
- User configuration (name, role, team, difficulty)
- Season state (calendar, standings, schedule)
- All 30 team rosters and contracts
- Player development tracking
- Manager states (trade history, scouting, financials)

---

## Manager Systems Architecture

### Manager Categories (45 total files)

#### 1. Personnel Management (11 managers)
- `RosterManager` - Team rosters and depth charts
- `ContractManager` - Contract negotiations and cap management
- `DraftManager` - Draft system and prospect evaluation
- `FreeAgencyManager` - Free agent signing
- `ScoutingManager` - Player scouting and reports
- `CoachingStaffManager` - Hiring/firing coaches
- `StaffMeetingManager` - Pre-game and halftime meetings
- `PlayerDevelopmentManager` - Attribute progression
- `InjuryManager` - Injury tracking and recovery
- `HealthManager` - Player health and fatigue
- `PlayerRelationshipsManager` - Player-staff relationships

#### 2. Trading System (6 managers)
- `TradeManager` - Trade proposal processing
- `AITradeEvaluator` - AI trade evaluation
- `TradeNegotiationManager` - Multi-step negotiation
- `WaiverManager` - Waiver wire system
- `TeamNeedsAnalyzer` - Identifies roster gaps
- `SalaryCappingManager` - Cap space calculations

#### 3. Development & Training (8 managers)
- `PracticeManager` - Practice session planning
- `DrillLibrary` - Available practice drills
- `PlaybookManager` - Play installation and familiarity
- `MentorshipManager` - Mentor-mentee relationships
- `PlayerTendencyManager` - Tendency development
- `SkillDevelopmentManager` - Skill training
- `TacticsManager` - Team tactics and schemes
- `GamePlanManager` - Pre-game preparation

#### 4. Simulation & Competition (7 managers)
- `GameCoach` - In-game coaching and play-calling
- `PlayByPlaySimulator` - Possession-by-possession simulation
- `OpponentScoutingManager` - Opponent tendency analysis
- `SeasonManager` - Season progression
- `StandingsManager` - League standings
- `ScheduleManager` - Game scheduling
- `StatisticsManager` - Stat tracking and aggregation

#### 5. Morale & Chemistry (5 managers)
- `MoraleManager` - Player morale tracking
- `ChemistryManager` - Team chemistry calculation
- `CaptainManager` - Captain selection and influence
- `DiscontentManager` - Escalating player issues
- `LockerRoomManager` - Team dynamics

#### 6. Meta Game Management (8 managers)
- `GameStateManager` - Global game state
- `SaveManager` - Save/load operations
- `CalendarManager` - Date progression
- `UIManager` - UI screen management
- `JobMarketManager` - Coaching job market
- `LeagueManager` - League-wide operations
- `TeamManager` - Individual team management
- `UserManager` - User profile and settings

### Manager Communication Pattern

```csharp
// Example: Trade completion event chain

1. TradeManager.ProcessTrade()
   ↓ (publishes OnTradeCompletedEvent)
2. RosterManager subscribes → Updates rosters
   ↓ (publishes OnRosterChangedEvent)
3. ChemistryManager subscribes → Recalculates chemistry
   ↓ (publishes OnChemistryChangedEvent)
4. MoraleManager subscribes → Updates affected players' morale
   ↓ (publishes OnMoraleChangedEvent)
5. UIManager subscribes → Refreshes UI panels
```

**Event Naming Convention**: `OnEventNameEvent` (e.g., `OnPlayerSignedEvent`, `OnGameCompletedEvent`)

---

## AI Systems Architecture

### AI Personality Systems (12 systems)

#### 1. AICoachPersonality (977 LOC)
**Purpose**: Simulates opponent coach behavior with 60+ personality parameters

**Key Components**:
- Offensive philosophy (8 styles: FastPaced, SlowPaced, StarHeavy, ThreePointHeavy, etc.)
- Defensive philosophy (6 styles: Aggressive, Conservative, ZoneHeavy, SwitchHeavy, etc.)
- Rotation management (player trust, depth, matchup-based subbing)
- In-game adjustments (timeout triggers, tactical changes)
- Clutch behavior (risk-taking, play-calling preferences)
- Predictability system (pattern recognition for scouting)

**Factory Methods**:
```csharp
CreateFastPacedCoach()     // High pace, transition-focused
CreateDefenseFirstCoach()  // Conservative, defensive schemes
CreatePlayerDevCoach()     // Young player-friendly, high minutes
CreateStarHeavyCoach()     // Star-dependent, low depth
CreateAnalyticsCoach()     // Three-heavy, efficient shots
CreateOldSchoolCoach()     // Mid-range, post-ups, man-to-man
```

**Output**: Generates `OpponentTendencyProfile` for pre-game preparation

#### 2. AIGMController (516 LOC)
**Purpose**: AI General Manager for Coach-Only mode with hidden personality traits discovered through interactions

**Hidden Personality Traits**:
- Trade tendencies: TradeHappy, PatientBuilder, WinNowMentality
- Financial: CostConscious, SpendsToBuild
- Player handling: ProtectsStar, LoyalToPlayers, TrustsVeterans
- Draft: ValuesDraftPicks, LongTermThinker
- Coach relationship: TrustsCoach, HandsOn

**Request Processing**:
- Evaluates roster requests (trades, signings, waivers, extensions)
- Personality-based approval chances
- Reveals traits through decision patterns over time
- Generates contextual approval/denial responses

#### 3. AITradeEvaluator (538 LOC)
**Purpose**: Stats-based trade evaluation integrated with player value calculation

**Evaluation Components**:
- Player value: Production + potential + contract efficiency
- Draft pick value: $25M (1st round), $5M (2nd round) with year discounts
- Trade availability: Discount/premium based on player status
- Former player GM bonuses: Position expertise, teammate preferences

**Front Office Modifiers**:
- Competence-based error (poor GMs misjudge value)
- Negotiation skill affects acceptance thresholds
- Situational modifiers (deadline desperation, cap pressure)

**Acceptance Thresholds**:
```
Aggression:     VeryPassive (+5%), Aggressive (-2%), Desperate (-5%)
Situation:      Championship (-3%), Rebuilding (+2%)
Competence:     Elite (accurate), Poor (±15% error)
```

**Counter-Offer Generation**: Modifies proposals to reach acceptability

### AI Integration with Game Systems

```
Game Coach Controller
        ↓
   Uses AICoachPersonality → Generates opponent behavior
        ↓
   OpponentTendencyProfile → Feeds into GamePlanBuilder
        ↓
   User adjusts game plan based on tendencies
        ↓
   In-game: AI coach adjusts based on personality + game state
```

```
Trade Proposal Submitted
        ↓
   AITradeEvaluator.EvaluateTrade()
        ↓
   Calculate value delta (assets in vs. assets out)
        ↓
   Apply FrontOfficeProfile modifiers
        ↓
   Check acceptance threshold
        ↓
   Accept / Reject / Counter-offer
```

---

## Component Overview

### Core Components

#### Boot Flow
1. **Boot.cs**: Entry point, initializes all Singleton managers
2. **GameBootstrap.cs**: Handles new game creation or save loading
3. **MainMenuController.cs**: Main menu UI and navigation

#### Match Flow
1. **GameCoach.cs**: Main game loop controller
   - Manages possession clock
   - Handles coaching decisions
   - Processes substitutions and timeouts
2. **PlayByPlaySimulator.cs**: Possession simulation engine
   - Calculates shot attempts
   - Processes fouls and free throws
   - Generates play-by-play narrative
3. **GameResultsController.cs**: Post-game processing
   - Updates season stats
   - Processes player development
   - Triggers morale/chemistry updates

#### UI Components
- **Panel Controllers**: `TradePanel`, `DraftPanel`, `FreeAgencyPanel`
- **View Controllers**: `RosterView`, `StatsView`, `StandingsView`
- **Popup Dialogs**: Confirmation dialogs, info displays

### Component Interaction Example: Trade Flow

```
User initiates trade (TradePanel)
        ↓
TradePanel.ProposeTradeButton_Clicked()
        ↓
TradeManager.ProposeTrade(offer)
        ↓
AITradeEvaluator.EvaluateTrade(offer)
        ↓
[AI accepts] → TradeManager.ExecuteTrade()
        ↓
RosterManager.TransferPlayer(player, newTeam)
        ↓
Event: OnPlayerTradedEvent
        ↓
ChemistryManager.RecalculateChemistry()
MoraleManager.ProcessTradeImpact()
ContractManager.UpdateSalaryCap()
UIManager.RefreshTradePanel()
```

---

## Source Tree Reference

For detailed annotated source tree, see [source-tree-analysis.md](./source-tree-analysis.md).

**Critical Directories**:
- `Assets/Scripts/Core/AI/` - AI personality and decision systems
- `Assets/Scripts/Core/Data/` - Data models (51 files, ~24,881 LOC)
- `Assets/Scripts/Core/Managers/` - Domain managers (45 files)
- `Assets/Scripts/Simulation/` - Game simulation engine
- `Assets/Scripts/UI/` - UI controllers and views

**Critical Integration Points**:
- Event system: Managers publish/subscribe to events
- Singleton access: `ManagerName.Instance`
- Save/load: `SaveManager` serializes entire game state

---

## Development Workflow

For detailed development instructions, see [development-guide.md](./development-guide.md).

**Quick Reference**:
1. Open project in Unity Editor (version 6000.3.0f1)
2. Navigate to scene in `Assets/Scenes/`
3. Edit scripts via IDE (double-click `.cs` files)
4. Press Play to test in Editor
5. Build via File → Build Settings

**Key Workflows**:
- Adding managers: Create in `Assets/Scripts/Core/Managers/`, initialize in `Boot.cs`
- Adding data models: Create in `Assets/Scripts/Core/Data/`, add to `SaveData.cs` if persistent
- Creating UI: Create prefab + controller script, register with `UIManager`

---

## Testing Strategy

### Manual Testing (Current Approach)
- **Play Mode Testing**: Run game in Unity Editor to verify functionality
- **Build Testing**: Test standalone builds on Windows platform
- **Save/Load Testing**: Verify save file integrity and compatibility
- **Integration Testing**: Test cross-system interactions (trades, morale, chemistry)

### No Automated Test Framework
This project does not currently use Unity Test Framework or NUnit. All testing is manual through Play Mode execution.

**To Add Automated Testing** (future):
1. Install Unity Test Framework via Package Manager
2. Create test assembly in `Assets/Tests/`
3. Write unit tests for data models and manager logic
4. Write integration tests for multi-system workflows

---

## Performance Considerations

### Optimization Strategies
1. **Object Pooling**: Reuse game objects instead of Instantiate/Destroy
2. **Manager Caching**: Avoid `FindObjectOfType`, use cached Singleton references
3. **Event Unsubscription**: Always unsubscribe from events in `OnDestroy()`
4. **String Operations**: Use `StringBuilder` for frequent string concatenation
5. **Update Loop Optimization**: Minimize work in `Update()`, prefer events

### Known Performance Concerns
- Large roster operations (30 teams × 15 players)
- Play-by-play simulation (82 games × ~200 possessions)
- Chemistry calculation (all player pairs on team)

---

## Deployment Architecture

### Build Configuration
- **Platform**: PC, Mac & Linux Standalone (Windows x86_64)
- **Build Type**: Standalone executable
- **Output**: `NBA Head Coach.exe` + `NBA Head Coach_Data/` folder

### Distribution
- No cloud services or online connectivity required
- Save files stored locally in `%APPDATA%/../LocalLow/`
- Single-player only (no multiplayer)

### System Requirements
- **OS**: Windows 10 or later
- **Processor**: 2 GHz or faster
- **Memory**: 4 GB RAM minimum
- **Graphics**: DirectX 11 compatible GPU
- **Storage**: 500 MB available space

---

## Security & Compliance

### Data Privacy
- All game data stored locally
- No telemetry or analytics
- No network communication
- No user account system

### Save File Integrity
- Ironman mode prevents save scumming
- Save file validation on load
- Backup save system (optional)

---

## Future Architecture Considerations

### Potential Enhancements
1. **Multiplayer Support**: Online league mode with shared draft and free agency
2. **Modding System**: Expose data models for user customization
3. **AI Learning**: Train AI coach personalities on user behavior
4. **Cloud Saves**: Optional cloud sync for save files
5. **Test Framework**: Add automated testing for data models and managers
6. **Performance Profiling**: Identify and optimize bottlenecks

### Scalability Concerns
- Current architecture supports single-user, single-season well
- Multi-season simulations may require performance optimization
- Large league expansions (40+ teams) would need manager refactoring

---

## Additional Documentation

- [Data Models Reference](./data-models.md) - Complete data model documentation
- [Manager Systems Reference](./manager-systems.md) - All 45 domain managers
- [AI Systems Reference](./ai-systems.md) - AI personality and decision-making
- [Development Guide](./development-guide.md) - Setup, workflow, and best practices
- [Project Overview](./project-overview.md) - High-level summary

---

**Document Version**: 1.0
**Last Updated**: 2026-02-16
**Unity Version**: 6000.3.0f1
**Architecture Pattern**: Domain-Driven Design with Singleton Managers + Event-Driven Communication

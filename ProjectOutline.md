# NBA Head Coach – Complete Design Document

> **Purpose**: This document is the single source of truth for understanding the entire game design. An AI or developer can reference this without scanning all project files.

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
│   ├── AI/                 # AI coach personalities, trade evaluation
│   ├── Data/               # All data models (Player, Team, Contract, etc.)
│   ├── Gameplay/           # In-game coaching logic
│   ├── Manager/            # Domain managers (trades, draft, development, etc.)
│   └── Simulation/         # Game/possession simulation engine
├── UI/
│   ├── Panels/             # All UI panels (Dashboard, Roster, Trade, etc.)
│   ├── Components/         # Reusable UI components
│   └── Match/              # Match-specific UI (overlay, clipboard)
├── View/                   # Camera, visualization, player visuals
├── Tools/                  # Scene setup, UI builders
└── Tests/                  # Unit and integration tests
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

**Manager Registration**: All domain managers register via `Register*` methods on init.

### SeasonController
**File**: `Assets/Scripts/Core/SeasonController.cs`

Manages the 82-game schedule, calendar, standings, and season phases.

**Phases**: Preseason → Regular Season → Play-In → Playoffs → Draft → Free Agency → Offseason

**Key Dates**:
- Trade deadline
- All-Star break
- Play-in tournament
- Playoffs
- Draft lottery + Draft
- Free agency opens

**Events**: `OnGameDay`, `OnPhaseChanged`, `OnDateChanged`

### MatchFlowController
**File**: `Assets/Scripts/Core/MatchFlowController.cs`

Bridges calendar events to simulation; handles pre-game, match, and post-game flow.

**Flow**:
1. Pre-game: Set lineup, strategy, view opponent scouting
2. Match: Instant sim OR interactive with coaching controls
3. Post-game: Results, box scores, player of game, headlines

---

## SIMULATION ENGINE

### GameSimulator
**File**: `Assets/Scripts/Core/Simulation/GameSimulator.cs`

Full game simulation via possession loop.

**Features**:
- 4 quarters + overtime
- Possession-by-possession gameplay
- Energy/fatigue drain
- Box score generation
- Quarter-by-quarter scoring

### PossessionSimulator
**File**: `Assets/Scripts/Core/Simulation/PossessionSimulator.cs`

Individual possession resolution.

**Calculates**:
- Shot selection (based on player attributes + strategy)
- Shot success (InsideScoring, MidRange, ThreePoint)
- Turnovers, steals, blocks
- Rebounds (offensive/defensive)
- Assists

**Factors**: Player attributes, team strategy, fatigue, chemistry, play called

### Supporting Classes
- `BoxScore` - Team and player statistics
- `PlayerGameStats` - Individual stat line
- `PossessionResult` - Outcome of single possession
- `SpatialState` - Court positioning
- `ShotCalculator` - Shot probability formulas

---

## DATA MODELS

### Player
**File**: `Assets/Scripts/Core/Data/Player.cs`

**Bio**: Name, Age, Height, Weight, Position, College, Nationality, Draft info

**Attributes** (ALL HIDDEN - 0-99 scale, internal only):
> **Design Philosophy**: Players never see raw attribute numbers. All attributes drive simulation and are revealed ONLY through text-based scouting reports. This creates a more realistic "eye test" experience where coaches evaluate players through observation and scouting, not spreadsheets.

- Offense: InsideScoring, MidRange, ThreePoint, FreeThrow, Passing, BallHandling
- Defense: PerimeterDefense, InteriorDefense, Stealing, Blocking
- Physical: Speed, Strength, Stamina, Rebounding, Athleticism
- Intangibles: WorkEthic, Consistency, Durability, Leadership, Clutch, Basketball IQ, Coachability, Loyalty

**How Attributes Are Revealed**:
- Scouting reports describe abilities in text form ("Elite three-point shooter", "Struggles against physical defenders")
- Game performance reveals tendencies through box scores and play-by-play
- Scout quality affects accuracy of text descriptions
- No numerical ratings shown anywhere in UI

**Development System**:
- Development Phase: Rising → Peak → Veteran → Decline
- Potential (hidden): Determines ceiling
- Age-based progression/decline curves

**Injury System**:
- InjuryType: None, Ankle, Knee, Back, Shoulder, Concussion, etc.
- InjurySeverity: Minor (1-3 days), Moderate (1-2 weeks), Major (3-6 weeks), Severe (season-ending)
- InjuryStatus: Healthy, Probable, Questionable, Doubtful, Out
- FatigueLevel (0-100): Separate from Energy, affects injury risk
- Load management tracking (minutes in last 7 days)

**Morale/Personality**:
- Morale (0-100): Affected by playing time, wins, role, contract
- CoachRelationship (0-100)
- RoleSatisfaction (0-100)
- Personality traits: Ego, Leadership, Composure, Aggression

**Contract**: Reference to Contract object

### Team
**File**: `Assets/Scripts/Core/Data/Team.cs`

**Identity**: TeamId, City, Name, Abbreviation, Conference, Division, Arena, Colors

**Roster**: RosterPlayerIds (list), StartingLineup mapping

**Strategy**:
- OffensiveStrategy: Pace, PlayStyle, ThreePointRate
- DefensiveStrategy: Intensity, SchemeFocus

**Performance**:
- Wins, Losses, HomeRecord, AwayRecord
- ConferenceRecord, DivisionRecord
- CurrentStreak

**Chemistry**:
- TeamChemistry (0-100): Calculated from personality compatibility
- LockerRoomMood: Excellent/Good/Neutral/Tense/Toxic
- VeteranLeaders: Players with leadership role

### Contract
**File**: `Assets/Scripts/Core/Data/Contract.cs`

**CBA-Compliant Structure**:
- Salary per year (up to 5 years)
- ContractType: Standard, TwoWay, 10Day, Exhibit10, TrainingCamp
- Bird Rights tracking
- Options: PlayerOption, TeamOption, EarlyTermination
- Restrictions: NoTradeClause, TradeKicker percentage
- Incentives and bonuses

### Coach
**File**: `Assets/Scripts/Core/Data/Coach.cs`

**For Player's Coach (CoachCareer)**:
- Name, Age, Experience
- Career stats: Wins, Losses, Championships, COY awards
- Current team and contract
- Media relationship
- Reputation

**For Staff/AI Coaches**:
- Specialty: HeadCoach, OffensiveCoordinator, DefensiveCoordinator, PlayerDevelopment
- Ratings: Offensive, Defensive, Development (0-100)
- Personality: RiskTaking, PlayStyle, Patience, Loyalty, TradeAggression

### Agent
**File**: `Assets/Scripts/Core/Data/Agent.cs`

- AgentId, Name
- ClientList (player IDs)
- Negotiation style (aggressive, fair, difficult)
- Relationship with teams

### SaveData
**File**: `Assets/Scripts/Core/Data/SaveData.cs`

**Complete Save Payload**:
- CoachCareer state
- Season number, current date
- All team states (roster, record, finances)
- All player states (attributes, contracts, injuries)
- Calendar and schedule
- Draft class and picks
- Transaction history
- Awards history
- Playoff bracket state
- AI memory (adaptation data)
- Franchise records
- Ironman mode flag

---

## MANAGER SYSTEMS

### Fully Implemented

| Manager | File | Purpose |
|---------|------|---------|
| TradeSystem | `Manager/TradeSystem.cs` | Trade proposals, validation, execution |
| SalaryCapManager | `Manager/SalaryCapManager.cs` | Cap calculations, exceptions, luxury tax |
| RosterManager | `Manager/RosterManager.cs` | Roster moves, waiving, signing |
| DraftSystem | `Manager/DraftSystem.cs` | Draft lottery, pick execution |
| PlayerDevelopmentManager | `Manager/PlayerDevelopmentManager.cs` | Attribute progression |
| ContractNegotiationManager | `Manager/ContractNegotiationManager.cs` | Contract offers, negotiation |
| ScoutingManager | `Manager/ScoutingManager.cs` | Scouting assignments |
| ScoutingReportGenerator | `Manager/ScoutingReportGenerator.cs` | Text-based reports |
| UnifiedCareerManager | `Manager/UnifiedCareerManager.cs` | Cross-track careers, transitions, non-player retirement |
| GMJobSecurityManager | `Manager/GMJobSecurityManager.cs` | GM hiring/firing, FO progression |
| FormerPlayerCareerManager | `Manager/FormerPlayerCareerManager.cs` | Former player coaching pipeline |
| RetirementManager | `Manager/RetirementManager.cs` | Player & non-player retirement |

### Needs Implementation/Enhancement

| Manager | File | Status | Work Needed |
|---------|------|--------|-------------|
| InjuryManager | `Manager/InjuryManager.cs` | NEW | Full injury system |
| PlayoffManager | `Manager/PlayoffManager.cs` | NEW | Play-in + bracket |
| MatchCoachingController | `Gameplay/MatchCoachingController.cs` | NEW | In-game controls |
| MediaManager | `Manager/MediaManager.cs` | NEW | Press conferences |
| AIAdaptationManager | `AI/AIAdaptationManager.cs` | NEW | Learning system |
| HistoryManager | `Manager/HistoryManager.cs` | NEW | Records, Hall of Fame |
| TrainingCampManager | `Manager/TrainingCampManager.cs` | NEW | Camp/preseason |
| AllStarManager | `Manager/AllStarManager.cs` | EXISTS | Needs content |
| OffseasonManager | `Manager/OffseasonManager.cs` | EXISTS | Needs content |
| SummerLeagueManager | `Manager/SummerLeagueManager.cs` | EXISTS | Needs content |
| AwardManager | `Manager/AwardManager.cs` | EXISTS | Voting logic |
| FinanceManager | `Manager/FinanceManager.cs` | EXISTS | Revenue system |
| PersonalityManager | `Manager/PersonalityManager.cs` | EXISTS | Morale logic |
| JobSecurityManager | `Manager/JobSecurityManager.cs` | EXISTS | Hot seat, firing |
| CoachJobMarketManager | `Manager/CoachJobMarketManager.cs` | EXISTS | Job offers |

---

## FEATURE SPECIFICATIONS

### Implementation Status Legend
- ✅ **COMPLETE** - Fully implemented and functional
- 🔶 **PARTIAL** - Core elements exist, needs enhancement
- ⬜ **STUB** - Code exists but minimal functionality
- ❌ **NOT IMPLEMENTED** - Needs to be built from scratch

### Implementation Status Summary

| # | Feature | Status | Notes |
|---|---------|--------|-------|
| 1 | Match Simulator | ✅ | Core sim + ESPN Gamecast visualization complete |
| 2 | Playoff System | ✅ | Play-in tournament + bracket fully implemented |
| 3 | Injury System | ✅ | Full system with generation, recovery, load management |
| 4 | Offseason Phases | ✅ | Summer League, Training Camp, Preseason complete |
| 5 | Awards System | ✅ | Full voting: MVP, DPOY, ROY, 6MOY, MIP, COY, All-Teams |
| 6 | Morale & Chemistry | ✅ | Full system with gameplay impact, locker room events |
| 7 | Job Security | ✅ | Fully functional with owner meetings |
| 8 | Media System | ✅ | Press conferences with consequences, headlines |
| 9 | AI Adaptation | ✅ | In-game learning, pattern detection, counter-strategies |
| 10 | Financial System | ✅ | Full revenue model, game-by-game income, projections |
| 11 | Scouting | ✅ | Text-based reports only, no numerical attributes shown |
| 12 | History & Records | ✅ | Season archives, franchise records, Hall of Fame |
| 13 | Save System | ✅ | Complete with Ironman mode |
| 14 | Former Player Careers | ✅ | Coaching progression, hiring bonuses, matchup notifications |
| 15 | Unified Career System | ✅ | Coach↔GM transitions, non-player retirement, cross-track careers |
| 16 | Architecture Consolidation | ✅ | Facade pattern, panel registry, shared UI components |

**UI Panels**: ✅ All core panels implemented (Dashboard, Roster, Calendar, Standings, Trade, Draft, Pre/Post Game)

---

### 1. MATCH SIMULATOR (Core Gameplay Experience) ✅ COMPLETE

> **This is the heart of the game.** The Match Simulator is where users coach their team in real-time, making strategic decisions that determine outcomes. Inspired by ESPN Gamecast.

**Implementation Files**:
- `Core/MatchSimulationController.cs` - Main simulation controller with speed controls, auto-pause, events
- `UI/Panels/MatchPanel.cs` - ESPN Gamecast-style UI layout
- `UI/Components/CourtDiagramView.cs` - 2D half-court with player dots
- `UI/Components/CoachingMenuView.cs` - Tabbed coaching interface (offense/defense/subs/matchups)
- `Core/PlayByPlayGenerator.cs` - Broadcast-style text generation with highlight detection

#### Visual Style
- **ESPN Gamecast-style** - Text-based play-by-play as primary visualization
- **NOT 3D graphics** - Focus on information and decisions, not visuals
- Simple 2D court diagram showing player positions (dots on half-court)

#### Screen Layout
```
+----------------------+--------------------+
|    PLAY-BY-PLAY      |    STATS PANEL     |
|    (scrolling feed)  |    - Box Score     |
|    newest at top     |    - Team Stats    |
|    scroll to history |    - Current Lineup|
|                      |    (fatigue/fouls) |
+----------------------+--------------------+
|         2D COURT DIAGRAM                  |
|      (dots showing player positions)      |
+-------------------------------------------+
|   SCOREBOARD  |  QUARTER  |  GAME CLOCK   |
+-------------------------------------------+
```

#### Simulation Flow

**Auto-Play with Pause**:
- Plays stream automatically at user-selected speed
- User can pause anytime to make decisions
- Pauses automatically at decision points

**Decision Points** (game auto-pauses):
- Timeouts (called by either team)
- Quarter breaks
- Foul-outs or injuries (substitution required)

**Speed Options**:
| Speed | Pace | Use Case |
|-------|------|----------|
| 1x | ~2 sec/play | Important games, immersive |
| 2x | ~1 sec/play | Default, balanced |
| 4x | ~0.5 sec/play | Quick progression |
| Instant | Skip to next decision point | Management focus |
| Full Sim | Instant to box score | Skip entire game |

**Clutch Time** (final 2 min of close game):
- Automatic slow-down of simulation speed
- Enhanced play-by-play descriptions (more dramatic)
- Clutch attribute becomes more impactful on outcomes

#### Coaching Controls

**Offensive Play Calling** (Full Control):
- Select specific play from playbook
- Designate target player to run play through
- 50/50 balance: Play selection and player attributes equally important

**Play Types** (15-20 per playbook):
- Half-court sets: Pick & roll, motion, post-up, isolation
- Fast break: Push pace, trail three, rim attack
- Out-of-bounds: Sideline plays, baseline plays
- End-of-clock: Quick hitters for shot clock/game situations

**Defensive Adjustments**:
- Team scheme: Man-to-man, 2-3 zone, 3-2 zone, 1-3-1 zone, box-and-one
- Intensity: Conservative (avoid fouls), Normal, Aggressive (pressure)
- Matchup assignments: "Put Green on Doncic"
- Double team triggers: "Double post touches", "Double [player] on catch"

**Substitutions**:
- Only during stoppages (realistic NBA rules)
- Can queue subs to happen at next dead ball
- Auto-pause when player fouls out or gets injured

**Coaching Menu**:
- Opens via popup on pause or timeout
- Not cluttering screen during play
- Full strategy adjustment interface

**Auto-Coach Mode**:
- Toggle to let AI handle subs and play calling
- AI follows team strategy settings
- User can take control back anytime
- For users who want to watch without micromanaging

#### Play-by-Play Text System

**Mixed Style Based on Moment**:

*Routine plays* (statistical, quick to read):
```
"Curry (5-8 3PT) - 3PT from right wing off PnR. MADE. GSW 98-95. Run: 12-4."
```

*Big moments* (broadcast style, dramatic):
```
"Green sets the screen at the top of the key. Curry comes off it,
Davis switches onto him. Curry with the step-back three... BANG!
That's his fifth triple of the quarter!"
```

**Occasional Color Commentary**:
- Only on highlight plays (dunks, clutch shots, blocks)
- "What a rejection by Davis!" or "The crowd is on their feet!"

**Information in Play Text**:
- Player name and current shooting line
- Shot type and location
- Result (MADE/MISSED/BLOCKED)
- Updated score
- Run tracker when relevant

#### No Hand-Holding
- No "Coach's Notes" or AI suggestions
- User must read the game through stats and play-by-play
- Part of the skill is understanding what adjustments to make
- Sink or swim coaching experience

#### PlayBook System (`Data/PlayBook.cs`, `Data/SetPlay.cs`)
- Each team has 15-20 plays
- Plays have familiarity levels (affects success rate)
- Practice plays during training to increase familiarity
- Can install new plays from library

#### PlayerGameInstructions (`Data/PlayerGameInstructions.cs`)
- Per-player offensive focus: Scoring, Distributing, Balanced
- Per-player defensive assignment
- Aggression level
- Shot selection tendencies

### 2. PLAYOFF SYSTEM ✅ COMPLETE

**Implementation Files**:
- `Core/Data/PlayoffData.cs` - Data structures for bracket, series, play-in games
- `Core/Manager/PlayoffManager.cs` - Singleton manager for playoff flow
- `UI/Panels/PlayoffBracketPanel.cs` - Bracket visualization UI

#### Play-In Tournament (seeds 7-10 per conference)
- Game 1: 7 vs 8 → Winner gets 7 seed
- Game 2: 9 vs 10 → Winner advances
- Game 3: Loser of G1 vs Winner of G2 → Winner gets 8 seed

#### Traditional Bracket (8 teams per conference)
- First Round: 1v8, 2v7, 3v6, 4v5 (best of 7)
- Conference Semis (best of 7)
- Conference Finals (best of 7)
- NBA Finals (best of 7)
- Home court: 2-2-1-1-1 format

#### Playoff Series Experience (Different from Regular Season)

**Series-Specific Stats**:
- Track player averages for THIS series (PPG, RPG, APG in series)
- Show series shooting percentages
- Display in box score and play-by-play

**Home Court Atmosphere**:
- Play-by-play reflects crowd: "The crowd erupts!" at home
- Road games: "Silencing the hostile crowd..."
- Atmosphere affects momentum calculations

**Increased Intensity**:
- Players tire faster (playoff minutes drain more energy)
- Defense is tighter (lower shooting percentages league-wide)
- Games tend to be grittier, lower-scoring

**Matchup Adjustments**:
- Teams learn from previous games in series
- AI opponent adapts based on what worked/didn't work
- Game 4 strategies differ from Game 1
- Scouting report updates with series data

#### Bracket UI

**Full Bracket View**:
- Traditional tournament bracket visualization
- All matchups with current series scores (3-2, 2-1, etc.)
- Click any series for details

**Detailed Series View**:
- Your current opponent breakdown
- Game-by-game results and scores
- Next game date/location
- Series stats comparison
- Key matchup analysis

**Other Series**:
- Resolve in background (not watchable)
- Bracket updates between your games
- Can scout potential future opponents

#### Elimination Games

**Special Handling**:
- Intro text: "It's do or die. Win or go home. Game 7 of the Western Conference Finals."
- Enhanced clutch mechanics (Clutch attribute more impactful)
- Pressure affects low-composure players
- Cannot be instant-simmed (must at least watch at 4x)

### 3. INJURY SYSTEM ✅ COMPLETE

**Implementation Files**:
- `Core/Data/InjuryData.cs` - Enums and data structures (InjuryType, InjurySeverity, InjuryStatus)
- `Core/Manager/InjuryManager.cs` - Full injury generation, recovery, and load management
- Enhanced `Core/Data/Player.cs` - Injury history tracking, fatigue, minutes tracking
- Enhanced `Core/Data/SaveData.cs` - Injury serialization

#### Injury Occurrence

**Base Mechanics**:
- Random chance each possession (very low %, ~0.01%)
- Higher on contact plays (drives, rebounds, screens)
- Lower on jump shots and free throws

**Risk Modifiers**:
- **Fatigue**: Risk scales up as energy drops (2x risk at 50% energy, 4x at 25%)
- **Durability attribute**: High durability players have lower base risk
- **Injury history**: Previously injured body parts have elevated re-injury risk

#### Injury Types & History Tracking

**Body Part Categories**:
| Body Part | Min Recovery | Max Recovery | Re-injury Risk |
|-----------|--------------|--------------|----------------|
| Ankle | 3 days | 3 weeks | +25% to ankle |
| Knee (minor) | 1 week | 4 weeks | +30% to knee |
| Knee (ACL/major) | Season | Season+ | +50% to knee |
| Back | 1 week | 6 weeks | +20% to back |
| Shoulder | 2 weeks | 8 weeks | +25% to shoulder |
| Concussion | 1 week | 4 weeks | Protocol required |
| Hamstring | 1 week | 4 weeks | +35% to hamstring |

**Historic Injury Tracking**:
- Each player has injury history record
- Past injuries create "weak spots" with elevated risk
- Play-by-play can reference: "Re-aggravated his surgically repaired knee"
- Chronic injury players may need load management all season

#### Load Management System

**Fatigue Meter**:
- Visible per-player fatigue level (0-100)
- Displayed in roster panel and lineup screens
- Drains with minutes played, recovers with rest

**Minutes Recommendations**:
- System suggests: "Thompson should play <28 min tonight"
- Based on current fatigue, recent workload, injury history
- Ignoring recommendations increases risk

**Rest Days Matter**:
- Back-to-backs: High fatigue risk, no recovery time
- 1 day rest: Partial fatigue recovery
- 2+ days rest: Full fatigue recovery
- Schedule impacts lineup decisions

**DNP-Rest Option**:
- Can mark healthy player as DNP-Rest
- Player fully recovers fatigue
- May impact morale (stars don't like sitting)
- Media/fans may react negatively to resting stars

#### Injury Report System

**Status Determination**:
- Based on days remaining + uncertainty factor
- **Out**: 5+ days remaining (100% miss)
- **Doubtful**: 3-4 days remaining (25% chance to play)
- **Questionable**: 1-2 days remaining (50-75% chance)
- **Probable**: 0 days, minor lingering (90% chance)

**Uncertainty Mechanic**:
- Questionable players: "60% to play" - unknown until game time
- User doesn't know for sure until pregame
- Can choose to risk borderline players or hold out

**Game Day Decision**:
- For Questionable/Probable players, you decide:
  - Play them (risk aggravation)
  - Hold out (guarantee rest)
- Playing injured players has performance penalty + re-injury risk

### 4. OFFSEASON PHASES 🔶 PARTIAL

#### Phase 1: Summer League

**Roster Selection**:
- Choose which players attend (rookies, sophomores, camp invites)
- Each team sends 12-15 players
- Mix of draft picks, young roster players, and tryouts

**Tournament Simulation**:
- Las Vegas Summer League format
- Games simulate in background
- See box scores and standout performers

**Development Decisions**:
- Set focus area for each SL participant
- Choose: Scoring, Playmaking, Defense, Conditioning
- Focus affects which attributes improve

**UDFA Signing Opportunity**:
- Standout undrafted players become available
- Hidden gems can emerge from other teams' SL rosters
- Compete with other teams to sign them

#### Phase 2: Training Camp

**Roster Cut Decisions**:
- Must cut to 15-man roster
- Non-guaranteed contracts compete for spots
- See practice performance reports
- Make final cut decisions

**Playbook Installation**:
- New plays need practice time
- See familiarity % increase during camp
- Can prioritize which plays to drill
- Higher familiarity = better execution in games

**No Position Battles** (streamlined):
- Depth chart set by user, not competition
- Focus on roster cuts and playbook, not starter battles

#### Phase 3: Preseason

**Exhibition Schedule**:
- 4-6 games against other teams
- No standings impact
- Results don't affect record

**Lineup Experimentation**:
- Try unusual lineups without consequence
- "What if we start the rookie at PG?"
- Test new acquisitions in game situations
- No fatigue carryover to regular season

**Injury Risk Active**:
- Players CAN get injured in preseason
- Creates stakes for who you play
- Balance evaluation vs. protection of stars

#### Phase 4: Staff Hiring

**Assistant Coaches**:
- Offensive Coordinator: Boosts team offensive rating
- Defensive Coordinator: Boosts team defensive rating
- Each has rating (1-100) affecting impact

**Player Development Coach**:
- Improves young player development speed
- Critical for rebuilding teams
- Higher rating = faster attribute growth

**Scouts** (Regional Specializations):
- West Coast Scout: Better intel on Pac-12, WCC, etc.
- East Coast Scout: Better intel on ACC, Big East, etc.
- International Scout: Better intel on overseas prospects
- Pro Scout: Better reports on NBA players (trade targets)

**Training/Medical Staff**:
- Better trainers = faster injury recovery
- Reduces base injury risk for all players
- Worth investment for injury-prone rosters

### 5. AWARDS SYSTEM 🔶 PARTIAL

#### Major Awards (End of Season)

**Award Categories**:
- MVP (Most Valuable Player)
- DPOY (Defensive Player of Year)
- ROY (Rookie of Year)
- 6MOY (Sixth Man of Year)
- MIP (Most Improved Player)
- COY (Coach of Year)

**Simulated Media Voting**:
- AI "voters" consider multiple factors:
  - Statistical performance
  - Team success (wins matter for MVP)
  - Narrative (comeback stories, breakout seasons)
  - Some randomness for unpredictability
- Results feel realistic, not purely formulaic

**Presentation**:
- Awards summary screen at end of season
- Shows winner + top 5 vote getters
- Vote totals and first-place votes displayed
- No lengthy ceremony - quick reveal and move on

#### All-League Teams

**Selections**:
- All-NBA (1st, 2nd, 3rd team) - 5 players each
- All-Defense (1st, 2nd team) - 5 players each
- All-Rookie (1st, 2nd team) - 5 players each

**Impact**:
- All-NBA affects contract eligibility (supermax)
- Boosts player morale and reputation
- Recorded in player career history

#### Monthly Awards

**Player of the Month** (per conference):
- East and West winners each month
- Based on that month's performance
- Notification + small morale boost

**Rookie of the Month**:
- Tracks rookie race throughout season
- Builds narrative toward ROY

#### Statistical Achievements

**Milestone Games** (celebrated in play-by-play):
- 50+ point games
- Triple-doubles
- 20/20 games (20 pts + 20 reb or 20 ast)
- 5x5 games (5+ in 5 categories)

**Career Milestones**:
- Points: 10,000 / 20,000 / 30,000 / 40,000
- Assists: 5,000 / 10,000 / 15,000
- Rebounds: 5,000 / 10,000 / 15,000
- Special notification when reached

**Streak Tracking**:
- Consecutive games with 10+ points
- Consecutive double-doubles
- Consecutive games with a 3-pointer
- Streaks noted in play-by-play and player profile

**Season Leaderboards**:
- Track league leaders in all major categories
- PPG, RPG, APG, SPG, BPG, FG%, 3P%
- Updated throughout season
- Viewable in standings/stats panel

#### Coach Career Achievements

**Tracked for Your Career**:
- Championships won
- Finals appearances
- Conference Finals appearances
- Playoff appearances
- Division titles
- COY awards
- Career wins/losses
- Win percentage

### 6. MORALE & CHEMISTRY ⬜ STUB

#### Individual Morale System

**Morale Categories** (visible to user):
- 😊 **Happy** (80-100): Playing great, loves the situation
- 😐 **Content** (60-79): Satisfied, no complaints
- 😕 **Unhappy** (40-59): Issues brewing, needs attention
- 😠 **Frustrated** (0-39): Major problems, may act out

**Morale Factors**:
- Playing time vs expectations (stars expect 30+ min)
- Team success (winning boosts morale)
- Role satisfaction (starter vs bench vs DNP)
- Contract status (underpaid players get frustrated)
- Coach relationship (built through decisions)
- Recent performance (slumps hurt morale)

#### Low Morale Consequences

**On-Court Performance**:
- Unhappy: -5 to key attributes
- Frustrated: -10 to -15 to key attributes
- Affects effort, focus, energy

**Trade Requests**:
- Very unhappy players formally request trade
- Shows in inbox as urgent message
- Creates pressure to move them or fix situation
- Public if not addressed

**Locker Room Issues**:
- One unhappy player drags down team chemistry
- Spreads negativity to others
- Can create team-wide morale drop

**Media Leaks**:
- Player makes comments to media
- "Sources say [Player] is unhappy with role"
- Creates external pressure
- Affects your job security with owner

#### Team Chemistry System

**Chemistry Level**: 0-100 (calculated, not directly set)

**Chemistry Factors**:
- Personality compatibility matrix
- Time played together (tenure bonus)
- Veteran leadership presence
- Recent conflicts vs bonding moments
- Win/loss streaks

#### Chemistry Gameplay Impact

**Passing & Ball Movement**:
- High chemistry: Better passing accuracy, fewer turnovers
- Low chemistry: Selfish play, more turnovers, iso-heavy offense

**Help Defense & Rotations**:
- High chemistry: Players help each other, good rotations
- Low chemistry: Breakdowns, blown coverages, miscommunication

**Clutch Performance**:
- High chemistry: Trust each other in big moments
- Low chemistry: Finger-pointing, tight in close games

**Playbook Execution**:
- High chemistry: Plays run smoothly, good timing
- Low chemistry: Freelancing, ignoring plays called

#### Personality Conflicts

**Baseline Tension** (automatic):
- Two high-ego players = constant friction
- Ball-dominant players compete for touches
- Veterans may resent young stars

**Event Triggers** (specific incidents):
- Shot selection disputes ("He never passes!")
- Blame for losses ("He cost us the game")
- Playing time complaints going public
- Contract jealousy

**Conflict Resolution**:
- Trade one player
- Reduce one player's role
- Let time heal (slow, risky)
- Some conflicts never resolve

#### Positive Chemistry Builders

**Veteran Leadership**:
- High-leadership vets boost team chemistry
- Mentor young players
- Calm locker room after losses

**Winning**:
- Win streaks build chemistry naturally
- Playoff success bonds teams

**Time Together**:
- Players who've been teammates 2+ years have bonus chemistry

### 7. JOB SECURITY ✅ COMPLETE

#### Job Security Meter
- 0-100 scale (visible to user)
- Below 50: Concerning, owner watching closely
- Below 25: "Hot seat" - active danger
- Below 10: Firing imminent

#### Setting Expectations (Season Start)

**Negotiable Goals**:
- Owner proposes expectations based on roster
- You can push back: "This roster is rebuilding"
- Agree on realistic targets together
- Targets become your benchmark

**Expectation Types**:
- Win total (e.g., "45+ wins")
- Playoff appearance
- Playoff round advancement
- Player development goals
- Chemistry/culture goals

#### What Hurts Job Security

**Missing Expectations**:
- Expected playoffs but missed
- Expected 50 wins, got 40
- Bigger gap = bigger drop in security

**Playoff Underperformance**:
- First round exit as top seed
- Losing to inferior opponent
- Blown 3-1 lead devastating

**Player Issues**:
- Star player demanding trade
- Morale issues going public
- Locker room problems in media

**Development Failures**:
- Young players not improving
- Top draft pick busting
- Wasted potential

#### Low Job Security Consequences

**Owner Meetings**:
- Summoned to owner's office
- Given ultimatums: "Win 5 of next 10 games"
- Clear, measurable survival requirements

**Front Office Interference**:
- Owner blocks certain trades
- Mandates playing time for favorites
- Forces you to start/bench specific players
- Overrules your decisions

**Firing**:
- **Mid-season**: Can be fired during season if security hits 0
- **End-of-season**: Survive season but fired in offseason
- Get a final meeting explaining why

#### After Being Fired

**Job Search**:
- Your career continues (unless you choose to retire)
- See list of open coaching jobs
- Apply for positions you want
- Interview process (simplified)
- Get hired based on your resume/reputation

**Reputation Matters**:
- Past success helps land new jobs
- Championships make you attractive
- Repeated firings hurt future prospects
- Can rebuild career with smaller program

#### Being Recruited

**Other Teams Notice Success**:
- Winning attracts attention
- Better jobs may reach out
- Can receive unsolicited offers
- Higher salary, better roster opportunities

**Loyalty Decisions**:
- Stay with team you built?
- Take the better opportunity?
- Breaking contract has consequences

### 8. MEDIA SYSTEM ❌ NOT IMPLEMENTED

#### Post-Game Press Conferences

**Frequency**: After notable games only (~30% of games)
- Big wins (blowouts, upsets, playoff clinching)
- Tough losses (blown leads, bad performances)
- Controversies (ejections, conflicts, injuries)
- Milestones (records, streaks, achievements)
- Playoff games (always)

#### Question Types

**Game Performance**:
- "Why did you go small in the 4th quarter?"
- "What happened on defense tonight?"
- "Walk us through that final possession."

**Player-Specific**:
- "Is [Player] in a slump?"
- "Why isn't [Player] getting more minutes?"
- "How do you evaluate [Rookie]'s development?"

**Controversy/Drama**:
- "Reports say [Player] is unhappy. Comment?"
- "There seemed to be tension on the bench. What happened?"
- "How do you respond to critics saying you've lost the locker room?"

**Forward-Looking**:
- "Can this team make the playoffs?"
- "What needs to change going forward?"
- "Is [Injured Player] close to returning?"

#### Response System

**Multiple Choice Tones**:
- 🔥 **Aggressive**: Call out players, challenge critics, show emotion
- 🤝 **Diplomatic**: Balanced, professional, non-committal
- 💯 **Honest**: Direct truth, even if uncomfortable
- 🚫 **Deflect**: Dodge the question, redirect to something else

Each question shows 3-4 response options with tone indicators.

#### Response Consequences

**Player Morale Impact**:
- Criticizing a player in press → Their morale drops
- Praising/supporting a player → Their morale rises
- "I need more from [Player]" vs "He's working hard"

**Team Chemistry Impact**:
- Taking blame yourself → Team appreciates it
- Blaming players → Locker room tension
- "I put them in a bad position" vs "They didn't execute"

**Owner/Job Security Impact**:
- Professional responses maintain owner confidence
- Controversial statements can concern ownership
- "We're going in the right direction" vs "This organization needs changes"

**No Media Relationship Tracking** (simplified):
- Each press conference is standalone
- No ongoing media reputation system

### 9. AI ADAPTATION ❌ NOT IMPLEMENTED

#### What AI Tracks

**Strategic Preferences** (primary focus):
- Pace preference (fast vs slow)
- Offensive style (3-point heavy, post-up, balanced)
- Defensive scheme tendencies (man vs zone usage)
- Aggressiveness (pressing, fouling, gambling)

**Not Tracked** (simplified):
- Individual play calls
- Specific lineup combinations
- Timeout timing

#### Adaptation Speed

**Within-Game Adaptation** (immediate):
- AI adjusts mid-game as patterns emerge
- If you spam 3-pointers, AI tightens perimeter D by halftime
- If you go small, AI exploits it quickly
- Real-time chess match with AI coach

#### How Adaptation Affects Gameplay

**Counters Your Style**:
- Heavy 3-point team? AI packs the perimeter
- Love fast pace? AI slows it down
- Zone defense? AI finds weaknesses in your scheme

**Exploits Weaknesses**:
- AI identifies what you're bad at defending
- Attacks your slow rotations, weak rebounding, etc.
- Targets your worst defenders

**Predictability Penalty**:
- Being one-dimensional has diminishing returns
- Same play/strategy repeated loses effectiveness
- AI "figures you out" faster if predictable

#### Counter-Play (Variety Rewarded)

**Mix Up Your Approach**:
- Varying pace keeps AI guessing
- Switching defensive schemes mid-game confuses AI
- Strategic diversity is rewarded

**The Meta-Game**:
- If you're always fast-paced, slow down sometimes
- If you're known for 3s, attack the rim unexpectedly
- Keeps opponents from fully adapting

### 10. FINANCIAL SYSTEM 🔶 PARTIAL

#### Salary Cap (Full CBA Rules)

**Cap Structure**:
- Soft cap: Can exceed with exceptions
- Luxury tax threshold: Penalties above this line
- Apron restrictions: Hard limits for taxpaying teams

**Exceptions**:
- Mid-Level Exception (MLE): Sign players when over cap
- Bi-Annual Exception (BAE): Smaller exception, every other year
- Minimum contracts: Always available
- Room Exception: For under-cap teams

**Bird Rights**:
- Bird Rights: Re-sign own players over cap (3+ years)
- Early Bird: 2 years
- Non-Bird: 1 year
- Affects max contract eligibility

**Trade Rules**:
- Salary matching requirements
- Sign-and-trade rules
- Trade exceptions (TPE)

#### Owner Spending System

**Owner Personality Types**:
| Type | Luxury Tax Willing | Staff Budget | Patience |
|------|-------------------|--------------|----------|
| Lavish | Always | High | Low |
| Competitive | For contender | Medium-High | Medium |
| Balanced | Occasionally | Medium | Medium |
| Frugal | Rarely | Medium-Low | High |
| Cheap | Never | Low | High |

**Budget Affects Everything**:
- Player salaries (luxury tax tolerance)
- Coaching staff quality (salary limits)
- Scouting staff (how many, how good)
- Training facilities (affects development)

**Revenue-Based Adjustments**:
- Market size affects base revenue
- Better markets have more financial flexibility
- Small market teams have tighter constraints

#### Financial Visibility (Full Transparency)

**Dashboard Shows**:
- Current cap situation
- Projected cap space next season
- Luxury tax status and bill
- All player salaries and contract years
- Dead money (waived players)

**When Making Decisions**:
- Clear display of financial impact
- "This trade puts you $3M into luxury tax"
- "Signing this player uses your MLE"

#### Pitch System

**Request Extra Spending**:
- Schedule meeting with owner
- Make case for luxury tax spending
- "We're one piece away from a championship"
- Owner accepts or rejects based on personality + situation

**Request Facility Upgrades**:
- Pitch for better training facility
- Affects player development speed
- Owner approval based on budget

**Request Staff Budget**:
- Ask for more scouts, better coaches
- Owner weighs cost vs benefit
- Success builds trust for future asks

### 11. SCOUTING ✅ COMPLETE

> **Core Design**: Scouting reports are TEXT-ONLY. No numerical attributes are ever shown to the user. This is the ONLY way to learn about player abilities beyond watching their stats in games.

#### Tiered Scouting Reports (Text-Based)

**Basic Report** (free, instant):
- Physical measurements (height, weight, wingspan)
- College/overseas stats (numbers from their history)
- Position and role description

**Detailed Report** (requires scout assignment):
- Strengths and weaknesses in prose form:
  - "Elite shooter with deep range and quick release"
  - "Struggles defending quicker guards on the perimeter"
  - "High motor player who never takes plays off"
- Player comparison: "Plays like a young Draymond Green"
- Tendencies: "Prefers going left", "First option in clutch situations"

**Full Report** (extended scouting):
- Everything above plus:
- Projection/ceiling estimate: "Projects as a solid starter" / "All-Star potential if shooting develops"
- Team fit analysis: "Would thrive in an up-tempo system"
- Character assessment: "Gym rat with excellent work ethic" / "Some maturity concerns"
- Injury history concerns if applicable

**Report Accuracy**:
- Scout quality determines how accurate descriptions are
- Low-quality scouts may miss weaknesses or overrate strengths
- Multiple scouts on same player gives more reliable picture

#### Scout Staff System

**Scout Types**:
- College Scout (West Coast): Pac-12, WCC, Big West
- College Scout (East Coast): ACC, Big East, SEC, Big Ten
- International Scout: Europe, Australia, other leagues
- Pro Scout: Reports on NBA players (trade targets)

**Scout Quality**:
- Higher rated scouts = more accurate reports
- Budget affects scout quality available
- Can assign scouts to specific players or regions

#### Draft Board System

**Consensus Big Board**:
- View "expert" rankings (AI-generated)
- See where media projects each prospect
- Compare to your own evaluation

**Your Board**:
- Auto-generated based on scouting
- Updates as you scout more players
- Higher scouting = more confident rankings
- Can see "?" for unscouted players

**No Manual Ranking**:
- Your board reflects your scouting investment
- Can't manually drag players around
- Scout more to update your evaluations

#### Draft Day

**Draft UI**:
- Live draft board showing picks as they happen
- Your board vs consensus comparison
- Remaining prospects highlighted

**Trade During Draft**:
- Can trade up or down
- AI teams will negotiate for picks
- Real-time decision making

**No Pre-Draft Events** (simplified):
- No individual workouts
- No combine mini-games
- No interview system
- Scouting reports are the only intel source

### 12. HISTORY & RECORDS ❌ NOT IMPLEMENTED

#### Season History (Per Year)

**Tracked Each Season**:
- All team records (W-L)
- Playoff brackets and results
- Champion and Finals MVP
- Award winners (MVP, DPOY, ROY, 6MOY, MIP, COY)
- Statistical leaders (PPG, RPG, APG, SPG, BPG)
- All-NBA and All-Defense teams

**Viewable In**:
- History panel with year selector
- Browse any past season in your save

#### Player Career Statistics

**Cumulative Totals**:
- Career points, rebounds, assists, steals, blocks
- Games played, minutes
- All-time rankings

**Season-by-Season**:
- View any player's stats per season
- See progression over career
- Identify peak years

**Career Averages**:
- PPG, RPG, APG, SPG, BPG
- Shooting percentages
- Minutes per game

**Accolades List**:
- All-Star appearances
- All-NBA selections
- All-Defense selections
- Awards won
- Championships

#### Franchise Records

**Single-Game Records**:
- Most points: "[Player] - 62 pts (vs LAL, Jan 15 2028)"
- Most rebounds, assists, steals, blocks
- Team records too (most points in a game)

**Season Records**:
- Best record: "67-15 (2027-28)"
- Most wins, fewest losses
- Team statistical records (PPG, defensive rating)

**Career Records (with team)**:
- All-time leading scorer
- All-time leader in each category
- Games played with franchise

**Championship History**:
- List of all franchise championships
- Finals appearances
- Finals MVPs from your franchise

#### Hall of Fame

**Eligibility**:
- 5 years after retirement
- Both players and coaches eligible

**Simulated Voting**:
- AI voters evaluate career achievements
- Consider: stats, awards, championships, longevity
- Great players usually get in
- Some randomness = occasional snubs or surprises
- First-ballot vs multiple attempts

**Player HoF Factors**:
- Career stats vs era averages
- MVP/awards
- All-NBA selections
- Championships and Finals MVPs
- Longevity

**Coach HoF Factors**:
- Championships won
- Career wins
- COY awards
- Playoff success rate

### 13. SAVE SYSTEM ✅ COMPLETE

#### Save Slots

**Manual Saves**: 3 slots
- Player-triggered saves
- Named by date/team/situation
- Overwrite any slot

**Auto-Saves**: 3 rotating slots
- Triggers after every game
- Rotates through 3 slots (newest overwrites oldest)
- Safety net for crashes/mistakes

**Total**: 6 save points available at any time

#### Ironman Mode

**Enabled at Career Start**:
- Optional toggle when creating new career
- Cannot be changed mid-career
- Clearly labeled in save file

**Restrictions**:
- Single auto-save only (no manual saves)
- Auto-save after every game
- Previous saves overwritten (cannot reload)
- Decisions are permanent

**Rewards**:
- Special Ironman badge on career
- Achievements only available in Ironman
- Bragging rights for completing seasons/championships

### 14. FORMER PLAYER CAREERS ✅ COMPLETE

**Implementation Files**:
- `Core/Data/FormerPlayerCoach.cs` - Coaching career data
- `Core/Data/FormerPlayerGM.cs` - Front office career data
- `Core/Manager/FormerPlayerCareerManager.cs` - Coaching pipeline management

#### Coaching Pipeline
- Players retire and enter coaching: Assistant Coach → Position Coach → Coordinator → Head Coach
- Former player bonuses when coaching (name recognition, player connections)
- User coaching history tracking (players you coached who became coaches)
- Matchup notifications when facing former players

### 15. UNIFIED CAREER SYSTEM ✅ COMPLETE

**Implementation Files**:
- `Core/Data/UnifiedCareerProfile.cs` - Central career data structure
- `Core/Data/CareerTransitionRequirements.cs` - Transition rules and eligibility
- `Core/Data/NonPlayerRetirementData.cs` - Non-player retirement factors
- `Core/Manager/UnifiedCareerManager.cs` - Central manager for cross-track careers

#### Career Tracks & Transitions

**Two Career Tracks**:
- **Coaching Track**: Assistant Coach → Position Coach → Coordinator → Head Coach
- **Front Office Track**: Scout → Assistant GM → General Manager

**Cross-Track Transitions** (with requirements):
| From | To | Min Years | Min Rep | Performance |
|------|-----|-----------|---------|-------------|
| Head Coach | General Manager | 3 | 65 | 2+ playoffs, 50%+ wins |
| General Manager | Head Coach | 2 | 60 | 1+ playoff |
| Scout | Assistant Coach | 2 | 40 | Open position |
| Coordinator | Assistant GM | 2 | 50 | Open position |
| Assistant GM | Coordinator | 2 | 50 | Open position |

**Rules**:
- One role only - must resign to switch tracks
- Both experience AND performance requirements for track switches
- Unified profile tracks career across both tracks

#### Non-Player Retirement System

**Retirement Triggers**:
- **Age-based**: Evaluation starts at 55, probability increases through 68
- **Forced retirement**: 3 years unemployed OR age 70+
- **Voluntary**: Based on weighted factors (age, success, health, wealth)

**Retirement Factors** (weighted):
- Age factor: 35% weight
- Unemployment factor: 30% weight
- Recent success factor: 20% weight (reduces retirement chance)
- Health factor: 10% weight (random)
- Wealth factor: 5% weight (long career = can afford to retire)

**Retirement Announcements**:
- News-style headlines generated automatically
- Career summary statement
- Track achievements across both coaching and FO roles

#### Integration Points

**FormerPlayerCareerManager Integration**:
- Creates UnifiedCareerProfile when coaching progression starts
- Calls UnifiedCareerManager.ProcessEndOfSeason()

**GMJobSecurityManager Integration**:
- Creates UnifiedCareerProfile when FO progression starts
- Notifies UnifiedCareerManager on GM firing/hiring

**RetirementManager Integration**:
- New `EvaluateNonPlayerRetirement()` method
- `ProcessAllNonPlayerRetirements()` for end-of-season batch processing
- `OnNonPlayerRetirementAnnounced` event for news integration

### 16. ARCHITECTURE CONSOLIDATION ✅ COMPLETE

**Implementation Files**:
- `Core/Manager/StaffManagementManager.cs` - Central facade for all staff operations
- `UI/GameSceneController.cs` - Panel registry and navigation
- `UI/Components/AttributeDisplayFactory.cs` - Shared UI component factory
- `UI/BasePanel.cs` - Base panel class with Show/Hide lifecycle

#### Manager Layer: Facade Pattern

**Problem Solved**: Multiple staff managers (CoachingStaffManager, ScoutingManager, StaffHiringManager) had overlapping responsibilities and duplicate free agent pools.

**Solution**: StaffManagementManager acts as the single entry point (facade):

```
StaffManagementManager (Facade - Singleton)
├── CoachingStaffManager (coach data storage, quality calculations)
├── ScoutingManager (scout data, assignments, reports)
└── StaffHiringManager (free agent pools, negotiations)
```

**Key Design Decisions**:
- Fired staff routes through StaffHiringManager (single free agent pool)
- StaffManagementManager wraps and initializes other managers
- UI panels call facade methods: `GetCoachingStaff()`, `FireCoach()`, etc.
- Save/load aggregates data from all wrapped managers

#### UI Layer: Panel Registry Pattern

**Problem Solved**: Duplicate navigation code, individual Show* methods for each panel, fragmented navigation.

**Solution**: GameSceneController maintains panel registry:

```csharp
private Dictionary<string, GameObject> _panelRegistry;
private Dictionary<string, BasePanel> _panelComponents;

public void ShowPanel(string panelId)
{
    HideAllPanels();
    _panelRegistry[panelId].SetActive(true);
    _panelComponents[panelId]?.Show();
    RefreshPanel(panelId);
}
```

**Benefits**:
- Unified `ShowPanel(panelId)` navigation
- BasePanel.Show()/Hide() called automatically
- Panel-specific refresh logic centralized
- Legacy Show* methods retained for compatibility

#### Shared UI Components: AttributeDisplayFactory

**Problem Solved**: Duplicate code in StaffPanel, StaffHiringPanel for rating colors and attribute rows.

**Solution**: Static factory class with shared methods:

```csharp
public static class AttributeDisplayFactory
{
    public static Color GetRatingColor(int rating);
    public static GameObject CreateAttributeRow(Transform parent, string name, int value);
    public static void PopulateAttributeContainer(Transform container, Dictionary<string, int> attrs);
    public static void ApplyRatingColor(Text text, int rating);
}
```

**Color Thresholds**:
- Elite (85+): Green
- Good (75-84): Blue
- Average (65-74): Yellow
- Below Average (<65): Gray

#### Save/Load Integration

**Staff System Save Data**:
```csharp
[Serializable]
public class StaffManagementSaveData
{
    public CoachingStaffSaveData CoachingData;
    public ScoutingSaveData ScoutingData;
}
```

**Personality System Save Data**:
```csharp
[Serializable]
public class PersonalitySystemSaveData
{
    public List<PlayerPersonalitySaveState> PlayerPersonalities;
    public Dictionary<string, float> TeamChemistry;
}
```

#### Files Modified/Created

| File | Change |
|------|--------|
| `StaffManagementManager.cs` | Facade implementation with wrapped managers |
| `CoachingStaffManager.cs` | Save/load support, removed internal pool |
| `ScoutingManager.cs` | Save/load support |
| `StaffHiringManager.cs` | Centralized fired staff handling |
| `PersonalityManager.cs` | Save/load integration |
| `GameSceneController.cs` | Panel registry pattern |
| `BasePanel.cs` | Removed UIManager dependency |
| `StaffPanel.cs` | Uses facade + AttributeDisplayFactory |
| `StaffHiringPanel.cs` | Uses AttributeDisplayFactory |
| `TabGroup.cs` | Direct BasePanel.Show() calls |
| `AttributeDisplayFactory.cs` | NEW - Shared UI factory |

**Deleted**: `UIManager.cs` (consolidated into GameSceneController)

#### Auto-Save Behavior

**Standard Mode**:
- Auto-saves after every game completes
- Rotates through 3 auto-save slots
- Can still manual save anytime

**Ironman Mode**:
- Auto-saves after every game
- Only 1 save slot (overwrites itself)
- No manual saves allowed

#### Career Management

**Careers Are Separate**:
- Each career is independent
- No cross-career tracking or legacy view
- Start fresh each time

**Load Game Screen**:
- See all save files
- Shows: Team, Season, Record, Date
- Sort by date modified
- Delete unwanted saves

---

## GAME FLOW ✅ COMPLETE

### Main Menu

**Visual Style**: Animated background with NBA imagery (team logos, highlights)

**Options**:
- **New Game** → Career creation flow
- **Continue** → Resume most recent save
- **Load Game** → Save slot selection
- **Settings** → Options menu
- **Quit** → Exit game

### New Career Flow

**Step 1: Coach Background** (Story Intro)
- Brief narrative setup
- Your coaching history/background
- How you got this opportunity
- Adds context to your career

**Step 2: Team Selection**
- Scrollable list of all 30 teams
- Hover/select shows preview:
  - Roster summary (key players, overall rating)
  - Cap space and financial situation
  - Draft picks owned
  - Owner type (Lavish/Cheap/etc.)
  - Difficulty indicator (contender/rebuilding/etc.)

**Step 3: Settings**
- Difficulty options
- Ironman mode toggle
- Simulation preferences

**Step 4: Start Career** → Enter game

### Main Game Navigation

**Tab Bar (Horizontal, Top of Screen)**:
```
[ Dashboard | Roster | Calendar | Standings | Trade | Free Agency* | Draft* | Staff | Inbox ]
```
*Contextual tabs that appear during relevant phases

### Time Progression

**Primary Action**: "Advance to Next Game" button
- Skips to next game day automatically
- Days in between auto-process:
  - Injury healing/recovery
  - Fatigue recovery
  - Contract negotiations progress
  - Other teams make moves

**Calendar Access**: Full schedule available for reference

### Season Flow (Linear Progression)

```
Regular Season (82 games)
    ↓
Play-In Tournament (if applicable)
    ↓
Playoffs (4 rounds)
    ↓
Awards Ceremony
    ↓
Draft
    ↓
Free Agency
    ↓
Offseason (Summer League → Training Camp → Preseason)
    ↓
Next Season
```

### Year-End Summary Screens

**Season Recap**:
- Your team's W-L record
- Playoff result (or miss)
- Key statistics
- Highlight moments

**Player Development Summary**:
- Who improved (green arrows)
- Who declined (red arrows)
- Biggest jumps/drops

**League Summary**:
- Champion and Finals MVP
- Award winners (MVP, DPOY, etc.)
- All-NBA teams

**Goals Review**:
- Pre-season owner expectations
- Your actual results
- Job security impact
- Owner feedback

---

## UI PANELS ✅ COMPLETE

### Visual Style
- **Dark theme** with team accent colors
- Modern, clean interface
- Easy on eyes for long sessions

### Main Navigation Tabs

#### Dashboard Panel (`Panels/DashboardPanel.cs`)
Home base showing at-a-glance information.

**Contents**:
- Next game info (opponent, date, location)
  - Quick buttons: "Play Game" / "Sim Game"
- Team record and standings snapshot
  - Current W-L, conference rank, games back
- Recent results (last 5 games)
  - Win/loss indicators with scores
- Alerts/notifications preview
  - Injuries, morale issues, trade offers
  - Badge counts for attention items

**Primary Action**: Advance to Next Game button

#### Roster Panel (`Panels/RosterPanel.cs`)
Manage your team's players.

**Tab Views**:
- **List View**: Sortable table of all players
- **Depth Chart**: Position-based organization
- **Stats View**: Season statistics focus
- **Contracts**: Financial information

**List Columns**:
- Name, Position, Age
- Contract info (salary, years remaining)
- Morale indicator (emoji)
- Injury status (if any)

**Detailed Player View** (click player):
- Season stats (current + career averages)
- Full contract details (years, salary, options)
- Morale and personality information
- Injury history

**Lineup Editor**:
- Drag and drop players to positions
- Visual court diagram with 5 starter slots
- Bench listed below
- Position eligibility shown

#### Calendar Panel (`Panels/CalendarPanel.cs`)
Season schedule view.

**Format**: Scrollable list
- Date
- Opponent (with logo)
- Home/Away indicator
- Result (if played): W/L with score
- Upcoming games highlighted

**Key Dates Marked**:
- Trade deadline
- All-Star break
- Playoff start
- Draft date

#### Standings Panel (`Panels/StandingsPanel.cs`)
League standings.

**Format**: Conference standings
- Eastern Conference (1-15)
- Western Conference (1-15)
- Playoff line clearly marked (top 10)
- Play-in zone highlighted (7-10)

**Columns**:
- Rank, Team, W-L, PCT, GB
- Home/Away record
- Last 10 games
- Streak

#### Trade Panel (`Panels/TradePanel.cs`)
Trade center for deals.

**Two Sections**:

**Trade Finder** (default view):
- AI-suggested trade opportunities
- Filter by: players you want, players to trade
- Click suggestion to open in builder

**Trade Builder**:
- Side-by-side layout
- Your team (left) | Their team (right)
- Add/remove assets to each side
- See trade info:
  - Salary matching indicator (legal/illegal)
  - AI acceptance likelihood (%)
  - Trade value comparison
  - Cap impact preview

#### Free Agency Panel (`Panels/FreeAgencyPanel.cs`)
*Available during free agency period*

**Format**: Filterable player list
- Sort by: Position, Rating, Age, Salary demand
- Filter by position, price range

**Player Row Shows**:
- Name, Position, Age, Rating
- Asking salary
- Interest level (how many teams pursuing)

**Actions**: Click to view details, make offer

#### Draft Panel (`Panels/DraftPanel.cs`)
*Available during draft period*

**Split View Layout**:
- Left: Available prospects (remaining)
- Right: Your draft board (rankings)

**Prospect Info**:
- Name, Position, School/Country
- Consensus rank vs your rank
- Scouting report summary

**During Draft**:
- Live picks displayed as they happen
- Your pick highlighted when on clock
- Trade button for draft-day deals

#### Staff Panel (`Panels/StaffPanel.cs`)
Manage coaching and scouting staff.

**Current Staff Section**:
- List of your staff members
- Role, Rating, Salary
- Impact description

**Available Hires Section**:
- Pool of available candidates
- Filter by role (coach, scout, trainer)
- Click to view and hire

#### Inbox Panel (`Panels/InboxPanel.cs`)
All messages and notifications.

**Message Types**:
- 📨 Trade offers from other teams
- 🏥 Injury updates
- 👔 Owner communications
- 😤 Player morale alerts
- 📰 League news

**Badge System**:
- Relevant tabs show badge counts
- Trade tab badge for incoming offers
- Roster tab badge for injuries
- Inbox for everything else

### Game Flow Panels

#### Pre-Game Panel (`Panels/PreGamePanel.cs`)
Prepare for upcoming game.

**Sections**:
- **Starting Lineup**: Set your 5 starters (drag-drop)
- **Strategy Settings**: Pace, offensive/defensive focus
- **Opponent Scouting Report**:
  - Key players and their strengths
  - Team tendencies
  - Recent form (last 5 games)
- **Injury Report**: Both teams' injury status

**Actions**:
- "Play Game" → Enter match simulator
- "Sim Game" → Instant simulation

#### Post-Game Panel (`Panels/PostGamePanel.cs`)
Results after game completion.

**Contents**:
- Final score (prominently displayed)
- Quarter-by-quarter breakdown
- Player of the game highlight
- Full box score (both teams)
- Key moments/headlines
- Press conference (if triggered)

**Actions**:
- "Continue" → Return to dashboard
- "View Box Score" → Detailed stats

#### Match Panel (`Panels/MatchPanel.cs`)
The core gameplay experience (see Match Simulator section).

### Additional Panels

| Panel | Purpose |
|-------|---------|
| NewGamePanel | Career creation wizard |
| TeamSelectionPanel | Choose your team |
| SettingsPanel | Game options and preferences |
| StaffPanel | Coaching and scouting staff |
| AwardsPanel | End-of-season awards display |
| HistoryPanel | Records, Hall of Fame, past seasons |
| PlayoffBracketPanel | Tournament bracket visualization |
| PressConferencePanel | Media questions interface |
| SummerLeaguePanel | Summer League management |
| TrainingCampPanel | Roster cuts and playbook |
| OwnerMeetingPanel | Owner interactions and pitches |

### Settings Menu

**Accessed via**: Pause menu (Escape key)

**Categories**:
- **Simulation**: Default game speed, auto-advance preferences
- **Notifications**: Toggle which alerts appear
- **Audio**: Volume levels, sound effects
- **Visual**: Graphics quality, resolution
- **Gameplay**: Difficulty sliders, realism options

### Save/Load

**Quick Save**: Hotkey (F5 or similar) for instant save
**Load**: Access through pause menu or main menu
**Auto-Save**: After every game (configurable)

---

## CRITICAL FILES FOR IMPLEMENTATION

These 5 files are touched by almost every feature:

1. **GameManager.cs** - State machine, manager registration, events
2. **Player.cs** - Core data model, needs injury/morale fields
3. **SaveData.cs** - Must accommodate all new data
4. **MatchFlowController.cs** - Match experience hub
5. **GameSimulator.cs** - Where gameplay happens

---

## IMPLEMENTATION PHASES (Updated Priority)

> Based on audit: ✅ = Done, 🔶 = Enhance, ❌ = Build

### Phase 1: Core Gameplay Completion ✅ COMPLETE
1. ✅ **Playoff System** - Play-in tournament + bracket (PlayoffManager, PlayoffData, PlayoffBracketPanel)
2. ✅ **Injury System** - Full InjuryManager with load management, risk modifiers (InjuryManager, InjuryData)
3. ✅ **Match Visualization** - ESPN Gamecast UI, play-by-play, speed controls (MatchSimulationController, MatchPanel, CourtDiagramView, CoachingMenuView, PlayByPlayGenerator)

### Phase 2: Season Experience Enhancement ✅ COMPLETE
1. ✅ **Awards System** - Full voting for MVP, DPOY, ROY, 6MOY, MIP, COY, Finals MVP, All-Teams (AwardManager enhanced)
2. ✅ **History & Records** - Season archives, franchise records, all-time leaders, Hall of Fame (HistoryManager)
3. ✅ **Offseason Content** - Training Camp with roster cuts, playbook installation, preseason games (TrainingCampManager)

### Phase 3: Personality & Dynamics ✅ COMPLETE
1. ✅ **Morale & Chemistry** - Full gameplay impact, locker room events, chemistry modifiers (MoraleChemistryManager)
2. ✅ **Media System** - Press conferences with consequences, headlines, coach persona tracking (MediaManager)
3. ✅ ~~Job Security~~ (Complete)

### Phase 4: Advanced Features ✅ COMPLETE
1. ✅ **AI Adaptation** - Pattern detection, counter-strategies, coaching insights (AIAdaptationSystem)
2. ✅ **Financial System** - Dynamic revenue model, game income, seasonal projections (RevenueManager)
3. ✅ **Scouting** - Advance scouting reports, scout development system (AdvanceScoutingManager)
4. ✅ **Ironman Mode** - Single auto-save, save-on-load deletion, no save-scumming (SaveLoadManager enhanced)

---

## TECHNICAL PATTERNS

### Manager Pattern
```csharp
public class NewManager : MonoBehaviour
{
    public static NewManager Instance { get; private set; }

    void Awake()
    {
        if (Instance == null) {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        } else Destroy(gameObject);
    }
}
```

### Event-Driven Communication
```csharp
// Managers fire events
public event Action<InjuryEvent> OnPlayerInjured;

// UI subscribes
InjuryManager.Instance.OnPlayerInjured += HandleInjury;
```

### Save/Load Pattern
```csharp
public ManagerState CreateSaveState() { ... }
public void RestoreFromSave(ManagerState state) { ... }
```

---

## PLAYER DATA & CAREER STATS SYSTEM

### Data Architecture

**Separation of Concerns:**
- **Base Data** (`players.json`): Biographical info + attributes only, never modified during gameplay
- **Runtime State** (Player objects): Modified during gameplay (energy, morale, injuries, stats)
- **Save State** (PlayerSaveState): Captures runtime changes + full career history

### Game Log System

**GameLog Class** (`Core/Data/GameLog.cs`):
- Individual game performance records
- Tracks: date, opponent, home/away, all box score stats, game result
- Only kept for **current season** - wiped at season end to keep saves small

**Retention Policy:**
- Current season: Full game logs available
- Past seasons: Only `SeasonStats` totals/averages persist
- Career: `List<SeasonStats>` with one entry per season played

### Career Stats Persistence

**What gets saved:**
- Full `List<SeasonStats>` for career history
- Current season includes `List<GameLog>` (cleared at season end)
- All totals and computed averages per season

**Save/Load Flow:**
- On Save: `PlayerSaveState.CreateFrom(player)` captures full career
- On Load: Apply `PlayerSaveState` including career history to base players
- On New Game: Fresh from `players.json` + mods, no career history

### Generated Players (Rookies)

**Integration:**
- Generated rookies become regular `Player` objects in same database
- `PlayerDatabase.RegisterGeneratedPlayer()` handles ID collision prevention
- Career stats start fresh from draft year
- Survive save/load cycles identically to base roster players

---

## PLAYER CARD UI SPECIFICATION

> **Key Design Principle**: NO ATTRIBUTE NUMBERS are shown anywhere. Players evaluate talent through statistics (box scores) and text-based scouting reports only.

### Layout: Split View
```
+---------------------------+--------------------------------+
|      LEFT SIDE            |         RIGHT SIDE             |
|      (Bio/Overview)       |     (Stats + Scouting Tabs)    |
+---------------------------+--------------------------------+
```

### Left Side - Bio & Overview

**Bio Section:**
- Player photo/avatar placeholder
- Name, Position, Jersey #
- Age, Height, Weight
- Team name + logo
- Years in league, Nationality
- Draft info (Year, Round, Pick, Team)

**Contract Section:**
- Current salary
- Years remaining
- Contract type (Standard, Rookie, Vet Min, etc.)
- Agent name

**Status Section:**
- Current injury status (if injured: type + expected return)
- Role: Starter / Rotation / Bench / Out of Rotation
- Depth chart position (e.g., "PG #2")

**Team Context Section:**
- Morale indicator (emoji + label: Happy/Content/Unhappy)
- Playing time trend (↑ increasing / → stable / ↓ decreasing)

**Scouting Summary:**
- 2-3 line text summary from scouting report
- "View Full Report" button → opens full scouting report

### Right Side - Tabs

**Tab 1: Current Season Stats**
```
Season Summary Row:
GP/GS | MPG | PPG | RPG | APG | SPG | BPG | TPG
FG% | 3P% | FT% | TS% | USG%
```

**Tab 2: Career Stats**
```
Career Totals Row (summary):
GP | PPG | RPG | APG | SPG | BPG | FG% | 3P%

Season-by-Season Table:
| Year | Team | GP | PPG | RPG | APG | FG% |
Click row → expands to show ALL stats for that season
```

**Tab 3: Game Log**
```
Recent Games (last 5-10):
| Date  | vs  | MIN | PTS | REB | AST | Result |
"View All Games" button → expands to full season list
```

**Tab 4: Scouting Report** (TEXT ONLY)
```
Full text-based scouting report:
- Strengths (prose description)
- Weaknesses (prose description)
- Player comparison
- Projection/ceiling
- Character/work ethic notes

NO numerical ratings - only descriptive text
```

### Action Buttons
- **Set Role** → Starter / Bench dropdown
- **Development Focus** → Opens focus assignment (text-based feedback)
- **Minutes Limit** → Set max minutes per game
- **DNP-Rest** → Toggle rest for next game
- **Request Trade** → Initiates trade finder for this player

---

## TESTING STRATEGY

- **Unit Tests**: Injury rates, award voting, chemistry formulas
- **Integration Tests**: Full season cycle, save/load at all states
- **Playtesting**: 1 full season, all offseason phases, match controls

---

*Last Updated: December 19, 2025*
*Version: Design Document v1.8 (Architecture Consolidation - Facade Pattern, Panel Registry, Shared UI Components)*

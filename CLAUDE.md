# NBA Head Coach — AI Assistant Guide

> **Last Updated**: March 2026

## Game Overview

Single-player NBA franchise management simulation in Unity 6. Play as GM, Head Coach, or Both. Manage roster, develop players, coach games through 82-game seasons + playoffs.

**Critical Design Rule**: Player attributes are NEVER shown as numbers. Players evaluate talent through statistics, scouting reports, and game observation only.

## Architecture

### Runtime UI System (Programmatic)
The game builds UI at runtime — no prefabs or scene wiring. Three auto-attached MonoBehaviours on GameManager handle this:

| Component | File | Purpose |
|-----------|------|---------|
| **GameShell** | `UI/Shell/GameShell.cs` | FM-style layout: header, sidebar, content area. Routes panels via `IGamePanel` registry. |
| **ArtInjector** | `UI/ArtInjector.cs` | Thin registrar — registers all GamePanels on Start(). ~45 lines. |
| **MenuInjector** | `UI/MenuInjector.cs` | Main menu + 5-step new game wizard + load game UI. |

### Panel System
Each game screen implements `IGamePanel.Build(RectTransform, Team, Color)`:

| Panel | File | Content |
|-------|------|---------|
| Dashboard | `UI/GamePanels/DashboardGamePanel.cs` | Card grid: next game, season, roster, finances, schedule |
| Roster | `UI/GamePanels/RosterGamePanel.cs` | Player table + click-to-detail with tabs (Overview/Stats/Scouting/Contract) |
| Schedule | `UI/GamePanels/ScheduleGamePanel.cs` | Split view: game list + detail panel |
| Standings | `UI/GamePanels/StandingsGamePanel.cs` | East/West conference tables |
| Inbox | `UI/GamePanels/InboxGamePanel.cs` | Contextual messages |
| Staff | `UI/GamePanels/StaffGamePanel.cs` | Coaching staff list |
| PreGame | `UI/GamePanels/PreGameGamePanel.cs` | Game day matchup, Simulate/Play buttons |
| PostGame | `UI/GamePanels/PostGameGamePanel.cs` | Tabbed box score (both teams), team totals, shooting % |
| SaveGame | `UI/GamePanels/SaveLoadGamePanel.cs` | FM-style save/load with slot management |

Register new panels: `_shell.RegisterPanel("id", new MyPanel());` in ArtInjector.Start()

### Shared UI Helpers
`UI/Shell/UIBuilder.cs` — static methods used by all panels:
- `UIBuilder.Child(parent, name)` — create RectTransform child
- `UIBuilder.Stretch(go)` — fill parent
- `UIBuilder.Text(parent, name, content, fontSize, style, color)` — styled text
- `UIBuilder.Card(parent, title, accentColor)` — FM-style card with header
- `UIBuilder.ScrollArea(parent)` — scrollable VLG content
- `UIBuilder.TableRow(parent, height, bgColor)` — table row with HLG
- `UIBuilder.TableCell(row, content, width, style, color)` — table cell
- `UIBuilder.FillCard(text)` — position text inside card body
- `UIBuilder.ApplyGradient(go, top, bottom)` — vertical gradient via UIGradient component
- `UIBuilder.ApplyShadow(go)` — drop shadow for depth
- `UIBuilder.ApplyOutline(go, color, dist)` — outline border

### Theme
`UI/UITheme.cs` — static colors and layout constants:
- Background: `#05050A`, Panels: `#141823`, Cards: `#191C26`
- Gold accent: `#FFD700`, Blue accent: `#60A5FA`
- White text, Slate-300 secondary text
- FM layout: 80px header, 160px sidebar, 32px card headers

## Key Files

### Core
| File | Lines | Purpose |
|------|-------|---------|
| `Core/GameManager.cs` | ~1050 | Singleton orchestrator. States, events, scene loading. Auto-attaches UI components. |
| `Core/SeasonController.cs` | ~735 | Schedule, calendar, standings, season phases. `GetTodaysGame()`, `GetNextGame()`. |
| `Core/MatchFlowController.cs` | ~705 | Pre-game, match, post-game flow. |
| `Core/SaveLoadManager.cs` | ~440 | JSON save/load to `.nbahc` files. Auto-save rotation. |

### Data
| File | Lines | Purpose |
|------|-------|---------|
| `Core/Data/Player.cs` | ~1100 | Player entity. 31 hidden attributes, stats, contract, injury. `Age` computed from `BirthDate`. |
| `Core/Data/Team.cs` | ~200 | Team identity, roster, strategy. |
| `Core/Data/Contract.cs` | ~400 | CBA-compliant contracts with options. |
| `Core/Data/SaveData.cs` | ~1070 | Complete save file structure. |
| `Core/Data/PlayerDatabase.cs` | ~500 | Loads from `StreamingAssets/Data/players.json`. Preprocesses Position enums, BirthDate strings, and Attributes block (nested JSON → flat Player fields). |

### Simulation
| File | Lines | Purpose |
|------|-------|---------|
| `Core/Simulation/GameSimulator.cs` | ~865 | Full game sim. `SimulateGame(home, away)` returns `GameResult` with `BoxScore`. |
| `Core/Simulation/PossessionSimulator.cs` | ~894 | Individual possession resolution. |
| `Core/Simulation/FoulSystem.cs` | ~480 | Team fouls, bonus, all foul types. |

### Data Files
- **Players**: `Assets/StreamingAssets/Data/players.json` — real NBA rosters (Dec 2025)
- **Teams**: `Assets/StreamingAssets/Data/teams.json` — 30 NBA teams
- **GM Profiles**: `Assets/Resources/Data/initial_front_offices.json`
- **Draft Picks**: `Assets/Resources/Data/initial_draft_picks.json`
- **Art**: `Assets/Resources/Art/Teams/logo_*.png` (30 team logos, transparent backgrounds)
- **Saves**: `Application.persistentDataPath/Saves/*.nbahc`

## Game Flow

```
Boot → MainMenu → [New Game Wizard | Load Game] → Game Scene
                                                      |
                                            GameShell builds FM layout
                                            ArtInjector registers panels
                                                      |
                                            Dashboard <-> Squad <-> Schedule <-> etc.
                                                      |
                                            Advance Day → check for game day
                                                      | (game day)
                                            PreGame screen → Simulate or Play
                                                      |
                                            PostGame box score → Continue → Dashboard
```

## Key APIs

```csharp
// Game state
GameManager.Instance.CurrentState          // GameState enum
GameManager.Instance.CurrentDate           // DateTime
GameManager.Instance.GetPlayerTeam()       // Team
GameManager.Instance.PlayerTeamId          // string
GameManager.Instance.AdvanceDay()          // advance calendar

// Season
SeasonController.GetTodaysGame()           // CalendarEvent or null
SeasonController.GetNextGame()             // CalendarEvent or null
SeasonController.GetUpcomingGames(5)       // List<CalendarEvent>
SeasonController.GetConferenceStandings()  // standings data
SeasonController.RecordGameResult(event, homeScore, awayScore)

// Simulation
var sim = new GameSimulator(PlayerDatabase);
var result = sim.SimulateGame(homeTeam, awayTeam);  // GameResult

// Players
PlayerDatabase.GetPlayer(id)               // Player
player.Age, player.HeightFormatted, player.WeightLbs
player.CurrentSeasonStats?.PPG/RPG/APG
player.CurrentContract                     // Contract or null
player.Energy, player.Morale, player.Form  // 0-100 dynamic state
```

## Patterns & Conventions

### Namespaces
- `NBAHeadCoach.Core` / `.Data` / `.Manager` / `.Simulation` / `.AI`
- `NBAHeadCoach.UI` / `.Shell` / `.GamePanels` / `.Panels` / `.Modals` / `.Components`

### Adding a New Game Panel
1. Create `UI/GamePanels/MyPanel.cs` implementing `IGamePanel`
2. Use `UIBuilder.*` for all UI construction
3. Register in `ArtInjector.Start()`: `_shell.RegisterPanel("MyPanel", new MyPanel());`
4. Add nav item in `GameShell.BuildSidebar()` if needed

### JSON Deserialization Gotchas
- `JsonUtility` can't parse ISO date strings to `DateTime` — `PlayerDatabase` strips BirthDate from JSON and parses separately
- `JsonUtility` can't parse string enums — Position strings ("PG","SG") are preprocessed to ints before deserialization
- `JsonUtility` can't map nested JSON objects to flat fields — `Attributes` block extracted via regex, mapped manually to Player fields
- After removing JSON blocks, clean trailing commas: `json = Regex.Replace(json, @",\s*}", "}")`
- Save restoration preserves BirthDates from database load in `GameManager.RestoreFromSaveData()`

### New Game Initialization Flow
1. `MenuInjector.StartGame()` → `GameManager.StartNewGame()`
2. Sets `_currentDate` to Oct 1, creates career profile
3. `SeasonController.InitializeSeason()` — generates full 30-team league schedule (~1000+ games)
4. `InitializeManagersForNewGame()` — trade systems, draft, development
5. `InitializeAllTeamCoaches()` — generates random `AICoachPersonality` for each team, sets strategy + starting lineup
6. Each team gets `AutoSetStartingLineup(coach)` — position-based + coach-style swaps
7. Each team gets `AutoSetStrategy(coach)` — maps coach traits to `TeamStrategy`
8. On `AdvanceDay()`, `SimulateLeagueGamesForDate()` auto-sims all non-player games

### Game Day Flow
1. "Sim to Game" advances days until next player game → shows PreGame → **disables sidebar buttons**
2. User clicks "Simulate" or "Play" → game simulated → PostGame shown → **buttons still disabled**
3. "Continue" advances 1 day past game day → Dashboard → **buttons re-enabled**
4. `GetNextGame()`/`GetTodaysGame()` filter by `PlayerTeamId` (league schedule has all 30 teams)

### Known Quirks
- `Energy`/`Morale`/`Form` default to 0 — initialized to 100/75/50 in `PlayerDatabase.LoadPlayersFromFile()`
- Legacy panel classes in `UI/Panels/` exist but are NOT used — the runtime `GamePanels/` system replaces them
- `Tools/UIBuilder.cs` is a separate older utility — not the same as `Shell/UIBuilder.cs`
- UIGradient is multiplicative — set `Image.color = Color.white` before applying, or use direct colors for dark elements

## Manager Systems (~45 managers in Core/Manager/)
Personnel, Roster, Contracts, Trade/AI, Season/Playoffs, Development, Practice, Mentorship, Morale/Chemistry, Finance, Injury, Media, Awards, Draft, Scouting, Job Market, Offseason, Summer League, Training Camp.

## Implemented Features
- 5-step new game wizard (coach creation, team selection, role, difficulty, confirmation)
- Save/Load with FM-style save management panel (create, overwrite, delete)
- FM-style dashboard with live date/record updates, frosted glass cards, team-color gradients
- Full roster with player detail (bio, stats, scouting, contract tabs)
- Schedule view filtered to player's team with W/L results for completed games
- Conference standings with playoff line — all 30 teams update via league-wide simulation
- Full game simulation with tabbed box score (both teams), team totals, shooting percentages
- Advance Day with league-wide game simulation for all 30 teams
- Sim to Game: advances to next game day, disables sidebar, shows PreGame → PostGame → Continue flow
- 30 AI coaches with random personalities (pace, 3PT emphasis, defensive aggression, rotation depth, etc.)
- Each team gets auto-generated starting lineup based on coach style + player talent
- 30 AI-generated team logos with transparent backgrounds
- Visual effects: gradients, shadows, outlines, panel transitions, frosted glass cards
- 4000+ unit tests

## Test Suite
`Assets/Scripts/Tests/` — 25 test files. Run via `TestRunner.cs` attached to any GameObject.

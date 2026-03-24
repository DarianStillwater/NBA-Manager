# NBA Head Coach — Project Outline

> **Last Updated**: March 2026

## Overview
Single-player NBA franchise management simulation built in Unity 6 (C#). ~80,000 lines of code across ~180 files.

## Project Structure
```
Assets/Scripts/
├── Core/
│   ├── GameManager.cs           — Central orchestrator (singleton, persists across scenes)
│   ├── SeasonController.cs      — Season flow, calendar, standings
│   ├── MatchFlowController.cs   — Pre-game → match → post-game flow
│   ├── MatchSimulationController.cs — Match orchestration
│   ├── SaveLoadManager.cs       — Persistence (.nbahc JSON files)
│   ├── GameState.cs             — State enums
│   ├── PlayByPlayGenerator.cs   — Broadcast-style text generation
│   ├── AI/                      — 12 files: coach personalities, trade AI, coordinators
│   ├── Data/                    — 52 files: Player, Team, Contract, SaveData, etc.
│   ├── Gameplay/                — GameCoach.cs (1,425 lines)
│   ├── Manager/                 — 45 domain managers (~32K lines total)
│   ├── Simulation/              — 11 files: game/possession sim, fouls, violations
│   └── Util/                    — NameGenerator, ArtManager
├── UI/
│   ├── Shell/                   — Runtime UI framework
│   │   ├── GameShell.cs         — FM-style layout (header, sidebar, content routing)
│   │   └── UIBuilder.cs         — Shared static UI construction helpers
│   ├── GamePanels/              — Individual game screen panels (IGamePanel)
│   │   ├── DashboardGamePanel.cs
│   │   ├── RosterGamePanel.cs   — Includes player detail with 4 tabs
│   │   ├── ScheduleGamePanel.cs
│   │   ├── StandingsGamePanel.cs
│   │   ├── InboxGamePanel.cs
│   │   ├── StaffGamePanel.cs
│   │   ├── IGamePanel.cs        — Interface
│   │   └── LegacyPanelAdapter.cs
│   ├── ArtInjector.cs           — Panel registration + PreGame/PostGame
│   ├── MenuInjector.cs          — Runtime main menu + new game wizard
│   ├── GameSceneController.cs   — Legacy panel management
│   ├── UITheme.cs               — Colors, fonts, layout constants
│   ├── BasePanel.cs             — Base class for legacy panels
│   ├── Panels/                  — 18 legacy panel classes (NOT actively used)
│   ├── Modals/                  — 9 modal dialogs
│   ├── Components/              — 22 reusable UI components
│   └── Match/                   — Match-specific UI overlays
├── View/                        — Camera, player visuals (5 files)
├── Tools/                       — Scene setup utilities (3 files)
└── Tests/                       — 25 test files (~4,000 tests)
```

## Scenes
| Scene | Purpose |
|-------|---------|
| Boot | Load data, initialize GameManager |
| MainMenu | New game wizard, load game, quit |
| Game | Main dashboard/management hub (FM-style) |
| Match | Interactive match experience |

## Core Systems

### Game State Flow
`Booting → MainMenu → NewGame/Loading → Playing ↔ PreGame ↔ Match ↔ PostGame`

### Dual-Role System
- **GM Only**: Roster control, AI coach runs games
- **Head Coach Only**: Game control, submit roster requests to AI GM
- **Both** (default): Full control

### Simulation Engine
- Possession-by-possession with fouls, free throws, violations
- Energy/fatigue, player rotation
- AI timeouts, matchup evaluation
- Box score generation with full stat lines

### Manager Systems (45 managers)
Personnel, Roster, Contracts, Salary Cap, Free Agency, Trade (with AI evaluation), Draft, Awards, Playoffs, All-Star, History, Development, Practice, Mentorship, Morale/Chemistry, Finance, Revenue, Injury, Media, Scouting, Job Market, Offseason, Summer League, Training Camp, and more.

### Trade AI
- `PlayerValueCalculator`: Stats-based evaluation (production, potential, contract, age curves)
- `AITradeOfferGenerator`: Proactive AI trade offers (~1-2/week)
- `DraftPickRegistry`: Central pick ownership tracking with Stepien Rule
- Real GM profiles with competence ratings and trade tendencies

### Player Data
- 31 hidden attributes (never shown to user)
- Real NBA rosters (Dec 2025) loaded from JSON
- BirthDate-based age calculation
- Contract system with options, clauses, bird rights
- Morale, energy, form as dynamic state
- Scouting reports (text-based, no numbers)

## Data Files
| File | Content |
|------|---------|
| `StreamingAssets/Data/players.json` | All NBA players with attributes |
| `StreamingAssets/Data/teams.json` | 30 NBA teams |
| `Resources/Data/initial_front_offices.json` | 30 GM profiles |
| `Resources/Data/initial_draft_picks.json` | Traded picks through 2031 |
| `Resources/Art/Teams/logo_*.png` | 30 team logos (transparent bg) |

## UI Architecture
Programmatic FM-style dark theme. No prefabs — all UI built at runtime via `UIBuilder` static helpers. `GameShell` manages layout, panels implement `IGamePanel` interface and are registered on startup. `MenuInjector` handles the main menu independently.

## Implementation Status
- New game wizard (5 steps) with coach/team/role/difficulty selection
- Save/Load with auto-save and Ironman mode
- FM-style dashboard with 6 navigable panels
- Player detail screen with Overview/Stats/Scouting/Contract tabs
- Game simulation with pre-game screen and post-game box scores
- Advance Day / Sim to Game functionality
- 30 AI-generated team logos
- 4,000+ unit tests
- Full NBA rules (fouls, free throws, violations, timeouts)
- Deep coaching: practice, tendencies, mentorship, staff meetings
- Morale/chemistry with captain system and escalation
- Former player career paths (coaching, scouting, GM)
- Job market system after firing

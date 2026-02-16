# Development Guide

## Prerequisites

### Required Software
- **Unity Editor**: Version `6000.3.0f1` (Unity 6)
  - Download from [Unity Hub](https://unity.com/download) or [Unity Archive](https://unity.com/releases/editor/archive)
  - Install with Windows Build Support and Visual Studio integration
- **IDE** (choose one):
  - Visual Studio 2022 (recommended for Unity C# development)
  - Visual Studio Code with C# extension
  - JetBrains Rider
- **Git**: For version control

### System Requirements
- **OS**: Windows 10 Pro (10.0.19045) or later
- **Platform**: Windows 64-bit
- **.NET**: Included with Unity installation

## Getting Started

### 1. Clone the Repository
```bash
git clone <repository-url>
cd "NBA Head Coach"
```

### 2. Open Project in Unity
1. Launch Unity Hub
2. Click "Add" → "Add project from disk"
3. Navigate to `d:/NBA Head Coach`
4. Select the project folder
5. Click "Open" to load the project

**First-time setup**: Unity will import all assets and compile scripts. This may take 5-10 minutes.

### 3. Project Structure
```
NBA Head Coach/
├── Assets/
│   ├── Scenes/               # Unity scenes (game screens)
│   ├── Scripts/              # C# codebase (191 files)
│   │   ├── Core/
│   │   │   ├── AI/          # AI personality & decision systems
│   │   │   ├── Data/        # Data models (51 files, ~24,881 LOC)
│   │   │   └── Managers/    # Domain managers (45 files)
│   │   ├── Simulation/      # Game simulation logic
│   │   ├── Gameplay/        # Gameplay systems
│   │   ├── UI/              # UI controllers and panels
│   │   └── Util/            # Utility classes
│   ├── Prefabs/             # Reusable Unity prefabs
│   └── Resources/           # Runtime-loadable assets
├── ProjectSettings/         # Unity project configuration
├── Packages/                # Unity package dependencies
└── docs/                    # Project documentation

```

### 4. Key Entry Points

**Boot Flow** ([Boot.cs](../Assets/Scripts/Boot.cs)):
- `Boot.cs` → Initializes all Singleton managers
- `MainMenuController.cs` → Loads main menu UI
- `GameBootstrap.cs` → Starts new game or loads save

**Match Flow**:
- `GameCoach.cs` → Main game loop controller
- `PlayByPlaySimulator.cs` → In-game simulation engine
- `GameResultsController.cs` → Post-game processing

**Save/Load**:
- `SaveManager.cs` → Handles save file I/O
- `SaveData.cs` → Serializable save structure
- Save location: `%APPDATA%/../LocalLow/<Company>/<Product>/saves/`

## Development Workflow

### Opening Scenes
1. Navigate to `Assets/Scenes/` in the Project window
2. Double-click a scene to open it (e.g., `MainMenu.scene`, `GameCoach.scene`)

### Editing Scripts
1. Double-click any `.cs` file in the Project window to open in your IDE
2. Unity auto-recompiles on save
3. Check Console window for compilation errors

### Running the Game
1. Open the starting scene (typically `Boot.scene` or `MainMenu.scene`)
2. Press **Play** button (▶) in Unity Editor
3. Game runs in the Editor's Game view
4. Press **Play** again to stop

### Debugging
- **Unity Console**: View logs, warnings, errors (Window → General → Console)
- **Visual Studio Debugger**:
  1. Set breakpoints in your IDE
  2. In Unity: Edit → Preferences → External Tools → check "Attach Unity Debugger"
  3. In VS: Debug → Attach to Unity
- **Debug.Log()**: Add logging statements in C# code
  ```csharp
  Debug.Log("Player name: " + player.Name);
  Debug.LogWarning("Low morale detected");
  Debug.LogError("Save file corrupted");
  ```

## Building the Game

### Build for Windows
1. **File** → **Build Settings**
2. Select **Platform**: PC, Mac & Linux Standalone
3. **Target Platform**: Windows
4. **Architecture**: x86_64
5. Click **Build** or **Build and Run**
6. Choose output directory (e.g., `Builds/Windows/`)

Unity generates:
- `NBA Head Coach.exe` - Main executable
- `NBA Head Coach_Data/` - Game data folder
- `UnityPlayer.dll` - Unity runtime

### Build Configurations
- **Development Build**: Enables profiler and debug features (for testing)
- **Release Build**: Optimized for distribution (uncheck "Development Build")

**Recommended settings**:
- Enable **"Create Visual Studio Solution"** for debugging standalone builds
- Set **"Compression Method"** to LZ4 for faster loading

## Architecture Overview

### Design Patterns
- **Singleton Managers**: Domain-Driven Design with centralized managers
  - `GameStateManager`, `RosterManager`, `TradeManager`, etc.
  - Accessible via static instances: `RosterManager.Instance.GetPlayer(id)`
- **Event-Driven**: Managers communicate via events
  ```csharp
  public static event Action<Player> OnPlayerTradedEvent;
  ```
- **No Visible Attributes**: Player ratings hidden from UI, only stats shown

### Key Systems
1. **AI Personality System** ([ai-systems.md](./ai-systems.md))
   - Opponent coach behavior simulation
   - Trade evaluation and GM decision-making

2. **Data Models** ([data-models.md](./data-models.md))
   - 51 data model classes (~24,881 LOC)
   - Player, contracts, schedules, playbooks

3. **Manager Systems** ([manager-systems.md](./manager-systems.md))
   - 45 domain managers
   - Personnel, trading, development, morale, simulation

## Testing

### Manual Testing
- **Play Mode Testing**: Run game in Unity Editor, verify functionality
- **Build Testing**: Test standalone builds on target platform
- **Save/Load Testing**: Verify save file integrity and loading

### No Automated Test Framework
This project does not currently use Unity Test Framework or NUnit. All testing is manual through Play Mode.

**To add testing** (optional):
1. Window → General → Test Runner
2. Create test assembly in `Assets/Tests/`
3. Write tests using Unity Test Framework

## Common Development Tasks

### Adding a New Manager
1. Create class in `Assets/Scripts/Core/Managers/`
2. Inherit from `MonoBehaviour` or create Singleton pattern
3. Initialize in `Boot.cs` or relevant bootstrap
4. Add events for cross-system communication

### Adding a New Data Model
1. Create class in `Assets/Scripts/Core/Data/`
2. Mark with `[System.Serializable]` for Unity serialization
3. Add to `SaveData.cs` if needs persistence

### Creating a New UI Panel
1. Create prefab in `Assets/Prefabs/UI/`
2. Create controller script in `Assets/Scripts/UI/`
3. Attach script to prefab
4. Register with `UIManager` or relevant controller

### Modifying Game Logic
1. Locate relevant manager in `Assets/Scripts/Core/Managers/`
2. Update logic in manager methods
3. Test in Play Mode
4. Verify no breaking changes to save file compatibility

## Code Style Conventions

### Naming Conventions
- **Classes**: PascalCase (`PlayerManager`, `GameCoach`)
- **Methods**: PascalCase (`GetPlayer()`, `ProcessTrade()`)
- **Private Fields**: camelCase (`currentPlayer`, `teamRoster`)
- **Public Properties**: PascalCase (`TeamName`, `CurrentSeason`)
- **Constants**: UPPER_SNAKE_CASE (`MAX_ROSTER_SIZE`, `SALARY_CAP`)

### File Organization
- One class per file
- Filename matches class name
- Organize by domain (AI, Data, Managers, etc.)

### Unity-Specific
- Use `[SerializeField]` for private fields editable in Inspector
- Prefer `GetComponent<T>()` over `FindObjectOfType<T>()`
- Cache component references in `Awake()` or `Start()`

## Performance Considerations

- **Object Pooling**: Reuse game objects instead of Instantiate/Destroy
- **Avoid FindObjectOfType**: Cache references to managers
- **String Concatenation**: Use `StringBuilder` for frequent string operations
- **Update Loop**: Minimize work in `Update()`, use events when possible

## Troubleshooting

### Common Issues

**"Assembly has reference to non-existent assembly"**
- Solution: Check Package Manager → verify all dependencies installed

**"Scripts have compiler errors"**
- Solution: Check Console window, fix syntax/reference errors

**"Scene not found in build"**
- Solution: File → Build Settings → Add scene to "Scenes in Build"

**Save file not loading**
- Solution: Check save file path in `SaveManager.cs`
- Verify `SaveData.cs` structure matches save file version

**Unity crashes on Play**
- Solution: Check for infinite loops in `Awake()`/`Start()`
- Verify no null references in initialization code

## Additional Resources

- [Unity Documentation](https://docs.unity3d.com/)
- [Unity Scripting API](https://docs.unity3d.com/ScriptReference/)
- [Unity Manual - Unity 6](https://docs.unity3d.com/6000.0/Documentation/Manual/)
- [C# Programming Guide](https://docs.microsoft.com/en-us/dotnet/csharp/)

## Project-Specific Documentation

- [Project Overview](./project-overview.md) - High-level summary
- [Architecture](./architecture.md) - Detailed architecture documentation
- [Data Models](./data-models.md) - Complete data model reference
- [AI Systems](./ai-systems.md) - AI personality and decision-making
- [Manager Systems](./manager-systems.md) - Domain manager details
- [Source Tree Analysis](./source-tree-analysis.md) - Annotated directory structure

---

**Generated**: 2026-02-16
**Unity Version**: 6000.3.0f1
**Platform**: Windows

# NBA Head Coach - Project Documentation Index

**Documentation Generated**: 2026-02-16
**Project Type**: Unity Game (Monolith)
**Unity Version**: 6000.3.0f1
**Primary Language**: C#
**Architecture Pattern**: Domain-Driven Design with Singleton Managers + Event-Driven Communication

---

## Project Overview

**NBA Head Coach** is a comprehensive NBA franchise management simulation game where players take on dual roles (GM + Head Coach or Coach-only) to manage every aspect of an NBA franchise including roster construction, trade negotiation, game coaching, practice planning, player development, and morale management.

**Project Scale**:
- 191 C# scripts
- ~24,881 LOC in data models alone
- 45 domain managers
- 12 AI systems
- 51 data model classes

**Key Features**:
- Dual-role system (GM + Head Coach, or Coach-only with AI GM)
- Real-time game coaching with tactical play-calling
- Comprehensive AI personality systems for opponent simulation
- Stats-based trade evaluation system
- Mentorship and morale systems with chemistry tracking
- No visible attributes design philosophy

---

## Quick Reference

### Technology Stack
| Category | Technology | Version |
|----------|-----------|---------|
| **Game Engine** | Unity | 6000.3.0f1 |
| **Language** | C# | .NET Standard 2.1 |
| **UI Framework** | Unity UI (UGUI) | 2.0.0 |
| **Platform** | Windows | 10+ |

### Architecture Pattern
- **Design**: Domain-Driven Design with Singleton managers
- **Communication**: Event-driven architecture
- **UI Pattern**: MVC-inspired (View controllers + Data models)
- **Persistence**: JSON serialization via Unity JsonUtility

### Critical Entry Points
- **Boot Flow**: [Boot.cs](../Assets/Scripts/Boot.cs) → Initializes all Singleton managers
- **Match Flow**: [GameCoach.cs](../Assets/Scripts/Core/Managers/GameCoach.cs) → Main game loop
- **Save/Load**: [SaveManager.cs](../Assets/Scripts/Core/Managers/SaveManager.cs) → Handles persistence

---

## Generated Documentation

### Core Documentation
- **[Project Overview](./project-overview.md)** - High-level project summary, features, and technology overview
- **[Architecture](./architecture.md)** - Comprehensive architecture documentation covering system design, patterns, and integration
- **[Source Tree Analysis](./source-tree-analysis.md)** - Complete annotated directory structure with critical paths

### System Documentation
- **[AI Systems](./ai-systems.md)** - AI personality systems, decision-making algorithms, and opponent simulation
- **[Data Models](./data-models.md)** - Complete data model reference (51 files, ~24,881 LOC)
- **[Manager Systems](./manager-systems.md)** - All 45 domain managers organized by category

### Development Documentation
- **[Development Guide](./development-guide.md)** - Setup instructions, workflow, debugging, and best practices

---

## Existing Documentation

- **[ProjectOutline.md](../ProjectOutline.md)** (57 KB) - Comprehensive original design document with complete game features, systems architecture, and implementation details
  - Last updated: December 2025
  - Covers: NBA Rules System, Initial Data (Dec 2025), Captain System, Trade AI

---

## Getting Started

### For New Developers
1. **Read First**: [Project Overview](./project-overview.md) - Understand the game concept and scope
2. **Setup Environment**: [Development Guide](./development-guide.md) → Prerequisites section
3. **Understand Architecture**: [Architecture](./architecture.md) → System Architecture section
4. **Navigate Codebase**: [Source Tree Analysis](./source-tree-analysis.md) → Find key files

### For Exploring Specific Systems
- **Understanding AI**: [AI Systems](./ai-systems.md) → AICoachPersonality, AITradeEvaluator
- **Data Structures**: [Data Models](./data-models.md) → Player, Contract, PlayBook, etc.
- **Business Logic**: [Manager Systems](./manager-systems.md) → Find relevant manager by domain
- **Game Architecture**: [Architecture](./architecture.md) → Component Overview

### For Building/Running
1. Open Unity Hub
2. Add project: `d:/NBA Head Coach`
3. Open with Unity 6000.3.0f1
4. Open scene: `Assets/Scenes/Boot.scene` or `MainMenu.scene`
5. Press **Play** (▶) to run

For detailed instructions: [Development Guide](./development-guide.md) → Development Workflow

---

## Documentation Map

### By Development Phase

**Planning & Design**:
- [Project Overview](./project-overview.md) - What we're building
- [Architecture](./architecture.md) - How we're building it

**Implementation**:
- [Development Guide](./development-guide.md) - Development workflow and best practices
- [Source Tree Analysis](./source-tree-analysis.md) - Where to find things
- [Data Models](./data-models.md) - Data structure reference
- [Manager Systems](./manager-systems.md) - Business logic reference
- [AI Systems](./ai-systems.md) - AI behavior reference

**Maintenance & Enhancement**:
- [Architecture](./architecture.md) → Component Interaction Examples
- [Development Guide](./development-guide.md) → Common Development Tasks
- [Manager Systems](./manager-systems.md) → Manager Communication Patterns

### By Role

**Game Designer**:
- [ProjectOutline.md](../ProjectOutline.md) - Original design vision
- [Project Overview](./project-overview.md) - Current features
- [AI Systems](./ai-systems.md) - AI personality and behavior tuning

**Programmer**:
- [Architecture](./architecture.md) - System architecture and patterns
- [Data Models](./data-models.md) - Data structure reference
- [Manager Systems](./manager-systems.md) - Business logic organization
- [Source Tree Analysis](./source-tree-analysis.md) - Code organization

**Technical Lead**:
- [Architecture](./architecture.md) - Complete architectural overview
- [Development Guide](./development-guide.md) - Development standards
- [Manager Systems](./manager-systems.md) - Domain boundaries

---

## Key Concepts & Patterns

### Design Philosophy
1. **No Visible Attributes**: Player ratings (50+ attributes) never shown to user - evaluation via stats and scouting only
2. **Singleton Managers**: Domain managers accessible globally via `ManagerName.Instance`
3. **Event-Driven**: Systems communicate via C# events to maintain loose coupling
4. **Separation of Concerns**: Data models, managers, controllers, and AI systems clearly separated

### Critical Systems
- **Personnel Management** (11 managers): Rosters, contracts, coaching staff, scouting
- **Trading System** (6 managers): Trade proposals, AI evaluation, negotiation, waivers
- **Development & Training** (8 managers): Practice, mentorship, playbook, skill development
- **Simulation** (7 managers): Game engine, opponent scouting, season progression, statistics
- **Morale & Chemistry** (5 managers): Player morale, team chemistry, captain influence, discontent

### Data Architecture
- **51 data model classes** (~24,881 LOC total)
- **Key models**: Player (1,185 LOC), UnifiedCareerProfile (1,161 LOC), SaveData (1,070 LOC), PlayBook (943 LOC)
- **Persistence**: JSON serialization to local save files
- **Ironman mode**: Prevents save scumming

### AI Systems
- **AICoachPersonality** (977 LOC): 60+ parameters simulating opponent coach behavior
- **AIGMController** (516 LOC): AI GM for coach-only mode with hidden personality traits
- **AITradeEvaluator** (538 LOC): Stats-based trade evaluation with front office modifiers

---

## Documentation Statistics

| Document | Size | Content |
|----------|------|---------|
| project-overview.md | ~200 lines | High-level project summary |
| architecture.md | ~400 lines | Comprehensive architecture documentation |
| source-tree-analysis.md | ~250 lines | Annotated directory structure |
| ai-systems.md | ~110 lines | AI systems documentation |
| data-models.md | ~484 lines | Complete data model reference |
| manager-systems.md | ~350 lines | Domain manager documentation |
| development-guide.md | ~350 lines | Development workflow and setup |
| **Total Generated** | **~2,144 lines** | **8 documentation files** |

---

## Additional Resources

### Unity Resources
- [Unity Documentation](https://docs.unity3d.com/)
- [Unity Scripting API](https://docs.unity3d.com/ScriptReference/)
- [Unity Manual - Unity 6](https://docs.unity3d.com/6000.0/Documentation/Manual/)

### C# Resources
- [C# Programming Guide](https://docs.microsoft.com/en-us/dotnet/csharp/)
- [.NET API Browser](https://docs.microsoft.com/en-us/dotnet/api/)

### Project-Specific
- Original design doc: [ProjectOutline.md](../ProjectOutline.md)
- Game design systems: [Architecture](./architecture.md) → System Architecture
- Data model catalog: [Data Models](./data-models.md)

---

## Documentation Maintenance

### Updating Documentation
When making significant changes to the codebase, update relevant documentation:
- **New Manager**: Update [Manager Systems](./manager-systems.md)
- **New Data Model**: Update [Data Models](./data-models.md)
- **New AI System**: Update [AI Systems](./ai-systems.md)
- **Architecture Change**: Update [Architecture](./architecture.md)
- **Build Process Change**: Update [Development Guide](./development-guide.md)

### Documentation Standards
- Use GitHub-flavored Markdown
- Include code examples where relevant
- Link to source files using relative paths: `[ClassName.cs](../Assets/Scripts/Path/ClassName.cs)`
- Reference specific lines: `[ClassName.cs:42](../Assets/Scripts/Path/ClassName.cs#L42)`
- Keep overview docs concise, create separate detailed docs for deep dives

---

## Quick Navigation

**Want to understand how the game works?**
→ [Project Overview](./project-overview.md) → [Architecture](./architecture.md)

**Want to add a new feature?**
→ [Architecture](./architecture.md) → [Manager Systems](./manager-systems.md) → [Development Guide](./development-guide.md)

**Want to understand existing code?**
→ [Source Tree Analysis](./source-tree-analysis.md) → [Data Models](./data-models.md) → [Manager Systems](./manager-systems.md)

**Want to modify AI behavior?**
→ [AI Systems](./ai-systems.md) → [Architecture](./architecture.md) → AI Systems Architecture section

**Want to set up development environment?**
→ [Development Guide](./development-guide.md) → Getting Started section

---

**This documentation was generated using the BMAD Game Dev Studio document-project workflow (exhaustive scan mode).**

For questions or issues, refer to the [Development Guide](./development-guide.md) or review the [Architecture](./architecture.md) documentation.

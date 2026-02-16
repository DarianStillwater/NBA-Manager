# Data Models Documentation

## Overview

The game contains 51 data model classes in `Assets/Scripts/Core/Data/`, totaling ~24,881 lines of code.

## Core Entity Models

### Player.cs (~1,185 lines)
**Location**: `Assets/Scripts/Core/Data/Player.cs`

Complete player data model with attributes, career tracking, and personality.

**Key Components**:
- **Attributes**: 50+ basketball attributes (never shown to user per design philosophy)
- **Career Tracking**: Statistics, awards, career history
- **Contract**: Current contract details
- **Personality**: 13 personality traits affecting morale and chemistry
- **Captain Status**: `IsCaptain` flag for team captain designation
- **Tendencies**: Innate and coachable behavioral tendencies
- **Mentor Profile**: Mentoring capabilities for veteran players
- **Energy/Fatigue**: In-game stamina tracking

**Design Philosophy**: All numeric attributes are hidden from UI. Users evaluate players through stats, scouting reports, and observation only.

### UnifiedCareerProfile.cs (~1,161 lines)
**Location**: `Assets/Scripts/Core/Data/UnifiedCareerProfile.cs`

Central profile for all non-player personnel (coaches, scouts, GMs).

**Personnel Types**:
- Head Coach
- Offensive Coordinator
- Defensive Coordinator
- Position Coaches (Point Guard, Wing, Big)
- Scouts (Regional, National, Director)
- Front Office (Assistant GM, GM)

**Dual Career Tracking**:
- Primary track (current role)
- Secondary track (previous/alternate career path)
- Cross-track transition requirements

**Former Player Integration**:
- Playing career statistics
- Transition from player → coach/scout/GM
- Position-specific coaching bonuses

### SaveData.cs (~1,070 lines)
**Location**: `Assets/Scripts/Core/Data/SaveData.cs`

Complete save file structure with Ironman mode support.

**Save Structure**:
- User configuration (name, role, team, difficulty)
- Season state (calendar, standings, schedule)
- All team rosters and contracts
- Player development tracking
- Manager states (trade history, scouting, financials)
- Ironman flag (prevents save scumming)

## Gameplay Data Models

### PlayBook.cs (~943 lines)
**Location**: `Assets/Scripts/Core/Data/PlayBook.cs`

Team playbooks with 15-20 plays, familiarity system, practice integration.

**Features**:
- Play catalog (15-20 per team)
- Familiarity tracking (players learn plays over time)
- Practice integration (plays installed through practice)
- Effectiveness tracking (hot/cold plays)
- Set play definitions with 20+ action types

### SetPlay.cs (~794 lines)
**Location**: `Assets/Scripts/Core/Data/SetPlay.cs`

Individual play definitions with detailed action sequences.

**Action Types** (20+):
- Ball screens, off-ball screens
- Cuts (backdoor, curl, straight)
- Post-ups, isolations
- Spot-ups, handoffs
- Motion offense principles

### PlayerGameInstructions.cs (~1,051 lines)
**Location**: `Assets/Scripts/Core/Data/PlayerGameInstructions.cs`

Per-player game focus and tactical instructions.

**Instruction Categories**:
- Offensive focus (shoot more/less, pass first, attack rim, etc.)
- Defensive assignment (lock down star, help defense, etc.)
- Usage rate adjustments
- Shot selection preferences

### TeamStrategy.cs (~677 lines)
**Location**: `Assets/Scripts/Core/Data/TeamStrategy.cs`

Team-wide tactical schemes.

**Offensive Schemes** (11):
- Motion, Isolation, PickAndRoll, PostUp, FastBreak, ThreeHeavy, Princeton, Triangle, etc.

**Defensive Schemes** (11):
- ManToMan, Zone23, Zone32, SwitchAll, Pack, HalfCourtTrap, FullCourtPress, etc.

**Pace Settings**:
- Slow, Normal, Push, FastBreak

## Practice & Development

### PracticeDrill.cs (~839 lines)
**Location**: `Assets/Scripts/Core/Data/PracticeDrill.cs`

Individual drill types for practice sessions.

**Drill Categories**:
- Shooting drills (spot-up, off-dribble, catch-shoot)
- Defense drills (closeouts, help rotation, 1-on-1)
- Team drills (scrimmage, plays, transition)
- Conditioning drills (cardio, strength)
- Film study sessions

**Drill Effects**:
- Attribute development bonuses
- Fatigue costs
- Familiarity gains (for play installation)

### PracticeSession.cs (referenced)
**Location**: `Assets/Scripts/Core/Data/PracticeSession.cs`

Practice sessions with focus areas, drills, intensity.

**Session Structure**:
- Date and duration
- Focus area (offense, defense, conditioning, plays)
- Drill selection
- Intensity level (light, moderate, intense)
- Fatigue impact on player development

### WeeklySchedule.cs (referenced)
**Location**: `Assets/Scripts/Core/Data/WeeklySchedule.cs`

Game days, practice days, off days, back-to-back handling.

## Mentorship System

### MentorProfile.cs (~570 lines)
**Location**: `Assets/Scripts/Core/Data/MentorProfile.cs`

Mentor capabilities and teaching effectiveness.

**Mentor Attributes**:
- Teaching ability (0-100)
- Patience level
- Specialties (shooting, defense, basketball IQ)
- Communication style

### MentorshipRelationship.cs (~564 lines)
**Location**: `Assets/Scripts/Core/Data/MentorshipRelationship.cs`

Mentor-mentee pairings with strength and compatibility.

**Relationship Tracking**:
- Mentor/mentee IDs
- Relationship strength (0-100)
- Compatibility factors
- Milestone tracking
- Development bonus (up to 30%)
- Organic formation tracking

## Opponent Analysis

### OpponentTendencyProfile.cs (~597 lines)
**Location**: `Assets/Scripts/Core/Data/OpponentTendencyProfile.cs`

Comprehensive opponent coach analysis.

**Tendency Categories**:
- Offensive tendencies (pace, play types, shot distribution)
- Defensive tendencies (schemes, trapping, help defense)
- Coach personality traits (predictability, aggression)
- Adjustment patterns (when losing, winning, halftime)
- Clutch tendencies (timeout usage, late-game play calls)

**Data Quality Tracking**:
- Observation count
- Data freshness
- Confidence level (0-100)

### PlayerTendencies.cs (~560 lines)
**Location**: `Assets/Scripts/Core/Data/PlayerTendencies.cs`

Innate and coachable behavioral tendencies.

**Innate Tendencies** (hard to change):
- Shot selection preferences
- Defensive gambling
- Help defense willingness

**Coachable Tendencies** (trainable):
- Ball movement
- Closeout discipline
- Transition defense effort

## Contracts & Financials

### Contract.cs (referenced)
**Location**: `Assets/Scripts/Core/Data/Contract.cs`

CBA-compliant contracts with options.

**Contract Features**:
- Multi-year structure
- Salary progression
- Options (player option, team option, early termination)
- Trade clauses (no-trade, partial no-trade)
- Incentives and bonuses

### Agent.cs (~669 lines)
**Location**: `Assets/Scripts/Core/Data/Agent.cs`

Player agents with negotiation styles and client relationships.

**Agent Traits**:
- Negotiation aggressiveness
- Relationship with front office
- Client loyalty
- Market knowledge

### LeagueCBA.cs (referenced)
**Location**: `Assets/Scripts/Core/Data/LeagueCBA.cs`

Salary cap rules and CBA compliance.

**CBA Rules**:
- Salary cap levels
- Luxury tax thresholds (first apron, second apron)
- Maximum contract rules
- Rookie scale contracts
- Mid-level exception, bi-annual exception
- Sign-and-trade rules

### TeamFinances.cs (~675 lines)
**Location**: `Assets/Scripts/Core/Data/TeamFinances.cs`

Revenue, expenses, budgets.

**Financial Tracking**:
- Game-by-game revenue (tickets, concessions, TV)
- Operating expenses
- Player salaries
- Luxury tax payments
- Owner budget limits

## Season & Competition

### SeasonCalendar.cs (~627 lines)
**Location**: `Assets/Scripts/Core/Data/SeasonCalendar.cs`

Schedule, game dates, season phases.

**Season Phases**:
- TrainingCamp
- Preseason
- RegularSeason
- PlayIn (play-in tournament)
- Playoffs
- Draft
- FreeAgency
- Offseason

**Schedule Management**:
- 82-game schedule generation
- Back-to-back detection
- Rest days
- All-Star break

### PlayoffData.cs (~799 lines)
**Location**: `Assets/Scripts/Core/Data/PlayoffData.cs`

Playoff bracket and series tracking.

**Playoff Structure**:
- Play-in tournament (7-10 seeds)
- 8-team bracket per conference
- Best-of-7 series tracking
- Home court advantage
- Series momentum tracking

### GameLog.cs (referenced)
**Location**: `Assets/Scripts/Core/Data/GameLog.cs`

Game result logging with box scores.

**Logged Data**:
- Final score
- Player statistics
- Team statistics
- Key moments (runs, comebacks)
- Injury occurrences

## Former Player Career System

### FormerPlayerCoach.cs (referenced)
**Location**: `Assets/Scripts/Core/Data/FormerPlayerCoach.cs`

Former players entering coaching careers.

**Transition Requirements**:
- 5+ NBA seasons
- Leadership ≥60
- Basketball IQ factors

### FormerPlayerScout.cs (referenced)
**Location**: `Assets/Scripts/Core/Data/FormerPlayerScout.cs`

Former players entering scouting careers.

**Transition Requirements**:
- 3+ NBA seasons
- Basketball IQ ≥65

### FormerPlayerGM.cs (~759 lines)
**Location**: `Assets/Scripts/Core/Data/FormerPlayerGM.cs`

Former players entering front office careers.

**Transition Requirements**:
- 8+ NBA seasons
- Leadership ≥70
- GM-specific skill development

### FormerPlayerProgressionData.cs (referenced)
**Location**: `Assets/Scripts/Core/Data/FormerPlayerProgressionData.cs`

Progression tracking for former players in new roles.

### CareerTransitionRequirements.cs (referenced)
**Location**: `Assets/Scripts/Core/Data/CareerTransitionRequirements.cs`

Requirements for cross-track career transitions.

**Transitions Supported**:
- HC → GM (5+ years as HC, 60%+ win rate)
- GM → HC (3+ years as GM, playoff success)
- Scout → Coach (3+ years scouting, recommendation)
- Coordinator → AGM (4+ years as coordinator)

## Dual-Role System

### JobMarketData.cs (referenced)
**Location**: `Assets/Scripts/Core/Data/JobMarketData.cs`

Job market data structures for unemployment phase.

**Data Classes**:
- JobOpening
- JobApplication
- FiringDetails
- UnsolicitedOffer
- JobMarketState

## NBA Rules System

### FoulSystem (via enums)
**Foul Types**:
- Personal (shooting/non-shooting)
- Loose ball
- Offensive
- Technical
- Flagrant 1/2

### Free Throw Scenarios
**Scenarios**:
- Two shots (bonus foul, shooting foul on 2-PT)
- Three shots (shooting foul on 3-PT)
- One shot (and-one)
- Technical (1 shot, retain possession)
- Flagrant (2 shots, retain possession)

### Violation Types
**Violations**:
- Traveling
- Backcourt violation
- 3-second violation

## Initial Data System (Dec 2025)

### InitialFrontOfficeData.cs (referenced)
**Location**: `Assets/Scripts/Core/Data/InitialFrontOfficeData.cs`

Serialization classes for real-world GM profiles.

**GM Profile Data** (30 teams):
- Name, title, years in position
- Competence rating (Elite/Good/Average/Poor/Terrible)
- Skills (trade evaluation, negotiation, scouting: 0-100)
- Trade aggression and team situation
- Behavioral traits

### InitialDraftPickData.cs (referenced)
**Location**: `Assets/Scripts/Core/Data/InitialDraftPickData.cs`

Serialization classes for traded draft picks (38 picks, 14 swaps, 2025-2031).

### FrontOfficeProfile.cs (~598 lines)
**Location**: `Assets/Scripts/Core/Data/FrontOfficeProfile.cs`

Front office personality and competence tracking.

**Profile Components**:
- Trade evaluation skill (0-100)
- Negotiation skill (0-100)
- Scouting skill (0-100)
- Trade aggression (VeryPassive → Desperate)
- Team situation (Championship, Contending, Rebuilding, StuckInMiddle)
- Value preferences (draft picks, young players, veterans)
- Former player traits (if applicable)

## Morale & Chemistry

### Personality.cs (referenced)
**Location**: `Assets/Scripts/Core/Data/Personality.cs`

Player/staff personality traits affecting morale.

**Personality Traits** (13):
- Leadership, Work Ethic, Confidence
- Coachability, Loyalty, Competitive
- Media Savvy, Selfless, Clutch
- Sensitive, Volatile, Resilient, etc.

**Morale Tracking**:
- Current morale (0-100)
- Morale events (win/loss, playing time, contract)
- ContractSatisfaction tracking
- DiscontentLevel (5-step escalation)

## Staff Meetings

### StaffMeeting.cs (~725 lines)
**Location**: `Assets/Scripts/Core/Data/StaffMeeting.cs`

Pre-game and halftime staff meetings.

**Meeting Structure**:
- Meeting type (PreGame, Halftime)
- Attendees (HC, coordinators, position coaches)
- Staff contributions (suggestions, insights)
- Disagreements and consensus building
- Meeting outcome and game plan adjustments

## Autonomous Game Results

### AutonomousGameResult.cs (referenced)
**Location**: `Assets/Scripts/Core/Data/AutonomousGameResult.cs`

Game results when AI coach runs games in GM-only mode.

**Result Data**:
- Box scores
- Key moments
- Coach performance assessment
- Tactical decisions made by AI coach

## Summary

The data model layer is comprehensive, covering:
- **Core Entities**: Players, personnel, contracts
- **Gameplay**: Plays, strategies, instructions
- **Simulation**: Opponent analysis, tendencies
- **Career**: Former player transitions, job market
- **Development**: Practice, mentorship, progression
- **Competition**: Season structure, playoffs, awards
- **Morale**: Personality, chemistry, captain system
- **Financial**: Contracts, cap management, team budgets
- **AI**: Personalities, trade evaluation, autonomous decisions

Total: **51 data model files, ~24,881 lines of code**

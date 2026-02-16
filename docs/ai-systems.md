# AI Systems Documentation

## Overview

The NBA Head Coach game contains sophisticated AI systems for autonomous game coaching, GM decision-making, and trade evaluation.

## AI Personality Systems

### AICoachPersonality.cs
**Location**: `Assets/Scripts/Core/AI/AICoachPersonality.cs`
**Size**: ~977 lines

Comprehensive AI coach personality system with:
- **Offensive Philosophy**: 8 different styles (FastPaced, SlowPaced, StarHeavy, TeamOriented, ThreePointHeavy, InsideOut, PnRHeavy)
- **Defensive Philosophy**: 6 styles (Aggressive, Conservative, ZoneHeavy, SwitchHeavy, TrapHeavy)
- **Rotation Management**: Player trust, depth settings, matchup-based subbing
- **In-Game Adjustments**: Timeout decision-making, substitution logic, tactical changes
- **Clutch Behavior**: Risk-taking tendencies, play-calling preferences
- **Predictability System**: Pattern recognition for scouting opponent coaches

**Key Features**:
- Factory methods for creating archetypes (FastPaced, DefenseFirst, PlayerDevelopment, StarHeavy, Analytics, OldSchool)
- Generates `OpponentTendencyProfile` for pre-game preparation
- Adjustment pattern generation (when losing big, when winning big, halftime)
- Timeout intelligence with contextual triggers

### AIGMController.cs
**Location**: `Assets/Scripts/Core/AI/AIGMController.cs`
**Size**: ~516 lines

AI General Manager for Coach-Only mode with hidden personality traits that are discovered through interactions.

**Personality Traits** (Hidden):
- **Trade Tendencies**: TradeHappy, PatientBuilder, WinNowMentality
- **Financial**: CostConscious, SpendsToBuild
- **Player Handling**: ProtectsStar, LoyalToPlayers, TrustsVeterans
- **Draft**: ValuesDraftPicks, LongTermThinker
- **Coach Relationship**: TrustsCoach, HandsOn

**Request Processing**:
- Evaluates roster requests with personality-based approval chances
- Reveals personality traits through decision patterns
- Tracks approval history and relationship status
- Generates contextual approval/denial responses

**Request Types Handled**:
- TradePlayer
- SignFreeAgent
- WaivePlayer
- ExtendContract
- AcquireBigMan/Guard/Shooter/Defender/Veteran
- IncreaseBudget
- TradeForPick

### AITradeEvaluator.cs
**Location**: `Assets/Scripts/Core/AI/AITradeEvaluator.cs`
**Size**: ~538 lines

Stats-based trade evaluation system integrated with PlayerValueCalculator.

**Evaluation Components**:
- **Player Value**: Production, potential, contract efficiency (via PlayerValueCalculator)
- **Draft Pick Value**: Base values ($25 for 1st, $5 for 2nd), year discounts, protection impacts
- **Trade Availability**: Discount/premium based on player's availability status
- **Former Player GM Bonuses**: Position scouting expertise, former teammate preferences

**Front Office Modifiers**:
- Competence-based evaluation error (poor GMs misjudge value)
- Negotiation skill affects acceptance thresholds
- Situational modifiers (deadline desperation, cap pressure)

**Counter-Offer Generation**:
- Modifies proposals to reach acceptability
- Suggests alternative deals when far apart
- Context-aware messaging

**Acceptance Thresholds**:
- Aggression: VeryPassive (+5), Conservative (+2), Moderate (0), Aggressive (-2), Desperate (-5)
- Situation: Championship (-3), Rebuilding (+2), StuckInMiddle (-1)

## AI Systems Overview

| AI System | Purpose | Key Features |
|-----------|---------|--------------|
| AICoachPersonality | Opponent coach behavior simulation | 60+ personality parameters, pattern-based adjustments |
| AIGMController | Coach-only mode GM decisions | Hidden traits, request processing, discovery system |
| AITradeEvaluator | Trade proposal evaluation | Stats-based valuation, FO modifiers, counter-offers |
| AIAdaptationSystem | Real-time opponent adjustment tracking | _(File not yet analyzed)_ |
| AIPersonalityDiscovery | Trait learning through interactions | _(File not yet analyzed)_ |
| AutonomousGameSimulator | AI coach game simulation (GM-only mode) | _(File not yet analyzed)_ |
| CoachingAdvisor | In-game suggestions for user | _(File not yet analyzed)_ |
| CoordinatorAI | Offensive/defensive coordinator AI | _(File not yet analyzed)_ |
| MatchupEvaluator | Matchup quality assessment | _(File not yet analyzed)_ |
| OpponentAdjustmentPredictor | Predicts opponent coach moves | _(File not yet analyzed)_ |
| PlayerValueCalculator | Stats-based player valuation | _(File not yet analyzed)_ |
| StaffAIDecisionMaker | Staff decision-making AI | _(File not yet analyzed)_ |

## Integration Points

- **GameCoach**: Uses AICoachPersonality for opponent behavior
- **JobMarketManager**: Uses AIGMController when user is coach-only
- **TradeSystem**: Uses AITradeEvaluator for AI trade responses
- **RosterRequestPanel**: Interfaces with AIGMController for coach-only mode
- **GamePlanBuilder**: Uses OpponentTendencyProfile from AICoachPersonality

## Notes

This documentation covers initial analysis of the AI personality and decision-making systems. Further analysis of remaining AI files is pending.

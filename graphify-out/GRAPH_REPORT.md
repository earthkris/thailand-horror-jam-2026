# Graph Report - thailand-horror-jam-2026  (2026-11-02)

## Corpus Check
- 73 files · ~59,460 words
- Verdict: corpus is large enough that graph structure adds value.
- Unclassified: 1477 file(s) not represented in the graph (top: .meta 856, .prefab 203, .fbx 176)

## Summary
- 1025 nodes · 2766 edges · 37 communities (23 shown, 14 thin omitted)
- Extraction: 95% EXTRACTED · 5% INFERRED · 0% AMBIGUOUS · INFERRED: 142 edges (avg confidence: 0.85)
- Token cost: 0 input · 0 output

## Community Hubs (Navigation)
- God Scene & Death Loop
- Lighting & Visual FX
- Interactables & Dialogue Zones
- Combat Action Data
- Boss AI Behaviour
- Physical Dice Roller
- Items & Dice Formatting
- Cutscene Playback
- Grid & Tile System
- Boss Victory & Dialogue Data
- Camera & See-Through Walls
- Sprite Animation & Props
- Game State & Resets
- Combat Unit Stats
- Enemy AI Tactics
- Dialogue Manager & Triggers
- Tutorial Readme Editor
- Combat Start & End
- Combat Movement & Pathing
- Support Script Files
- Turn Order State
- Choice Menu & Action Picks
- Turn Panel HUD
- Enemy Encounters
- Enemy Chase
- Dialogue Box UI
- Dice Simulation & Statuses
- Fog of War
- Player Interactor Input
- Teams
- Enemy Attack Styles
- Menu Hover
- Game State Tester

## God Nodes (most connected - your core abstractions)
1. `CombatUnit` - 151 edges
2. `PlayerMovement` - 61 edges
3. `CombatTargeting` - 57 edges
4. `TurnManager` - 55 edges
5. `PauseMenu` - 50 edges
6. `BossAI` - 48 edges
7. `GridManager` - 43 edges
8. `GodScene` - 41 edges
9. `TutorialCombat` - 38 edges
10. `EnemyAI` - 36 edges

## Surprising Connections (you probably didn't know these)
- `BlessAura` --references--> `CombatUnit`  [EXTRACTED]
  Assets/Preuk/Scripts/BlessAura.cs → Assets/Preuk/Scripts/CombatUnit.cs
- `BossAI` --references--> `CombatManager`  [EXTRACTED]
  Assets/Preuk/Scripts/BossAI.cs → Assets/Preuk/Scripts/CombatManager.cs
- `BossAI` --references--> `CombatUnit`  [EXTRACTED]
  Assets/Preuk/Scripts/BossAI.cs → Assets/Preuk/Scripts/CombatUnit.cs
- `BossAI` --references--> `GridManager`  [EXTRACTED]
  Assets/Preuk/Scripts/BossAI.cs → Assets/Preuk/Scripts/GridManager.cs
- `BossAI` --references--> `TurnManager`  [EXTRACTED]
  Assets/Preuk/Scripts/BossAI.cs → Assets/Preuk/Scripts/TurnManager.cs

## Import Cycles
- None detected.

## Communities (37 total, 14 thin omitted)

### Community 0 - "God Scene & Death Loop"
Cohesion: 0.06
Nodes (7): GodScene, Instance, TakesOverDeath, ScreenFade, Quitting, TutorialCombat, BlocksInput

### Community 1 - "Lighting & Visual FX"
Cohesion: 0.06
Nodes (12): Aura, BlessAura, CarriedLight, FakeShadows, HitEffects, Cover, LoopScribble, SpriteFlipbook (+4 more)

### Community 2 - "Interactables & Dialogue Zones"
Cohesion: 0.06
Nodes (11): DialogueZone, Interactable, CanAct, Prompt, PathBlocker, PlayerMovement, CurrentTile, IsStepping (+3 more)

### Community 3 - "Combat Action Data"
Cohesion: 0.05
Nodes (35): ActionArea, AroundTarget, AroundUser, Single, ActionEffect, Buff, Damage, Heal (+27 more)

### Community 4 - "Boss AI Behaviour"
Cohesion: 0.08
Nodes (7): BossAI, ForcedFight, TongueOutTime, BossIntro, DiceTray, Instance, IsBusy

### Community 5 - "Physical Dice Roller"
Cohesion: 0.05
Nodes (15): DiceRoller, ClickToRoll, IsRolling, ResultText, DiceSide, DiceSides, DiceSidesEditor, CountdownTimer (+7 more)

### Community 6 - "Items & Dice Formatting"
Cohesion: 0.06
Nodes (17): DiceFormat, ItemData, DiceText, MaxAmount, MinAmount, RangeText, ItemEffect, Heal (+9 more)

### Community 7 - "Cutscene Playback"
Cohesion: 0.07
Nodes (5): VidPlayer, CutsceneSkip, GameToast, DeathScreen, IsShowing

### Community 8 - "Grid & Tile System"
Cohesion: 0.08
Nodes (10): GridManager, Height, IsBuilt, OriginTile, Width, Room, Bounds, Box (+2 more)

### Community 9 - "Boss Victory & Dialogue Data"
Cohesion: 0.07
Nodes (9): DialogueScriptableObject, Line, BossVictory, Follower, LoopStart, StartedFromLoop, TrapChest, HasArt (+1 more)

### Community 10 - "Camera & See-Through Walls"
Cohesion: 0.09
Nodes (5): CameraController, SeeThroughMaterials, Part, Wall, WallFader

### Community 11 - "Sprite Animation & Props"
Cohesion: 0.10
Nodes (12): PropSlot, Facing, Down, Left, Right, Up, SpriteAnimator, FacingDirection (+4 more)

### Community 12 - "Game State & Resets"
Cohesion: 0.07
Nodes (13): GameMode, Combat, Cutscene, Dialogue, Exploring, Menu, GameState, CanMove (+5 more)

### Community 14 - "Combat Unit Stats"
Cohesion: 0.10
Nodes (14): CombatUnit, AnchorTile, ArmorClass, BlessBonus, BlessDie, BonusAC, CenterOffset, CurrentHP (+6 more)

### Community 16 - "Dialogue Manager & Triggers"
Cohesion: 0.10
Nodes (7): dialogueManager, Instance, TriggerDialogue, HoverProb, Billboard, EditorOnly, LightReveal

### Community 17 - "Tutorial Readme Editor"
Cohesion: 0.13
Nodes (8): ReadmeEditor, BodyStyle, ButtonStyle, HeadingStyle, LinkStyle, TitleStyle, Readme, Section

### Community 19 - "Combat Start & End"
Cohesion: 0.16
Nodes (4): CombatManager, Enemies, InCombat, Party

### Community 22 - "Turn Order State"
Cohesion: 0.12
Nodes (11): TurnManager, Current, HasActed, HasMoved, InBattle, IsPartyTurn, MovesLeft, Order (+3 more)

### Community 23 - "Choice Menu & Action Picks"
Cohesion: 0.20
Nodes (3): ChoiceMenu, IsOpen, Option

### Community 25 - "Enemy Encounters"
Cohesion: 0.23
Nodes (3): EnemyEncounter, InCombat, Unit

### Community 27 - "Dialogue Box UI"
Cohesion: 0.29
Nodes (4): DialogueBox, Instance, IsContentOpen, IsTyping

### Community 32 - "Teams"
Cohesion: 0.40
Nodes (3): Team, Enemy, Party

### Community 33 - "Enemy Attack Styles"
Cohesion: 0.40
Nodes (4): AttackStyle, Both, Melee, Ranged

## Knowledge Gaps
- **134 isolated node(s):** `ForcedFight`, `TongueOutTime`, `IsOpen`, `Enemy`, `Ally` (+129 more)
  These have ≤1 connection - possible missing edges. (Counts symbols only; 250 node(s) total have ≤1 connection when file, concept and rationale nodes are included.)
- **14 thin communities (<3 nodes) omitted from report** — run `graphify query` to explore isolated nodes.

## Suggested Questions
_Questions this graph is uniquely positioned to answer:_

- **Why does `CombatUnit` connect `Combat Unit Stats` to `God Scene & Death Loop`, `Lighting & Visual FX`, `Combat Action Data`, `Boss AI Behaviour`, `Items & Dice Formatting`, `Cutscene Playback`, `Boss Victory & Dialogue Data`, `Camera & See-Through Walls`, `Sprite Animation & Props`, `Enemy AI Tactics`, `Dialogue Manager & Triggers`, `Boss Targeting Geometry`, `Combat Start & End`, `Combat Movement & Pathing`, `Turn Order State`, `Choice Menu & Action Picks`, `Turn Panel HUD`, `Enemy Encounters`, `Enemy Chase`, `Dice Simulation & Statuses`, `Fog of War`, `Turn Advance Loop`, `Teams`?**
  _High betweenness centrality (0.192) - this node is a cross-community bridge._
- **What connects `ForcedFight`, `TongueOutTime`, `IsOpen` to the rest of the system?**
  _134 weakly-connected nodes found - possible missing edges._
- **Should `God Scene & Death Loop` be split into smaller, more focused modules?**
  _Cohesion score 0.05664568678267309 - nodes in this community are weakly interconnected._
- **Why does `PauseMenu` connect `Items & Dice Formatting` to `God Scene & Death Loop`, `Lighting & Visual FX`, `Combat Action Data`, `Boss AI Behaviour`, `Cutscene Playback`, `Boss Victory & Dialogue Data`, `Game State & Resets`, `Core Script Files`, `Dialogue Manager & Triggers`, `Boss Targeting Geometry`, `Turn Order State`, `Turn Panel HUD`?**
  _High betweenness centrality (0.067) - this node is a cross-community bridge._
- **Should `Lighting & Visual FX` be split into smaller, more focused modules?**
  _Cohesion score 0.05563093622795115 - nodes in this community are weakly interconnected._
- **Why does `PlayerMovement` connect `Interactables & Dialogue Zones` to `Lighting & Visual FX`, `Boss AI Behaviour`, `Items & Dice Formatting`, `Grid & Tile System`, `Boss Victory & Dialogue Data`, `Camera & See-Through Walls`, `Core Script Files`, `Dialogue Manager & Triggers`, `Boss Targeting Geometry`, `Combat Start & End`, `Fog of War`, `Turn Advance Loop`?**
  _High betweenness centrality (0.067) - this node is a cross-community bridge._
- **Should `Interactables & Dialogue Zones` be split into smaller, more focused modules?**
  _Cohesion score 0.05920745920745921 - nodes in this community are weakly interconnected._
# Graph Report - FirstPersonV2  (2026-10-06)

## Corpus Check
- Corpus is ~28,019 words - fits in a single context window. You may not need a graph.

## Summary
- 551 nodes · 936 edges · 29 communities (25 shown, 4 thin omitted)
- Extraction: 96% EXTRACTED · 4% INFERRED · 0% AMBIGUOUS · INFERRED: 39 edges (avg confidence: 0.85)
- Token cost: 0 input · 0 output

## Community Hubs (Navigation)
- Statechart Core Engine
- Namespaces & Imports
- Grunt AI States
- Revolver Cylinder Rotation
- Design Docs & Rig Notes
- Player Movement States
- Viewmodel & Hitboxes
- Grunt Animation Driver
- Revolver Hip & Hammer States
- Clamber Controller Config
- Revolver Controller
- Walk/Crouch/Sprint States
- Player Controller
- Pause & FPS UI
- Parallel States
- Cylinder Open/Close States
- Revolver Aim State
- Revolver Idle State
- Camera Controller
- Reload Intro State
- Revolver Fire State
- Insert Bullet State
- Reload Interrupt State
- Turn Cylinder State
- Project Build Config
- Rig Controller
- Transition Docs Link

## God Nodes (most connected - your core abstractions)
1. `State` - 40 edges
2. `StateMachine` - 37 edges
3. `ClamberController` - 33 edges
4. `RevolverController` - 33 edges
5. `RevolverCylinderController` - 29 edges
6. `FirstPerson.StateMachines` - 26 edges
7. `PlayerController` - 23 edges
8. `AtomicState` - 21 edges
9. `GruntAnimator` - 18 edges
10. `AnimationTreeDriver` - 17 edges

## Surprising Connections (you probably didn't know these)
- `Revolver Cylinder Actions (RevolverCylinderOpen/Close, RevolverRigReload*)` --conceptually_related_to--> `Hierarchical State Machine (Statechart)`  [AMBIGUOUS]
  docs/blender-cylinder-rotation-bone.md → StateMachine/README.md
- `GruntState` --inherits--> `AtomicState`  [EXTRACTED]
  Grunt/States/GruntState.cs → StateMachine/AtomicState.cs
- `AimState` --references--> `RevolverController`  [EXTRACTED]
  PlayerRigStates/AimState.cs → RevolverController.cs
- `AimState` --references--> `RigController`  [EXTRACTED]
  PlayerRigStates/AimState.cs → RigController.cs
- `AimState` --inherits--> `AtomicState`  [EXTRACTED]
  PlayerRigStates/AimState.cs → StateMachine/AtomicState.cs

## Import Cycles
- None detected.

## Hyperedges (group relationships)
- **Statechart node types** — statemachine_readme_statemachine, statemachine_readme_atomicstate, statemachine_readme_compoundstate, statemachine_readme_parallelstate, statemachine_readme_transition [EXTRACTED 1.00]
- **Viewmodel lighting fix options** — docs_viewmodel_camera_dynamic_lighting_subviewport_lighting_bug, docs_viewmodel_camera_dynamic_lighting_projection_matrix_shader, docs_viewmodel_camera_dynamic_lighting_subviewport_light_feed, player_viewmodelcamera [EXTRACTED 1.00]
- **Animation plus code-driven cylinder spin pipeline** — docs_blender_cylinder_rotation_bone_revolvercylinderbone, docs_blender_cylinder_rotation_bone_revolvercylinderrotationbone, docs_blender_cylinder_rotation_bone_revolver_actions, docs_blender_cylinder_rotation_bone_skeletonmodifier3d_spin [EXTRACTED 1.00]

## Communities (29 total, 4 thin omitted)

### Community 0 - "Statechart Core Engine"
Cohesion: 0.06
Nodes (20): CompoundState, ActiveState, ChildrenStates, EntryState, State, Enabled, Transitions, ChangeStateEventArgs (+12 more)

### Community 1 - "Namespaces & Imports"
Cohesion: 0.09
Nodes (6): FirstPerson.PlayerRigStates, FirstPerson, FirstPerson.PlayerStates, FirstPerson.StateMachines, RevolverShell, LifetimeSeconds

### Community 2 - "Grunt AI States"
Cohesion: 0.06
Nodes (12): FirstPerson.GruntStates, ActionAimState, ActionFireState, ActionNoneState, FallingState, GruntState, Animator, IdleState (+4 more)

### Community 3 - "Revolver Cylinder Rotation"
Cohesion: 0.07
Nodes (12): RevolverCylinderController, Bullets, BulletShells, CylinderBoneName, EjectSpeed, EjectSpread, RevolverController, RevolverCylinderRotationModifier (+4 more)

### Community 4 - "Design Docs & Rig Notes"
Cohesion: 0.06
Nodes (32): Blender Cylinder Rotation Bone Guide, Assets/Blender/Arms.blend, Blender 4.4+ Action Slots, Revolver Cylinder Actions (RevolverCylinderOpen/Close, RevolverRigReload*), revolverCylinderBone, revolverCylinderRotationBone, RigArmature, SkeletonModifier3D Cylinder Spin (_ProcessModification) (+24 more)

### Community 5 - "Player Movement States"
Cohesion: 0.08
Nodes (8): ClamberingState, CoyoteState, CoyoteTime, GroundedState, LandPunch, LandPunchThreshold, InAirState, AtomicState

### Community 6 - "Viewmodel & Hitboxes"
Cohesion: 0.08
Nodes (12): HitboxComponent, Hitboxes, HitboxParent, HitboxType, Normal, Weakspot, PhysicalBone3DHitbox, Type (+4 more)

### Community 7 - "Grunt Animation Driver"
Cohesion: 0.08
Nodes (17): AnimationTreeDriver, AnimationTree, CurrentNode, RootPlayback, TargetBranch, TargetNode, TargetPlayback, GruntAnimator (+9 more)

### Community 8 - "Revolver Hip & Hammer States"
Cohesion: 0.08
Nodes (8): HipState, Idle, RevolverController, RigController, PushHammerDownState, RevolverController, RigController, WeaponRevolverState

### Community 9 - "Clamber Controller Config"
Cohesion: 0.08
Nodes (20): ClamberController, ClamberableLayers, ClamberReach, ClamberSpeed, ClamberTarget, Clearance, CooldownSeconds, DebugLog (+12 more)

### Community 10 - "Revolver Controller"
Cohesion: 0.15
Nodes (6): RevolverController, CanAct, CanReload, Idle, RevolverCylinderController, StateMachine

### Community 11 - "Walk/Crouch/Sprint States"
Cohesion: 0.13
Nodes (8): CrouchingState, HasHeadroom, SpeedMultiplier, SprintingState, SpeedMultiplier, WalkingState, SpeedMultiplier, WantsSprint

### Community 12 - "Player Controller"
Cohesion: 0.13
Nodes (9): PlayerController, Camera, CrouchToggled, FallSpeed, JumpedThisAirborne, JumpPressed, LookPitch, MoveInput (+1 more)

### Community 14 - "Parallel States"
Cohesion: 0.21
Nodes (5): GruntActiveState, Animator, LocomotingState, ParallelState, ChildrenStates

### Community 15 - "Cylinder Open/Close States"
Cohesion: 0.25
Nodes (3): CloseCylinderState, RevolverController, RigController

### Community 16 - "Revolver Aim State"
Cohesion: 0.22
Nodes (4): AimState, Idle, RevolverController, RigController

### Community 18 - "Revolver Idle State"
Cohesion: 0.27
Nodes (3): IdleState, RevolverController, RigController

### Community 19 - "Camera Controller"
Cohesion: 0.28
Nodes (3): CameraController, Crouched, CrouchOffset

### Community 20 - "Reload Intro State"
Cohesion: 0.25
Nodes (4): IntroInsertBulletState, InsertFinished, RevolverController, RigController

### Community 21 - "Revolver Fire State"
Cohesion: 0.29
Nodes (3): FireState, RevolverController, RigController

### Community 22 - "Insert Bullet State"
Cohesion: 0.40
Nodes (3): InsertBulletState, RevolverController, RigController

### Community 23 - "Reload Interrupt State"
Cohesion: 0.40
Nodes (3): InterruptState, RevolverController, RigController

### Community 24 - "Turn Cylinder State"
Cohesion: 0.40
Nodes (3): TurnCylinderState, RevolverController, RigController

### Community 25 - "Project Build Config"
Cohesion: 0.50
Nodes (4): FirstPersonV3, net8.0, net9.0, Godot.NET.Sdk/4.7.0

### Community 26 - "Rig Controller"
Cohesion: 0.50
Nodes (3): RigController, Revolver, Stance

## Ambiguous Edges - Review These
- `Hierarchical State Machine (Statechart)` → `Revolver Cylinder Actions (RevolverCylinderOpen/Close, RevolverRigReload*)`  [AMBIGUOUS]
  docs/blender-cylinder-rotation-bone.md · relation: conceptually_related_to

## Knowledge Gaps
- **137 isolated node(s):** `AnimationTree`, `RootPlayback`, `TargetPlayback`, `TargetBranch`, `TargetNode` (+132 more)
  These have ≤1 connection - possible missing edges or undocumented components. (Counts symbols only; 215 node(s) total have ≤1 connection when file, concept and rationale nodes are included.)
- **4 thin communities (<3 nodes) omitted from report** — run `graphify query` to explore isolated nodes.

## Suggested Questions
_Questions this graph is uniquely positioned to answer:_

- **What is the exact relationship between `Hierarchical State Machine (Statechart)` and `Revolver Cylinder Actions (RevolverCylinderOpen/Close, RevolverRigReload*)`?**
  _Edge tagged AMBIGUOUS (relation: conceptually_related_to) - confidence is low._
- **Why does `State` connect `Statechart Core Engine` to `Namespaces & Imports`, `Grunt AI States`, `Player Movement States`, `Viewmodel & Hitboxes`, `Revolver Hip & Hammer States`, `Revolver Controller`, `Parallel States`, `Revolver Aim State`?**
  _High betweenness centrality (0.182) - this node is a cross-community bridge._
- **What connects `AnimationTree`, `RootPlayback`, `TargetPlayback` to the rest of the system?**
  _137 weakly-connected nodes found - possible documentation gaps or missing edges._
- **Should `Statechart Core Engine` be split into smaller, more focused modules?**
  _Cohesion score 0.05879692446856626 - nodes in this community are weakly interconnected._
- **Why does `RevolverController` connect `Revolver Controller` to `Statechart Core Engine`, `Namespaces & Imports`, `Revolver Cylinder Rotation`, `Viewmodel & Hitboxes`, `Revolver Hip & Hammer States`, `Cylinder Open/Close States`, `Revolver Aim State`, `Revolver Idle State`, `Reload Intro State`, `Revolver Fire State`, `Insert Bullet State`, `Reload Interrupt State`, `Turn Cylinder State`, `Rig Controller`?**
  _High betweenness centrality (0.171) - this node is a cross-community bridge._
- **Should `Namespaces & Imports` be split into smaller, more focused modules?**
  _Cohesion score 0.09219858156028368 - nodes in this community are weakly interconnected._
- **Why does `RigController` connect `Rig Controller` to `Grunt Animation Driver`, `Revolver Hip & Hammer States`, `Revolver Controller`, `Player Controller`, `Cylinder Open/Close States`, `Revolver Aim State`, `Clamber Sweep Logic`, `Revolver Idle State`, `Reload Intro State`, `Revolver Fire State`, `Insert Bullet State`, `Reload Interrupt State`, `Turn Cylinder State`?**
  _High betweenness centrality (0.121) - this node is a cross-community bridge._
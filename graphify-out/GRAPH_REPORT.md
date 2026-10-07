# Graph Report - FirstPersonV2  (2026-10-07)

## Corpus Check
- 66 files · ~22,284 words
- Verdict: corpus is large enough that graph structure adds value.
- Unclassified: 127 file(s) not represented in the graph (top: .uid 59, .res 26, .import 19)

## Summary
- 616 nodes · 1043 edges · 41 communities (26 shown, 15 thin omitted)
- Extraction: 95% EXTRACTED · 5% INFERRED · 0% AMBIGUOUS · INFERRED: 55 edges (avg confidence: 0.88)
- Token cost: 0 input · 0 output

## Graph Freshness
- Built from commit: `3f016c58`
- Run `git rev-parse HEAD` and compare to check if the graph is stale.
- Run `graphify update .` after code changes (no API cost).

## Community Hubs (Navigation)
- State
- godot
- FallingState
- RevolverCylinderController
- revolverCylinderRotationBone
- .Of
- ViewmodelRenderer
- GruntAnimator
- HipState
- ClamberController
- GruntEnemy
- WalkingState
- PlayerController
- PauseMenu
- ParallelState
- AtomicState
- AimState
- IdleState
- CameraController
- IntroInsertBulletState
- FireState
- RigController
- TurnCylinderState
- FirstPersonV3
- RevolverController
- GruntRotationState
- GruntState
- .AddTransition
- PushHammerDownState
- ActionFiringState
- StaggeredState
- RevolverShell
- ActionNoneState
- IdleState
- InCombatState
- NotInCombatState
- WalkingState
- CLAUDE.md

## God Nodes (most connected - your core abstractions)
1. `State` - 43 edges
2. `StateMachine` - 40 edges
3. `ClamberController` - 33 edges
4. `RevolverController` - 33 edges
5. `RevolverCylinderController` - 29 edges
6. `GruntEnemy` - 27 edges
7. `FirstPerson.StateMachines` - 27 edges
8. `PlayerController` - 24 edges
9. `AtomicState` - 22 edges
10. `GruntAnimator` - 19 edges

## Surprising Connections (you probably didn't know these)
- `Worked example: player movement` --references--> `PlayerController`  [INFERRED]
  Shared/StateMachine/README.md → Player/PlayerController.cs
- `The node types` --references--> `AtomicState`  [INFERRED]
  Shared/StateMachine/README.md → Shared/StateMachine/AtomicState.cs
- `The node types` --references--> `CompoundState`  [INFERRED]
  Shared/StateMachine/README.md → Shared/StateMachine/CompoundState.cs
- `Rules` --references--> `ParallelState`  [INFERRED]
  Shared/StateMachine/README.md → Shared/StateMachine/ParallelState.cs
- `Transitions` --references--> `State`  [INFERRED]
  Shared/StateMachine/README.md → Shared/StateMachine/State.cs

## Import Cycles
- None detected.

## Hyperedges (group relationships)
- **Animation plus code-driven cylinder spin pipeline** — docs_blender_cylinder_rotation_bone_revolvercylinderbone, docs_blender_cylinder_rotation_bone_revolvercylinderrotationbone, docs_blender_cylinder_rotation_bone_revolver_actions, docs_blender_cylinder_rotation_bone_skeletonmodifier3d_spin [EXTRACTED 1.00]
- **Viewmodel lighting fix options** — docs_viewmodel_camera_dynamic_lighting_subviewport_lighting_bug, docs_viewmodel_camera_dynamic_lighting_projection_matrix_shader, docs_viewmodel_camera_dynamic_lighting_subviewport_light_feed, player_viewmodelcamera [EXTRACTED 1.00]

## Communities (41 total, 15 thin omitted)

### Community 0 - "State"
Cohesion: 0.06
Nodes (22): CompoundState, ActiveState, ChildrenStates, EntryState, Rules, The state lifecycle, State, Enabled (+14 more)

### Community 1 - "godot"
Cohesion: 0.08
Nodes (6): FirstPerson.GruntStates, FirstPerson.PlayerRigStates, FirstPerson, FirstPerson.PlayerStates, FirstPerson.StateMachines, ExtensionMethods

### Community 3 - "RevolverCylinderController"
Cohesion: 0.07
Nodes (12): RevolverCylinderController, Bullets, BulletShells, CylinderBoneName, EjectSpeed, EjectSpread, RevolverController, RevolverCylinderRotationModifier (+4 more)

### Community 4 - "revolverCylinderRotationBone"
Cohesion: 0.12
Nodes (16): Blender Cylinder Rotation Bone Guide, Assets/Blender/Arms.blend, Blender 4.4+ Action Slots, Revolver Cylinder Actions (RevolverCylinderOpen/Close, RevolverRigReload*), revolverCylinderBone, revolverCylinderRotationBone, RigArmature, SkeletonModifier3D Cylinder Spin (_ProcessModification) (+8 more)

### Community 5 - ".Of"
Cohesion: 0.09
Nodes (7): CoyoteState, CoyoteTime, GroundedState, LandPunch, LandPunchThreshold, InAirState, Trace: walking, then jump

### Community 6 - "ViewmodelRenderer"
Cohesion: 0.08
Nodes (12): HitboxComponent, Hitboxes, HitboxParent, ViewmodelRenderer, Fov, Shader, Viewmodel, HitboxType (+4 more)

### Community 7 - "GruntAnimator"
Cohesion: 0.06
Nodes (28): GruntAction, Firing, None, GruntAnimator, Action, CombatState, Motion, Override (+20 more)

### Community 8 - "HipState"
Cohesion: 0.09
Nodes (6): ClamberingState, HipState, Idle, RevolverController, RigController, WeaponRevolverState

### Community 9 - "ClamberController"
Cohesion: 0.08
Nodes (20): ClamberController, ClamberableLayers, ClamberReach, ClamberSpeed, ClamberTarget, Clearance, CooldownSeconds, DebugLog (+12 more)

### Community 10 - "GruntEnemy"
Cohesion: 0.10
Nodes (16): GruntEnemy, CombatStartZone, DistanceToTarget, FallGraceTime, HasTarget, HorizontalVelocity, IsFalling, IsInCombat (+8 more)

### Community 11 - "WalkingState"
Cohesion: 0.13
Nodes (8): CrouchingState, HasHeadroom, SpeedMultiplier, SprintingState, SpeedMultiplier, WalkingState, SpeedMultiplier, WantsSprint

### Community 12 - "PlayerController"
Cohesion: 0.13
Nodes (9): PlayerController, Camera, CrouchToggled, FallSpeed, JumpedThisAirborne, JumpPressed, LookPitch, MoveInput (+1 more)

### Community 14 - "ParallelState"
Cohesion: 0.08
Nodes (19): GruntActiveState, Animator, LocomotingState, ParallelState, ChildrenStates, Hoisting a terminal state: both traps at once, How a transition is applied, Imperative transitions (+11 more)

### Community 15 - "AtomicState"
Cohesion: 0.18
Nodes (7): CloseCylinderState, RevolverController, RigController, InterruptState, RevolverController, RigController, AtomicState

### Community 16 - "AimState"
Cohesion: 0.22
Nodes (4): AimState, Idle, RevolverController, RigController

### Community 18 - "IdleState"
Cohesion: 0.31
Nodes (3): IdleState, RevolverController, RigController

### Community 19 - "CameraController"
Cohesion: 0.28
Nodes (3): CameraController, Crouched, CrouchOffset

### Community 20 - "IntroInsertBulletState"
Cohesion: 0.33
Nodes (4): IntroInsertBulletState, InsertFinished, RevolverController, RigController

### Community 21 - "FireState"
Cohesion: 0.29
Nodes (3): FireState, RevolverController, RigController

### Community 22 - "RigController"
Cohesion: 0.25
Nodes (6): RigController, Revolver, Stance, InsertBulletState, RevolverController, RigController

### Community 24 - "TurnCylinderState"
Cohesion: 0.50
Nodes (3): TurnCylinderState, RevolverController, RigController

### Community 25 - "FirstPersonV3"
Cohesion: 0.50
Nodes (4): FirstPersonV3, net8.0, net9.0, Godot.NET.Sdk/4.7.0

### Community 26 - "RevolverController"
Cohesion: 0.15
Nodes (6): RevolverController, CanAct, CanReload, Idle, RevolverCylinderController, StateMachine

### Community 27 - "GruntRotationState"
Cohesion: 0.17
Nodes (6): GruntRotationState, FiringState, InCombatState, NoRotationState, RotateToMovementDirectionState, RotateToTargetState

### Community 29 - "GruntState"
Cohesion: 0.25
Nodes (4): AIIdleState, GruntState, Animator, Grunt

### Community 31 - "PushHammerDownState"
Cohesion: 0.33
Nodes (3): PushHammerDownState, RevolverController, RigController

## Knowledge Gaps
- **165 isolated node(s):** `HitboxParent`, `Hitboxes`, `net8.0`, `net9.0`, `Godot.NET.Sdk/4.7.0` (+160 more)
  These have ≤1 connection - possible missing edges or undocumented components. (Counts symbols only; 253 node(s) total have ≤1 connection when file, concept and rationale nodes are included.)
- **15 thin communities (<3 nodes) omitted from report** — run `graphify query` to explore isolated nodes.

## Suggested Questions
_Questions this graph is uniquely positioned to answer:_

- **Why does `State` connect `State` to `godot`, `ViewmodelRenderer`, `HipState`, `ParallelState`, `AtomicState`, `AimState`, `.StateEntered`, `RevolverController`, `GruntRotationState`, `.AddTransition`?**
  _High betweenness centrality (0.185) - this node is a cross-community bridge._
- **Are the 2 inferred relationships involving `State` (e.g. with `The state lifecycle` and `Transitions`) actually correct?**
  _`State` has 2 INFERRED edges - model-reasoned connections that need verification._
- **What connects `HitboxParent`, `Hitboxes`, `net8.0` to the rest of the system?**
  _165 weakly-connected nodes found - possible documentation gaps or missing edges._
- **Should `State` be split into smaller, more focused modules?**
  _Cohesion score 0.05754475703324808 - nodes in this community are weakly interconnected._
- **Why does `RevolverController` connect `RevolverController` to `State`, `godot`, `RevolverCylinderController`, `ViewmodelRenderer`, `HipState`, `AtomicState`, `AimState`, `IdleState`, `IntroInsertBulletState`, `FireState`, `RigController`, `TurnCylinderState`, `PushHammerDownState`?**
  _High betweenness centrality (0.148) - this node is a cross-community bridge._
- **Are the 3 inferred relationships involving `StateMachine` (e.g. with `State Machine` and `The node types`) actually correct?**
  _`StateMachine` has 3 INFERRED edges - model-reasoned connections that need verification._
- **Should `godot` be split into smaller, more focused modules?**
  _Cohesion score 0.08315863032844165 - nodes in this community are weakly interconnected._
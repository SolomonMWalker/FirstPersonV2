# Roadmap — what's left to make this an FPS

Working checklist. Tick things off, move things between phases, delete what you decide you don't want.

---

## Where you are

**Built, and built well:**

| System | Files | State |
|---|---|---|
| Hierarchical statechart (parallel regions, history, microsteps) | `StateMachine/` | Done, tested (`test_state_machine.tscn`) |
| Player locomotion: walk/sprint/crouch × grounded/air | `PlayerStates/` | Done, tested |
| Clamber/mantle via capsule sweeps | `ClamberController.cs` | Done, tested |
| Camera: bob, strafe roll, crouch dip, impact punch spring | `CameraController.cs` | Done, tested |
| Damage-punch API (directional) | `CameraController.AddDamagePunch` | API exists, **no caller** |
| Greybox test level | `test_level.tscn` | Enough for traversal testing |

**Not built: everything that makes it a game.** No weapon, no enemy, no health, no audio, no UI, no menu, no level beyond the greybox, no export.

### The actual risk

Four research docs (`CAMERA_JUICE`, `IMPACT_JUICE`, `CLAMBER`, `STEP_SMOOTHING`) totalling ~2,200 lines, against zero gameplay. The traversal core is deeper than most shipped indie FPS have. The failure mode here isn't "the movement won't feel good" — it's spending another six months on feel for a game with nothing to shoot.

**Recommendation: freeze feel work after Phase 0 and don't resume it until Phase 1 is playable end to end.** You cannot tune weapon feel, enemy reaction, or hit feedback in the abstract, and those will dominate the moment-to-moment experience far more than head bob will.

---

## Phase 0 — plumbing debt that blocks everything

Small, boring, and every later phase trips over them. Do these first.

- [ ] **InputMap actions.** `PlayerController.SampleInput` reads raw `Key.W`/`Key.Shift`/`Key.C` — its own `ponytail:` comments flag this. No rebinding, no gamepad, and every new verb (fire, reload, interact, use) hardcodes another key. Replace with named actions + `Input.GetVector`. Do it before there are ten verbs, not after.
- [ ] **Pause.** `GameManager` currently just quits on Esc. Needs: Esc → pause, `Input.MouseMode.Visible`, `GetTree().Paused = true`, and `ProcessMode` set correctly on the state machine and camera so they freeze. Touching this later means auditing every node.
- [ ] **Audio buses.** Create Master / SFX / Music / UI in the audio bus layout now. Retrofitting bus routing across 50 `AudioStreamPlayer3D` nodes is miserable.
- [ ] **A settings resource.** Sensitivity, FOV, `BobAmount`, `RollAngle` — you already noted motion sickness is the top complaint and made bob switchable at 0. That switch needs to reach a menu eventually. One `Resource` saved to `user://`, not a config framework.
- [x] ~~**Decide: is step smoothing in or out?**~~ **Built** — `CameraController.StepSmooth()`, test stairs in `test_level`, seven assertions in `PlayerStateTests`. See `STEP_SMOOTHING_ANALYSIS.md` §3. Two things came out of it that changed the picture, both below.
- [ ] **Step-up traversal.** Measured: the capsule climbs **0.15m and no higher** — `ClamberController` won't touch anything under 0.4m, so there is a dead band from 0.15m to 0.4m where the player simply stops. Walk the `StepRuler` lane in `test_level` and you hit it on the third slab. This is now the biggest single hole in the movement set, and it makes the step smoothing above pay off: Godot's capsule *rolls* over a step over several frames (nothing to smooth), whereas a sweep-and-teleport traversal produces the one-frame pop the smoothing was written for. The three-sweep shape is already written in `ClamberController.TryFindLanding`.

---

## Phase 1 — the vertical slice

**Goal: shoot a thing, it dies; it shoots you, you die.** Ugly is correct here. Capsule enemies, no animation, placeholder sounds. Ship the loop, then make it good.

### Combat spine

- [ ] **Health + damage.** One `Health` node (current, max, `TakeDamage(amount, fromPosition)`, `Died` signal). Put it on the player *and* the enemy — same component, no interface, no hierarchy.
- [ ] **Wire the damage punch.** `AddDamagePunch` already exists and takes a player→attacker vector. Player `Health.TakeDamage` calls it. That's your first real payoff on the impact research and it's ~2 lines.
- [ ] **Weapon: hitscan first.** `PhysicsRayQueryParameter3D` from the camera, one `Weapon` node with damage / fire rate / spread / ammo as exports. Hitscan before projectiles — projectiles add lead, travel time, and pooling for no learning.
- [ ] **Weapon state as a parallel region.** Your statechart already has `MovementState` and `AirState` running in parallel. A `WeaponState` region (Idle → Firing → Reloading → Empty) drops in beside them and gets you weapon/movement interaction (no reload while clambering, sprint-to-fire delay) for free. This is the single best use of the machine you already built.
- [ ] **Player death + respawn.** Simplest thing that closes the loop: reload the scene. Not a checkpoint system.

### Something to shoot

- [ ] **Enemy: `NavigationRegion3D` + `NavigationAgent3D`.** Bake a navmesh over the greybox. Standard.
- [ ] **Enemy brain: reuse your state machine.** Idle → Patrol → Chase → Attack → Dead. Your `StateMachine` is engine-generic — do not write a second AI-specific one.
- [ ] **Enemy attack.** Even a hitscan with a telegraph delay. Enough to make the player move.
- [ ] **Hit registration on the enemy.** Damage, a flinch, and a death that removes the body.

### Minimum feedback (skip this and the loop feels broken, not unpolished)

- [ ] Crosshair (a `Control`, or a texture).
- [ ] Hitmarker — the single highest value-per-line element in shooter feedback.
- [ ] Muzzle flash + fire sound.
- [ ] Impact decal + impact sound, different for world vs. enemy.
- [ ] Health readout.

**Phase 1 exit test:** can you play it for 60 seconds without narrating what's supposed to be happening?

---

## Phase 2 — make it feel good

Now the tuning work pays off, because there's something to tune against.

- [ ] **Recoil through `AddPunch`.** Same spring, positive pitch (kicks up). One system for landing, damage, and recoil, exactly as `CameraController`'s comment already anticipates. Add per-shot recoil accumulation and a recovery-to-origin.
- [ ] **Viewmodel.** Render layer 2 + a second `Camera3D` culling everything else — that's the standard fix for both weapon-clipping-into-walls and independent weapon FOV. Note: Godot 4.3's reverse-Z depth buffer broke the older shader-based viewmodel trick, so **prefer the separate-camera approach**, not a copied shader.
- [ ] **Weapon sway + bob.** HL2 moved bob from the camera to the gun. You have a `_bobPhase` already advancing on distance travelled — feed it to the viewmodel instead of adding a second oscillator.
- [ ] **Footsteps.** Drive off the same `_bobPhase` wrap. Surface-dependent if you want it, one sound if you don't.
- [ ] **Ammo, reload, weapon switching.** Falls out of the `WeaponState` region.
- [ ] **Second weapon.** The first one that isn't the first one is where the weapon abstraction actually gets tested. Don't design for N weapons before you have 2.
- [ ] **Screen shake.** Distinct from punch: noise-based, not spring-based. `IMPACT_JUICE_ANALYSIS.md` already cites Eiserloh (GDC 2016) for this and scoped it out — that's still the right call until explosions exist.
- [ ] **Enemy hit reactions.** Flinch, knockback, directional death. This is where hit feedback stops being UI and starts being physical.

---

## Phase 3 — from slice to game

- [ ] **Real level(s).** Greybox first, in `CSGBox3D` like now, then art pass. Level design is a bigger lever on your game than any of the code above.
- [ ] **Level flow & encounter pacing.** Sightlines, cover, arena shapes, ammo/health placement. Study, not code.
- [ ] **Enemy variety.** Ranged / melee / heavy. Two enemy types create more gameplay than ten weapon stats.
- [ ] **Main menu, pause menu, options.** Options must expose sensitivity, FOV, bob/roll, and volume — the accessibility floor.
- [ ] **Save/checkpoint.** Only when levels are long enough that losing progress hurts.
- [ ] **Interaction verb.** Doors, buttons, pickups. One `RayCast3D` from the camera and an `IInteractable`-free duck-typed `Interact()` method.
- [ ] **Objectives / progression / whatever the game is actually about.**

---

## Phase 4 — ship

- [ ] Export presets (Windows first).
- [ ] Performance pass — occlusion culling, LODs, static lighting bake. Not before Phase 3 has real geometry.
- [ ] Controller support (falls out of Phase 0's InputMap).
- [ ] Icon, name, build pipeline.
- [ ] Get five people to play it.

---

## Explicitly deferred — write these down so you stop reconsidering them

- **Multiplayer / netcode.** It is not a feature you add. Decide now, and the answer should be no.
- **Procedural generation.**
- **Full-body animation / IK.** Viewmodel arms only.
- **Weapon modding, perks, skill trees, loadouts.**
- **A dialogue or quest system.**
- **A second state machine implementation for anything.**

---

## Open decisions worth making early

They change what you build, and reversing them later is expensive:

1. **What kind of shooter is it?** Arena/boomer (fast, no reload, hordes) vs. tactical (slow, lethal, sparse)? Your movement work — sprint, clamber, air control, jumping — points hard at the arena end. If so: more enemies, more mobility verbs, faster TTK, and drop cover shooting entirely.
2. **Does clamber stay a core verb?** If yes, levels must be built vertically around it, and the enemy AI needs to path in three dimensions or deliberately not. Right now it's a beautifully engineered mechanic with nothing designed for it.
3. **Weapon count.** Two done well beats six half-done, and it halves Phase 2 and 3.

---

## Suggested next action

Phase 0's InputMap conversion, then Phase 1's health + hitscan weapon, in one sitting. That gets you from "movement tech demo" to "you can shoot a capsule" faster than anything else on this list.

---

## Sources

- [FPS Game Design Fundamentals: Movement, Gunplay, and Level Flow](https://www.strayspark.studio/blog/fps-game-design-fundamentals-ue5) — StraySpark
- [Implementing Hitscan Weapons (FPS Series Part 3)](https://gameidea.org/2025/09/07/your-first-gun-implementing-hitscan-weapons-fps-series-part-3/) — gameidea
- [Adding Recoil and Impact to the Weapon (FPS Series Part 4)](https://gameidea.org/2025/09/07/adding-recoil-and-impact-to-the-weapon-fps-series-part-4/) — gameidea
- [How to Make Your Game Feel Good: A Guide to Game Feel and Juice](https://egmatic.com/blog/how-to-make-your-game-feel-good) — egmatic
- [Add Default Material Setting For FPS Weapon FOV Scaling & Depth](https://github.com/godotengine/godot-proposals/discussions/8941) — godot-proposals
- [Updated Viewmodel Shader for Godot 4.3](https://chafmere.itch.io/godot-4-fps-pro/devlog/783301/updated-viewmodel-shader-for-godot-43) — reverse-Z breakage
- [Godot 4 Enemy AI Tutorial: Patrol, Chase, and Attack](https://codingquests.io/blog/godot-4-enemy-ai-tutorial) — Coding Quests
- [Godot Engine – Raycast shotgun tutorial](https://victorkarp.com/godot-engine-raycast-shotgun-tutorial/) — Victor Karp

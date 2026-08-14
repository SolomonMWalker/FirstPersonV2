# Roadmap — what's left to make this an FPS

Working checklist. Tick things off, move things between phases, delete what you decide you don't want.

---

## Where you are

**Built, and built well:**

| System | Files | State |
|---|---|---|
| Hierarchical statechart (parallel regions, history, microsteps) | `StateMachine/` | Done, tested (`test_state_machine.tscn`) |
| Player locomotion: walk/sprint/crouch × grounded/air | `Player/States/` | Done, tested |
| Clamber/mantle via capsule sweeps | `ClamberController.cs` | Done, tested |
| Camera: bob, strafe roll, crouch dip, impact punch spring | `CameraController.cs` | Done, tested |
| Damage-punch API (directional) | `CameraController.AddDamagePunch` | Done, wired to both damage signals (Phase 1) |
| Viewmodel: separate render pass, sway + bob off the camera's phase | `ViewmodelCamera.cs`, `ViewmodelSway.cs` | Done, tested (`test_viewmodel.tscn`) |
| Player weapon: hitscan, recoil, muzzle flash, fire sound | `Components/HitscanComponent.cs` | Done, tested (`test_player_gun.tscn`) |
| Greybox test level | `test_level.tscn` | Enough for traversal testing |

**Was "not built: everything that makes it a game."** That line is now out of date, which is the point — weapon, enemy, health, shields, hitmarker, HUD, pause and death menus all exist, and the first audio (the fire sound) is in. What's still missing: **any audio besides the one gunshot** (no footsteps, no impacts, no music), a `WeaponState` region, ammo/reload, a second weapon, enemy hit reactions, a level beyond the greybox, and an export.

### The actual risk

Four research docs (`CAMERA_JUICE`, `IMPACT_JUICE`, `CLAMBER`, `STEP_SMOOTHING`) totalling ~2,200 lines, against zero gameplay. The traversal core is deeper than most shipped indie FPS have. The failure mode here isn't "the movement won't feel good" — it's spending another six months on feel for a game with nothing to shoot.

**Recommendation: freeze feel work after Phase 0 and don't resume it until Phase 1 is playable end to end.** You cannot tune weapon feel, enemy reaction, or hit feedback in the abstract, and those will dominate the moment-to-moment experience far more than head bob will.

---

## Phase 0 — plumbing debt that blocks everything

Small, boring, and every later phase trips over them. Do these first.

- [x] ~~**InputMap actions.**~~ **Built** — `move_forward/back/left/right`, `sprint`, `crouch`, `jump` in `project.godot`'s `[input]` section, same physical keys as before. `PlayerController.SampleInput` now reads `Input.GetVector` and `Input.IsAction*`, no raw `Key.*` left. `JumpPressed`'s old ponytail comment (edge lost under 1 physics frame) is resolved for free by `IsActionJustPressed`. Rebinding UI and gamepad bindings are still open — this only gets the named actions in place.
- [x] ~~**Pause.**~~ **Built** — Esc (a new `pause` InputMap action) toggles `GetTree().Paused` in `GameManager`, which also swaps `Input.MouseMode` and shows/hides a `PauseMenu` CanvasLayer (grey tint + Continue/Settings/Restart Level/Quit) in `test_level.tscn`. Player/camera/state machine freeze for free — they were already default `ProcessMode.Pausable`; only `GameManager` and `PauseMenu` needed `Always`. Settings does nothing yet, by design. `GameManagerTests.cs` covers the toggle headless.
- [x] ~~**Audio buses.**~~ **Built** — `default_bus_layout.tres` defines Master, SFX, Music, UI (all sending to Master), wired in as the project's default via `project.godot`'s `[audio]` section. Every future `AudioStreamPlayer*` just needs its `Bus` property set to one of these names.
- [x] ~~**A settings resource.**~~ **Built** — `GameSettings.cs`: a `[GlobalClass] Resource` holding `MouseSensitivity`, `Fov`, `BobAmount`, `RollAngle`, with `Load()`/`Save()` against `user://settings.tres` and an `ApplyTo(player, camera)` that pushes the values into the live nodes. `GameManager` loads and applies it on startup (falls back to defaults — no file exists yet, nothing writes one until there's a menu). `GameSettingsTests.cs` covers the save/load round trip headless.
- [x] ~~**Decide: is step smoothing in or out?**~~ **Built** — `CameraController.StepSmooth()`, test stairs in `test_level`, seven assertions in `PlayerStateTests`. See `STEP_SMOOTHING_ANALYSIS.md` §3. Two things came out of it that changed the picture, both below.
- [x] ~~**Step-up traversal.**~~ **Built** — `ClamberController.TryStepUp()` closes the 0.15–0.4m dead band. `TryFindLanding`'s up/forward/down sweep is now `TrySweep(direction, reach, minRise, maxRise, ...)`, shared by clamber (`MinClamberHeight`–`MaxClamberHeight`, facing direction) and step-up (`MinStepHeight`–`MinClamberHeight`, velocity direction) so the two ranges meet exactly with no gap or overlap. `PlayerController` calls it once per tick before `MoveAndSlide`; a cheap single forward probe gates the full three-sweep so it only runs on ticks the player is actually blocked. `PlayerStateTests` now walks the `StepRuler` lane end to end as part of the existing run.

---

## Phase 1 — the vertical slice

**Goal: shoot a thing, it dies; it shoots you, you die.** Ugly is correct here. Capsule enemies, no animation, placeholder sounds. Ship the loop, then make it good.

### Combat spine

- [x] ~~**Health + damage.**~~ **Built** — as the first piece of a component system: a `Components` Node3D on each GameObject, one node per capability under it, looked up by type via `Component.Get<T>` with null meaning "can't be damaged". `HealthComponent` has `Max`/`Current`/`Alive`, `TakeDamage(amount, fromPosition)`, and `Damaged`/`Died` signals; it's on the player in `test_level`, and goes on the enemy unchanged the moment there is one. See `Components/README.md`; `test_health.tscn` covers it headless.
- [x] ~~**Shields.**~~ **Built** — `ShieldComponent`, Halo rules: absorbs damage whole (no bleed-through on the breaking hit), a recharge cooldown that any damage restarts, and two delay/rate pairs so a break costs a long wait and then refills fast all the way to full. It reaches health by installing itself into a `HealthComponent.AbsorbDamage` hook rather than by health knowing shields exist — the pattern for any future component that modifies another, documented in `Components/README.md`. On the player in `test_level` at 70; `test_shield.tscn` covers it headless. Not a roadmap item originally, but it changes the shape of every combat encounter, so it belonged before the weapon rather than after.
- [x] ~~**Wire the damage punch.**~~ **Built** — `CameraController` subscribes itself to the player's `Health.Damaged` *and* `Shield.Damaged` in `_Ready` and calls the `AddDamagePunch` that had been sitting there unused. Exactly one of the two fires per hit by construction, so it cannot double-punch, and a hit the shield soaks still kicks the view. Magnitude is Quake's: `count` = half the damage floored at `MinDamageKick` (10), times `DamageKickPitch`/`DamageKickRoll` (0.6, both disable at 0) — see `IMPACT_JUICE_ANALYSIS.md` §1.1 and §2.8. Directionless damage (`fromPosition` = `Vector3.Zero`) is skipped rather than aimed at the world origin. `test_gun` asserts the sign on both axes, not just that something fired.
- [x] ~~**Weapon: hitscan first.**~~ **Built**, now for real — `HitscanComponent` replaced `GunComponent` on the player. `PhysicsRayQueryParameters3D` from the camera (same manual-ray reasoning as `InteractorComponent`'s — a component can't inherit the camera's rotation without living somewhere else in the tree), `Damage`/`Interval`/`Range` as exports, no travel time. `GunComponent`/`Projectile` are untouched and still what turrets and the enemy use — hitscan is specifically the player's weapon, the dodgeable telegraphed projectile specifically theirs. `HealthComponent.TakeDamage`'s `DamageResult` return and the `ShotLanded` signal chain built for the hitmarker carried over unchanged; `HitscanComponent` just fires it from a different collision query. `test_player_gun.tscn` covers it — rewritten once the removal of travel time exposed that the true input-to-shot latency here is ~15 ticks, not the ~1 tick most other actions have (this test node is the tree root, so it and every priority-0 node — including `HitscanComponent` — run before `PlayerController`'s priority-1 `SampleInput` each tick, adding a tick beyond the usual latency on top of `Interval`'s own 12). **The trigger since had a real bug fixed:** the cooldown started at a full `Interval` *and* only counted down inside the `if (!Firing) return;` guard, so it froze the remainder the moment you let go and charged it to your next click — any click shorter than `Interval` fired nothing at all, and the one after it fired early. It now drains every tick regardless of the trigger, gated on `Firing` separately, and starts at zero: the first pull fires on the spot and `Interval` gates only what follows. That copied `GunComponent`'s "wait a full interval first", which is right for an *enemy* — the delay is a telegraph you give the player time to react to — and simply a dead click on the player's own weapon. `test_player_gun.tscn` now presses, releases, waits past `Interval` and presses again, which is the sequence that actually catches it.
- [ ] **Weapon state as a parallel region.** Your statechart already has `MovementState` and `AirState` running in parallel. A `WeaponState` region (Idle → Firing → Reloading → Empty) drops in beside them and gets you weapon/movement interaction (no reload while clambering, sprint-to-fire delay) for free. This is the single best use of the machine you already built.
- [x] ~~**Player death + respawn.**~~ **Built** — a `DeathScreen` CanvasLayer ("YOU DIED", Restart, Quit) that `GameManager` shows off the player's `Health.Died`. Restart is `ReloadCurrentScene`, reusing the pause menu's own handler rather than a second copy. Unlike pausing it deliberately leaves `GetTree().Paused` alone, so the world keeps running behind it; only the cursor comes back. Death also takes the pause key away, or Escape stacks the two menus and they fight over the mouse. `GameManagerTests` covers all of that. Not a checkpoint system, by design.

### Something to shoot

- [x] ~~**A dummy to shoot at you.**~~ **Built** — `GunComponent` (then still called `TurretComponent`) + `projectile.tscn`: a static box with a barrel in `test_level` that spits a damaging projectile down one fixed line every 2s. Not an enemy and not on the way to being one; it's the rig that makes health and shields tunable by playing instead of by reading test output. It's undamageable purely by having no `HealthComponent`, and `Projectile` damages whatever it hits by asking for one — so it will work against the real enemy below with no changes. Covered end to end by `test_gun.tscn`. A second one at (5.5, 0, 8.5) starts switched off and toggles on and off through the interaction system (Phase 3), which makes the level's damage source controllable while tuning anything else.
- [x] ~~**Enemy: `NavigationRegion3D` + `NavigationAgent3D`.**~~ **Built** — a `NavigationRegion3D` in `test_level` sourcing geometry from the `navmesh` group (`geometry_source_geometry_mode = GROUPS_WITH_CHILDREN`), so nothing had to be reparented and no node path changed; the world boxes, both turrets and the stair/step containers carry the group tag. **Baked in the editor, and it must be re-baked by hand after the greybox changes** — a runtime `BakeNavigationMesh()` bakes the resource correctly but never pushes the result to the navigation map, so every path query silently returns nothing while `polygon_count` still reads fine. `EnemyTests` asserts the mesh has polygons so a stale or cleared bake fails loudly instead of looking like a broken brain.
- [x] ~~**Enemy brain: reuse your state machine.**~~ **Built** — `Enemy/States/`, a second machine on the same engine-generic `StateMachine`: `Brain` (compound) over Idle → Chase → Attack → Dead, with the edge to Dead hoisted onto `Brain`. Patrol is deliberately not there yet; the point of the statechart is that it's a new node, not an edit. `EnemyController` holds the body (nav agent, facing, ranges) and makes no decisions. It ships **dormant**: `Active` is false on the `Walker` instance and a blue interactable cube switches it on, so the level doesn't open with something already running at you. Switching it back off returns it to Idle from any live state. The cube sits ~17m from the enemy rather than beside it — flipping it has to leave you inside `SightRange` (20) and outside `AttackRange` (12), or the enemy plants and shoots you at point blank and the chase never happens at all. Two statechart traps came out of it and are now written up in `StateMachine/README.md`: child guards must exclude a hoisted parent condition, and a hoisted edge to a terminal state must exclude that state or it re-enters it every frame.
- [x] ~~**Enemy attack.**~~ **Built** — the enemy carries the same `GunComponent` the turrets do; its `Attack` state flips `Firing` on entry and off on exit, and the body yaw does the aiming. Because the gun's countdown only runs while `Firing`, entering Attack always costs a full `Interval` first — that's the telegraph delay, for free. It stops to shoot rather than firing on the move.
- [ ] **Hit registration on the enemy.** The player now has a weapon (see above), so this half works — an enemy can be killed by the player today, covered by `test_player_gun.tscn` against the dormant `Walker`. Still missing: a flinch.

### Minimum feedback (skip this and the loop feels broken, not unpolished)

- [x] ~~Crosshair (a `Control`, or a texture).~~ **Built** — `Crosshair.cs`, a dot + four separated tick marks drawn directly (`_Draw`, not child `Control`s), white, centered in `Hud`.
- [x] ~~Hitmarker — the single highest value-per-line element in shooter feedback.~~ **Built**, shield/health color-coded (weakspot reserved, no hit-location system exists to drive it yet). `HealthComponent.TakeDamage` now returns a `DamageResult` (`None`/`Shield`/`Health`) — the same "hand a value back" exception already established for `AbsorbDamage`, since the caller needs it synchronously rather than by signal. `Projectile` reports it via `Landed`; `GunComponent` forwards it via `ShotLanded` so a listener only has to subscribe once per gun rather than per transient shot; `Crosshair` reacts with a brief four-line X (NE/SE/SW/NW) in the result's color, slotted into the gaps between the resting cross's own four arms rather than resizing or recoloring them — the resting dot and lines never change, only the X appears and disappears. Covered by `GunComponentTests` (shielded target → `Shield`) and `PlayerGunTests` (unshielded → `Health`) — both verified to actually fail if the signal chain is broken, not just to pass.
- [x] ~~Muzzle flash + fire sound.~~ **Built**, both hung off `HitscanComponent` as optional exports next to `RecoilPitch` — all three fire on the trigger pull, hit or miss, because that's what they're a property of. Sound is `Assets/Audio/revolver_shot.wav` (Gamemaster pack out of the Sonniss GDC 2017 bundle, royalty-free) on a plain `AudioStreamPlayer`, not a `3D` one: it's the player's own gun beside their own ears and has no position in the world to pan from. The flash is an `OmniLight3D` under the existing `Camera3D/TestGun/Muzzle`, shown for `FlashTime` (~2 frames) and hidden by a float countdown in `_PhysicsProcess` — the same no-Timer-node posture `ShieldComponent` uses. The countdown deliberately runs *above* the `Firing` guard: releasing the trigger inside the flash window would otherwise skip the hide and leave the muzzle lit. Light first and sprite second was the right order — the light spill is what actually sells it, and it needs no art asset. The flare on top is a `Sprite3D` parented *under* the light (so the one `Visible` toggle drives both, and anything added later comes along free) on visual layer 2, textured with a radial `GradientTexture2D` sub-resource rather than a PNG — nothing to import or keep in sync. Each shot spins the flash root a random amount about the barrel axis so repeated shots stop looking stamped; an omni light is rotation-invariant, so that costs nothing. `test_player_gun.tscn` asserts the flash is dark before the first shot, lights on firing, and goes back out after the trigger is released. Still open, and deliberately: the sample is 96kHz/24-bit stereo and 2.2s long (~85% shrinkable, and its tail is cut off by any fire rate faster than it), `Sprite3D` exposes no additive blend so the flare is alpha-blended, and there's no scale jitter or flash sprite sheet yet.
- [ ] Impact decal + impact sound, different for world vs. enemy.
- [x] ~~Health readout.~~ **Built** — `Hud.cs`, two `ProgressBar`s bottom-left of `test_level`: blue shield over red health. It finds its subject with `PlayerController.Of` and polls `Current` each frame rather than chasing signals (the shield's recharge is a ramp, so a bar has to poll regardless), and hides either bar whose component is absent.

**Phase 1 exit test:** can you play it for 60 seconds without narrating what's supposed to be happening?

---

## Phase 2 — make it feel good

Now the tuning work pays off, because there's something to tune against.

- [x] ~~**Recoil through `AddPunch`.**~~ **Built**, placeholder-quality on purpose: `HitscanComponent.RecoilPitch` (1.2°, positive kicks up) calls `Camera.AddPunch` on every shot, hit or miss. Accumulation and recovery-to-origin are both free — `AddPunch` already sums into the same spring landing and damage punch use, so sustained fire stacks and settles with no extra code. Explicitly a stand-in for real per-weapon recoil (spray patterns, etc.) once there's a proper weapon system; flagged as such in the code so it doesn't get mistaken for tuned feel. `test_player_gun.tscn` asserts a shot produces an upward kick.
- [x] ~~**Viewmodel.**~~ **Built**, out of order, alongside the weapon above. `TestGun` stays a real child of the world `Camera3D` (free bob/roll/mouse-look/punch inheritance) but moves to visual layer 2; the world camera's `cull_mask` excludes that layer so it never draws the gun at world scale. A `SubViewport` (`Hud/ViewmodelContainer`) holds a second camera (`ViewmodelCamera`, `cull_mask` = layer 2 only, its own `fov`) that renders only the gun, composited over the 3D view. Because a `SubViewport` is its own transform root, `ViewmodelCamera.cs` copies the world camera's `GlobalTransform` every rendered frame — the one piece of this that isn't free. Originally wired through `GunComponent`'s `MuzzleOverride`/`Projectile.Shooter` (a projectile spawning inside the player's own capsule and hitting the shooter); since the player moved to `HitscanComponent` (see Weapon: hitscan first, Combat spine) those two only matter for `GunComponent`'s remaining users (turrets, the enemy) now — the hitscan ray solves the same self-hit problem with `query.Exclude` instead. **The `cull_mask` above was silently not applied until the muzzle-flash work found it:** a `#` comment inside a `.tscn` node block swallows the one property line directly after it, and the explanatory comment sat immediately above `cull_mask`. The world camera had been rendering at `1048575` (every layer), drawing the gun at world scale in the world pass the whole time — exactly the failure the comment described preventing. Fixed by deleting the comment; the reasoning now lives in the C#, which is also where it survives an editor save, since Godot strips `.tscn` comments on write. Worth grepping the other scenes for stray `#` lines. Still not verified visually — no reliable way to screenshot a live Godot window from here.
- [x] ~~**Weapon sway + bob.**~~ **Built** — `ViewmodelSway.cs` on the `TestGun` mesh, writing nothing but a local offset from its authored rest pose, so look/roll/punch/camera-bob all still arrive by inheritance. Bob adds no oscillator: `CameraController` now exposes its running `_bobPhase`/`_bobAmp` as the read-only C# properties `BobPhase`/`BobAmp` (not `[Export]`s — they are recomputed every tick, so there is nothing to author) and the gun traces the same figure-8 at `BobScale`× the camera's amplitude, which carries the speed ramp and the standing-still fade along with it. Going full HL2 is a tuning move, not a code one — drop `BobAmount`, raise `BobScale`. Sway is look-delta lag: `AngleDifference` on body yaw (raw subtraction spikes a full turn at the ±π wrap) plus `LookPitch` delta, per second so it measures turn speed not tick length, clamped at `SwayMax` so a flick can't throw the gun off-screen, chased back with the same exponential `CameraController` uses. Both channels off at 0, same posture as every other channel. `test_viewmodel.tscn` drives a turn and a strafe, asserts peak offset on each, and — the half that regresses silently — asserts both return all the way to rest; verified failing with each channel zeroed.
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
- [x] ~~**Interaction verb.**~~ **Built** — `InteractableComponent` (a `Verb`, an `Enabled` flag, an `Interacted` signal) plus `InteractorComponent` on the player, which raycasts from the camera and publishes what's under the crosshair. No interface and no interact volume: the ray hits the object's own collider and `Component.Get` walks up to the GameObject that owns it, so an object becomes interactable by carrying the component. `E` is a new `interact` InputMap action; `Hud` shows "Press E to \<verb\>" with the key read from the InputMap rather than hardcoded. Walk-over pickups stayed as they were — `HealthPickup` needs no verb. `test_interact.tscn` covers it.
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

~~Phase 0's InputMap conversion, then Phase 1's health + hitscan weapon, in one sitting.~~ **Done, and it worked** — that's what got this from "movement tech demo" to a playable loop.

Next: **run the Phase 1 exit test.** Play it for 60 seconds without narrating what's supposed to be happening, and write down what actually bothers you. Phases 1 and 2 are far enough along that the remaining checkboxes are guesses about what's wrong, and 60 seconds at the keyboard beats all of them. Two known candidates it will probably surface — the two unticked items in Phase 1's feedback list (impact decals and sound, and an enemy flinch), which are the pieces that make a shot feel like it connected with *something* rather than with the hitmarker.

Note that a fair amount of this is verified only headless: the viewmodel, the muzzle flash and the flare have never been looked at on a screen.

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

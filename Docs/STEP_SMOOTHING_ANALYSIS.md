# Step smoothing — research & recommendation

Research into how shipped FPS games keep the camera smooth when the body's vertical position jumps
— walking over kerbs, rubble, stair treads and other bumps small enough to traverse without a
dedicated climb — and what that should look like in *this* project.

**Section 1** is the research. **Section 2** is the recommendation. **Section 3** is what was built, and
the one measured fact that turned out to contradict §2.5.

---

## 0. The distinction that makes this tractable

Two different problems get filed under "stairs", and conflating them is why most tutorials on the
subject are three times longer than they need to be:

| | Problem | Where it lives |
|---|---|---|
| **Traversal** | Can the body get up the step *at all*, or does it stop dead? | Movement / collision |
| **Smoothing** | The body's Y is now correct but it *teleported* — does the eye follow in one frame? | View |

They're independent. Traversal is solved by sweeps and teleports; smoothing is solved by lagging the
camera behind the body and catching up over ~0.15s. **This document is about smoothing**, which is
what "small enough to just walk over" implies — a capsule collider already rounds over small bumps
on its own, and that's precisely the case where the body rises correctly and the eye snaps.

Traversal is covered briefly in §2.5 because the boundary matters for picking constants, but it is
not the ask.

---

## 1. What the shipped games actually do

The striking thing is how little the technique has changed in thirty years. Quake 1 and Source
implement the same idea with the same structure, and every Godot solution found is a restatement of
it.

### Quake (1996) — `V_CalcRefdef`, `WinQuake/view.c`

```c
// smooth out stair step ups
if (cl.onground && ent->origin[2] - oldz > 0)
{
	float steptime;

	steptime = cl.time - cl.oldtime;
	if (steptime < 0)
		steptime = 0;

	oldz += steptime * 80;
	if (oldz > ent->origin[2])
		oldz = ent->origin[2];
	if (ent->origin[2] - oldz > 12)
		oldz = ent->origin[2] - 12;
	r_refdef.vieworg[2] += oldz - ent->origin[2];
	view->origin[2] += oldz - ent->origin[2];
}
else
	oldz = ent->origin[2];
```

Six lines, and every one earns its place:

1. **`oldz` is a lagging copy of the body's Z**, held in a `static float` across frames. The view is
   drawn at `oldz`, not at the body's real height. The body teleports; the *eye* is what the player
   sees, and it's on a leash.
2. **`oldz += steptime * 80`** — the leash reels in at a **constant rate**, 80 units/second. Not a
   lerp, not a spring. It arrives.
3. **`if (oldz > origin[2]) oldz = origin[2]`** — clamp on arrival. The lag term hits exactly zero
   and stops; no permanent eye-height error accumulates.
4. **`if (origin[2] - oldz > 12) oldz = origin[2] - 12`** — clamp the *lag itself* to 12 units. A
   large legitimate rise (a lift, a teleport, a launch) can't drag the camera into the floor.
5. **`cl.onground`** — only smooth while grounded. Falling and jumping change Z legitimately and
   must not be smoothed, or the whole jump arc turns to mush.
6. **`else oldz = origin[2]`** — the moment any guard fails, snap the lag to zero. No stale state.

Quake smooths **up only** (`origin[2] - oldz > 0`). Stepping down is left to snap.

**Known cost, documented at the time:** the view drifts below the actual aim point while walking up
stairs and ramps, which visibly desyncs muzzleflashes. The eye is lying about where it is, and
anything that renders *from* the eye inherits the lie.

### Source / Half-Life 2 — `CBasePlayer::SmoothViewOnStairs`

```cpp
static ConVar smoothstairs( "smoothstairs", "1", FCVAR_REPLICATED,
    "Smooth player eye z coordinate when traversing stairs." );

void CBasePlayer::SmoothViewOnStairs( Vector& eyeOrigin )
{
	CBaseEntity *pGroundEntity = GetGroundEntity();
	float flCurrentPlayerZ = GetLocalOrigin().z;
	float flCurrentPlayerViewOffsetZ = GetViewOffset().z;

	// NOTE: Don't want to do this when the ground entity is moving the player
	if ( ( pGroundEntity != NULL && pGroundEntity->GetMoveType() == MOVETYPE_NONE ) &&
	     ( flCurrentPlayerZ != m_flOldPlayerZ ) && smoothstairs.GetBool() &&
	     m_flOldPlayerViewOffsetZ == flCurrentPlayerViewOffsetZ )
	{
		int dir = ( flCurrentPlayerZ > m_flOldPlayerZ ) ? 1 : -1;

		float steptime = gpGlobals->frametime;
		if (steptime < 0) steptime = 0;

		m_flOldPlayerZ += steptime * 150 * dir;

		const float stepSize = 18.0f;

		if ( dir > 0 )
		{
			if (m_flOldPlayerZ > flCurrentPlayerZ) m_flOldPlayerZ = flCurrentPlayerZ;
			if (flCurrentPlayerZ - m_flOldPlayerZ > stepSize) m_flOldPlayerZ = flCurrentPlayerZ - stepSize;
		}
		else
		{
			if (m_flOldPlayerZ < flCurrentPlayerZ) m_flOldPlayerZ = flCurrentPlayerZ;
			if (flCurrentPlayerZ - m_flOldPlayerZ < -stepSize) m_flOldPlayerZ = flCurrentPlayerZ + stepSize;
		}

		eyeOrigin[2] += m_flOldPlayerZ - flCurrentPlayerZ;
	}
	else
	{
		m_flOldPlayerZ = flCurrentPlayerZ;
		m_flOldPlayerViewOffsetZ = flCurrentPlayerViewOffsetZ;
	}
}
```

Structurally identical to Quake, with three upgrades:

- **Both directions.** `dir` is ±1 and the clamps are mirrored, so stepping *down* is smoothed too.
  Descending stairs pops just as hard as ascending, and Quake simply didn't bother.
- **`m_flOldPlayerViewOffsetZ == flCurrentPlayerViewOffsetZ` guard.** If the *view offset* changed
  this frame — the player ducked — skip smoothing entirely. **Crouching is a deliberate eye
  movement and must not be treated as a step to be absorbed.** This is the single most important
  guard for our project, which has exactly such a crouch dip.
- **Moving-platform guard.** `pGroundEntity->GetMoveType() == MOVETYPE_NONE` — don't smooth when the
  ground itself is carrying the player. A lift's rise is not a step.
- **It's a ConVar, defaulted on.** Same accessibility posture as head bob: shippable, and
  disable-able.

### The constants, and the one number that transfers

Raw values differ, and unit conventions between Quake and Source are muddy enough that converting to
metres invites error. The *ratio* is convention-independent and is what actually transfers:

| Engine | Catch-up rate | Max lag | Rate ÷ lag | Time to absorb a full-height step |
|---|---|---|---|---|
| Quake | 80 u/s | 12 u | 6.7 /s | **~0.15 s** |
| Source | 150 u/s | 18 u | 8.3 /s | **~0.12 s** |

Both land on **roughly an eighth of a second** to absorb a maximum step. That's the number to port.
It's short — this is not a floaty camera effect, it's just long enough to turn a one-frame
discontinuity into a handful of frames of motion.

### Godot — no built-in support, verified locally

Queried against the engine actually in use (`ClassDB.class_get_property_list("CharacterBody3D")`,
Godot 4.7.stable.mono):

```
motion_mode, up_direction, slide_on_ceiling, velocity, max_slides, wall_min_slide_angle,
floor_stop_on_slope, floor_constant_speed, floor_block_on_wall, floor_max_angle,
floor_snap_length, platform_on_leave, platform_floor_layers, platform_wall_layers, safe_margin
```

Methods matching step/snap: `apply_floor_snap`, `get_floor_snap_length`, `set_floor_snap_length`.

**There is no `max_step_height` and no view smoothing of any kind.** `move_and_slide()` treats a step
as a wall. [godot-proposals#2751](https://github.com/godotengine/godot-proposals/issues/2751) has
been open for years. Every Godot solution is community code, and they split into:

- **Sweep-and-teleport traversal** (`test_move` / `PhysicsServer3D.body_test_motion`): sweep up,
  forward, then down; if the landing is walkable, move the body there.
  [dresswithpockets](https://dresswithpockets.github.io/2025/03/19/godot-stair-stepping.html) and
  [hadamard.space](https://hadamard.space/blog/godot-stairs/) are the two clearest writeups. This is
  **the same three-sweep shape as our `ClamberController.TryFindLanding`** — up, forward, down, check
  the normal against `floor_max_angle`.
- **Ramp colliders**: keep the detailed stair mesh visually, give it a smooth ramp collision shape.
  Zero code, and it's what most shipped games do. Irrelevant to *view* smoothing, but it dissolves
  the traversal half of the problem outright.
- **Camera smoothing**: dresswithpockets is the only source found that covers it, using exponential
  decay — `position.y = b + (a - b) * exp(-decay * delta)` — explicitly to give "weight to each step"
  rather than perfect interpolation. Note this differs from Quake/Source, which both use a constant
  rate. See §2.3.

One engine note worth recording: multiple sources report CharacterBody3D jitter on steps under Godot
Physics that does not occur under **Jolt**, which this project already uses (`project.godot`:
`3d/physics_engine="Jolt Physics"`).

---

## 2. Recommendation for this project

### 2.1 Where it goes — and the one thing it must not touch

`CameraController.cs`, as a third term in the same `Position.Y` composition that already carries the
crouch dip and the bob:

```csharp
Position = Position with { Y = _eyeY + bobY + _stepLag };
```

`_stepLag` is **negative** while catching up from a step *up* (the eye trails below the body), and
positive after a step *down*.

The critical constraint is the one already established for bob, and it applies verbatim:

> `CrouchOffset => _standY - _eyeY` feeds `PlayerController.ApplyCrouchHeight()` and
> `ClamberController.HeightScale`.

**`_stepLag` must never reach `_eyeY`.** If it did, the collision capsule would resize every time the
player walked over a kerb, and clamber reach would flicker with it. Same hazard as bob, same fix:
`_eyeY` stays the crouch-only eye height and every view-space offset is added at the last moment.
The existing split already accommodates this — no restructuring needed, just another addend.

### 2.2 The measurement problem: the camera is a *child* of the body

Quake and Source read `ent->origin[2]` — the body's world Z — because their view is computed in world
space. Our `Camera3D` is a child of `CharacterBody3D`, so when the body pops up 0.15m the camera's
**global** Y pops with it while its **local** `Position.Y` doesn't change at all.

So the smoothing has to:

1. Track the body's `GlobalPosition.Y` in a field across physics ticks (`_lastBodyY`).
2. On each tick, compute `rise = _player.GlobalPosition.Y - _lastBodyY`.
3. Subtract that rise from `_stepLag`, so the eye stays put in world space.
4. Decay `_stepLag` toward 0 at a constant rate.

That's the direct translation of `oldz`, expressed as a lag rather than an absolute — which suits a
child node better, because a lag of zero is the correct resting state and needs no initialisation
against the parent.

### 2.3 Constant rate, not exponential decay

The project already has both idioms: `MoveToward` for the crouch, `1 - exp(-k·dt)` for bob amplitude
and roll. For step lag, **constant rate is correct**, for the reason already written into
`CameraController`:

> `MoveToward`, not `Lerp`: constant speed and it actually arrives.

Exponential decay never reaches zero. A residual `_stepLag` is a *permanent eye-height error* — small,
but it accumulates across every bump on a rough path and there's no mechanism to clear it. Quake and
Source both use a constant rate and both explicitly clamp on arrival, which is exactly what
`Mathf.MoveToward` does in one call. The Godot article's exponential decay is the outlier here, and
its stated goal (giving each step "weight") is better served by tuning the rate.

### 2.4 The guards, ported

Each of these maps onto something concrete in this codebase, and every one of them prevents a
specific visible bug:

| Guard | Ported as | What breaks without it |
|---|---|---|
| `cl.onground` | `_player.IsOnFloor()` | The entire jump and fall arc gets smoothed into mush. |
| Source's view-offset guard | `Mathf.IsEqualApprox(_eyeY, _lastEyeY)` | The **crouch dip gets absorbed as a step** — press C and the camera doesn't move. |
| Moving-platform guard | *(no moving platforms yet)* | — |
| — | `!_player.Clamber.IsClambering` | A 1.6m clamber rise is clamped to the step limit, then dumps a large lag; the eye sinks through the floor mid-mantle. |
| `stepSize` clamp | `Mathf.Clamp(_stepLag, -MaxStepLag, MaxStepLag)` | Any large teleport (respawn, the tests' `GlobalPosition` writes) drags the camera underground. |
| `else oldz = origin[2]` | `_stepLag = 0` in the else branch | Stale lag from before a jump reappears on landing. |

The crouch guard deserves emphasis: **this project will hit that bug**, because `_eyeY` moves
0.5m under `CrouchDrop` at exactly the kind of rate step smoothing is built to absorb. Source hit it
in 2004 and added `m_flOldPlayerViewOffsetZ` for it. Since our smoothing watches the *body's* global
Y and the crouch only moves `_eyeY` (a local offset), we may be structurally immune — but that's
worth asserting in a test rather than assuming.

### 2.5 Traversal — the adjacent gap, explicitly out of scope

Worth recording because it bounds the constants. Right now there are three height bands:

> **⚠️ Corrected after measurement — see §3.1.** The first row below was an estimate and it is wrong.
> The capsule rolls over **0.15m and no more**; 0.20m stops the player dead. The gap band is
> therefore 0.15–0.4m, not 0.3–0.4m, and it is nearly twice as wide as assumed here.

| Height | Handled by | How |
|---|---|---|
| Below ~0.2–0.3m | Capsule collider (radius 0.5) | Rounds over it automatically. **This is the band that needs view smoothing.** |
| ~0.3m to 0.4m | **Nothing** | Too tall for the capsule, and `ClamberController` rejects it: `MinClamberHeight = 0.4f`, `"too low, that is a step"`. |
| 0.4m to 1.6m | `ClamberController` | Full mantle animation. |

There's a real gap in the middle band. Closing it means a step-up sweep — and the three-sweep
up/forward/down shape is *already written* in `ClamberController.TryFindLanding`, including the
`floor_max_angle` normal check. That's a separate piece of work, but it's the natural next one, and
whatever `MaxStepHeight` it lands on should become the `MaxStepLag` clamp for the smoothing.

Until then, pick `MaxStepLag` from what the capsule can actually roll over.

### 2.6 Ordering — `ProcessPhysicsPriority`

A concrete gotcha. `PlayerController` sets `ProcessPhysicsPriority = 1` so it runs *after* the state
machine and its `MoveAndSlide()` applies the velocity the states wrote. `CameraController` is at the
default **0**, so it currently runs *before* the body moves.

That's fine for bob and roll, which read `Velocity`. It is **not** fine for step smoothing, which
must read `GlobalPosition` *after* `MoveAndSlide()` — otherwise every step is detected one tick late
and the first frame of the pop is rendered unsmoothed, which is the exact frame that's visible.

Fix: `ProcessPhysicsPriority = 2` on `CameraController`.

Note the side effect, which should be a deliberate decision rather than a surprise: bob and roll
would then read post-`MoveAndSlide` velocity instead of the states' intended velocity. That's
arguably *better* — walking into a wall would stop the bob, because the slide zeroed the velocity —
but it is a behaviour change to the shipped bob, and `PlayerStateTests` asserts on bob peak
amplitude. Verify against the suite.

### 2.7 Proposed shape

```csharp
[Export] public float StepSmoothRate = 2.5f;   // metres/sec the eye catches up. 0 disables.
[Export] public float MaxStepLag = 0.35f;      // furthest the eye may trail the body

private float _stepLag;      // negative while catching up from a step up
private float _lastBodyY;
private float _lastEyeY;

// ... in _PhysicsProcess, after _eyeY is updated:

var bodyY = _player.GlobalPosition.Y;
var rise = bodyY - _lastBodyY;
_lastBodyY = bodyY;

var crouching = !Mathf.IsEqualApprox(_eyeY, _lastEyeY);   // Source's view-offset guard
_lastEyeY = _eyeY;

if (grounded && !crouching && _player.Clamber is { IsClambering: false })
{
    // Hold the eye where it was in world space, then reel it back in at a constant rate so it
    // arrives at exactly zero rather than asymptotically approaching it.
    _stepLag = Mathf.Clamp(_stepLag - rise, -MaxStepLag, MaxStepLag);
    _stepLag = Mathf.MoveToward(_stepLag, 0f, StepSmoothRate * d);
}
else
{
    _stepLag = 0f;   // Quake's `else oldz = origin[2]` -- never carry stale lag across a guard
}

Position = Position with { Y = _eyeY + bobY + _stepLag };
```

### 2.8 Starting values

| Knob | Start | Derivation |
|---|---|---|
| `MaxStepLag` | `0.35` m | What a 0.5m-radius capsule plausibly rolls over; below `MinClamberHeight = 0.4`, so the two systems can't both claim the same bump. |
| `StepSmoothRate` | `2.5` m/s | `MaxStepLag × 7` ≈ 2.45, from the ~0.14s convergence both Quake and Source land on. Falls out equal to the existing `CrouchSpeed = 2.5f`, so the eye moves at one consistent speed for every view-space correction — probably worth keeping deliberately. |

Both `[Export]`, `0` on `StepSmoothRate` disabling the channel — same posture as the bob knobs, and
`smoothstairs` was a ConVar in Source for the same reason.

### 2.9 How to test it

The existing `PlayerStateTests` harness already does everything needed: it drives the real
`test_level` scene, writes `_body.GlobalPosition` directly, and samples camera values on specific
frames. A step-smoothing case needs a small box to walk over — `test_level` has CSGBox3Ds already —
and three assertions:

- Walking onto a low step, the camera's **global** Y rises over several frames, not one. (The
  measurement trap from the bob work applies again: measure against the body, not against a
  remembered stand height.)
- `_stepLag` returns to exactly `0` once settled, so no permanent eye-height error accumulates.
- Crouching does **not** produce step lag, and clambering does not either. These are the two guards
  most likely to regress silently.

### Deliberately out of scope

- **Step-up traversal** (§2.5). Separate system, and the ask was smoothing.
- **Landing dip on a hard fall.** Still the impulse system deferred in `CAMERA_JUICE_ANALYSIS.md`;
  step smoothing is continuous and deliberately guarded *off* while airborne, so the two don't
  overlap.
- **Moving-platform handling.** Source guards for it; no moving platforms exist here yet.
- **The muzzleflash desync Quake documented.** Nothing renders from the eye yet. When a viewmodel or
  hitscan tracer arrives, remember the eye is deliberately lying about its position for ~0.15s after
  every step, and origin those from the body rather than the camera.

---

## 3. As built

Implemented as specified in §2.7, with one addition (§3.2) and one finding that changes what the
feature is *for* (§3.3).

### Files changed

| File | Change |
|---|---|
| `CameraController.cs` | `StepSmoothRate` / `MaxStepLag` exports, `StepSmooth()`, `_stepLag` as a third addend on `Position.Y`, `ProcessPhysicsPriority = 2`, `StepLag` accessor for tests |
| `test_level.tscn` | `Stairs` (six 0.15m treads to a landing), `StepRuler` (0.10 / 0.15 / 0.20 / 0.30m slabs), `floor_snap_length = 0.35` on the body |
| `Player/States/PlayerStateTests.cs` | Step phase at frames 775–1070, plus a clamber-lag sampler inside the existing clamber phase |

### 3.1 ⚠️ The capsule climbs 0.15m, not 0.3m

§2.5 assumed the capsule rounds over anything below ~0.2–0.3m and that this was the band needing
smoothing. Measured directly — walk the real player into deep test treads and read the settled
height:

```
step 0.10m -> settled 0.101m  CLIMBED
step 0.15m -> settled 0.151m  CLIMBED
step 0.20m -> settled 0.000m  blocked
step 0.25m -> settled 0.000m  blocked
```

Identical with `floor_block_on_wall` on and off, and identical at both snap lengths tried. **0.15m is
the ceiling**, which is why the test stairs are built at 0.15m treads — that number is measured, not
chosen. The first version of this work used 0.2m treads and the player could not climb them at all.

The consequence for §2.5: the untraversable gap runs 0.15m → 0.4m, not 0.3m → 0.4m. `StepRuler` in
`test_level` walks straight into it — the 0.20 and 0.30 slabs stop the player dead, and neither the
capsule nor `ClamberController` will take them.

### 3.2 `floor_snap_length = 0.35`

Not in the §2.7 spec, and required. At Godot's default `0.1`, walking *down* anything taller than
10cm makes the body briefly airborne, the grounded guard correctly refuses to smooth a fall, and
descent smoothing never fires at all. Raising the snap to match `MaxStepLag` keeps the body glued
over the drop so there is a step to smooth. Verified not to affect climbing either way (§3.1).

Strictly this is traversal, which §2.5 scoped out. It is one property, and without it half the
feature is dead code.

### 3.3 ⚠️ Ascent has nothing to smooth — and that is the real finding

Measured on the 0.15m treads, at the shipped constants:

| | Peak lag | Worst single-tick eye motion | Worst single-tick body motion |
|---|---|---|---|
| **Ascending** | **0.000 m** | — | 0.042 m |
| **Descending** | 0.028 m | 0.046 m | 0.069 m |

Ascent produces *no lag whatsoever*, and it is not a bug. Godot has no step-up teleport: the capsule
**rolls** over a tread across four or so ticks at ≈2.5 m/s — which is exactly `StepSmoothRate` — so
the eye keeps pace precisely and the lag term never accumulates. Confirmed by dropping
`StepSmoothRate` to `0.5`, at which the same climb does produce lag. There was never a
discontinuity to absorb, because Godot's own collision response already spread the rise over
several frames.

Descent is the half that pops, because floor snap pulls the body down in one tick, and the
smoothing measurably absorbs it: the eye's worst frame is a third smaller than the body's.

**So: this system is currently a solution to a problem this project only half has.** It becomes
load-bearing the moment step-up traversal lands (§2.5), because a sweep-and-teleport implementation
*is* a one-frame discontinuity — the exact thing Quake wrote `oldz` for. Build traversal, and this
starts earning its keep on the way up as well as down. Until then, expect a subtle effect on
descent and nothing at all on ascent.

### Tests

Seven assertions in `PlayerStateTests`, frames 775–1070:

- the 0.15m treads are walkable at all (guards §3.1 — a regression here silently turns every
  assertion below into a test of flat ground)
- ascent lag stays within `MaxStepLag`
- descending lags the eye above the body, and within `MaxStepLag`
- the eye drops less per tick than the body does
- lag settles on **exactly** zero, so no permanent eye-height error accumulates
- crouching is not absorbed as a step (Source's 2004 bug)
- clambering is not absorbed as a step

The `ProcessPhysicsPriority = 2` change predicted in §2.6 did land, and bob and roll now read
post-`MoveAndSlide` velocity. The existing bob-amplitude assertions passed unchanged.

### One guard the spec did not have

`grounded && _wasGrounded` — grounded on *both* ticks, not just this one. On the frame a fall ends,
`rise` is the entire last tick of the drop; absorbing it floats the eye **above** the body at
exactly the moment the landing punch fires. Quake sidesteps this by only ever smoothing upward; the
two-tick test costs one bool and also permits the downward smoothing Quake never had.

---

## Sources

- [Quake `WinQuake/view.c`](https://github.com/defunkt/quake/blob/master/WinQuake/view.c) — the `oldz` stair smoothing block in `V_CalcRefdef`
- [Source SDK `baseplayer_shared.cpp`](https://github.com/pmrowla/hl2sdk-csgo/blob/master/game/shared/baseplayer_shared.cpp) — `CBasePlayer::SmoothViewOnStairs`, `smoothstairs` ConVar
- [godot-proposals#2751 — automatic smooth stairs step-up/step-down for CharacterBody](https://github.com/godotengine/godot-proposals/issues/2751)
- [dresswithpockets — Godot Stair Stepping](https://dresswithpockets.github.io/2025/03/19/godot-stair-stepping.html) — sweep approach plus exponential camera smoothing
- [hadamard.space — Walking up stairs in Godot](https://hadamard.space/blog/godot-stairs/) — `body_test_motion` approach, `2 × max_step_height` sweep sizing
- [JheKWall — Godot Stair Step Demo](https://github.com/JheKWall/Godot-Stair-Step-Demo)
- [Andicraft/stairs-character](https://github.com/Andicraft/stairs-character) · [mrjshzk/StairsCharacter3D](https://github.com/mrjshzk/StairsCharacter3D) — `move_and_stair_step()` drop-in replacements
- [Bugnet — Fix: Godot CharacterBody3D getting stuck on stairs](https://bugnet.io/blog/fix-godot-characterbody3d-stairs-climbing-stuck) — ramp-collider approach, Jolt vs Godot Physics jitter
- Godot 4.7.stable.mono, `ClassDB.class_get_property_list("CharacterBody3D")` — verified locally, no step-height property exists

# Impact juice — landing and damage camera reactions

Research into how shipped FPS games make the camera react to landing from a fall and to taking
damage, and what that should look like in *this* project.

**Section 1** is the research. **Section 2** is the implementation — **built, tested, passing**.
**Section 3** records what changed and what the tests caught. This delivers on the landing dip
deferred in [`CAMERA_JUICE_ANALYSIS.md`](CAMERA_JUICE_ANALYSIS.md).

---

## 0. The finding that shapes everything else

**Landing and damage are the same system with different inputs.** Quake and Source both implement
them as one mechanism — an angular impulse applied to the view that decays back to zero — invoked
from two places. Quake calls it a *kick*, Source calls it a *punch*.

That matters because the obvious approach is to build a landing dip and a damage shake separately,
and then discover they fight over the camera. Valve wrote one `m_viewPunchAngle` and had
`PlayerRoughLandingEffects()` and weapon recoil and damage all push into it.

There's also a **third** thing that keeps getting conflated with these and shouldn't be: *screen
shake*. Punch is directional and deterministic (you got hit from the left, so the view kicks right).
Shake is noise-driven and non-directional (an explosion went off nearby). Different math, different
purpose. §1.4 and §2.7.

---

## 1. What the shipped games actually do

### 1.1 Quake — the damage kick, `V_ParseDamage`

```c
cvar_t	v_kicktime  = {"v_kicktime",  "0.5", false};
cvar_t	v_kickroll  = {"v_kickroll",  "0.6", false};
cvar_t	v_kickpitch = {"v_kickpitch", "0.6", false};

// ... in V_ParseDamage, after reading armor/blood/from off the wire:

count = blood*0.5 + armor*0.5;
if (count < 10)
	count = 10;

ent = &cl_entities[cl.viewentity];

VectorSubtract (from, ent->origin, from);
VectorNormalize (from);

AngleVectors (ent->angles, forward, right, up);

side = DotProduct (from, right);
v_dmg_roll = count*side*v_kickroll.value;

side = DotProduct (from, forward);
v_dmg_pitch = count*side*v_kickpitch.value;

v_dmg_time = v_kicktime.value;
```

Three things to take:

1. **The kick is directional, derived from the hit's world position.** `from` is normalised
   attacker→player, then projected onto the player's `right` and `forward` vectors — the exact same
   `DotProduct(direction, right)` idiom as `V_CalcRoll` in the bob work. Roll from the sideways
   component, pitch from the forward component. Shot from the left, the view rolls one way; shot
   from behind, it pitches. **The player can tell where the hit came from without a HUD indicator.**
2. **Magnitude scales with damage, with a floor.** `count = blood*0.5 + armor*0.5`, clamped to a
   minimum of 10, so even a scratch produces a readable kick. Feedback that vanishes for small hits
   reads as an input being dropped.
3. **Decay is dead simple — linear, over a fixed duration.** From `V_CalcViewRoll`:

```c
if (v_dmg_time > 0)
{
	r_refdef.viewangles[ROLL]  += v_dmg_time/v_kicktime.value*v_dmg_roll;
	r_refdef.viewangles[PITCH] += v_dmg_time/v_kicktime.value*v_dmg_pitch;
	v_dmg_time -= host_frametime;
}
```

A timer counting down from `v_kicktime` (0.5s), used as a normalised 1→0 scalar. No spring, no
overshoot. Quake has **no landing kick at all** — falling is communicated by sound and damage only.

### 1.2 Source — the punch, modelled as a damped spring

`gamemovement.cpp`:

```cpp
#define PUNCH_DAMPING		9.0f		// bigger number makes the response more damped, smaller is less damped
										// currently the system will overshoot, with larger damping values it won't
#define PUNCH_SPRING_CONSTANT	65.0f	// bigger number increases the speed at which the view corrects
```

```cpp
//-----------------------------------------------------------------------------
// Purpose: Decays the punchangle toward 0,0,0.
//			Modelled as a damped spring
//-----------------------------------------------------------------------------
	if ( player->m_Local.m_viewPunchAngle->LengthSqr() > 0.001 || player->m_Local.m_vecPunchAngleVel->LengthSqr() > 0.001 )
	{
		player->m_Local.m_viewPunchAngle += player->m_Local.m_viewPunchAngleVel * gpGlobals->frametime;
		float damping = 1 - (PUNCH_DAMPING * gpGlobals->frametime);

		if ( damping < 0 )
			damping = 0;
		player->m_Local.m_viewPunchAngleVel *= damping;

		// torsional spring
		// UNDONE: Per-axis spring constant?
		float springForceMagnitude = PUNCH_SPRING_CONSTANT * gpGlobals->frametime;
		springForceMagnitude = clamp(springForceMagnitude, 0, 2 );
		player->m_Local.m_viewPunchAngleVel -= player->m_Local.m_vecPunchAngle * springForceMagnitude;

		// don't wrap around
		player->m_Local.m_vecPunchAngle.Init(
			clamp(player->m_Local.m_viewPunchAngle->x, -89, 89 ),
			clamp(player->m_Local.m_viewPunchAngle->y, -179, 179 ),
			clamp(player->m_Local.m_viewPunchAngle->z, -89, 89 ) );
	}
	else
	{
		player->m_Local.m_viewPunchAngle.Init( 0, 0, 0 );
		player->m_Local.m_viewPunchAngleVel.Init( 0, 0, 0 );
	}
```

A textbook spring-damper on *angles*: velocity integrates into position, damping bleeds velocity,
and a restoring force proportional to displacement pulls back to centre. The comment is explicit
that **at `PUNCH_DAMPING = 9` the system deliberately overshoots** — the view snaps back past centre
and settles. That overshoot is the "recoil recovery" feel; it's a design choice, not a bug.

The `else` branch is the same discipline seen in the step-smoothing research: below a threshold,
**hard-zero both position and velocity**. No asymptotic crawl, no residual offset.

**The plot twist:** in the CS:GO source, this entire block is commented out and replaced with:

```cpp
void CGameMovement::DecayAngles( QAngle& v, float fExp, float fLin, float dT )
{
	fExp *= dT;
	fLin *= dT;
	v *= expf(-fExp);
	float fMag = v.Length();
	if ( fMag > fLin )
		v *= (1.0f - fLin / fMag);
	else
		v.Init(0.0f, 0.0f, 0.0f);
}

void CGameMovement::DecayViewPunchAngle( void )
{
	QAngle punchAngle = player->m_Local.m_viewPunchAngle;
	DecayAngles(punchAngle, view_punch_decay.GetFloat(), 0.0f, TICK_INTERVAL);
	player->m_Local.m_viewPunchAngle = punchAngle;
}
```

Valve replaced the spring with plain exponential decay, tunable by ConVar. Worth reading the context
before copying the conclusion: in CS:GO the punch is dominated by **weapon recoil**, where a
competitive game wants recovery to be perfectly predictable and monotonic. Overshoot is a liability
when you're compensating recoil by hand. For landing and damage in a single-player movement game,
the overshoot is the *point*. Note also that `DecayAngles` combines an exponential term with an
optional linear term whose only job is to force an exact arrival at zero — the same "must actually
arrive" concern that made `MoveToward` the right call for the crouch.

### 1.3 Source — the landing punch, `CheckFalling`

```cpp
void CGameMovement::CheckFalling( void )
{
	// this function really deals with landing, not falling, so ignore everything else
	if ( player->GetGroundEntity() == NULL || player->m_Local.m_flFallVelocity <= 0 )
		return;

	if ( !IsDead() && player->m_Local.m_flFallVelocity >= PLAYER_FALL_PUNCH_THRESHOLD )
	{
		bool bAlive = true;
		float fvol = 0.5;
		...
			if ( player->m_Local.m_flFallVelocity > PLAYER_MAX_SAFE_FALL_SPEED )
			{
				bAlive = MoveHelper( )->PlayerFallingDamage();
				fvol = 1.0;
			}
			else if ( player->m_Local.m_flFallVelocity > PLAYER_MAX_SAFE_FALL_SPEED / 2 )
				fvol = 0.85;
			else if ( player->m_Local.m_flFallVelocity < PLAYER_MIN_BOUNCE_SPEED )
				fvol = 0;

		PlayerRoughLandingEffects( fvol );
		...
	}
	// Clear the fall velocity so the impact doesn't happen again.
	OnLand(player->m_Local.m_flFallVelocity);
	player->m_Local.m_flFallVelocity = 0;
}

void CGameMovement::PlayerRoughLandingEffects( float fvol )
{
	if ( fvol > 0.0 )
	{
		// Knock the screen around a little bit, temporary effect.
		player->m_Local.m_viewPunchAngle.Set( ROLL, (player->m_Local.m_flFallVelocity - PLAYER_MAX_SAFE_FALL_SPEED) * 0.013 );

		if ( player->m_Local.m_viewPunchAngle[PITCH] > 8 )
			player->m_Local.m_viewPunchAngle.Set( PITCH, 8 );
	}
}
```

Four structural points:

- **`m_flFallVelocity` is accumulated during the fall and consumed on landing.** It cannot be read
  from the velocity at the moment of impact — by then the collision has already zeroed it. This is
  the single most important implementation detail in this document; see §2.4.
- **`OnLand(); m_flFallVelocity = 0;`** — consumed exactly once, unconditionally, outside the `if`.
  A latch, cleared whether or not it fired.
- **A threshold below which nothing happens at all.** `PLAYER_FALL_PUNCH_THRESHOLD`. Ordinary
  hopping does not punch the screen; if it did, the effect would be constant noise and stop reading
  as impact.
- **A hard clamp on the result** (`PITCH > 8` → 8 degrees).

### 1.3.1 The constants, and why they transfer cleanly

`shareddefs.h`, HL2 branch — and the comments give the derivation, which is the gift here:

```cpp
#ifdef HL2_DLL
// HL2 has 600 gravity by default
#define PLAYER_FATAL_FALL_SPEED		922.5f // approx 60 feet sqrt( 2 * gravity * 60 * 12 )
#define PLAYER_MAX_SAFE_FALL_SPEED	526.5f // approx 20 feet sqrt( 2 * gravity * 20 * 12 )
#define PLAYER_LAND_ON_FLOATING_OBJECT	173
#define PLAYER_MIN_BOUNCE_SPEED		173
#define PLAYER_FALL_PUNCH_THRESHOLD 303.0f // ... at least a 76" fall (sqrt( 2 * g * 76))
#else
#define PLAYER_FATAL_FALL_SPEED		1024 // approx 60 feet
#define PLAYER_MAX_SAFE_FALL_SPEED	580  // approx 20 feet
#define PLAYER_FALL_PUNCH_THRESHOLD (float)350
#endif
```

These aren't magic numbers — they're **fall heights** run through `v = sqrt(2·g·h)`. That makes them
fully portable: pick the heights, apply this project's gravity, get metres per second. Converted at
Godot's default `9.8 m/s²`:

| Source constant | As a fall height | This project's threshold |
|---|---|---|
| `PLAYER_FALL_PUNCH_THRESHOLD` | 76 in ≈ 1.93 m | **6.15 m/s** |
| `PLAYER_MAX_SAFE_FALL_SPEED` | 20 ft ≈ 6.10 m | **10.9 m/s** |
| `PLAYER_FATAL_FALL_SPEED` | 60 ft ≈ 18.3 m | **18.9 m/s** |

**Immediately useful sanity check.** `JumpVelocity = 4.5` gives an apex of `4.5² / (2·9.8) ≈ 1.03 m`
and a landing speed of 4.5 m/s. Dropping off a max-height clamber ledge (1.6 m) lands at 5.6 m/s.
Both are **below** the 6.15 m/s punch threshold — so with Source's tuning ported faithfully,
*neither a normal jump nor a clamber drop would punch the camera at all.* That's Valve's deliberate
choice for a grounded shooter and it may well be wrong for this project. See §2.6.

### 1.4 Screen shake — Eiserloh, GDC 2016

From *Math for Game Programmers: Juicing Your Cameras With Math*:

- **Keep a normalised `trauma` in [0,1].** Events *add* trauma (+0.2, +0.5); trauma decays linearly
  over time. Shake is applied as **`trauma²` or `trauma³`**, not `trauma`. The nonlinearity is what
  makes escalation perceptible — a big hit must feel categorically different, not 2× bigger.
- **Use Perlin noise, not `random()`.** Smoothed fractal noise looks better, is continuous under
  pause and slow-motion, has a tunable frequency, and is reproducible for replays. Random per-frame
  jitter reads as a rendering fault.
- **In 3D, rotate — do not translate.** Positional shake in a 3D camera is called out as feeling
  "super lame" and as a discomfort/VR problem. 2D wants both; 3D wants angular only.

```
yaw   = maxYaw   × shake × Perlin(seed,     time)
pitch = maxPitch × shake × Perlin(seed + 1, time)
roll  = maxRoll  × shake × Perlin(seed + 2, time)
```

The 3D-is-rotational rule is a strong endorsement of what Quake and Source already do: both express
impact as **angles**, never as camera translation.

---

## 2. What was implemented

Everything in this section is shipped code. §2.7's damage effect is the one deliberate exception —
only its API exists, because there is no health system to drive it.

### 2.1 One punch system, two callers

Build a single angular impulse channel in `CameraController` and give it one public entry point:

```csharp
public void AddPunch(float pitchDegrees, float rollDegrees);
```

Landing calls it with pitch only. Damage calls it with pitch and roll derived from the hit
direction. Weapon recoil, when it exists, calls the same thing. **Do not build a "landing dip" and a
"damage shake" as separate systems** — that's the mistake this research exists to prevent, and both
Quake and Source demonstrate the merged design.

### 2.2 Angular, not positional

Eiserloh is explicit that 3D positional shake is a mistake, and both reference engines express
impact purely as view angles. So the landing reaction is a **pitch punch** (the head snaps down and
recovers), not a drop in `Position.Y`.

This is a deliberate revision of the note in `CAMERA_JUICE_ANALYSIS.md` that filed the landing dip
as a positional spring. The research says angular. It's also the cheaper option here, because
`Position.Y` is already carrying the crouch, the bob and (proposed) the step lag, whereas the
rotational channel is comparatively empty.

### 2.3 ⚠️ Punch pitch collides with mouse-look in a way roll did not

Roll was free: mouse-look writes `Camera.Rotation.X` and the player's yaw, leaving `Rotation.Z`
unclaimed, which is why `_roll` could simply be written to the node.

**Punch needs pitch, and mouse-look already owns pitch.** Worse, `PlayerController._UnhandledInput`
currently accumulates it *by reading the node back*:

```csharp
Camera.Rotation = Camera.Rotation with
{
    X = Mathf.Clamp(Camera.Rotation.X - motion.Relative.Y * MouseSensitivity, -1.5f, 1.5f)
};
```

If `CameraController` also writes `Rotation.X`, the next mouse motion reads the punched value back
and **integrates the punch permanently into the player's aim**. The view would drift downward every
time they landed.

The fix is the pattern already applied to `_roll` when chasing the jitter: stop reading the node
back. Store look pitch in a field on `PlayerController`, and let `CameraController` compose:

```csharp
// PlayerController._UnhandledInput
LookPitch = Mathf.Clamp(LookPitch - motion.Relative.Y * MouseSensitivity, -1.5f, 1.5f);

// CameraController, composing every view-space rotation in one place
Rotation = new Vector3(_player.LookPitch + _punchPitch, 0f, _roll + _punchRoll);
```

This also finally makes the ownership rule clean and stateable: **`PlayerController` owns input and
body yaw; `CameraController` owns every camera angle.** Worth doing as its own small refactor before
any punch code lands.

### 2.4 ⚠️ The landing velocity is already gone by the time you can detect landing

`GroundedState` is entered when `!IsOnFloor()` stops holding — which is *after* `MoveAndSlide()` has
resolved the floor collision and **zeroed `Velocity.Y`**. Reading the fall speed in
`GroundedState.StateEntered()` will reliably read approximately zero.

This is exactly why Source accumulates `m_flFallVelocity` during the fall rather than sampling on
impact. Port that:

```csharp
// InAirState.StatePhysicsProcessing -- record the worst of the fall
_player.FallSpeed = Mathf.Max(_player.FallSpeed, -_player.Velocity.Y);

// GroundedState.StateEntered -- consume it exactly once, cleared either way
var speed = _player.FallSpeed;
_player.FallSpeed = 0f;
// Negative: the impact drives the head down, and Godot counts positive pitch as looking up.
if (speed > LandPunchThreshold)
    _player.Camera.AddPunch(-(speed - LandPunchThreshold) * LandPunch, 0f);
```

The unconditional clear mirrors Source's `m_flFallVelocity = 0` sitting outside the `if`. Note the
existing `StateEntered()` hook on `State` makes this a natural fit — no new machinery.

There is one wrinkle worth testing rather than assuming: `LocomotingState` is a `ParallelState` and
clambering suspends both regions. Verify that a clamber does not leave stale `FallSpeed` that fires
a punch on the next genuine landing.

### 2.5 Spring, not exponential decay — with eyes open

Use the HL2 damped spring, not the CS:GO exponential decay:

```csharp
_punchVel -= _punch * (PunchSpring * d);       // torsional restoring force
_punchVel *= Mathf.Max(1f - PunchDamping * d, 0f);
_punch += _punchVel * d;
if (_punch.LengthSquared() < 1e-6f && _punchVel.LengthSquared() < 1e-6f)
    _punch = _punchVel = Vector2.Zero;          // hard zero, no asymptotic crawl
```

The overshoot that `PUNCH_DAMPING = 9.0f` deliberately permits is what makes a landing feel like a
landing — the head dips, rebounds slightly past level, and settles. Valve's move to non-overshooting
decay in CS:GO was driven by competitive weapon-recoil predictability, which does not apply to
landing impacts in a single-player movement game.

Keep the hard-zero `else` branch. It's in Quake, it's in Source's spring, and it's the linear term in
CS:GO's `DecayAngles` — three independent implementations all making sure the value *arrives*.

Starting values: `PunchSpring = 65`, `PunchDamping = 9`, straight from Source. They're expressed
per-second against a `frametime` multiply, so they carry over to `delta` in Godot unchanged — no unit
conversion, unlike the fall speeds.

### 2.6 The threshold question — a real design decision, not a default

Ported faithfully, Source's 6.15 m/s threshold means **neither a normal jump (4.5 m/s) nor a drop off
a max clamber ledge (5.6 m/s) punches the camera.** That's correct for Half-Life 2. For a game built
around a clamber/sprint movement kit, landing from every mantle with zero feedback may feel inert.

Two defensible options:

| | Threshold | Effect |
|---|---|---|
| **Faithful** | `6.15` m/s | Only "real" falls register. Jumps and clambers are silent. |
| **Movement-game** | `~4.0` m/s | Every jump landing gets a small punch; clamber drops read as impacts. |

Recommend shipping the **movement-game** value with the threshold `[Export]`ed, and A/B-ing it once
there's a level with verticality. Whichever is chosen, keep the *shape* — a threshold plus a linear
scale above it, plus a hard clamp — because that's what stops small hops turning the effect into
permanent camera noise.

Clamp the result the way Source does (`PITCH > 8` → 8°). A punch that scales without limit turns a
long fall into a camera flip.

### 2.7 Damage — build the API, defer the effect

There is no damage or health system in this project yet, so this is speculative. Build only the
signature so the effect drops in later without rework:

```csharp
// Direction is attacker -> player, world space. Roll from the sideways component, pitch from the
// forward one, exactly as Quake's V_ParseDamage does -- the same DotProduct-against-right idiom
// already used for strafe roll.
public void AddDamagePunch(Vector3 fromDirection, float amount)
{
    var right = _player.GlobalBasis.X;
    var forward = -_player.GlobalBasis.Z;
    AddPunch(amount * fromDirection.Dot(forward) * DamageKickPitch,
             amount * fromDirection.Dot(right)   * DamageKickRoll);
}
```

Quake's `v_kickroll` / `v_kickpitch` are both `0.6`, with a minimum `count` of 10 so small hits still
register. Keep the minimum — feedback that disappears for light damage reads as a dropped input.

**Do not build screen shake yet.** Trauma-and-Perlin (§1.4) is the right design when it's needed, but
it's needed for explosions and area effects, and none exist. Punch covers landing and directional
damage completely. When shake does arrive it should be a *separate* additive channel, because its
math is genuinely different — noise-driven and non-directional versus impulse-driven and
directional — and because `trauma²` decay does not compose with a spring.

### 2.8 Proposed exports

| Knob | Start | Derivation |
|---|---|---|
| `PunchSpring` | `65` | `PUNCH_SPRING_CONSTANT`, unit-free, ports directly. |
| `PunchDamping` | `9` | `PUNCH_DAMPING`. Overshoots deliberately — that's the bounce. |
| `LandPunchThreshold` | `4.0` m/s | §2.6. Faithful port would be `6.15`. |
| `LandPunch` | `1.2` °/(m/s) | ~8° at a 10.9 m/s (20 ft) landing, matching Source's clamp. |
| `MaxPunch` | `8`° | Source's explicit `PITCH > 8` clamp. |
| `DamageKickPitch` / `DamageKickRoll` | `0.6` | Quake's `v_kickpitch` / `v_kickroll`. |

`0` on `LandPunch` and the damage knobs disables each channel — same accessibility posture as the bob
exports, and `smoothstairs` and `v_kicktime` were both tunable in the originals for the same reason.

### 2.9 How to test it

The `PlayerStateTests` harness covers this without new machinery — it already writes
`_body.GlobalPosition` directly, which is how you stage a fall:

- Drop the player from a known height, and assert the camera's pitch deviates from `LookPitch` on
  landing and returns to it. **The measurement trap from the bob work applies again**: measure punch
  against the look pitch, not against a remembered absolute, or the assertion silently measures
  mouse-look.
- Assert the punch **overshoots** — pitch crosses zero at least once before settling. That is the
  behaviour distinguishing the spring from plain decay, and it's the thing that would regress
  invisibly if someone "simplified" it to a lerp.
- Assert a fall below `LandPunchThreshold` produces **no** punch at all.
- Assert `FallSpeed` is zero after landing, and that a clamber does not leave a stale value that
  fires a punch on the next landing (§2.4).
- Assert punch never exceeds `MaxPunch` from an extreme drop.

### Deliberately out of scope

- **Screen shake / trauma system** (§2.7). No explosions exist. YAGNI.
- **Fall damage.** This is about the camera. The thresholds in §1.3.1 are sitting right there if a
  health system arrives.
- **Landing sound and step sounds.** `PlayerRoughLandingEffects` does both in one call, and the
  `fvol` volume tiers (1.0 / 0.85 / 0.5 / 0) are a ready-made mapping when there's audio.
- **Weapon recoil.** Same punch channel, no viewmodel yet.
- **Positional landing dip.** Explicitly rejected in §2.2 on Eiserloh's 3D-rotation-only finding.
  Revisit only if the angular punch alone reads as too weightless.

---

## 3. As built

### Files changed

| File | Change |
|---|---|
| `PlayerController.cs` | `LookPitch` and `FallSpeed`; mouse-look records pitch instead of writing the camera node. |
| `CameraController.cs` | `AddPunch` / `AddDamagePunch`, the spring in `StepPunch`, and one composed `Rotation` write. |
| `Player/States/InAirState.cs` | Accumulates `FallSpeed` on the way down. |
| `Player/States/GroundedState.cs` | `StateEntered` consumes it and punches; `LandPunchThreshold` / `LandPunch` exports. |
| `Player/States/ClamberingState.cs` | Clears `FallSpeed` on entry — a mantle catches the fall. |
| `test_level.tscn` | `JumpPlatform` and `Ramp`. |
| `Player/States/PlayerStateTests.cs` | Five new assertions and a `Drop()` helper. |

The `LookPitch` refactor landed first, as §2.3 required. `CameraController` now makes exactly one
write to `Rotation`:

```csharp
Rotation = new Vector3(_player.LookPitch + _punch.X, 0f, _roll + _punch.Y);
```

### Two guards that the research predicted and the code needed

**Clamber banking a fall.** §2.4 flagged this as "worth testing rather than assuming". It's real: a
clamber is normally entered *from a jump*, so `FallSpeed` is already non-zero when
`LocomotingState` gets suspended, and it would still be banked when `GroundedState` was re-entered
at the top of the mantle — punching the view for a fall that never happened. `ClamberingState.StateEntered`
now zeroes it.

**Null camera at startup.** `StateMachine._Ready` calls `EnterInitialConfiguration()`, and Godot
readies children before parents, so `GroundedState.StateEntered` fires *before*
`PlayerController._Ready` has resolved `Camera`. It is safe only because `FallSpeed` is 0 at that
point and the threshold test short-circuits before touching `Camera`. Worth knowing before anything
else is added to that hook.

### ⚠️ Sign conventions do not survive the port

**Godot counts positive `Rotation.X` as looking UP. Quake and Source both count positive pitch as
looking DOWN.** Verified directly rather than argued:

```
rotation.x =   +20 deg -> forward = (0.0, 0.34202, -0.939693)  => looking UP
rotation.x =   -20 deg -> forward = (0.0, -0.34202, -0.939693) => looking DOWN
```

So Source's `punchAngle.x = flFallVel * 0.001` — positive, and correct there — **inverts on import**.
The first implementation ported the constant literally and kicked the view upward on landing, which
is exactly backwards.

Landing now passes a negative pitch. `AddPunch` is documented as taking Godot's convention, so the
primitive stays unsurprising for future callers: weapon recoil kicks *up* and is positive, a landing
nods *down* and is negative.

The same inversion applies twice over in `AddDamagePunch`, which was also corrected: Quake's `from`
vector points player→attacker (not attacker→player as first written), its positive pitch is down,
and its positive roll is the opposite hand from `Rotation.Z` — which the strafe-roll work had already
established, since that needed `-RollAngle` to lean correctly.

### Measured behaviour

From a temporary probe, since removed — a 6m drop onto the platform:

```
punch down -8.00 / clamp 8     rebound +0.017 deg (at PunchSpring 32.5)
small drop 0.0000 deg          FallSpeed after landing 0.000
```

Negative is the load-bearing sign: the view nods down into the impact and rebounds back past level
before settling. That rebound is the damped spring behaving as Valve's `PUNCH_DAMPING = 9.0f` comment
describes, and is precisely what a "simplify this to a lerp" refactor would silently delete.

Its *size* is a tuning choice, not a fixed property. At the researched `PunchSpring = 65` the damping
ratio is ≈0.56 and the rebound is around 0.6–1.0°; the scene currently runs `32.5`, which raises the
ratio to ≈0.79 and shrinks the rebound to hundredths of a degree — nearly critically damped, so the
view returns almost monotonically. The assertion therefore tests only that the punch crosses zero at
all, which holds for any underdamped setting and fails only if the spring is replaced outright.

### The ramp and platform

- **`JumpPlatform`** — 6×5×6 at `(-9, 2.5, 9)`, so its top is at exactly `y = 5`.
- **`Ramp`** — 6 wide × 0.5 thick × 12 long, pitched 30° about X, top face running from
  `(-9, -1, -4.39)` (buried in the floor) to `(-9, 5, 6)` (flush with the platform edge).

Placed in the empty `-X / +Z` quadrant, clear of the clamber ledge, the overhang, the spawn point,
and the paths the existing tests walk and teleport through.

A 5m drop off the platform lands at 9.9 m/s → ~7.1° of punch. Jumping off the edge first adds the
1.03m jump apex, which reaches the 8° clamp.

**Two `.tscn` gotchas worth writing down**, both of which produced a silently wrong ramp on the first
attempt — a 4.13° slope, floating, connected to nothing:

1. **`Transform3D` in a `.tscn` is serialised row-major**, not as the three basis columns. The
   literal is `(xx, yx, zx, xy, yy, zy, xz, yz, zz, ox, oy, oz)`.
2. **`Basis.scaled()` scales rows, not axes.** For "rotate a box of this size", use
   `rot * Basis.from_scale(size)`. `rot.scaled(size)` shears it.

Both were caught by raycasting the ramp's centreline and printing the surface profile, which is the
only reason the error was visible at all — the geometry looked plausible in the file. Verified after
the fix: continuous from `y=0.000` to `y=5.000` at a constant `30.00°`, well inside the default
`floor_max_angle` of 45°.

### Tests

Six new assertions in `PlayerStateTests`, all passing:

| Assertion | Guards against |
|---|---|
| `landing pitches the view down` | The path from `InAirState` to `AddPunch` being dead — **and the sign being inverted**. |
| `punch respects MaxPunch` | An unbounded fall flipping the camera. |
| `punch rebounds back past level` | The spring being "simplified" into a decay. |
| `FallSpeed is consumed on landing` | A stale fall leaking into the next landing. |
| `a drop under the threshold does not punch` | Every hop turning the effect into noise. |
| `walking up the ramp reaches the platform` | The new geometry being unwalkable. |

Two scheduling notes. The sub-threshold drop is staged at frame 560 rather than 500 because the
spring's envelope still carries ~0.4° half a second after the previous landing — at frame 500 that
read as a false positive, and the later staging now doubles as an assertion that the punch fully
rings out. The churn cap rose from 16 to 24 to account for two extra ground→air→ground round trips.

All three suites pass: `test_player_states`, `test_clamber`, `test_state_machine`.

---

## Sources

- [Quake `WinQuake/view.c`](https://github.com/defunkt/quake/blob/master/WinQuake/view.c) — `V_ParseDamage`, `V_CalcViewRoll`, `v_kicktime` / `v_kickroll` / `v_kickpitch`
- [`cstrike15_src/game/shared/gamemovement.cpp`](https://github.com/perilouswithadollarsign/cstrike15_src/blob/master/game/shared/gamemovement.cpp) — `CheckFalling`, `PlayerRoughLandingEffects`, `DecayViewPunchAngle`, `DecayAngles`, `PUNCH_DAMPING` / `PUNCH_SPRING_CONSTANT`
- [`cstrike15_src/game/shared/shareddefs.h`](https://github.com/perilouswithadollarsign/cstrike15_src/blob/master/game/shared/shareddefs.h) — fall speed constants and their `sqrt(2·g·h)` derivations
- [Source SDK `baseplayer_shared.cpp`](https://github.com/pmrowla/hl2sdk-csgo/blob/master/game/shared/baseplayer_shared.cpp)
- [Squirrel Eiserloh — *Math for Game Programmers: Juicing Your Cameras With Math*, GDC 2016](https://archive.org/details/GDC2016Eiserloh) ([slides](http://www.mathforgameprogrammers.com/gdc2016/GDC2016_Eiserloh_Squirrel_JuicingYourCameras.pdf) · [video](https://www.youtube.com/watch?v=tu-Qe66AvtY)) — trauma², Perlin over random, 3D rotational-only
- [Valve Developer Community — ViewPunch](https://developer.valvesoftware.com/wiki/ViewPunch)
- [Ryan Juckett — Damped Springs](https://www.ryanjuckett.com/damped-springs/) · [Game Developer — Springs Explained](https://www.gamedeveloper.com/game-platforms/instant-game-feel---springs-explained)

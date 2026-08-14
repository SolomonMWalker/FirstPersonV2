# Head bob and movement roll â€” research & recommendation

Research into how shipped FPS games implement head bob and the camera roll that leans into
strafing, and what that should look like in *this* project.

**Section 1** is the research. **Section 2** is the implementation, which is built and merged into
`CameraController.cs`. **Section 3** covers the tests and what they caught.

---

## 1. What the shipped games actually do

### Quake (1996) â€” the original, and still the template

`WinQuake/view.c` is the ancestor of nearly every implementation since. Two separate functions,
two separate ideas.

**Bob** â€” `V_CalcBob()`:

```c
cvar_t cl_bob      = {"cl_bob",      "0.02"};   // amplitude per unit of speed
cvar_t cl_bobcycle = {"cl_bobcycle", "0.6"};    // seconds per full cycle
cvar_t cl_bobup    = {"cl_bobup",    "0.5"};    // fraction of the cycle spent rising

cycle = cl.time - (int)(cl.time/cl_bobcycle.value)*cl_bobcycle.value;
cycle /= cl_bobcycle.value;
if (cycle < cl_bobup.value)
    cycle = M_PI * cycle / cl_bobup.value;
else
    cycle = M_PI + M_PI*(cycle-cl_bobup.value)/(1.0 - cl_bobup.value);

bob = sqrt(cl.velocity[0]*cl.velocity[0] + cl.velocity[1]*cl.velocity[1]) * cl_bob.value;
bob = bob*0.3 + bob*0.7*sin(cycle);
if (bob > 4)  bob = 4;
else if (bob < -7) bob = -7;
```

Four things worth stealing from ten lines of 1996 C:

1. **Amplitude is proportional to horizontal speed.** `sqrt(vxÂ² + vyÂ²)`, Z deliberately excluded so
   jumping doesn't inflate the bob. Stand still â†’ amplitude zero â†’ bob disappears with no explicit
   "am I walking" check anywhere.
2. **`bob*0.3 + bob*0.7*sin(cycle)`** â€” a DC offset plus the oscillation. The eye sits slightly
   *above* neutral while moving and dips through it, rather than swinging symmetrically. Cheap, and
   it reads as "leaning into the run".
3. **`cl_bobup` skews the waveform.** The rise and fall get different fractions of the cycle, so it
   isn't a pure sine â€” footfalls land harder than the recovery. This is the single biggest
   contributor to bob feeling like *steps* instead of like a boat.
4. **Hard clamps.** `[-7, 4]` units. No matter how fast you go, the view never leaves a sane box.

Applied in `V_CalcRefdef` to the view origin, and to the gun at `bob*0.4` along forward + `bob` on Z
â€” **the weapon bobs more than the eye does**, and along a different axis.

**Roll** â€” `V_CalcRoll()`:

```c
cvar_t cl_rollspeed = {"cl_rollspeed", "200"};  // sideways speed at which roll maxes out
cvar_t cl_rollangle = {"cl_rollangle", "2.0"};  // degrees at max

side = DotProduct (velocity, right);            // how much of the velocity is sideways
sign = side < 0 ? -1 : 1;
side = fabs(side);
if (side < cl_rollspeed.value)
    side = side * cl_rollangle.value / cl_rollspeed.value;   // ramp
else
    side = cl_rollangle.value;                                // clamp
return side*sign;
```

Note what this is *not*: it is not driven by input keys, and it is not an animation. It's a pure
function of `dot(velocity, right)` â€” the projection of actual velocity onto the player's right
vector. That means it works for free with air control, knockback, sliding, moving platforms, and
diagonal movement (which gets a partial roll, correctly). Two degrees. That's all it ever was.

Half-Life shipped the same function with `cl_rollangle = 0.65` â€” a third of Quake's. Quake wanted
arcade, Half-Life wanted grounded.

Quake also has `V_AddIdle()`, which adds `sin(t)` on all three axes scaled by `v_idlescale` â€” the
drunk/underwater sway. Zero by default, but it's the same primitive and worth knowing about for a
future "exhausted" or "concussed" state.

### Source / Half-Life 2 â€” the bob moved to the gun

Source's constants (`basehlcombatweapon_shared.cpp`):

```cpp
#define HL2_BOB_CYCLE_MIN  1.0f
#define HL2_BOB_CYCLE_MAX  0.45f    // cycle shortens as you speed up
#define HL2_BOB            0.002f
#define HL2_BOB_UP         0.5f
```

`CalcViewmodelBob()` computes **two** channels â€” `g_verticalBob` and `g_lateralBob` â€” each
`speed * 0.005f`, each shaped by `0.3 + 0.7*sin(cycle)` and clamped to `[-7, 4]`, exactly the Quake
shape. `AddViewmodelBob()` then applies them:

```cpp
VectorMA( origin, g_verticalBob * 0.1f, forward, origin );
origin[2] += g_verticalBob * 0.1f;
VectorMA( origin, g_lateralBob * 0.8f, right, origin );
// plus roll/pitch/yaw nudges at 0.5f / 0.4f / 0.3f of the bob values
```

Two structural lessons:

- **Vertical and lateral together trace a figure-8.** Lateral runs at half the vertical frequency
  (one sway per stride, two vertical dips â€” left foot, right foot). This is why HL2's bob reads as
  *walking* and a single sine reads as *floating*. It's the single highest-value detail in this
  whole document.
- **Position bob is accompanied by small rotation bob.** Roll/pitch/yaw at 30â€“50% of the positional
  amount. Translation alone looks like a camera on a rail; adding a few tenths of a degree of
  rotation makes it a head on a neck.
- **The cycle time shortens with speed** (`CYCLE_MIN` â†’ `CYCLE_MAX`) rather than staying fixed.
  Running takes faster steps, not just bigger ones.

Crucially, HL2's *camera* bob is near-zero â€” almost everything you perceive as bob is the weapon
model. That's a deliberate motion-sickness tradeoff: the viewmodel occupies the lower third of the
screen, so bobbing it sells physicality without moving the horizon.

### Titanfall 2 / Apex â€” Source lineage, tuned for speed

Both are Source derivatives and inherit this exact machinery. What Respawn changed is the *weighting*:
roll is pushed hard during wallrunning and slides (well past 2Â°), while walk bob stays subtle. The
takeaway is that roll is the channel that scales up gracefully for special movement states â€” bob does
not. If clamber/slide/wallrun get camera treatment later, roll and FOV are the knobs, not bob amplitude.

### Destiny / Overwatch (GDC animation talks)

The relevant principle, stated in Bungie's and Blizzard's first-person animation talks: **camera
animation for persistent physical states (locomotion, breathing, exertion), screen-space effects for
momentary events (damage, impact, landing).** Continuous vs. impulse. Don't implement a landing
thump by spiking the bob amplitude â€” it's a different system with different lifetime.

### Accessibility â€” not optional, and it's one export away

This came up in every source consulted. Head bob is a top-three motion-sickness trigger (vestibular
mismatch: eyes see motion, inner ear reports none). Practical consensus:

- Ship a **slider, not a toggle** â€” 0 to ~150% of default, where 0 fully disables.
- Most games land around **30â€“60% of "realistic"** amplitude. Physically accurate head motion is
  nauseating.
- Roll is *less* nauseating than vertical bob and contributes more to the feel of physicality per
  unit of discomfort. If forced to pick one, pick roll.

Building this as `[Export]` floats where `0` cleanly disables each channel costs nothing now and is
annoying to retrofit.

---

## 2. What was implemented

**Status: built, tested, passing.** All of section 2 below describes shipped code, not a proposal.
Everything lives in `CameraController.cs`; nothing was added to the state machine, `PlayerController`,
or any `.tscn`.

### Where it went

`CameraController.cs`. It already owned "camera movement beyond mouse-look" and already had the
`_standY` / `MoveToward` shape bob needed. `PlayerController._UnhandledInput` still owns mouse-look.

The old invariant was *"PlayerController touches Rotation, CameraController touches Position, so they
never fight."* Roll needed `Rotation.Z`, so the class comment was rewritten to state the real
division rather than the old approximation:

> That one writes the player's yaw and this camera's `Rotation.X`; this one writes `Position` and
> `Rotation.Z`, so the two still never fight.

### âš ï¸ The integration hazard â€” confirmed real, fixed as designed

`CrouchOffset => _standY - Position.Y` is consumed by `PlayerController.ApplyCrouchHeight()` to
resize the capsule *and* by `ClamberController.HeightScale`. **If bob writes into `Position.Y`, the
collision capsule will oscillate with the head bob** â€” resizing several times a second, jittering
clamber reach, and potentially popping the player off the floor.

Fixed with one field, exactly as planned: `_eyeY` holds the bob-free eye height â€” standing, crouched,
or easing between the two â€” `CrouchOffset` derives from *that* rather than from the live node, and
bob is added at the last moment.

```csharp
private float _eyeY;                             // bob-free eye height, whatever the crouch state
public float CrouchOffset => _standY - _eyeY;    // bob no longer leaks into the capsule
// ...
Position = Position with { Y = _eyeY + bobY };   // bob applied to the node only
```

The test suite confirms it: `crouch dip 0.500m` measured against a `CrouchDrop` of exactly `0.5`,
with bob running at the same time.

### The design, in four decisions

**1. Phase by distance travelled, not by time.** Quake advances the cycle on `cl.time` and scales
amplitude by speed; Source patches around the resulting mismatch by interpolating cycle length with
speed. Advancing the phase by `speed * delta / stride` gets the same result in one line, and steps
stay locked to ground actually covered â€” accelerating shortens the stride instead of jumping the sine.

**2. Amplitude from speed, not from the state machine.** Tempting to hang bob off `WalkingState` /
`SprintingState`, but that's three states to keep in sync and it breaks for anything that moves the
player without a state (clamber, knockback). `velocity.Length()` already encodes all of it, and
sprinting is `Speed * 1.4f`, so sprint bob scales automatically. **Nothing new to wire into the state
machine.**

**3. Vertical at 2Ã— the lateral frequency.** The figure-8. `sin(2Î¸)` and `sin(Î¸)` â€” a free character
upgrade over a lone sine.

**4. Smooth the amplitude.** Project-specific: `WalkingState` writes velocity straight from input with
no acceleration or friction, so `speed` is a *step function* â€” release W and the bob offset would snap
to centre with a visible pop. One exponential smooth on the amplitude fixes it and doubles as the
graceful fade when going airborne.

### The code, as built

The whole feature is the body of `CameraController._PhysicsProcess`. Reproduced here as it actually
ships (comments trimmed â€” see `CameraController.cs` for the full versions):

```csharp
[Export] public float BobAmount = 0.045f;  // metres, peak vertical at full speed. 0 disables.
[Export] public float BobStride = 1.5f;    // metres of travel per full bob cycle
[Export] public float BobSway   = 0.6f;    // lateral bob as a fraction of vertical
[Export] public float RollAngle = 2.0f;    // degrees at full sideways speed. 0 disables.
[Export] public float Smoothing = 8f;      // how fast roll and bob amplitude chase their targets

private PlayerController _player;
private float _standX, _standY;
private float _eyeY, _bobPhase, _bobAmp;

public override void _Ready()
{
    _player = PlayerController.Of(this);
    _standX = Position.X;                  // bob writes X too, so the authored offset is preserved
    _standY = _eyeY = Position.Y;
}

public override void _PhysicsProcess(double delta)
{
    var d = (float)delta;
    var crouchTarget = Crouched ? _standY - CrouchDrop : _standY;
    _eyeY = Mathf.MoveToward(_eyeY, crouchTarget, CrouchSpeed * d);

    var velocity = _player.Velocity with { Y = 0 };   // Y excluded so jumping doesn't inflate bob
    var speed = velocity.Length();
    var grounded = _player.IsOnFloor();

    // Phase advances with distance, not time: steps stay locked to ground actually covered, so
    // speeding up shortens the stride instead of jumping the sine.
    // Wrapped, so the phase cannot drift into the range where float precision coarsens the sine.
    if (grounded) _bobPhase = Mathf.Wrap(_bobPhase + speed * d / BobStride * Mathf.Tau, 0f, Mathf.Tau);

    // Exponential, not Lerp(x, y, rate*delta): this one is actually framerate-independent.
    var chase = 1f - Mathf.Exp(-Smoothing * d);
    // Amplitude rides on speed, so bob fades out when standing still without asking the state
    // machine anything. Smoothed because velocity here is a step function -- no friction.
    _bobAmp = Mathf.Lerp(_bobAmp, grounded ? BobAmount * Mathf.Min(speed / _player.Speed, 1f) : 0f, chase);

    // Vertical at twice the lateral rate: two footfalls per stride. The figure-8.
    Position = Position with
    {
        X = _standX + Mathf.Sin(_bobPhase) * _bobAmp * BobSway,
        Y = _eyeY + Mathf.Sin(_bobPhase * 2f) * _bobAmp,
    };

    // Quake's V_CalcRoll: how much of the velocity is sideways, ramped and clamped. Driven by
    // velocity rather than input, so air control and knockback lean correctly for free.
    // Rotation.Z is untouched by mouse-look (which writes only X), so the two still never fight.
    var side = Mathf.Clamp(velocity.Dot(_player.GlobalBasis.X) / _player.Speed, -1f, 1f);
    Rotation = Rotation with
    {
        Z = Mathf.Lerp(Rotation.Z, Mathf.DegToRad(-RollAngle) * side, chase),
    };
}
```

Sign on `RollAngle` is taste â€” flip it if strafing right should lean the other way. Try both; they
feel genuinely different and neither is wrong.

### Two things the research plan didn't anticipate

**`_standX`.** Lateral bob writes `Position.X`, which the plan wrote as a bare assignment. The
camera is authored at `(0, 0.5, 0)` so it made no difference today, but any future X offset on the
camera node would have been silently yanked to zero. One field, and the authored offset survives.

**`Mathf.Wrap` on the phase.** `_bobPhase` accumulates forever, and a float large enough eventually
quantises `sin()` into visible steps. Wrapping to `[0, Ï„)` is free and sine is periodic anyway.

### Shipped values, and how to tune

All five are `[Export]`, so they're inspector-tunable per scene, and `0` cleanly disables each
channel â€” the accessibility requirement from section 1, satisfied by construction.

| Knob | Shipped | Notes |
|---|---|---|
| `BobAmount` | `0.045` m | ~4.5cm peak. Quake's clamp works out around 0.1m; halved, per the 30â€“60% guidance. |
| `BobStride` | `1.5` m | At `Speed = 5`, â‰ˆ0.6s per cycle â€” the same rhythm as `cl_bobcycle`. |
| `BobSway` | `0.6` | HL2 weights lateral high (`0.8f` vs `0.1f`); on the camera it needs to be gentler. |
| `RollAngle` | `2.0`Â° | Quake's default. Half-Life used `0.65` for a grounded feel â€” try both. |
| `Smoothing` | `8` | ~0.12s to settle. Lower feels drunk, higher feels twitchy. |

These are research-derived starting points, **not playtested numbers** â€” they have been verified to
behave correctly, not to feel good. Tune bob with roll at 0 and vice versa; together they mask each
other's problems.

### Deliberately skipped

- **`cl_bobup` waveform skew.** Real, and it's what makes footfalls *land*. Two extra lines, but
  only worth adding once the plain figure-8 is tuned and still feels too smooth.
- **Landing dip / spring.** Per the Destiny principle this is an *impulse* system, not part of bob â€”
  it belongs on the `InAir â†’ Grounded` transition, scaled by impact velocity, as a separate decaying
  offset. A critically damped spring (Ryan Juckett / Unity's `SmoothDamp`) is the standard tool.
  Add it after bob lands, not with it.
- **Sprint FOV kick.** The cheapest remaining juice, and it's a `SprintingState` enter/exit lerp on
  `Camera3D.Fov` â€” genuinely unrelated to this system.
- **Rotational bob** (HL2's roll/pitch/yaw nudges at 0.3â€“0.5Ã— the positional bob). Add if positional
  bob still reads as rail-mounted after tuning.
- **Weapon-model bob.** No viewmodel in the project yet. When there is one, HL2's lesson applies:
  bob the gun harder than the eye and much of the camera bob can come back down.

---

## 3. Tests, and what they caught

### Changes to `Player/States/PlayerStateTests.cs`

The existing crouch assertion sampled `Cam().Position.Y` at frame 98 â€” while the player is walking
backwards, so with bob now on the node that reading is crouch *plus* bob. Switched to
`Cam().CrouchOffset`, which is the bob-free value and also the one that actually drives the capsule,
so the assertion got stricter rather than looser:

```csharp
if (_frame == 98) { _crouchDip = Cam().CrouchOffset; _crouchHeight = Capsule().Height; return; }
// ...
Near(drop, _crouchDip, "crouch lowers the camera");
```

Four new assertions, plus a `True(bool, string)` helper alongside the existing `Is`/`Near`/`Contains`:

| Assertion | Why it's the one worth having |
|---|---|
| `walking bobs the camera` | The system is on at all. |
| `bob stays within BobAmount` | The upper bound â€” the export must actually be a ceiling. |
| `strafing rolls the view` | Roll fires, and in the correct direction (negative for a right strafe). |
| `walking straight keeps the view level` | The `dot(velocity, right)` projection isn't leaking forward motion into roll. |

Driven by a new strafe window at frames 370â€“390: press `D` on open ground, track peak bob, sample
roll, release.

### The bug the tests caught

First run failed on `bob stays within BobAmount (peak 0.2917m)` â€” nearly 7Ã— the 0.045 ceiling. Not a
bob bug: the window opened at frame 370 while the frame-365 stand-up was still easing (0.5m at
2.5 m/s = 0.2s = ~12 frames), so it was measuring the crouch recovery. The measurement was wrong,
not the code. Fixed by measuring bob against the live crouch height rather than against stand height:

```csharp
var eye = _standCamY - Cam().CrouchOffset;
_maxBob = Mathf.Max(_maxBob, Mathf.Abs(Cam().Position.Y - eye));
```

Worth recording because it's the same confusion the `_eyeY` split exists to prevent â€” "eye
height" and "bob offset" are different quantities and mixing them reads plausible right up until
the number comes out 7Ã— too big.

### Measured behaviour

Values printed from a temporary probe, then removed:

```
peak bob 0.0395m / BobAmount 0.045    strafe roll -1.82 deg / RollAngle 2.0
straight roll 0.000 deg               crouch dip 0.500m / CrouchDrop 0.5
```

Bob and roll both approach their ceilings without reaching them â€” correct, since `Smoothing = 8`
means the amplitude is still chasing over a ~0.3s window. Straight-line roll is exactly zero and the
crouch dip is exactly `CrouchDrop`, which is the capsule-isolation fix demonstrated end to end.

### âš ï¸ Running the tests

**`godot --headless` does not rebuild the C# assembly.** The first run of this session reported
"all passed" against a stale DLL that predated every change. Always build first:

```
dotnet build FirstPersonV2.sln
& "C:\Godot\Godot_v4.7-stable_mono_win64\Godot_v4.7-stable_mono_win64_console.exe" --headless --path . res://Tests/test_player_states.tscn
```

Use `..._console.exe`, not `Godot_v4.7-stable_mono_win64.exe` â€” the non-console binary detaches and
prints nothing to the terminal.

`PlayerStateTests._Ready` pins `Engine.PhysicsTicksPerSecond = 60`. Every frame number in that suite
is really a duration â€” crouch easing, clamber flight and the camera smoothing all run on wall-clock
seconds â€” so inheriting the project's tick rate made the schedule mean different things at different
settings. Raising the project to 120Hz (see below) broke `clambering does not cancel the sprint`
before the pin went in.

### Follow-up: the 60Hz judder

Head bob made an existing problem visible. Physics ran at Godot's default 60Hz with rendering
uncapped, so the camera transform only changed 60Ã—/second while frames drew at monitor refresh â€”
each position held for an uneven number of frames. Before bob, the camera's local position was
static while walking, so nothing made it obvious.

Fixed with one project setting, `physics/common/physics_ticks_per_second=120`. Physics interpolation
(`physics/common/physics_interpolation`, 3D support restored in Godot 4.4) is the more thorough fix
for world motion, but it interpolates the camera too â€” and with mouse-look living in
`_UnhandledInput`, that would add latency to aiming and trip Godot's "Interpolated Camera3D
triggered from outside physics process" warning. Not worth it here.

Also removed a second, narrower jitter source while in there: the roll lerp used to read `Rotation.Z`
back off the node each tick, which decomposes the basis to Euler angles. Mouse-look clamps pitch to
Â±85.9Â°, close enough to gimbal lock for that decomposition to be ill-conditioned, so roll now lives
in a `_roll` field and the node is only ever written.

Final state of all three suites, against a fresh build:

| Suite | Result |
|---|---|
| `res://Tests/test_player_states.tscn` | all passed (exit 0) |
| `res://Tests/test_clamber.tscn` | all passed (exit 0) |
| `res://Tests/test_state_machine.tscn` | all passed (exit 0) |

`test_state_machine` prints a `StateMachine exceeded 64 transitions` error with a stack trace on the
way through â€” that's `RunawayGuardTripsTheCap` deliberately tripping the loop cap, and the suite
passes.

---

## Sources

- [Quake `WinQuake/view.c`](https://github.com/defunkt/quake/blob/master/WinQuake/view.c) â€” `V_CalcBob`, `V_CalcRoll`, `V_AddIdle`, cvar defaults
- [Valve Developer Community â€” Camera Bob](https://developer.valvesoftware.com/wiki/Camera_Bob)
- [Source SDK `basehlcombatweapon_shared.cpp`](https://swarm.workshop.perforce.com/files/guest/knut_wikstrom/ValveSDKCodegame_shared/hl2/basehlcombatweapon_shared.cpp) â€” `CalcViewmodelBob` / `AddViewmodelBob`
- [TWHL â€” VERC: View Roll When Strafing (like DMC)](https://twhl.info/wiki/page/VERC:_View_Roll_When_Strafing_(like_DMC)) â€” Half-Life's `cl_rollangle = 0.65` / `cl_rollspeed = 300`
- [TWHL â€” Tutorial: View bobbing](https://twhl.info/wiki/page/Tutorial:_View_bobbing:_Part_1)
- [GameDev.net â€” How to make camera roll whilst strafing](https://www.gamedev.net/forums/topic/632823-how-to-make-camera-roll-whilst-strafing/4990366/)
- [GDC Vault â€” The Art of First Person Animation for Destiny](https://www.gdcvault.com/play/1022297/The-Art-of-First-Person)
- [GDC Vault â€” Animation Bootcamp: The First Person Animation of Overwatch](https://gdcvault.com/play/1024319/Animation-Bootcamp-The-First-Person)
- [Ryan Juckett â€” Damped Springs](https://www.ryanjuckett.com/damped-springs/) â€” for the landing dip later
- [Game Developer â€” Instant Game Feel: Springs Explained](https://www.gamedeveloper.com/game-platforms/instant-game-feel---springs-explained)
- [Alleviating motion sickness in first-person video games](https://nicolas.busseneau.fr/en/blog/2020/09/alleviating-motion-sickness-in-first-person-video-games)
- [Godot forum â€” smooth camera bob system](https://forum.godotengine.org/t/how-to-make-a-smooth-camera-bob-system/90613)

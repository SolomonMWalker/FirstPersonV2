# ClamberController â€” Analysis, Research, and Recommendations

Analysis of `ClamberController.cs` as pulled in, plus a comparison against how this problem is
solved elsewhere, and what I'd change.

---

## Part 1 â€” How the original worked

> Parts 1â€“4 analyse the class **as it was pulled in**. It has since been rewritten along the lines
> Part 4 recommends â€” see **Part 5** for how the code works today.

### The pipeline

```
TryHandleClamber()          entry point, called once to decide "can we clamber?"
  â””â”€ AttemptClamber()       cooldown gate + iterate rows
       â””â”€ AttemptClamberCheckRow(row)   per-row decision
            â””â”€ GetRaycastEndPoints(row) read raycasts â†’ (localSlice, globalEndpoint, collided)
  â†’ caches ClamberDestination / StartPoint / XzDirection / XzDistanceSquared

Clamber()                   called every physics frame while PlayerController.Clambering
  â”œâ”€ phase 1: feet below destination.Y + margin â†’ Velocity = up * ClamberVelocity
  â”œâ”€ phase 2: XZ travelled < XZ distance      â†’ Velocity = dir * ClamberVelocity
  â””â”€ else: Clambering = false
```

### The detection model

The rig is a `Raycasts` node holding N child nodes, each holding M `RayCast3D`s â€” a **grid of
forward-facing rays**, rows stacked vertically. For one row:

1. Read every ray's endpoint (real collision point if hit, nominal target position if not).
2. If nothing hit â†’ fail.
3. Compute `maxY` over **all** endpoints. If any *collided* ray sits at `maxY` â†’ fail
   ("the top ray hit, so the wall is too tall").
4. Otherwise take the **highest colliding** point as the clamber destination.

So the model is: *find the vertical extent of the wall's front face; if the face stops below the
top of the ray fan, that stopping height is where we clamber to.*

### The execution model

Strictly two axis-locked phases: rise until the feet clear `destination.Y + ClamberMargin`, then
translate horizontally until the XZ distance travelled matches the XZ distance measured at
detect-time. Both phases drive `PlayerController.Velocity` directly and call `MoveAndSlide()`.

**The one genuinely good decision in here:** collision stays on the whole time. `MoveAndSlide` is
authoritative for the entire manoeuvre. Most tutorials disable collision or lerp `GlobalPosition`
and then spend the rest of their life fighting "player is inside a wall" bugs. You don't have that
class of bug. Keep this property through any rewrite.

---

## Part 2 â€” Bugs and gaps

### Won't compile (expected â€” this came from another project)

| # | Issue |
|---|---|
| B1 | `using FirstPerson.Scenes.Player;` â€” namespace doesn't exist in this repo. `PlayerController` is global-namespace here. |
| B2 | `PlayerController.BottomOfPlayer`, `.ClamberVelocity`, `.Clambering` â€” none exist on the current `PlayerController.cs`. |
| B3 | `[Export] public PlayerController PlayerController;` â€” field named identically to its type. Legal C#, but every future reference is ambiguous to read. |

### Soft-lock â€” this one is a hard bug

**`Clamber()` has no timeout and no abort condition.** If phase 1 can't complete â€” a `RigidBody3D`
shoves you, the ledge is on a platform that moved, geometry snags the capsule, gravity is off so
you don't fall out of it â€” `Clambering` never becomes `false`. The player is frozen, weightless,
permanently. There is no escape path in the code.

Every real implementation has a hard duration cap. This is the first thing to add regardless of
what else changes.

### Detection correctness

**D1 â€” Nothing verifies there is a surface to stand on.**
The rays only ever see the wall's **front face**. The code infers "the face stops at height Y,
therefore Y is a ledge." That inference is wrong for:

- **Railings, fence rails, pipes, chain-link, thin signage.** Topmost front-face hit is the top of
  the rail. You clamber up onto a 3cm-deep surface and immediately fall. This is the single most
  common failure this design will produce in a real level.
- **Overhangs.** The face stops because the geometry recedes, not because it ends.
- **Sloped tops.** A 70Â° wedge reads as a perfectly good clamber target.

Every other implementation surveyed solves this with a **downward trace** from above the detected
lip. You have no downward trace.

**D2 â€” No surface-normal / floor-angle validation.** Related to D1 but separate: even given a real
top surface, nothing checks it's walkable. `CharacterBody3D` already exposes `FloorMaxAngle`; the
check is one line and it isn't there.

**D3 â€” No capsule-fit / headroom check.** Nothing confirms the player's collision shape fits at the
destination. Clamber into a crawlspace and you drive the capsule into a ceiling. `MoveAndSlide`
will resolve it *somehow* â€” which is exactly the problem, the resolution is arbitrary.

**D4 â€” Height resolution is quantized to ray spacing.** With rays every 0.25m, a ledge at 1.13m
reports as 1.0m (the highest ray that hit). You then rise to `1.0 + ClamberMargin (0.26) = 1.26m`,
which happens to clear it â€” but only because the margin is coincidentally larger than the spacing.
Change either constant independently and clamber silently starts putting feet inside geometry.
**Nothing in the code ties `ClamberMargin`, `RaycastLength`, and the rig's vertical spacing
together, and nothing validates the rig at `_Ready`.**

**D5 â€” The `maxY` epsilon comparison is a fragile stand-in for "did the top ray hit".**

```csharp
var maxY = rawCollisions.Select(rc => rc.localSlice.Y).Max();
if (collidedCollisions.Any(rc => Math.Abs(rc.localSlice.Y - maxY) < 0.0001f)) return (false, null);
```

This compares **collision-point Y** against the max **endpoint Y**. It only works because the rays
are assumed exactly horizontal, so a ray's Y is constant along its length. Tilt any ray â€” or parent
the rig to something that pitches â€” and it breaks silently. The thing it's actually asking is
`raycasts.Last().IsColliding()`.

**D6 â€” `ToLocal()` assumes this node is unrotated.** All the Y reasoning happens in
`ClamberController`'s local space. Parent this under a pitching camera/head and "up" stops being up.
Currently unenforced and undocumented.

**D7 â€” The XZ destination is the wall face, not the ledge.** `ClamberDestination` comes from a
front-face collision point. Phase 2 stops when the capsule *centre* reaches the wall plane â€” half
the capsule still overhangs. It works only because `MoveAndSlide` shoves you out afterwards. There's
no explicit "push past the lip by X" depth parameter, so final standing position varies with capsule
radius.

**D8 â€” Row iteration order is scene-tree order, and first hit wins.** Which row is checked first is
whatever the editor's child ordering happens to be. Unvalidated, invisible, and silently reorderable
by anyone dragging nodes. If rows are meant as left/centre/right columns, first-hit means you
systematically favour one side rather than picking the nearest or best candidate.

**D9 â€” Raycasts are read without `ForceRaycastUpdate()`.** `RayCast3D` results reflect the state at
the start of the physics step. Yaw fast into a ledge and you're testing last frame's orientation.
Either force the update before querying or make the one-frame lag a deliberate, commented choice.

**D10 â€” Destination is a world position captured once.** Moving platforms, elevators, rotating
doors: you clamber to where the ledge *was*.

**D11 â€” Rays run every frame regardless of state.** NÃ—M casts even while walking on flat ground
with nothing in front of you. Cheap individually, free to avoid.

### Cooldown design â€” this is the worst *feel* bug

```csharp
private (bool, RaycastCollisionResult) AttemptClamber()
{
    if (!PauseBetweenClamberAttemptsTimer.IsStopped()) return (false, null);
    PauseBetweenClamberAttemptsTimer.Start();
    ...
}
```

The cooldown starts on **every attempt, including failures**. So a *failed query* blacks out
detection entirely. And `WaitPerCallInSec` is **exported but never assigned to the Timer's
`WaitTime`** â€” so unless it's set in the scene, the cooldown is Godot's default **1.0 second**, not
0.25.

Player-visible consequence: run at a ledge, have the poll land 5ms before you're in range, and get a
full second of nothing. It reads as "the game ignored my input," which is the worst possible failure
mode for a traversal mechanic.

**Detection should run every physics frame.** A cooldown belongs after a *completed or aborted
clamber*, not after a failed *question*.

### Execution correctness

**E1 â€” No velocity handoff on exit.** `Clambering = false` leaves `Velocity` at the last horizontal
clamber velocity, full magnitude, zero Y. The player is ejected forward off the ledge at
`ClamberVelocity`.

**E2 â€” Phase-2 progress is measured from the start point, not toward the target.**

```csharp
ClamberXzDistanceSquared > ClamberStartPointXz.DistanceSquaredTo(currentXz)
```

This measures *distance from origin*, which grows if the player is pushed sideways â€” the phase
terminates without ever approaching the ledge. Correct metric is distance **to the destination**, or
`dot(direction, remaining) > 0`.

**E3 â€” Mixed reference frames.** Phase 1 measures `PlayerController.BottomOfPlayer.GlobalPosition.Y`
(the feet). Phase 2 measures `this.GlobalPosition` (the rig node). If the rig isn't at the player's
centre, phase 2's travel distance is offset by that difference.

**E4 â€” `MoveAndSlide()` called from a helper node.** `ClamberController` is a `Node3D` driving a
`CharacterBody3D` it doesn't own â€” and `PlayerController._PhysicsProcess` also calls `MoveAndSlide`.
Unless the state machine perfectly suppresses the normal path, that's two `MoveAndSlide` calls per
frame with different velocities. Ownership should be one-directional.

**E5 â€” Motion is axis-locked and linear.** Up fully, then forward fully, both at constant speed.
Reads as an elevator followed by a conveyor belt. No easing, no overlap at the crossover.

**E6 â€” No input or facing requirement.** Any entry into the clamber state clambers. Combined with
polling, you can clamber from brushing a ledge sideways with no intent to climb.

### Code smell

- `RaycastCollisionResult` is a heap-allocated class wrapping one `Vector3?` that is never null,
  returned alongside a `bool success` that already carries the same information. The
  `?? Vector3.Zero` fallback means a null would silently clamber you to world origin. Delete the
  class; return `(bool, Vector3)`.
- `WaitPerCallInSec` â€” exported, never read.
- `localSlice.X` (the Z component) â€” computed every frame, never read. Leftover from the removed
  angle check.
- `GetRaycastEndPoints` allocates a `List` + tuples per row per frame; `AttemptClamberCheckRow` then
  does `Select`/`Max`/`Where`/`ToArray`/`Any`/`OrderByDescending`/`First` â€” six enumerations for
  what is one loop tracking a running max. GC pressure in the physics hot path, which Godot C# is
  genuinely sensitive to.
- `child.GetChildren().Cast<RayCast3D>()` throws `InvalidCastException` on any non-raycast child.
  `OfType<RayCast3D>()` degrades gracefully; better still, validate and error loudly at `_Ready`.
- **No debug visualisation.** For a feature whose entire debugging story is "where were the rays and
  why didn't it fire," there is no draw, no gizmo, no log. This is the biggest hidden time cost in
  the file.

### Architecture

`Clambering` is a `bool` on `PlayerController` â€” a state variable living outside the state machine
this repo already has. Two sources of truth for the same thing. The statechart in `StateMachine/` is
built precisely for this: `TryHandleClamber()` is a transition **guard**, `Clamber()` is
`StatePhysicsProcessing`, and the timeout is a transition **out**. The soft-lock bug (no abort path)
is a direct symptom of state living outside the state machine.

---

## Part 3 â€” How others solve this

### A. Front-face raycast ladder â€” *your current approach*

Rows of forward rays at increasing heights; highest hit is the ledge.

**Pros** â€” Trivially simple. Cheap. No shape casts. Naturally reports a height. Works on any convex
wall face.

**Cons** â€” Only ever sees the front face, never the top surface (D1). Height quantized to ray
spacing (D4). No normal, so no walkability check. No room check. Cast count grows as the product of
rows Ã— columns.

**Verdict** â€” Fine as a *first* pass to find the wall and its normal. Insufficient as the *only*
pass. This is the core structural problem with the file.

### B. Forward trace â†’ downward trace â†’ room check *(the industry standard)*

[Cinderflame's ledge detection](http://cinderflame.com/ledge-climbing-1/) describes the canonical
shape: fire a ray **downward from a point above head height, out in front of the player** â€” if it
hits a horizontal surface, that's the ledge top. Their framing of the tunables as **WIDTH** (spacing
between anchor points), **HEIGHT** (how high above the player), and **DEPTH** (how far out you can
reach) is a much better parameterisation than your `RaycastLength` / `ClamberMargin` pair, because
each knob maps to something a designer can reason about.

[Daniel Martinez Amigo's mantle system](https://danimtz.github.io/devpost/Mantle/) runs five line
traces from the capsule top downward: an initial trace to confirm the path forward is clear, then
subsequent traces to find empty space suitable for mantling. Their validation set is worth copying
wholesale â€” **must be airborne, must be pressing forward, must not have jumped in the last ~0.5s,
clearance check for headroom at the landing point, facing-within-tolerance for tall mantles**, and
notably *uncrouch before the clearance check* so capsule size doesn't corrupt the result.

Unreal's [Advanced Locomotion System](https://github.com/PanicPetal/ALS-Community/) formalises this
as a `MantleCheck` producing a `FrontLedge` position + rotation, with a
[`MantleDetectionTrace` struct](https://doc.spabastudio.com/knowledgebase/tutorials/blueprint-tutorials/manting/mantle)
splitting wall detection, ledge detection, and available-space detection into separate tunable
sub-structures â€” plus a `BaseDistance` that scales with **velocity and capsule height**, so a
sprinting player reaches further than a walking one. That velocity-scaled reach is a small addition
that does a lot for feel.

**Pros** â€” Correct by construction. Rejects railings and thin geometry (down trace finds nothing).
Reports the *exact* ledge height, unquantized. Yields both the wall normal (facing/alignment) and
the floor normal (walkability). Every tunable is designer-legible.

**Cons** â€” More casts. Needs a "how far past the lip do I trace down" parameter. Still needs a
separate capsule room check as a third stage.

### C. Up-forward-down capsule sweep *(the Godot-idiomatic answer)*

Godot's stair-stepping community has converged on sweeping the **player's own collision shape**
through three motions: up by max step height, forward by velocity, then down.
[dresswithpockets' writeup](https://dresswithpockets.github.io/2025/03/19/godot-stair-stepping.html)
gives the algorithm and, more usefully, the scars:

- Validate the landing with `result.get_normal().angle_to(Vector3.UP) > floor_max_angle â†’ reject`.
- **Test upward *before* forward**, or you tunnel through ceilings. (Their first version had this
  bug.)
- `safe_margin` must be ~0.001; higher values produce false positives and snags. The
  [Godot asset-library implementations](https://github.com/Andicraft/stairs-character) echo this â€”
  collider margin at most 0.01 or you snag.
- Split horizontal and vertical `move_and_slide()` calls, or gravity's accumulated Y velocity eats
  the step.
- Add exponential-decay smoothing to the camera offset or the step reads as a snap.

The key insight, and the reason I'd lead with this: **clamber is stair-stepping with a bigger
`max_step_height` and a slower, visible execution.** They are the same query.

**Pros** â€” Detection and validation are *the same operation*. If the sweep completes, the
destination is provably clear and reachable, because you swept the actual player shape â€” D2, D3, D4
and D7 all disappear at once, not one at a time. Shape-agnostic. Handles slopes, corners and
irregular ledges with no special cases.

**Note on D1 (thin geometry).** The capsule sweep resolves this *differently* from approach B, not
identically. A line trace passes over a railing and finds nothing, so B rejects it. A capsule
**rests on** the railing, so the sweep succeeds and reports the rail top as the landing point. That
is not the old ray-ladder bug â€” the sweep lands you exactly where the collider is genuinely
supported, so you don't fall through â€” but it does mean rails and pipes remain clamberable. If
rejecting them is a *design* choice rather than a correctness one, it needs an explicit
minimum-ledge-depth check (a second down-sweep further forward), which neither approach gives free.

**Cons** â€” Margin-sensitive. Three shape sweeps cost more than three rays. Gives you a landing
*position* but no wall normal for facing checks or animation alignment. Requires `TestMove` /
`PhysicsServer3D.BodyTestMotion`, which is a slightly awkward API in C#.

### D. Designer-placed ledge volumes *(Assassin's Creed / Uncharted lineage)*

Level geometry carries explicit climbable annotations; the character queries markers, not physics.

**Pros** â€” Total authorial control. Zero false positives. Per-ledge authored animation. Cheapest at
runtime.

**Cons** â€” Enormous authoring cost, breaks on procedural or user-built geometry, brittle under level
edits. **Wrong for this project** â€” listed for completeness, since it's why AAA traversal looks so
much better than the generic version and isn't a technique you can borrow.

### E. Multi-column casts for corner handling

Cinderflame's fix for partial ledges: fire from **each shoulder**, not just centre â€” if the middle
ray misses but the right-shoulder ray hits, rotate the player around the corner rather than
rejecting.

**Pros** â€” Fixes "you were 10cm off-centre and the game said no," a very common and very annoying
failure.

**Cons** â€” Multiplies cast count and needs an explicit combination policy. Your grid already has the
structure for this; what's missing is the policy. Right now it's `first row wins` (D8), which is the
one policy that's never correct.

### Execution: how the character actually moves

| Approach | Where it's used | Pros | Cons |
|---|---|---|---|
| **Motion warping / root-motion matching** | UE5 `MotionWarping`, Unity `MatchTarget` | Looks excellent. One clip covers a whole height range. Warping is what stops a 1.2m-authored mantle from breaking on a 0.9m ledge. | Requires authored animations (you have none). Usually needs `MovementMode = Flying` with collision effectively off. Painful to replicate over network. |
| **Two-phase velocity + MoveAndSlide** | *yours* | Collision stays authoritative â€” you cannot end up inside geometry. | Robotic. Can stall forever without a timeout. No easing. |
| **Curve-driven position over fixed duration** | Common in indie / first-person | Easy to tune, easy to make feel good, no animation assets needed. | Usually implemented as a `GlobalPosition` lerp, which throws away collision safety. |

Note the pattern in the danimtz system: **the warp is split into a vertical phase and a horizontal
phase within one montage**. That's the same up-then-forward decomposition you arrived at
independently â€” the ordering is correct, they just blend across the crossover instead of snapping.

---

## Part 4 â€” What I'd change

### Tier 0 â€” do these regardless of any redesign

1. **Add a timeout to `Clamber()`.** Accumulate elapsed time; past `MaxClamberDuration`, abort and
   restore normal movement. The current code can permanently soft-lock a player.
2. **Move the cooldown off the failure path.** Detect every physics frame. Cooldown after a
   *completed or aborted* clamber only. Also actually assign `WaitPerCallInSec` to the timer, or
   delete both and use an elapsed-time float.
3. **Fix E2** â€” measure progress toward the destination, not distance from the start.
4. **Zero or damp `Velocity` on exit** so the player isn't launched off the ledge.
5. **Require forward input** (and optionally a facing-within-tolerance check, per danimtz) before
   clambering.

### Tier 1 â€” the detection overhaul

**Replace the raycast ladder with a hybrid of B and C: one forward ray for the wall, then an
up/forward/down sweep of the player's own shape for validation.** The ray gives you the wall normal
(facing checks, later animation alignment); the sweep gives you a provably-legal landing spot.

```csharp
// One forward ray gives the wall + its normal (facing checks, alignment).
// The sweep of the player's OWN shape gives a landing spot that is
// provably clear, correctly-heighted, and standable â€” replacing D1-D4, D7 in one step.
private bool TryFindClamber(out Vector3 landing, out Vector3 wallNormal)
{
    landing = Vector3.Zero;
    wallNormal = Vector3.Zero;

    if (!WallRay.IsColliding()) return false;
    wallNormal = WallRay.GetCollisionNormal();

    var body = PlayerController;
    var xform = body.GlobalTransform;
    var collision = new KinematicCollision3D();

    // 1. Up FIRST â€” going forward first tunnels through ceilings.
    var up = Vector3.Up * MaxClamberHeight;
    if (body.TestMove(xform, up, collision, SafeMargin))
        up = Vector3.Up * collision.GetTravel().Length();  // clipped by a ceiling
    xform.Origin += up;

    // 2. Forward. Blocked here means there's no room at ledge height.
    var forward = -body.GlobalBasis.Z * ClamberReach;
    if (body.TestMove(xform, forward, null, SafeMargin)) return false;
    xform.Origin += forward;

    // 3. Down. Nothing to land on -> it was a railing, not a ledge.
    var down = Vector3.Down * (up.Y + 0.05f);
    if (!body.TestMove(xform, down, collision, SafeMargin)) return false;
    if (collision.GetNormal().AngleTo(Vector3.Up) > body.FloorMaxAngle) return false;

    landing = xform.Origin + Vector3.Down * collision.GetTravel().Length();
    return landing.Y - body.GlobalPosition.Y >= MinClamberHeight;  // below this it's a step, not a clamber
}
```

`TestMove` uses the body's real collision shape and collision mask, so the room check, the headroom
check, the walkability check and the exact ledge height all fall out of the same three calls. Keep
`SafeMargin` at 0.001 â€” the Godot stair-step community is unanimous that higher values cause
snagging and false positives.

**This deletes:** the whole `Raycasts` scene rig, `GetRaycastEndPoints`, `AttemptClamberCheckRow`,
`RaycastCollisionResult`, the `maxY` epsilon comparison, all the per-frame LINQ, and every
"quantized to ray spacing" tuning headache. Net line count goes down.

**What you lose and must decide about:** the multi-column corner handling (E) that the grid *could*
have supported. If off-centre ledges matter, keep it as a small forward ray fan for the wall pass
only, then run one sweep at the winning column. Don't rebuild the grid.

### Tier 2 â€” execution and structure

6. **Make clambering a real state.** `ClamberController` becomes pure detection â€”
   `bool TryFindClamber(out ...)` and `Vector3 GetClamberMotion(double delta)`. The state owns
   `MoveAndSlide` and the timeout. One `MoveAndSlide` per frame, one owner. This is what the
   statechart in `StateMachine/README.md` is for, and it structurally eliminates the soft-lock
   because "abort" becomes a transition rather than something you have to remember to write.

7. **Curve-driven progress instead of two hard phases.** Export two `Curve` resources (height,
   forward), evaluate at `t = elapsed / duration`, and drive the result through `MoveAndSlide`
   deltas â€” *not* a `GlobalPosition` lerp, so you keep the collision safety you currently have.
   `Curve` is a native editor-editable Godot resource: no dependency, no code, and it turns robotic
   motion into something tunable by feel. Scale `duration` with climb height so short and tall
   clambers both read correctly.

8. **In first person, spend the polish budget on the camera, not the body.** Nobody sees a body.
   A small pitch/roll/bob curve during the manoeuvre sells a mantle far harder than the body path
   does â€” and the stair-step writeup's warning about camera snap applies directly: smooth the offset
   or the transition reads as a teleport.

9. **Handle moving ledges (D10).** Cheapest correct fix: keep the collider reference from the down
   sweep and re-run detection every few frames during the clamber; if the surface moved beyond a
   threshold, abort. Falls out for free once abort is a transition.

10. **Add debug draw.** Sweep start/end transforms, the landing point, and the reject reason
    (no wall / no room / not standable / too low / too high). One `[Export] bool DebugDraw`. This
    pays for itself the first afternoon.

### Tier 3 â€” the strategic reframe

Right now you have one mechanic with one height band. The standard framing across all the systems
surveyed is **one detection pass, classified by height, with different execution parameters**:

| Height | Mechanic | Execution |
|---|---|---|
| 0 â€“ 0.4 m | step-up | automatic, no state, same sweep run every frame â€” this is just stair-stepping |
| 0.4 â€“ 1.2 m | vault / clamber | fast, retains forward momentum |
| 1.2 â€“ 2.2 m | mantle | slower, triggerable from air or ground |
| > 2.2 m | ledge hang | grab and hold; second input pulls up |

The up-forward-down sweep answers all four with the same three `TestMove` calls â€” only
`MaxClamberHeight`, the duration, and the curves change per band. That's the argument for doing the
detection overhaul before adding anything else: **it's the version that scales to the rest of the
traversal kit for free**, whereas the raycast ladder needs a new rig per band.

Also worth adopting from Titanfall-lineage systems: allow detection **while airborne**, not only
from the ground. Mantling out of a jump is most of what makes first-person traversal feel fluid, and
it costs one condition.

---

## Part 5 â€” How the current implementation works

### The one-paragraph version

You press jump near a ledge. Before anything else happens, the game **test-drives your own
collision capsule** through the move you are about to make â€” lift it straight up, push it forward,
drop it down â€” without actually moving you. If that rehearsal ends somewhere flat, clear and high
enough to be worth it, the real move replays that same path over a fixed span of time and you end
up standing on the ledge. If the rehearsal fails at any step, nothing happens and your jump goes
through normally.

The whole design rests on one idea: **the rehearsal uses the real collider, so a rehearsal that
succeeds is proof the destination fits.** There is no separate "will I fit?" check to get wrong,
because fitting *is* the check.

### The shape of it

Two classes, one seam between them:

| | Answers | Never does |
|---|---|---|
| `ClamberController` | **Whether** a clamber is possible and **where** it lands; what velocity to apply right now | Read input. Move anything. Call `MoveAndSlide`. |
| `PlayerController` | **When** to ask; applies the returned velocity | Decide whether a ledge is valid |

```
PlayerController._PhysicsProcess
  â”œâ”€ read space, derive fresh-press edge
  â”œâ”€ tryClamber?  â”€â”€â–º ClamberController.TryStartClamber()
  â”‚                      â””â”€ TryFindLanding()   3 Ã— TestMove, the rehearsal
  â”œâ”€ IsClambering? â”€â”€â–º ClamberController.GetClamberVelocity(delta)
  â”‚                    Velocity = result; MoveAndSlide(); return
  â””â”€ otherwise: normal gravity / jump / WASD
```

The controller is a `Node3D` hanging off the `CharacterBody3D`, but it is really just a calculator â€”
it holds no authority over movement. That is deliberate: it makes the class a drop-in for a
statechart later, where `TryStartClamber()` becomes a transition guard, `GetClamberVelocity()`
becomes `StatePhysicsProcessing`, and `!IsClambering` becomes the transition out.

---

### 1. The trigger â€” `PlayerController._PhysicsProcess`

```csharp
var jump = Input.IsPhysicalKeyPressed(Key.Space);
var jumpPressed = jump && !_jumpHeld;
_jumpHeld = jump;

var tryClamber = jumpPressed || (jump && !IsOnFloor());
```

`Input.IsPhysicalKeyPressed` is a *held* query, so the fresh-press edge is derived manually by
remembering last frame's state. Both actions read from that one edge, which is what makes them
mutually exclusive.

`tryClamber` has two arms and they exist for different reasons:

- **`jumpPressed`** â€” a fresh press always tries, grounded or not. This is the standing mantle.
- **`jump && !IsOnFloor()`** â€” while airborne, a *held* key keeps trying every frame. This is what
  lets you hold jump, leap at a wall, and mantle the instant the ledge comes into reach, rather
  than having to time a second tap at the apex.

Grounded-and-held is deliberately excluded. That is the clause that stops you from bouncing the
instant you land a clamber with the key still down.

Two ordering rules carry real weight:

1. **The clamber block sits above the jump branch and returns early.** A press next to a ledge is
   consumed by the mantle, so you never get a hop *and* a mantle from one press. If detection
   fails, control falls through and the same press becomes an ordinary jump.
2. **`Velocity.Y` is zeroed on the frame the clamber ends.** The last clamber velocity is whatever
   was needed to reach the target; left in place, gravity would resume from a large upward value
   and fling you off the ledge.

Note `IsOnFloor()` here reflects the *previous* `MoveAndSlide`, so it lags by a frame. Harmless in
practice â€” it only delays the first airborne attempt by ~16ms.

---

### 2. Detection â€” `TryFindLanding`, the three sweeps

This is the heart of the class. Three `TestMove` calls walk a *copy of the transform* through the
manoeuvre while the player stands still:

```csharp
var xform = Player.GlobalTransform;   // a copy â€” the player does not move
```

`CharacterBody3D.TestMove` sweeps the body's **actual collision shape** along a motion vector using
its **actual collision mask** and reports whether it would hit, how far it got, and the surface
normal. That is why detection and validation collapse into one operation.

| # | Motion | What a failure proves | Reject reason |
|---|---|---|---|
| 1 | `Up * (MaxClamberHeight + Clearance)` | A ceiling pins you below usable height | `no headroom to rise` |
| 2 | `-GlobalBasis.Z * ClamberReach` | The wall is taller than you can clamber | `no room in front` |
| 3 | `Down * (rise + 0.05)` | There is a gap, not a ledge | `nothing to stand on` |

**Sweep 1 â€” up.** Rises by `MaxClamberHeight + Clearance`. The `+ Clearance` is not cosmetic: a
ledge of *exactly* `MaxClamberHeight` has to get *above* its own lip before the forward sweep can
pass over it. Without the extra, the export would be off by `Clearance` from what its name
promises. If a ceiling blocks the rise, `rise` is clamped to the distance actually travelled rather
than failing outright, so low rooms still allow short clambers.

**Up must come first.** Sweeping forward at head height and *then* up would let the capsule pass
under a ceiling it should have hit â€” the classic tunnelling bug in this family of algorithms, and
one the Godot stair-step community documented the hard way.

**Sweep 2 â€” forward.** `-GlobalBasis.Z` is the player's facing, so the clamber goes where you look,
not where you are moving. Being blocked here is the "wall too tall" signal: at maximum rise the
capsule still overlaps the obstacle.

**Sweep 3 â€” down.** Casts back down by slightly more than it rose. Two rejections come out of it:

```csharp
if (!Player.TestMove(xform, Vector3.Down * (rise + 0.05f), hit, SafeMargin))
    return Reject("nothing to stand on");
if (hit.GetNormal().AngleTo(Vector3.Up) > Player.FloorMaxAngle) return Reject("surface too steep");
```

Hitting nothing means open air â€” a gap, a pit, the far side of a thin wall. Hitting something at a
steeper angle than the body's own `FloorMaxAngle` means it is a slope you could not stand on
anyway. Reusing `Player.FloorMaxAngle` rather than a private threshold means clamber and walking
agree on what "floor" means, for free.

Finally the landing point, and the floor of the height range:

```csharp
landing = xform.Origin + Vector3.Down * hit.GetTravel().Length();
if (landing.Y - Player.GlobalPosition.Y < MinClamberHeight) return Reject("too low, that is a step");
```

`MinClamberHeight` is the guard against false positives on open ground â€” the down-sweep *always*
finds the floor you are standing on, so without this every jump anywhere would register as a
clamber onto your own feet. It is also the boundary that leaves short obstacles to stair-stepping.

**`SafeMargin` at 0.001** is not arbitrary. Above roughly 0.01 the sweeps snag on geometry and
report collisions that are not really there; this is the single most consistent piece of advice in
the Godot stair-step literature.

**What detection deliberately does *not* establish:** that the ledge is *deep* enough. A capsule
rests on top of a railing rather than passing over it, so sweep 3 succeeds and rails stay
clamberable. That is not the old ray-ladder bug â€” you land where the collider is genuinely
supported â€” but rejecting rails as a design choice would need a second down-sweep further forward.

---

### 3. Committing â€” `TryStartClamber`

```csharp
if (IsClambering || _cooldown > 0f) return false;
if (!TryFindLanding(out var landing)) return false;
```

Two gates, and note what is *absent*: nothing blocks a *failed* detection from being retried next
frame. Detection runs as often as it is asked. The cooldown only ever starts after a clamber
**completes**, so a near-miss never blacks out the next attempt.

On success it snapshots the run and precomputes two derived values:

```csharp
_duration = Mathf.Max(Mathf.Clamp((landing.Y - _start.Y) / ClamberSpeed, MinDuration, MaxDuration), 0.01f);
_maxSpeed = 4f * _start.DistanceTo(_landing) / _duration;
```

`_duration` scales with climb height, so a shin-high vault and a chest-high haul both read
correctly instead of sharing one timing; the clamps stop extremes from looking silly and the
`Max(â€¦, 0.01f)` protects the division in `GetClamberVelocity` if someone zeroes `MinDuration`.
`_maxSpeed` is the velocity ceiling explained below.

`_start` and `_landing` are **world positions captured once**. This is the known weak point: on a
moving platform you clamber to where the ledge *was*.

---

### 4. The motion â€” `GetClamberVelocity`

Called every physics frame while `IsClambering`. Progress is a function of **elapsed time**, not of
position:

```csharp
_elapsed += (float)delta;
var t = Mathf.Min(_elapsed / _duration, 1f);
```

That single choice is why the manoeuvre can never hang. `t` advances whether or not you actually
moved, so it always reaches 1 and the state always exits. The original's position-based
termination could stall forever; this cannot.

**Two curves, no overlap.**

```csharp
var h = HeightCurve?.Sample(t) ?? Mathf.SmoothStep(0f, 0.5f, t);   // rise, t 0.0 â†’ 0.5
var f = ForwardCurve?.Sample(t) ?? Mathf.SmoothStep(0.5f, 1f, t);  // forward, t 0.5 â†’ 1.0
```

The rise finishes before the forward motion starts, and that separation is load-bearing. Standing
flush against a ledge, the capsule only clears the lip at the *very top* of the rise â€” so any
overlap between the two phases drives it straight into the wall face. (An earlier version blended
them across t 0.4â€“0.6 for smoothness and jammed exactly this way.) Assigning `HeightCurve` /
`ForwardCurve` in the inspector overrides the defaults entirely, so custom curves must preserve
that ordering.

**The clearance arc.**

```csharp
Mathf.Lerp(_start.Y, _landing.Y + Clearance, h) - Clearance * f
```

Read it at three points: at `t=0` (`h=0, f=0`) it is `_start.Y`; at `t=0.5` (`h=1, f=0`) it is
`_landing.Y + Clearance` â€” floating just above the lip; at `t=1` (`h=1, f=1`) the `- Clearance * f`
term has cancelled it back to exactly `_landing.Y`. So the path goes *up and over* and settles down
as it comes forward, which keeps the capsule's rounded bottom from catching on the edge.

**Velocity, not teleportation.**

```csharp
return ((target - Player.GlobalPosition) / (float)delta).LimitLength(_maxSpeed);
```

The method returns the velocity needed to reach this frame's point on the path, and the *caller*
applies it through `MoveAndSlide`. Physics stays authoritative for the whole manoeuvre, so you can
never be pushed inside geometry â€” the one property worth preserving from the original design.

Because it targets an absolute point rather than adding a delta, it self-corrects: a transient
scrape that costs you ground is made up next frame. `LimitLength(_maxSpeed)` caps that correction.
Without it, a *sustained* block accumulates position error until the computed velocity is large
enough to tunnel through the very ledge you are climbing.

On the final frame the state closes itself and arms the cooldown:

```csharp
if (t >= 1f) { IsClambering = false; _cooldown = CooldownSeconds; }
```

---

### 5. Wiring and failure behaviour

Both node references self-heal, because hand-written `NodePath` entries in a `.tscn` do not reliably
resolve into typed node exports:

```csharp
Player  ??= GetParent() as CharacterBody3D;                    // ClamberController._Ready
Clamber ??= GetNodeOrNull<ClamberController>("ClamberController");  // PlayerController._Ready
```

Assign them in the inspector if you like; leave them empty and the conventional layout â€” controller
parented to the body â€” just works. `ClamberController` pushes a clear error if neither path yields
a body.

`[Export] bool DebugLog` prints the reason for every decision, which is the entire debugging story
for this feature: `no headroom to rise`, `no room in front`, `nothing to stand on`,
`surface too steep`, `too low, that is a step`, plus an `accepted:` line carrying the start
position, rise, down-travel and landing point.

**When something goes wrong mid-clamber**, the failure is graceful rather than sticky: `t` runs out,
`IsClambering` flips false wherever you happen to be, `Velocity.Y` is zeroed, gravity resumes and
you fall. No soft-lock, no frozen player, no stuck-in-geometry.

---

### 6. The tunables

| Export | Default | Controls |
|---|---|---|
| `MaxClamberHeight` | 1.6 | Tallest ledge that can be clambered. The sweep rises past it by `Clearance`, so the name is literal. |
| `MinClamberHeight` | 0.4 | Floor of the range. Below this it is a step â€” and this is what stops flat ground registering. |
| `ClamberReach` | 0.75 | How far forward the landing spot may be. Must exceed the capsule radius to clear the lip. |
| `SafeMargin` | 0.001 | Sweep collision margin. Raising it causes snags and false positives. |
| `ClamberSpeed` | 3.0 | Metres per second used to derive duration from climb height. |
| `MinDuration` / `MaxDuration` | 0.2 / 0.9 | Clamps on that derived duration. |
| `Clearance` | 0.1 | Arc height over the lip, and the rise overshoot that makes `MaxClamberHeight` literal. |
| `HeightCurve` / `ForwardCurve` | unset | Optional `Curve` overrides for the motion shape. Must stay non-overlapping. |
| `CooldownSeconds` | 0.25 | Blackout after a *completed* clamber. Never applies to a failed attempt. |
| `DebugLog` | off | Prints accept/reject reasons. |

---

### 7. What the tests pin down

`ClamberTests.cs` (`godot --headless --path . res://Tests/test_clamber.tscn`, exit 0 on pass) builds each
case its own slice of world 40m apart with its own controller, waits two physics frames for the
bodies to register, then asserts accept/reject and â€” where it accepts â€” the exact landing height:

| Case | Expected | Pins down |
|---|---|---|
| flat ground | reject | The false-positive guard; the down-sweep always finds *something* |
| 1.0m ledge | accept at exactly y=2.0 | Height is exact, not quantised |
| ledge at exactly `MaxClamberHeight` | accept at y=2.6 | The `+ Clearance` rise; the export means what it says |
| 0.2m curb | reject | The step/clamber boundary |
| 3m wall | reject | Tall walls stay rejected |
| low ceiling | reject | Up-before-forward ordering |
| 60Â° slope | reject | The `FloorMaxAngle` normal check |

The slope case is deliberately thin and low: a large slab gets rejected by the *forward* sweep for
lack of room, which would pass the test without ever exercising the normal check.

---

### 8. Known limits, deliberately

- **Not a statechart state yet.** `PlayerController` has no state machine wired at all, so the
  clamber is driven inline. The API is already shaped for the transplant.
- **Moving ledges.** The destination is a world position captured at detect time. Clamber onto a
  moving platform and you land where it was, then fall â€” non-fatal, not handled.
- **Thin ledges are clamberable.** Rails and pipes support a capsule, so they pass. Rejecting them
  needs an explicit minimum-depth check.
- **No visual debug.** Reject-reason logging covers the real question; there is no gizmo drawing
  the sweeps.
- **No camera motion.** In first person this is where the polish budget belongs, and none has been
  spent yet.
- **Grounded-and-held does not re-arm.** Walking into a ledge while still holding jump from an
  earlier press will not mantle; release and press again.

---

## Summary

The original's execution half was more principled than most tutorials â€” keeping `MoveAndSlide`
authoritative throughout is a real advantage, and the rewrite preserves it. Its detection half had a
structural problem that no amount of tuning fixes: **front-face rays cannot know whether there's
anything to stand on**, which produces railing-climbing and slope-climbing bugs that look like
tuning issues and aren't.

The highest-value changes, in the order they were worth doing:

1. ~~**Timeout on `Clamber()`**~~ â€” **done.** Solved structurally: progress is time-parameterised,
   so the manoeuvre always terminates.
2. ~~**Cooldown off the failure path**~~ â€” **done.** Detection runs whenever asked; the cooldown
   arms only after a completed clamber.
3. ~~**Swap detection for an up-forward-down `TestMove` sweep**~~ â€” **done.** The raycast rig,
   `RaycastCollisionResult` and the per-frame LINQ are gone.
4. **Make it a state in your existing statechart** â€” still outstanding. `PlayerController` has no
   state machine wired yet, so this is a separate job; the API is already shaped for it.
5. **Curves + camera motion** â€” curves done (`HeightCurve` / `ForwardCurve`, with non-overlapping
   defaults). Camera motion is where the remaining feel lives, and none has been spent yet.

See **Part 5** for how the rewritten class works.

---

## Sources

- [Ledge Climbing â€“ Part 1 â€“ Detecting a Ledge â€” Cinderflame Studios](http://cinderflame.com/ledge-climbing-1/)
- [Mantle/Ledge grabbing system â€” Daniel Martinez Amigo](https://danimtz.github.io/devpost/Mantle/)
- [Godot 4 Stair-Stepping â€” dresswithpockets](https://dresswithpockets.github.io/2025/03/19/godot-stair-stepping.html)
- [Andicraft/stairs-character â€” stair-stepping CharacterBody3D for Godot 4](https://github.com/Andicraft/stairs-character)
- [JheKWall/Godot-Stair-Step-Demo](https://github.com/JheKWall/Godot-Stair-Step-Demo)
- [Godot proposal #2751 â€” automatic step-up/step-down for CharacterBody](https://github.com/godotengine/godot-proposals/issues/2751)
- [ShapeCast3D â€” Godot Engine documentation](https://docs.godotengine.org/en/stable/classes/class_shapecast3d.html)
- [CharacterBody3D â€” Godot Engine documentation](https://docs.godotengine.org/en/stable/classes/class_characterbody3d.html)
- [Mantle â€” Advanced Movement System documentation](https://doc.spabastudio.com/knowledgebase/tutorials/blueprint-tutorials/manting/mantle)
- [PanicPetal/ALS-Community â€” Advanced Locomotion System V4](https://github.com/PanicPetal/ALS-Community/)
- [Motion Warping in Unreal Engine â€” Epic documentation](https://dev.epicgames.com/documentation/en-us/unreal-engine/motion-warping-in-unreal-engine)
- [Climbing Animation for Games: Ledge Grabs, Walls â€” MoCap Online](https://mocaponline.com/blogs/mocap-news/climbing-animation-games-guide)
- [Making Climbing/Vaulting System in Unity â€” Md Mohammad Sarfraz Alam](https://medium.com/@alamsarfraz422/making-climbing-vaulting-system-in-unity-225b11a636a)

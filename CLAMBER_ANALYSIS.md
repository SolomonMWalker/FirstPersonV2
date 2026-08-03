# ClamberController — Analysis, Research, and Recommendations

Analysis of `ClamberController.cs` as pulled in, plus a comparison against how this problem is
solved elsewhere, and what I'd change.

---

## Part 1 — How it currently works

### The pipeline

```
TryHandleClamber()          entry point, called once to decide "can we clamber?"
  └─ AttemptClamber()       cooldown gate + iterate rows
       └─ AttemptClamberCheckRow(row)   per-row decision
            └─ GetRaycastEndPoints(row) read raycasts → (localSlice, globalEndpoint, collided)
  → caches ClamberDestination / StartPoint / XzDirection / XzDistanceSquared

Clamber()                   called every physics frame while PlayerController.Clambering
  ├─ phase 1: feet below destination.Y + margin → Velocity = up * ClamberVelocity
  ├─ phase 2: XZ travelled < XZ distance      → Velocity = dir * ClamberVelocity
  └─ else: Clambering = false
```

### The detection model

The rig is a `Raycasts` node holding N child nodes, each holding M `RayCast3D`s — a **grid of
forward-facing rays**, rows stacked vertically. For one row:

1. Read every ray's endpoint (real collision point if hit, nominal target position if not).
2. If nothing hit → fail.
3. Compute `maxY` over **all** endpoints. If any *collided* ray sits at `maxY` → fail
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

## Part 2 — Bugs and gaps

### Won't compile (expected — this came from another project)

| # | Issue |
|---|---|
| B1 | `using FirstPerson.Scenes.Player;` — namespace doesn't exist in this repo. `PlayerController` is global-namespace here. |
| B2 | `PlayerController.BottomOfPlayer`, `.ClamberVelocity`, `.Clambering` — none exist on the current `PlayerController.cs`. |
| B3 | `[Export] public PlayerController PlayerController;` — field named identically to its type. Legal C#, but every future reference is ambiguous to read. |

### Soft-lock — this one is a hard bug

**`Clamber()` has no timeout and no abort condition.** If phase 1 can't complete — a `RigidBody3D`
shoves you, the ledge is on a platform that moved, geometry snags the capsule, gravity is off so
you don't fall out of it — `Clambering` never becomes `false`. The player is frozen, weightless,
permanently. There is no escape path in the code.

Every real implementation has a hard duration cap. This is the first thing to add regardless of
what else changes.

### Detection correctness

**D1 — Nothing verifies there is a surface to stand on.**
The rays only ever see the wall's **front face**. The code infers "the face stops at height Y,
therefore Y is a ledge." That inference is wrong for:

- **Railings, fence rails, pipes, chain-link, thin signage.** Topmost front-face hit is the top of
  the rail. You clamber up onto a 3cm-deep surface and immediately fall. This is the single most
  common failure this design will produce in a real level.
- **Overhangs.** The face stops because the geometry recedes, not because it ends.
- **Sloped tops.** A 70° wedge reads as a perfectly good clamber target.

Every other implementation surveyed solves this with a **downward trace** from above the detected
lip. You have no downward trace.

**D2 — No surface-normal / floor-angle validation.** Related to D1 but separate: even given a real
top surface, nothing checks it's walkable. `CharacterBody3D` already exposes `FloorMaxAngle`; the
check is one line and it isn't there.

**D3 — No capsule-fit / headroom check.** Nothing confirms the player's collision shape fits at the
destination. Clamber into a crawlspace and you drive the capsule into a ceiling. `MoveAndSlide`
will resolve it *somehow* — which is exactly the problem, the resolution is arbitrary.

**D4 — Height resolution is quantized to ray spacing.** With rays every 0.25m, a ledge at 1.13m
reports as 1.0m (the highest ray that hit). You then rise to `1.0 + ClamberMargin (0.26) = 1.26m`,
which happens to clear it — but only because the margin is coincidentally larger than the spacing.
Change either constant independently and clamber silently starts putting feet inside geometry.
**Nothing in the code ties `ClamberMargin`, `RaycastLength`, and the rig's vertical spacing
together, and nothing validates the rig at `_Ready`.**

**D5 — The `maxY` epsilon comparison is a fragile stand-in for "did the top ray hit".**

```csharp
var maxY = rawCollisions.Select(rc => rc.localSlice.Y).Max();
if (collidedCollisions.Any(rc => Math.Abs(rc.localSlice.Y - maxY) < 0.0001f)) return (false, null);
```

This compares **collision-point Y** against the max **endpoint Y**. It only works because the rays
are assumed exactly horizontal, so a ray's Y is constant along its length. Tilt any ray — or parent
the rig to something that pitches — and it breaks silently. The thing it's actually asking is
`raycasts.Last().IsColliding()`.

**D6 — `ToLocal()` assumes this node is unrotated.** All the Y reasoning happens in
`ClamberController`'s local space. Parent this under a pitching camera/head and "up" stops being up.
Currently unenforced and undocumented.

**D7 — The XZ destination is the wall face, not the ledge.** `ClamberDestination` comes from a
front-face collision point. Phase 2 stops when the capsule *centre* reaches the wall plane — half
the capsule still overhangs. It works only because `MoveAndSlide` shoves you out afterwards. There's
no explicit "push past the lip by X" depth parameter, so final standing position varies with capsule
radius.

**D8 — Row iteration order is scene-tree order, and first hit wins.** Which row is checked first is
whatever the editor's child ordering happens to be. Unvalidated, invisible, and silently reorderable
by anyone dragging nodes. If rows are meant as left/centre/right columns, first-hit means you
systematically favour one side rather than picking the nearest or best candidate.

**D9 — Raycasts are read without `ForceRaycastUpdate()`.** `RayCast3D` results reflect the state at
the start of the physics step. Yaw fast into a ledge and you're testing last frame's orientation.
Either force the update before querying or make the one-frame lag a deliberate, commented choice.

**D10 — Destination is a world position captured once.** Moving platforms, elevators, rotating
doors: you clamber to where the ledge *was*.

**D11 — Rays run every frame regardless of state.** N×M casts even while walking on flat ground
with nothing in front of you. Cheap individually, free to avoid.

### Cooldown design — this is the worst *feel* bug

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
`WaitTime`** — so unless it's set in the scene, the cooldown is Godot's default **1.0 second**, not
0.25.

Player-visible consequence: run at a ledge, have the poll land 5ms before you're in range, and get a
full second of nothing. It reads as "the game ignored my input," which is the worst possible failure
mode for a traversal mechanic.

**Detection should run every physics frame.** A cooldown belongs after a *completed or aborted
clamber*, not after a failed *question*.

### Execution correctness

**E1 — No velocity handoff on exit.** `Clambering = false` leaves `Velocity` at the last horizontal
clamber velocity, full magnitude, zero Y. The player is ejected forward off the ledge at
`ClamberVelocity`.

**E2 — Phase-2 progress is measured from the start point, not toward the target.**

```csharp
ClamberXzDistanceSquared > ClamberStartPointXz.DistanceSquaredTo(currentXz)
```

This measures *distance from origin*, which grows if the player is pushed sideways — the phase
terminates without ever approaching the ledge. Correct metric is distance **to the destination**, or
`dot(direction, remaining) > 0`.

**E3 — Mixed reference frames.** Phase 1 measures `PlayerController.BottomOfPlayer.GlobalPosition.Y`
(the feet). Phase 2 measures `this.GlobalPosition` (the rig node). If the rig isn't at the player's
centre, phase 2's travel distance is offset by that difference.

**E4 — `MoveAndSlide()` called from a helper node.** `ClamberController` is a `Node3D` driving a
`CharacterBody3D` it doesn't own — and `PlayerController._PhysicsProcess` also calls `MoveAndSlide`.
Unless the state machine perfectly suppresses the normal path, that's two `MoveAndSlide` calls per
frame with different velocities. Ownership should be one-directional.

**E5 — Motion is axis-locked and linear.** Up fully, then forward fully, both at constant speed.
Reads as an elevator followed by a conveyor belt. No easing, no overlap at the crossover.

**E6 — No input or facing requirement.** Any entry into the clamber state clambers. Combined with
polling, you can clamber from brushing a ledge sideways with no intent to climb.

### Code smell

- `RaycastCollisionResult` is a heap-allocated class wrapping one `Vector3?` that is never null,
  returned alongside a `bool success` that already carries the same information. The
  `?? Vector3.Zero` fallback means a null would silently clamber you to world origin. Delete the
  class; return `(bool, Vector3)`.
- `WaitPerCallInSec` — exported, never read.
- `localSlice.X` (the Z component) — computed every frame, never read. Leftover from the removed
  angle check.
- `GetRaycastEndPoints` allocates a `List` + tuples per row per frame; `AttemptClamberCheckRow` then
  does `Select`/`Max`/`Where`/`ToArray`/`Any`/`OrderByDescending`/`First` — six enumerations for
  what is one loop tracking a running max. GC pressure in the physics hot path, which Godot C# is
  genuinely sensitive to.
- `child.GetChildren().Cast<RayCast3D>()` throws `InvalidCastException` on any non-raycast child.
  `OfType<RayCast3D>()` degrades gracefully; better still, validate and error loudly at `_Ready`.
- **No debug visualisation.** For a feature whose entire debugging story is "where were the rays and
  why didn't it fire," there is no draw, no gizmo, no log. This is the biggest hidden time cost in
  the file.

### Architecture

`Clambering` is a `bool` on `PlayerController` — a state variable living outside the state machine
this repo already has. Two sources of truth for the same thing. The statechart in `StateMachine/` is
built precisely for this: `TryHandleClamber()` is a transition **guard**, `Clamber()` is
`StatePhysicsProcessing`, and the timeout is a transition **out**. The soft-lock bug (no abort path)
is a direct symptom of state living outside the state machine.

---

## Part 3 — How others solve this

### A. Front-face raycast ladder — *your current approach*

Rows of forward rays at increasing heights; highest hit is the ledge.

**Pros** — Trivially simple. Cheap. No shape casts. Naturally reports a height. Works on any convex
wall face.

**Cons** — Only ever sees the front face, never the top surface (D1). Height quantized to ray
spacing (D4). No normal, so no walkability check. No room check. Cast count grows as the product of
rows × columns.

**Verdict** — Fine as a *first* pass to find the wall and its normal. Insufficient as the *only*
pass. This is the core structural problem with the file.

### B. Forward trace → downward trace → room check *(the industry standard)*

[Cinderflame's ledge detection](http://cinderflame.com/ledge-climbing-1/) describes the canonical
shape: fire a ray **downward from a point above head height, out in front of the player** — if it
hits a horizontal surface, that's the ledge top. Their framing of the tunables as **WIDTH** (spacing
between anchor points), **HEIGHT** (how high above the player), and **DEPTH** (how far out you can
reach) is a much better parameterisation than your `RaycastLength` / `ClamberMargin` pair, because
each knob maps to something a designer can reason about.

[Daniel Martinez Amigo's mantle system](https://danimtz.github.io/devpost/Mantle/) runs five line
traces from the capsule top downward: an initial trace to confirm the path forward is clear, then
subsequent traces to find empty space suitable for mantling. Their validation set is worth copying
wholesale — **must be airborne, must be pressing forward, must not have jumped in the last ~0.5s,
clearance check for headroom at the landing point, facing-within-tolerance for tall mantles**, and
notably *uncrouch before the clearance check* so capsule size doesn't corrupt the result.

Unreal's [Advanced Locomotion System](https://github.com/PanicPetal/ALS-Community/) formalises this
as a `MantleCheck` producing a `FrontLedge` position + rotation, with a
[`MantleDetectionTrace` struct](https://doc.spabastudio.com/knowledgebase/tutorials/blueprint-tutorials/manting/mantle)
splitting wall detection, ledge detection, and available-space detection into separate tunable
sub-structures — plus a `BaseDistance` that scales with **velocity and capsule height**, so a
sprinting player reaches further than a walking one. That velocity-scaled reach is a small addition
that does a lot for feel.

**Pros** — Correct by construction. Rejects railings and thin geometry (down trace finds nothing).
Reports the *exact* ledge height, unquantized. Yields both the wall normal (facing/alignment) and
the floor normal (walkability). Every tunable is designer-legible.

**Cons** — More casts. Needs a "how far past the lip do I trace down" parameter. Still needs a
separate capsule room check as a third stage.

### C. Up-forward-down capsule sweep *(the Godot-idiomatic answer)*

Godot's stair-stepping community has converged on sweeping the **player's own collision shape**
through three motions: up by max step height, forward by velocity, then down.
[dresswithpockets' writeup](https://dresswithpockets.github.io/2025/03/19/godot-stair-stepping.html)
gives the algorithm and, more usefully, the scars:

- Validate the landing with `result.get_normal().angle_to(Vector3.UP) > floor_max_angle → reject`.
- **Test upward *before* forward**, or you tunnel through ceilings. (Their first version had this
  bug.)
- `safe_margin` must be ~0.001; higher values produce false positives and snags. The
  [Godot asset-library implementations](https://github.com/Andicraft/stairs-character) echo this —
  collider margin at most 0.01 or you snag.
- Split horizontal and vertical `move_and_slide()` calls, or gravity's accumulated Y velocity eats
  the step.
- Add exponential-decay smoothing to the camera offset or the step reads as a snap.

The key insight, and the reason I'd lead with this: **clamber is stair-stepping with a bigger
`max_step_height` and a slower, visible execution.** They are the same query.

**Pros** — Detection and validation are *the same operation*. If the sweep completes, the
destination is provably clear and reachable, because you swept the actual player shape — D1, D2, D3,
D4 and D7 all disappear at once, not one at a time. Shape-agnostic. Handles slopes, corners and
irregular ledges with no special cases.

**Cons** — Margin-sensitive. Three shape sweeps cost more than three rays. Gives you a landing
*position* but no wall normal for facing checks or animation alignment. Requires `TestMove` /
`PhysicsServer3D.BodyTestMotion`, which is a slightly awkward API in C#.

### D. Designer-placed ledge volumes *(Assassin's Creed / Uncharted lineage)*

Level geometry carries explicit climbable annotations; the character queries markers, not physics.

**Pros** — Total authorial control. Zero false positives. Per-ledge authored animation. Cheapest at
runtime.

**Cons** — Enormous authoring cost, breaks on procedural or user-built geometry, brittle under level
edits. **Wrong for this project** — listed for completeness, since it's why AAA traversal looks so
much better than the generic version and isn't a technique you can borrow.

### E. Multi-column casts for corner handling

Cinderflame's fix for partial ledges: fire from **each shoulder**, not just centre — if the middle
ray misses but the right-shoulder ray hits, rotate the player around the corner rather than
rejecting.

**Pros** — Fixes "you were 10cm off-centre and the game said no," a very common and very annoying
failure.

**Cons** — Multiplies cast count and needs an explicit combination policy. Your grid already has the
structure for this; what's missing is the policy. Right now it's `first row wins` (D8), which is the
one policy that's never correct.

### Execution: how the character actually moves

| Approach | Where it's used | Pros | Cons |
|---|---|---|---|
| **Motion warping / root-motion matching** | UE5 `MotionWarping`, Unity `MatchTarget` | Looks excellent. One clip covers a whole height range. Warping is what stops a 1.2m-authored mantle from breaking on a 0.9m ledge. | Requires authored animations (you have none). Usually needs `MovementMode = Flying` with collision effectively off. Painful to replicate over network. |
| **Two-phase velocity + MoveAndSlide** | *yours* | Collision stays authoritative — you cannot end up inside geometry. | Robotic. Can stall forever without a timeout. No easing. |
| **Curve-driven position over fixed duration** | Common in indie / first-person | Easy to tune, easy to make feel good, no animation assets needed. | Usually implemented as a `GlobalPosition` lerp, which throws away collision safety. |

Note the pattern in the danimtz system: **the warp is split into a vertical phase and a horizontal
phase within one montage**. That's the same up-then-forward decomposition you arrived at
independently — the ordering is correct, they just blend across the crossover instead of snapping.

---

## Part 4 — What I'd change

### Tier 0 — do these regardless of any redesign

1. **Add a timeout to `Clamber()`.** Accumulate elapsed time; past `MaxClamberDuration`, abort and
   restore normal movement. The current code can permanently soft-lock a player.
2. **Move the cooldown off the failure path.** Detect every physics frame. Cooldown after a
   *completed or aborted* clamber only. Also actually assign `WaitPerCallInSec` to the timer, or
   delete both and use an elapsed-time float.
3. **Fix E2** — measure progress toward the destination, not distance from the start.
4. **Zero or damp `Velocity` on exit** so the player isn't launched off the ledge.
5. **Require forward input** (and optionally a facing-within-tolerance check, per danimtz) before
   clambering.

### Tier 1 — the detection overhaul

**Replace the raycast ladder with a hybrid of B and C: one forward ray for the wall, then an
up/forward/down sweep of the player's own shape for validation.** The ray gives you the wall normal
(facing checks, later animation alignment); the sweep gives you a provably-legal landing spot.

```csharp
// One forward ray gives the wall + its normal (facing checks, alignment).
// The sweep of the player's OWN shape gives a landing spot that is
// provably clear, correctly-heighted, and standable — replacing D1-D4, D7 in one step.
private bool TryFindClamber(out Vector3 landing, out Vector3 wallNormal)
{
    landing = Vector3.Zero;
    wallNormal = Vector3.Zero;

    if (!WallRay.IsColliding()) return false;
    wallNormal = WallRay.GetCollisionNormal();

    var body = PlayerController;
    var xform = body.GlobalTransform;
    var collision = new KinematicCollision3D();

    // 1. Up FIRST — going forward first tunnels through ceilings.
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
`SafeMargin` at 0.001 — the Godot stair-step community is unanimous that higher values cause
snagging and false positives.

**This deletes:** the whole `Raycasts` scene rig, `GetRaycastEndPoints`, `AttemptClamberCheckRow`,
`RaycastCollisionResult`, the `maxY` epsilon comparison, all the per-frame LINQ, and every
"quantized to ray spacing" tuning headache. Net line count goes down.

**What you lose and must decide about:** the multi-column corner handling (E) that the grid *could*
have supported. If off-centre ledges matter, keep it as a small forward ray fan for the wall pass
only, then run one sweep at the winning column. Don't rebuild the grid.

### Tier 2 — execution and structure

6. **Make clambering a real state.** `ClamberController` becomes pure detection —
   `bool TryFindClamber(out ...)` and `Vector3 GetClamberMotion(double delta)`. The state owns
   `MoveAndSlide` and the timeout. One `MoveAndSlide` per frame, one owner. This is what the
   statechart in `StateMachine/README.md` is for, and it structurally eliminates the soft-lock
   because "abort" becomes a transition rather than something you have to remember to write.

7. **Curve-driven progress instead of two hard phases.** Export two `Curve` resources (height,
   forward), evaluate at `t = elapsed / duration`, and drive the result through `MoveAndSlide`
   deltas — *not* a `GlobalPosition` lerp, so you keep the collision safety you currently have.
   `Curve` is a native editor-editable Godot resource: no dependency, no code, and it turns robotic
   motion into something tunable by feel. Scale `duration` with climb height so short and tall
   clambers both read correctly.

8. **In first person, spend the polish budget on the camera, not the body.** Nobody sees a body.
   A small pitch/roll/bob curve during the manoeuvre sells a mantle far harder than the body path
   does — and the stair-step writeup's warning about camera snap applies directly: smooth the offset
   or the transition reads as a teleport.

9. **Handle moving ledges (D10).** Cheapest correct fix: keep the collider reference from the down
   sweep and re-run detection every few frames during the clamber; if the surface moved beyond a
   threshold, abort. Falls out for free once abort is a transition.

10. **Add debug draw.** Sweep start/end transforms, the landing point, and the reject reason
    (no wall / no room / not standable / too low / too high). One `[Export] bool DebugDraw`. This
    pays for itself the first afternoon.

### Tier 3 — the strategic reframe

Right now you have one mechanic with one height band. The standard framing across all the systems
surveyed is **one detection pass, classified by height, with different execution parameters**:

| Height | Mechanic | Execution |
|---|---|---|
| 0 – 0.4 m | step-up | automatic, no state, same sweep run every frame — this is just stair-stepping |
| 0.4 – 1.2 m | vault / clamber | fast, retains forward momentum |
| 1.2 – 2.2 m | mantle | slower, triggerable from air or ground |
| > 2.2 m | ledge hang | grab and hold; second input pulls up |

The up-forward-down sweep answers all four with the same three `TestMove` calls — only
`MaxClamberHeight`, the duration, and the curves change per band. That's the argument for doing the
detection overhaul before adding anything else: **it's the version that scales to the rest of the
traversal kit for free**, whereas the raycast ladder needs a new rig per band.

Also worth adopting from Titanfall-lineage systems: allow detection **while airborne**, not only
from the ground. Mantling out of a jump is most of what makes first-person traversal feel fluid, and
it costs one condition.

---

## Summary

The execution half is more principled than most tutorials — keeping `MoveAndSlide` authoritative
throughout is a real advantage and you should protect it. The detection half has a structural
problem that no amount of tuning fixes: **front-face rays cannot know whether there's anything to
stand on**, which will produce railing-climbing and slope-climbing bugs that look like tuning
issues and aren't.

Highest-value changes, in order:

1. **Timeout on `Clamber()`** — the current code can soft-lock a player permanently.
2. **Cooldown off the failure path** — the "game ignored my input" bug, and the most player-visible.
3. **Swap detection for an up-forward-down `TestMove` sweep** — deletes five separate bug classes
   and the entire raycast rig at once, and is the version that scales to step-up/vault/mantle/hang.
4. **Make it a state in your existing statechart** — removes the duplicate `Clambering` flag and
   makes abort structural rather than something you have to remember.
5. **Curves + camera motion** — where the feel actually comes from, once it's correct.

---

## Sources

- [Ledge Climbing – Part 1 – Detecting a Ledge — Cinderflame Studios](http://cinderflame.com/ledge-climbing-1/)
- [Mantle/Ledge grabbing system — Daniel Martinez Amigo](https://danimtz.github.io/devpost/Mantle/)
- [Godot 4 Stair-Stepping — dresswithpockets](https://dresswithpockets.github.io/2025/03/19/godot-stair-stepping.html)
- [Andicraft/stairs-character — stair-stepping CharacterBody3D for Godot 4](https://github.com/Andicraft/stairs-character)
- [JheKWall/Godot-Stair-Step-Demo](https://github.com/JheKWall/Godot-Stair-Step-Demo)
- [Godot proposal #2751 — automatic step-up/step-down for CharacterBody](https://github.com/godotengine/godot-proposals/issues/2751)
- [ShapeCast3D — Godot Engine documentation](https://docs.godotengine.org/en/stable/classes/class_shapecast3d.html)
- [CharacterBody3D — Godot Engine documentation](https://docs.godotengine.org/en/stable/classes/class_characterbody3d.html)
- [Mantle — Advanced Movement System documentation](https://doc.spabastudio.com/knowledgebase/tutorials/blueprint-tutorials/manting/mantle)
- [PanicPetal/ALS-Community — Advanced Locomotion System V4](https://github.com/PanicPetal/ALS-Community/)
- [Motion Warping in Unreal Engine — Epic documentation](https://dev.epicgames.com/documentation/en-us/unreal-engine/motion-warping-in-unreal-engine)
- [Climbing Animation for Games: Ledge Grabs, Walls — MoCap Online](https://mocaponline.com/blogs/mocap-news/climbing-animation-games-guide)
- [Making Climbing/Vaulting System in Unity — Md Mohammad Sarfraz Alam](https://medium.com/@alamsarfraz422/making-climbing-vaulting-system-in-unity-225b11a636a)

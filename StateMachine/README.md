# State Machine

A hierarchical state machine (a *statechart*) built on Godot nodes. States are nodes in the scene
tree, so the tree **is** the diagram: nesting is hierarchy, and a `StateMachine` node drives whatever
sits under it.

It follows [W3C SCXML](https://www.w3.org/TR/scxml/) semantics for the parts that matter — exit and
entry sets computed from the least common ancestor, exit innermost-first, entry outermost-first,
deepest-source-wins transition priority.

---

## Why hierarchy

A flat FSM explodes. Ten movement states that can each be *rested* or *winded* is twenty states and
forty edges. A statechart gives you two tools to avoid that:

- **Compound states** nest related states. A transition declared on `Grounded` applies to every state
  inside it, so "if you leave the floor, you're airborne" is written **once** instead of once per
  ground state.
- **Parallel states** run independent regions at the same time. Movement and stamina become `n + m`
  states instead of `n × m`.

---

## The node types

| Node | Meaning |
|---|---|
| `StateMachine` | The driver. Add states under it, point `RootState` at the top state. Not a state itself. |
| `AtomicState` | A leaf. No children. |
| `CompoundState` | Exactly **one** child active at a time. `DefaultState` picks which child is entered when the compound is entered without a more specific target (defaults to the first child). Set `RememberActiveState` for shallow history: it resumes the child it last had active instead. |
| `ParallelState` | **All** children active at once. Independent regions. |
| `Transition` | An edge: a target, an optional guard, an optional effect. Created via `State.AddTransition(...)`, not placed in the tree. |

A typical tree:

```
StateMachine              <- RootState points at "Player"
└── Player                (ParallelState)
    ├── Movement          (CompoundState, default: Grounded)
    │   ├── Grounded      (CompoundState, default: Idle)
    │   │   ├── Idle      (AtomicState)
    │   │   ├── Walking   (AtomicState)
    │   │   └── Sprinting (AtomicState)
    │   └── Airborne      (CompoundState, default: Falling)
    │       ├── Rising    (AtomicState)
    │       └── Falling   (AtomicState)
    └── Stamina           (CompoundState, default: Rested)
        ├── Rested        (AtomicState)
        └── Winded        (AtomicState)
```

Because `Player` is parallel, `Movement` and `Stamina` are **both** always active. The machine's
current configuration is a *set* of states, not one state — here always one leaf from each region.

---

## The state lifecycle

Override these on your `State` subclass:

```csharp
public override void StateEntered()  { base.StateEntered(); /* set up */ }
public override void StateExited()   { base.StateExited();  /* tear down */ }
public override void StateProcessing(double delta)        { /* per render frame */ }
public override void StatePhysicsProcessing(double delta) { /* per physics frame */ }
```

`StateEntered`/`StateExited` fire **exactly once** per entry and exit, for every state on the path —
compounds and parallels included, not just leaves.

Processing is delivered by the `StateMachine` from its own `_Process`/`_PhysicsProcess`, walking only
the active configuration. State nodes do **not** define `_Process` themselves, so inactive states cost
nothing.

`Enabled` tells you whether a state is currently active. `Enable()`/`Disable()` are plain flag
setters the machine calls — **they are not hooks, do not override them, do not call them yourself.**

---

## Transitions

Declare them in the state's `_Ready`, which runs before the machine's `_Ready` and so gets validated
at startup:

```csharp
AddTransition(targetState, guard, effect);   // preferred: a State reference
AddTransition("Movement/Airborne/Rising");   // by path, relative to RootState
AddTransition("Winded");                     // by bare name, if unambiguous
```

- **guard** — `Func<bool>`, polled while the source is active. Omit for an unconditional edge.
- **effect** — `Action`, fired as the edge is taken. Runs *after* the source exits and *before* the
  target enters.

Both are optional. Targets can also be resolved after startup, but you lose the startup check.

Prefer a `State` reference: it is rename-safe and needs no lookup. Use a **path** whenever a bare name
is ambiguous — Godot only enforces unique names among siblings, so two leaves called `Idle` under
different parents are legal, and the bare name will refuse to resolve.

### Priority

1. **Deepest source wins.** A transition on `Sprinting` beats one on `Grounded`.
2. **Insertion order wins** within a single state. First eligible transition is taken.

### Imperative transitions

For one-off changes from inside a state:

```csharp
OnStateChangeRequired(new ChangeStateEventArgs("Movement/Airborne/Falling"));
```

This enqueues; it does not change state inline. Prefer declarative transitions — they keep the graph
readable from the outside.

---

## How a transition is applied

Given a source and a target:

1. **Transition domain** = the deepest state that is a *proper* ancestor of both.
2. **Exit** every active descendant of that domain, **innermost first**.
3. **Run the effect.**
4. **Enter** from the domain down to the target, **outermost first**, then resolve defaults below the
   target: a compound enters its `DefaultState` — or, with `RememberActiveState` set, the child it
   last had active — and a parallel enters every region.

The domain itself never exits or re-enters. Everything below it does.

Because the domain uses *proper* ancestors, a transition from a state to itself exits and re-enters it
— which is how you restart an animation or re-roll a timer.

### Microsteps

Transitions are queued, then drained to completion within one tick, so a chain through transient
states resolves in a single frame rather than one hop per frame. At most one transition per source
state is in flight at a time.

Guards are evaluated on **both** `_Process` and `_PhysicsProcess`, so a guard reading `IsOnFloor()` or
velocity samples them at the right cadence.

If more than 64 transitions resolve in one frame the machine assumes a loop, logs an error, and
clears the queue. An unguarded self-transition will do this.

---

## Worked example: player movement

Assumes a `PlayerController` exposing:

```csharp
public Vector2 MoveInput;    // current WASD vector
public bool SprintHeld;
public bool JumpPressed;     // true on the frame jump was pressed
public float Stamina;        // 0..1
public void Jump();          // applies jump velocity
```

### Movement region

`Grounded` owns the transitions that are true for *all* of its children — this is the hoisting that
hierarchy exists for. Written on each of `Idle`/`Walking`/`Sprinting` it would be three copies.

```csharp
public partial class GroundedState : CompoundState
{
    [Export] public PlayerController Player;

    public override void _Ready()
    {
        base._Ready();   // CompoundState._Ready collects children and validates DefaultState

        // Order matters: jump is checked before the plain fall-off-a-ledge edge.
        AddTransition("Rising", () => Player.JumpPressed, () => Player.Jump());
        AddTransition("Airborne", () => !Player.IsOnFloor());
    }
}
```

Note the two targets. `Rising` is explicit — a jump means rising specifically. `Airborne` is the
compound, so walking off a ledge lands in its `DefaultState` (`Falling`) without naming it.

```csharp
public partial class AirborneState : CompoundState
{
    [Export] public PlayerController Player;

    public override void _Ready()
    {
        base._Ready();
        AddTransition("Grounded", () => Player.IsOnFloor());
    }
}
```

Leaves carry only what is specific to them:

```csharp
public partial class IdleState : AtomicState
{
    [Export] public PlayerController Player;

    public override void _Ready()
    {
        AddTransition("Walking", () => Player.MoveInput != Vector2.Zero);
    }
}

public partial class WalkingState : AtomicState
{
    [Export] public PlayerController Player;

    public override void _Ready()
    {
        AddTransition("Idle", () => Player.MoveInput == Vector2.Zero);
        AddTransition("Sprinting", () => Player.SprintHeld && Player.Stamina > 0f);
    }

    public override void StatePhysicsProcessing(double delta) => Player.Speed = 5.0f;
}

public partial class RisingState : AtomicState
{
    [Export] public PlayerController Player;

    public override void _Ready()
    {
        AddTransition("Falling", () => Player.Velocity.Y <= 0f);
    }
}
```

`Falling` needs no outgoing transition at all — landing is `Airborne`'s job, one level up.

### Stamina region

Completely independent. It never mentions movement states, and movement never mentions it:

```csharp
public partial class RestedState : AtomicState
{
    [Export] public PlayerController Player;

    public override void _Ready() => AddTransition("Winded", () => Player.Stamina <= 0.05f);
}

public partial class WindedState : AtomicState
{
    [Export] public PlayerController Player;

    public override void _Ready() => AddTransition("Rested", () => Player.Stamina >= 0.5f);
}
```

The two regions only meet through `Player.Stamina` — a plain field, not a state reference. That is the
point: adding a third region later costs nothing to the other two.

### Trace: walking, then jump

Configuration before: `Player`, `Movement`, `Grounded`, `Walking`, `Stamina`, `Rested`.

`Grounded`'s first transition becomes eligible, targeting `Rising`.

| Step | Result |
|---|---|
| Domain | `LCA(Grounded, Rising)` → `Movement` |
| Exit (innermost first) | `Walking`, `Grounded` |
| Effect | `Player.Jump()` |
| Entry (outermost first) | `Airborne`, `Rising` |

`GetStateMachineString()` afterwards:

```
Player(Movement(Airborne(Rising)), Stamina(Rested))
```

Three things to notice:

- **`Movement` did not exit and re-enter.** It is the domain, so it is untouched.
- **The `Stamina` region was not disturbed at all.** Parallel regions are independent.
- **`Walking` exited even though the transition was declared on `Grounded`.** The exit set is
  everything active below the domain, not just the source.

Now let go of jump. `Rising` → `Falling` when velocity turns over — a shallow transition, domain
`Airborne`, so `Airborne` stays entered. On landing, `Airborne`'s transition to `Grounded` fires and
`Grounded` default-enters `Idle`.

### The one sharp edge

Deepest-source-wins means a child transition can shadow a parent one. If you are `Sprinting`, run out
of stamina, *and* walk off a ledge on the same frame, `Sprinting`'s edge to `Walking` beats
`Grounded`'s edge to `Airborne`, because `Sprinting` is deeper. You stay grounded for one extra frame.

When a parent condition must always win, make the child guards exclude it:

```csharp
AddTransition("Walking", () => !Player.SprintHeld && Player.IsOnFloor());
```

### Hoisting a terminal state: both traps at once

The enemy brain (`EnemyStates/`) is the second machine in this project and it hit both. `Brain` is a
compound containing `Idle`, `Chase`, `Attack` and `Dead`, and death is true from anywhere, so the
edge to `Dead` is written once on the parent instead of three times on the children:

```csharp
_dead = GetNode<State>("Dead");
AddTransition(_dead, () => !_enemy.Alive && !_dead.Enabled);
```

Both halves of that guard exist because something broke without them.

- **`!_enemy.Alive` is repeated on every child guard** (`AddTransition("Attack", () => Enemy.Alive
  && ...)`). Deepest-source-wins means `Chase → Attack` beats the parent's `→ Dead` forever, so a
  corpse ping-pongs between Chase and Attack and never dies. This is the remedy above, applied.
- **`!_dead.Enabled` stops the parent edge firing at its own descendant.** A compound's edges are
  evaluated while *any* descendant is active, and `!Alive` stays true after arriving — so the machine
  exits and re-enters `Dead` every frame. That re-runs `StateEntered`, which reset the corpse's
  removal timer, and the body lay there permanently one frame away from disappearing. It never
  tripped the loop cap, because one transition per frame is not a loop.

The rule that falls out: **a hoisted edge to a terminal state must exclude the terminal state.**

---

## Rules

- **Call `base._Ready()`** when overriding `_Ready` on a `CompoundState` or `ParallelState`. They
  collect their children there; skipping it throws.
- **Guards must be side-effect free.** They are polled twice per frame and may be evaluated without
  the transition being taken.
- **Entry logic goes in `StateEntered`,** never in `Enable()` or `StateProcessing`.
- **A compound state must have at least one child** and its `DefaultState` must be one of them.
- **Don't name two states the same** unless you always address them by path.
- **A hoisted edge to a terminal state must exclude that state** in its guard, or it re-enters it
  every frame. See "Hoisting a terminal state" above.

---

## Tests

`StateMachineTests.cs` covers ordering, default entry, parallel region entry, parent-vs-child
priority, self-transitions, name collisions, and the loop cap.

```
"C:\Godot\Godot_v4.7-stable_mono_win64\Godot_v4.7-stable_mono_win64_console.exe" \
    --headless --path . res://test_state_machine.tscn
```

Exit 0 is a pass. One `ERROR: StateMachine exceeded 64 transitions` line is expected — that is the
loop-cap test asserting the guard fires.

# Components

A GameObject is defined by the components hanging off it. Every object that participates in
gameplay â€” the player, an enemy, a door, a health pickup â€” gets one `Components` child node, and
under it, one node per capability.

```
CharacterBody3D            <- the GameObject
â”œâ”€â”€ Components
â”‚   â”œâ”€â”€ HealthComponent    <- it can be damaged and killed
â”‚   â””â”€â”€ ...
â”œâ”€â”€ CollisionShape3D
â””â”€â”€ Camera3D
```

The set of children **is** the object's contract. There is no `IGameObject`, no `Damageable`
interface, no entity base class. "Can this be shot?" is answered by asking the object whether it has
a `HealthComponent`, and the answer being *no* is a normal answer, not an error.

---

## Why

Godot already gives you composition through the scene tree, but with nothing to organise it a
character node ends up with fifteen unrelated children and no way to tell "this describes the
object" from "this is geometry". The `Components` node is that separation, and it costs one node.

The real payoff is that a capability written once works on everything. `HealthComponent` is on the
player and on every enemy â€” the same file, no subclassing, no shared base with virtual hooks. When
the fourth thing that can be destroyed turns up (a barrel), it is one node in a scene, not a class.

---

## The rules

1. **One `Components` node per GameObject, named exactly `Components`.** Lookup depends on the name.
2. **Components are direct children of it.** No nesting components inside components; if two things
   are that entangled they are one component.
3. **One concern per component.** A component holds the data for its concern and the behaviour that
   only makes sense against that data. When you find yourself grouping a component's exports under
   two headings, it is two components.
4. **No component requires another to exist.** Look siblings up, branch on null. A component that
   throws when its partner is missing has quietly reinvented the inheritance you were avoiding.
5. **Method calls in, signals out.** Other code calls a component (`TakeDamage`); the component
   announces what happened (`Damaged`, `Died`) and never knows who is listening. This is what keeps
   HUD, sound, ragdoll and score from all having to be wired into `HealthComponent`.
6. **Look up by type, not by node name.** `Component.Get<T>` ignores names, so renaming a node in
   the editor breaks nothing.

---

## Writing one

Derive from `Component`, mark it `[GlobalClass]` so it shows up in Godot's *Add Node* dialog, and
expose the tuning knobs as `[Export]`s so a designer sets them per object in the scene.

```csharp
[GlobalClass]
public partial class ExampleComponent : Component
{
    [Signal] public delegate void SomethingHappenedEventHandler();

    [Export] public float Knob = 1f;

    public override void _Ready()
    {
        base._Ready();   // resolves GameObject; skipping it leaves that null
        // ...
    }
}
```

`Component` gives you exactly two things:

| Member | Meaning |
|---|---|
| `GameObject` | The node this component describes â€” the parent of the `Components` container. |
| `Component.Get<T>(node)` | The `T` on that node's GameObject, or **null**. Walks up from `node` to the first ancestor carrying a `Components` child, so you can pass a collider the physics engine handed you â€” a turret's body, an enemy's hitbox â€” and get the object that owns it. The walk stops at that first GameObject: one that has components but not this one answers null rather than inheriting its parent's. |

A component ticks itself with `_Process`/`_PhysicsProcess` if it needs to, and most don't.

---

## Changing another component's behaviour

Sooner or later a new component has to alter what an existing one does â€” a shield that soaks damage
before it reaches hit points, armour that scales it, a buff that changes a rate. The rule is that
**the newcomer attaches itself to the incumbent, never the reverse.**

The incumbent exposes a slot. It is a plain delegate field, not a `[Signal]`, because a signal
cannot return a value and this kind of hook has to hand something back:

```csharp
// HealthComponent
public Func<float, Vector3, float> AbsorbDamage;    // incoming damage in, damage that gets through out

// ...in TakeDamage, before anything is applied
if (AbsorbDamage is not null) amount = AbsorbDamage(amount, fromPosition);
if (amount <= 0f) return;
```

The newcomer fills it in `_Ready`, and errors rather than silently displacing whatever was there:

```csharp
// ShieldComponent
if (_health.AbsorbDamage is not null) GD.PushError(...);
else _health.AbsorbDamage = Absorb;
```

What this buys: `HealthComponent` never mentions shields, every existing caller of `TakeDamage` is
untouched, and an object with no shield is the identity case rather than a special case. What it
costs, and you should know before reaching for it: reading `HealthComponent` gives you no hint that
shields exist â€” you have to grep the hook's name. One slot means one absorber; chain them at the
hook when a second one genuinely exists, not before. And if a component is ever freed while its
partner survives, the stale delegate throws on the next call, so a component that can be removed at
runtime must clear the slot in `_ExitTree`.

---

## Components vs. states

Both describe the object, and it is worth being clear which is which, because putting something in
the wrong one is the expensive mistake here.

- A **component** is a *noun*: it has health, it carries ammo, it can be picked up. Long-lived,
  owns data, and the object either has it or doesn't for its whole life.
- A **state** (see `StateMachine/README.md`) is a *mode*: walking, reloading, chasing. Short-lived,
  mutually exclusive with its siblings, and switching between them is the point.

They compose the obvious way: a component may own a `StateMachine` for its own modes, and a state
may read a component (`Component.Get<HealthComponent>(Player).Current`). What should not happen is a
component tracking "am I currently reloading" with a bool, or a state holding the ammo count that
survives leaving the state.

---

## Health

`HealthComponent` is the first one, and the shape every other should follow.

```csharp
[Export] public float Max;                                  // set per object in the scene
public float Current { get; }                               // read-only from outside
public bool Alive { get; }
public void TakeDamage(float amount, Vector3 fromPosition = default);
[Signal] Damaged(float amount, Vector3 fromPosition);
[Signal] Died();
```

`fromPosition` is the damage source in world space, and `Vector3.Zero` means the damage had no
direction (a fall, poison). It is carried through to the signal because the interesting listeners â€”
the camera's directional damage punch, a hit indicator, enemy "who shot me" logic â€” all need it, and
none of them are in a position to ask afterwards.

Damage at or below zero is a no-op, overkill clamps to zero rather than going negative, and a dead
component absorbs further damage silently so `Died` can only ever fire once. That last one matters:
two pellets landing on the same frame must not run every death listener twice.

`Damaged` fires only for damage that actually reaches hit points. With a shield installed, a hit the
shield soaks is silent here â€” "took real damage" and "was hit at all" are different questions, and
the second one is answered by listening to the absorber too.

A caller looks like this, and works against anything in the world without knowing what it is:

```csharp
if (Component.Get<HealthComponent>(hit.Collider) is { } health)
    health.TakeDamage(Damage, GlobalPosition);
```

---

## Shield

`ShieldComponent` is a Halo-style regenerating shield, and the first component that changes another
one's behaviour â€” it installs itself into `HealthComponent.AbsorbDamage` and nothing else in the
project changes. Adding the node to an object is the entire integration.

```csharp
[Export] public float Max;
[Export] public float RechargeDelay, BrokenRechargeDelay;    // seconds without damage
[Export] public float RechargeRate, BrokenRechargeRate;      // points per second
public float Current { get; }
public bool Up { get; }
[Signal] Damaged(float amount, Vector3 fromPosition);
[Signal] Broken();
[Signal] Recharged();    // back to full
```

| Situation | Delay before recharge | Rate once it starts |
|---|---|---|
| Chipped, still up | `RechargeDelay` (3s) | `RechargeRate` (25/s) |
| Broken | `BrokenRechargeDelay` (6s) | `BrokenRechargeRate` (50/s), until full |

Three decisions worth knowing, because they are what makes it feel like a shield rather than like
extra health:

- **No bleed-through.** The hit that pops the shield is absorbed *whole*, however large it was, so a
  one-point shield is still a free hit. Remove this and a shield is just a second health bar.
- **The break latches until full.** A break means the long wait *and* the fast refill, and the fast
  rate survives the whole climb back to max. The punishment lives in the delay, not the rate.
- **Any damage restarts the timer,** including damage passing through a shield that is already down.
  Sustained fire has to keep a broken shield broken.

The recharge cooldown is a float counted down in `_PhysicsProcess`, not a `Timer` node â€” the
component already ticks for the refill ramp, so the countdown needs no child node and no scene
wiring. Running on the physics tick also means the default `Pausable` process mode stops recharging
during a pause for free.

---

## Interaction

Two components and no wiring between them. `InteractableComponent` marks an object as usable and
announces `Interacted`; `InteractorComponent` on the player finds one and presses it.

```csharp
// InteractableComponent -- knows nothing about what it is attached to
[Export] public string Verb = "interact";   // completes "Press E to ___", mutable at runtime
[Export] public bool Enabled = true;
[Signal] Interacted();

// InteractorComponent -- on the player
[Export] public float Range = 3f;
public InteractableComponent Target { get; }   // under the crosshair right now, or null
```

**There is no interact volume.** `InteractorComponent` raycasts from the camera and asks whatever it
hit for an `InteractableComponent`, exactly as `Projectile` asks for a `HealthComponent`. So an
object becomes interactable by carrying the component and having a collider â€” which anything solid
enough to walk up to already has. That buys three things an `Area3D` per interactable would not:
range is one export instead of a hand-authored volume on every object, line of sight is free (the ray
stops at the wall in front of the thing), and the *nearest* thing you are looking at wins with no
tie-breaking between overlapping volumes.

The ray is a manual `IntersectRay` rather than a `RayCast3D` node because it has to follow the
camera, and a component under `Components` cannot inherit the camera's rotation without living
somewhere else in the tree or copying its transform every frame.

Behaviour attaches the usual way â€” the specific subscribes to the generic, never the reverse:

```csharp
// GunComponent._Ready
_switch = Get<InteractableComponent>(GameObject);
if (_switch is not null) { _switch.Interacted += Toggle; UpdateVerb(); }
```

`Verb` is mutable on purpose. A switch has to read "turn the turret on" or "turn the turret off"
depending on which way it is currently thrown, and only the thing that owns the behaviour knows
which â€” so `Toggle` rewrites it. The prompt describes what pressing the key will *do*, not what the
object currently is.

**The subscriber does not have to be a sibling.** A console across the room is the ordinary case, and
`test_level`'s blue cube is one: `EnemyController` takes an `[Export] InteractableComponent Switch`
wired by NodePath to the cube next to the enemy, and toggles its own `Active` off that. The
interactable still knows nothing â€” it announces, and whatever cares subscribes, near or far.

`Hud` renders it: it polls `_interactor.Target` alongside the bars and shows `Press {key} to {Verb}`.
The key comes from `InputMap.ActionGetEvents("interact")`, not a literal, so the prompt cannot start
lying the day there is a rebinding screen.

---

## Gun

`GunComponent` spits `projectile.tscn` down its own -Z every `Interval` seconds while `Firing`. It
does not track, aim, lead, or check line of sight â€” where it points is wherever the object carrying
it faces, and that is the carrier's problem.

**`Firing` is the seam, and it is why two very different things share this one component.** A turret
bolts it to something that never moves and leaves `Firing` on, so shots go down one fixed lane
forever. The enemy bolts it to a body that yaws to face you and lets its `Attack` state switch
`Firing` on and off â€” the same fixed -Z becomes an aimed weapon, for nothing. And because the
countdown only runs while `Firing` is true, every switch-on costs a full `Interval` before the first
shot, which is exactly the telegraph delay an enemy wants.

`test_level` has two turrets. The first is always on (`Firing` defaults true and it carries no
interactable). The second starts off and is wired to a switch â€” see Interaction above â€” and its body
is 2m rather than 1.2m so it meets the player's 1.5m eye line; a short box would have to be aimed at
from above.

That is deliberate for what it's for. Tuning health and shields wants damage arriving on a schedule
you can predict and step out of, not an opponent â€” and "walk out of the line of fire" needs no code
at all, the same way the shot being stopped by a wall needs no code that knows what cover is.

It shows off two things the component system is for:

- **The enemy is invulnerable purely by having no `HealthComponent`.** There is no invulnerable flag
  and no branch anywhere â€” `Component.Get<HealthComponent>` returns null and the projectile does
  nothing. `test_level`'s `Enemy` node is a `Components` node with a turret in it and nothing else.
- **`Projectile` targets nobody.** It asks whatever it collided with for a `HealthComponent` and
  damages it if there is one, which is the caller snippet from the Health section verbatim. The same
  projectile works against the player, a future enemy, or a destructible crate, and the wall case
  falls out for free.

The component's own position *and rotation* are the muzzle â€” the one case so far where a component
being a `Node3D` matters. In `test_level` it sits just past the end of the `Barrel` box, which is
decoration with no collider of its own; the `Enemy` node is yawed so that line runs through the
player's spawn.

---

## Tests

`HealthComponentTests.cs` covers owner resolution, lookup (including the missing-component case),
the damage clamp, non-positive damage, and single-fire death.

`ShieldComponentTests.cs` covers installation, absorption, no-bleed-through, pass-through once
broken, both delays, both rates, the timer reset mid-recharge, the latch clearing at full, and that
a corpse does not regenerate. It drives time by calling `_PhysicsProcess` directly with a fixed
delta instead of waiting real frames, which keeps it deterministic and synchronous.

`GunComponentTests.cs` runs the real `test_level` and waits: it proves the whole chain is actually
connected in the scene â€” turret fires, projectile collides for real, it finds a `HealthComponent`,
and the damage routes through the shield's hook â€” and that the enemy has no health of its own.

`EnemyTests.cs` runs `test_level` end to end for the enemy: the navmesh has polygons, the enemy sleeps
out of range, wakes and measurably closes the distance, enters Attack and lands a shot on the player's
shield, then dies and is removed. It switches both turrets off first, or their fire is a second
unrelated source of damage in the middle of every assertion.

`InteractableComponentTests.cs` also runs `test_level`, because the ray is the part that can silently
miss. It aims the player at the second turret, checks the target resolves **from a hit on the child
`Body` collider** (the ancestor walk, end to end), checks the prompt text, toggles the turret on and
off with injected `E` presses, and checks the target goes null when looking away and when standing
out of range.

```
"E:\Godot\Godot_v4.7-stable_mono_win64\Godot_v4.7-stable_mono_win64_console.exe" \
    --headless --path . res://Tests/test_health.tscn
    --headless --path . res://Tests/test_shield.tscn
    --headless --path . res://Tests/test_gun.tscn
    --headless --path . res://Tests/test_interact.tscn
    --headless --path . res://Tests/test_enemy.tscn
```

Exit 0 is a pass.

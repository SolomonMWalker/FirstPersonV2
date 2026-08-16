# Ragdoll Joints — From a Bare Skeleton to a Working Ragdoll

*Reverse-engineered from `godot-physics-bone-examples` (akjava), cross-checked against the Godot 4 `PhysicalBone3D`/`Skeleton3D` docs and the official ragdoll tutorial. Companion to [`RIGGED_CHARACTER_PIPELINE.md`](RIGGED_CHARACTER_PIPELINE.md) §05, which covers this project's current (deliberately deferred) hitbox setup.*

## The one idea worth remembering

A good-looking ragdoll is not the result of hand-tuning joint spring/damping values. In the example project, every `PhysicalBone3D` ships with Godot's stock joint constraint defaults (`bias 0.3`, `damping 1.0`, `impulse_clamp 0.0`) and an unconstrained **Pin** joint — nothing exotic. What actually makes it look natural is upstream of the joint math entirely: **how many physical bones exist, and how their capsules are sized.**

That's the gap between "jointless/badly jointed" and "perfect": most bad ragdolls have *too many* physical bones (one per skeleton bone, Godot's own auto-generated default), which produces a jittery chain of undersized capsules with a joint at every knuckle. The example project uses **one physical bone per limb segment**, spanning several skeleton bones at once, sized to fill the limb. Fewer, bigger, better-placed rigid bodies beat more, smaller, precisely-constrained ones.

## Why "one PhysicalBone3D per bone" fights you

Select a `Skeleton3D` → toolbar → *Create Physical Skeleton* and Godot generates one `PhysicalBone3D` + capsule per skeleton bone, auto-sized to the mesh. This is also what's currently deferred in this project's own Grunt setup (§05 of the pipeline doc). It looks thorough, but for simulation it's a trap:

- Fingers, toes, and clavicles become independent rigid bodies with their own joints — mass you don't want, capsules too thin to matter, and more joints for the solver to fight into a stable pose every frame.
- Every extra joint in a chain is another place for the physics solver to lose energy and jitter, especially at Godot's default physics tick rate.
- A whole forearm-to-fingertip chain of 4-5 tiny capsules reads as "twitchy skeleton," not "arm."

The fix isn't better constraint tuning — it's fewer bones.

## The technique: one capsule per limb segment

The example script (`5bones_ragdoll_test_main.gd`) builds a full ragdoll for a ~50-bone character out of **six** `PhysicalBone3D` nodes total:

| Physical bone | Spans skeleton bones | height_mult | radius_ratio | notes |
|---|---|---|---|---|
| Torso | `DEF-spine` → `DEF-spine.005` | 0.75 | 0.2 | `rotating=false` — see below |
| Right leg | `DEF-thigh.R` → `DEF-toe.R` | 0.9 | 0.1 | whole leg, one capsule |
| Left leg | `DEF-thigh.L` → `DEF-toe.L` | 0.9 | 0.1 | |
| Right arm | `DEF-upper_arm.R` → `DEF-f_middle.01.R` | 0.9 | 0.2 | shoulder to fingertip, one capsule |
| Left arm | `DEF-upper_arm.L` → `DEF-f_middle.01.L` | 0.9 | 0.2 | |
| Head | `DEF-spine.005` → `DEF-spine.006` | 4 | 0.8 | placed *past* the neck bone, see below |

Six rigid bodies, six joints, one per limb. That's the whole skeleton.

### The capsule-sizing formula

Each physical bone's `CollisionShape3D` is a `CapsuleShape3D` derived purely from the two named bones' global rest positions — no manual shape editing:

```gdscript
var origin1 = skeleton.get_bone_global_pose(skeleton.find_bone(bone_name1)).origin
var origin2 = skeleton.get_bone_global_pose(skeleton.find_bone(bone_name2)).origin
var diff = origin2 - origin1

capsule.height = diff.length() * shape_height_multiply
capsule.radius = capsule.height * radius_ratio
shape.global_transform.origin = origin1 + diff * shape_location_ratio   # default ratio 0.5 = midpoint
```

- `shape_height_multiply` stretches the capsule past the raw bone-to-bone distance (arms/legs use 0.9 — slightly *shorter* than the full span so neighboring capsules don't overlap and jitter against each other; the head uses 4 to turn a thin neck-to-crown span into a head-sized blob).
- `radius_ratio` is radius as a fraction of height — thin for legs (0.1), thicker for arms/torso (0.2), and deliberately fat for the head (0.8, since a head reads as round, not capsule-shaped).
- `shape_location_ratio` defaults to 0.5 (capsule centered between the two bones). The head uses `2` — that pushes the capsule's center *past* `DEF-spine.006` entirely, which combined with the `4x` height stretch approximates a head volume from a neck bone that has no "head bone" to point at.
- `rotating=false` on the torso keeps the collision shape's local rotation at identity instead of aligning it to the spine's rest-pose twist. Spine bones in most rigs have an odd roll baked into their rest transform; without this flag the torso capsule comes out visibly cocked to one side.

### How the joints connect without a node hierarchy

All six `PhysicalBone3D` nodes are flat siblings, direct children of `Skeleton3D` — none is nested inside another in the scene tree. They still form a correct kinematic chain (leg → torso → arm → head) because **Godot wires joints by walking the skeleton's *bone* parent hierarchy, not the node tree.** When a `PhysicalBone3D` is assigned `bone_name`, Godot walks up that bone's ancestors in `Skeleton3D`'s rest hierarchy until it finds the nearest ancestor bone that also has a `PhysicalBone3D`, and joints to *that*. Scene-tree nesting is irrelevant.

Practically: you never need to parent physical bones under each other. Add them all as siblings under `Skeleton3D` (or a `PhysicalBoneSimulator3D` child of it, in current Godot versions), set each one's `bone_name`, and the joint chain assembles itself from bone ancestry.

## Step-by-step: converting a bare or badly-jointed skeleton

### 0. Pick limb-segment endpoints, not individual bones

Look at the skeleton's bone list and group it into ~5-8 segments: pelvis/spine, each upper arm through hand, each thigh through foot, head/neck. Skip fingers, toes, clavicles, and twist-helper bones entirely — they contribute nothing to how a ragdoll reads and only add solver work.

### 1. Create the physical bones

Either:
- **Editor:** *Create Physical Skeleton*, then delete everything that isn't one of your chosen segment-start bones, and manually re-point the survivors' `bone_name` / resize shapes to span the full segment, or
- **Script**, adapting the example addon's `add_bone()` (`addons/akjava_physical_bone3d/physical_bone3d_utils.gd` in the reference project) — pass it `(skeleton, start_bone_name, end_bone_name, height_multiply, radius_ratio)` per segment and it creates the `PhysicalBone3D` + sized/placed `CapsuleShape3D` for you using the formula above. This is the faster, more repeatable path if you'll iterate on the rig more than once.

### 2. Size and place each capsule

Use the formula above. Start with `height_multiply ≈ 0.85-0.95` (a hair short of the raw span, so neighboring limbs don't overlap) and `radius_ratio ≈ 0.1` for legs, `~0.2` for arms/torso. Tune per-character from there — these aren't physical constants, they're what looked right on this rig.

### 3. Choose joint types

Every `PhysicalBone3D.joint_type` defaults to `JOINT_TYPE_PIN` (unconstrained ball joint — free rotation, no limits). That alone is enough for a "perfect-looking" *loose, floppy* death ragdoll, which is exactly what the example project ships for all six joints. Pin is the right default when you just want a corpse to flop convincingly.

If you need the ragdoll to look anatomically constrained (an elbow that won't bend backward, a knee that won't hyperextend), mix in the other types per-joint — Godot's tutorial recommends:

| Joint | `joint_type` enum | Good for | Typical limits |
|---|---|---|---|
| Pin | `JOINT_TYPE_PIN` (1) | anything where "floppy" is fine | none (free) |
| Cone (ball-and-socket) | `JOINT_TYPE_CONE` (2) | shoulders, hips, neck | swing span 20-90°, twist span 20-45° |
| Hinge | `JOINT_TYPE_HINGE` (3) | elbows, knees | enable Angular Limit, set lower/upper |
| Slider | `JOINT_TYPE_SLIDER` (4) | rarely used for ragdolls | — |
| 6DOF | `JOINT_TYPE_6DOF` (5) | anything needing per-axis control | enable per-axis via `joint_constraints/x|y|z/angular_limit_enabled` + `_lower`/`_upper` |

Set joint limits **before** resizing/repositioning collision shapes — rotating a joint also rotates its child shape, so doing it in the other order means redoing the shape sizing.

### Physics backend note: `bias`/`softness`/`relaxation` do nothing on Jolt

`PhysicalBone3D`'s Cone and Hinge joints expose `joint_constraints/bias`, `joint_constraints/softness`, and `joint_constraints/relaxation` (`angular_limit_bias`/`angular_limit_softness`/`angular_limit_relaxation` for Hinge). Those three are a **Godot Physics** thing — they map to Bullet-style constraint softness, and Godot Physics silently honors them. **Godot Jolt does not implement them at all.** Setting any of the three on a Cone or Hinge joint logs a warning per joint per simulation start and the value is discarded:

```
WARNING: Cone twist joint bias is not supported when using Jolt Physics. Any such value will be ignored.
WARNING: Cone twist joint softness is not supported when using Jolt Physics. Any such value will be ignored.
WARNING: Hinge joint bias limit is not supported when using Jolt Physics. Any such value will be ignored.
WARNING: Hinge joint softness is not supported when using Jolt Physics. Any such value will be ignored.
```

This project runs on Jolt, so these three properties are dead weight on every Cone/Hinge joint — don't set them, and delete them if `Create Physical Skeleton` or an old edit left them in. **The only levers that actually change how "stiff" a joint feels on Jolt are the ones already in the table above:** `swing_span`/`twist_span` for Cone, `angular_limit_upper`/`angular_limit_lower` for Hinge (or dropping the joint back to Pin). There is no Jolt-side equivalent exposed through `PhysicalBone3D` for a *soft* limit that gives before it hard-stops — on Jolt a joint limit is a hard wall at whatever angle you set, full stop.

If this project ever runs under Godot Physics instead of Jolt, `bias`/`softness`/`relaxation` come back to life and are worth revisiting — but tune them there when it's actually the active backend, not preemptively.

### 4. Collision layers and masks — the gotcha that bites everyone

Put every `PhysicalBone3D` on its own dedicated physics layer with `collision_mask = 0`. If the ragdoll bones share a layer with the character's movement capsule, an inactive ragdoll collides with its own (still-animating) body — the character appears to stick to or bounce off itself. This project already hit exactly this failure mode once (see `GruntWiringTests.cs` / pipeline doc §04 gotcha) and it's called out in Godot's own ragdoll tutorial as the most common pitfall.

### 5. Start and stop simulation

```gdscript
skeleton.physical_bones_start_simulation()          # all bones go limp
skeleton.physical_bones_start_simulation(["l_arm"]) # partial ragdoll — only these bones
skeleton.physical_bones_stop_simulation()            # hand control back to AnimationPlayer/Tree
```

(In current Godot, this lives on `PhysicalBoneSimulator3D` if you've added one explicitly as a child of `Skeleton3D`; the `Skeleton3D` methods above still work via its internal simulator for the simple case.) Rebuilding the physical bones fresh before each simulation start — as the example project does in `_create_ragdoll()` — sidesteps a known issue where changing `joint_type` after a simulation has already run doesn't always take effect cleanly.

## Checklist

- [ ] Physical bones grouped by limb segment, not one per skeleton bone (aim for 5-8 total, not 20+)
- [ ] Capsule height/radius derived from bone-to-bone distance, not eyeballed
- [ ] `height_multiply` slightly under 1.0 so neighboring capsules don't overlap
- [ ] Torso/spine capsule uses `rotating=false` (or manually zeroed shape rotation) if it comes out twisted
- [ ] All physical bones are flat children of `Skeleton3D` (or its `PhysicalBoneSimulator3D`) — no manual nesting needed, joints follow bone ancestry automatically
- [ ] Every physical bone on its own non-colliding layer, `collision_mask = 0`
- [ ] Joint type chosen per-joint: Pin where floppy is fine, Cone/Hinge/6DOF with limits where it needs to look anatomical
- [ ] Joint limits set before final shape sizing/positioning
- [ ] On Jolt: no `bias`/`softness`/`relaxation` set on Cone or Hinge joints — they're ignored with a warning; only the span/angle limits actually do anything

## Sources

- [PhysicalBone3D — Godot 4 class reference](https://docs.godotengine.org/en/stable/classes/class_physicalbone3d.html)
- [Skeleton3D — Godot 4 class reference](https://docs.godotengine.org/en/stable/classes/class_skeleton3d.html)
- [Ragdoll system — Godot 4 official tutorial](https://docs.godotengine.org/en/stable/tutorials/physics/ragdoll_system.html)
- [`godot-physics-bone-examples`](https://github.com/akjava/godot-physics-bone-examples) — the example project this guide is reverse-engineered from (`5bones_ragdoll_test_main.gd`, `addons/akjava_physical_bone3d/physical_bone3d_utils.gd`)
- [`jolt_cone_twist_joint_3d.cpp`](https://github.com/godotengine/godot/blob/master/modules/jolt_physics/joints/jolt_cone_twist_joint_3d.cpp) / [`jolt_hinge_joint_3d.cpp`](https://github.com/godotengine/godot/blob/master/modules/jolt_physics/joints/jolt_hinge_joint_3d.cpp) — source of the `bias`/`softness`/`relaxation` "not supported" warnings on Jolt

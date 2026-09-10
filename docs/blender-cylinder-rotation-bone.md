# Adding `revolverCylinderRotationBone` for Godot-driven cylinder spin

## Goal

Keep the existing animation setup untouched, but add a dedicated bone that Godot
can rotate for the 6-chamber indexing spin — stacked *on top of* whatever the
animations are doing.

- `revolverCylinderBone` — **unchanged**. The actions already drive it (the
  cylinder swing-out / eject during reload).
- `revolverCylinderRotationBone` — **new**. A child of `revolverCylinderBone`,
  sitting exactly on top of it. The cylinder mesh and the bullet meshes move to
  this bone.
- Result: the new bone rigidly inherits everything the animations do to its
  parent, and Godot adds the indexing rotation to the child. You get the reload
  motion for free and only author the spin in code.

## Current state (verified against commit `fc41633`)

Single armature `RigArmature`, no linked libraries, no duplicates.

```
palm.r
└─ handHold.r                     (deform OFF — attachment point only)
   └─ revolverRootBone
      ├─ revolverCylinderBone     ← animations drive this
      ├─ revolverTriggerBone
      └─ revolverHammerBone
```

Mesh → vertex group (every mesh is 100 % weighted to one group, weight `1.000`,
no unweighted verts):

| Mesh | Vertex group |
|---|---|
| `revolverCylinderMesh` | `revolverCylinderBone` |
| `bulletCasing1` … `bulletCasing6` | `revolverCylinderBone` |
| `bulletWithCasing1` … `bulletWithCasing6` | `revolverCylinderBone` |
| `revolverMesh` | `revolverRootBone` |
| `revolverHammerMesh` | `revolverHammerBone` |
| `revolverTriggerMesh` | `revolverTriggerBone` |

Actions that key `revolverCylinderBone`: `RevolverCylinderOpen`,
`RevolverCylinderClose`, `RevolverRigDefault`, and every `RevolverRigReload*`
except the two `StartOpenCylinder*` ones.

## Why the earlier attempts broke — and the rule that prevents each

1. **Connected child bone.** Extrude makes the new bone *connected* to the
   parent. Connected bones share the joint, so later nudging the new bone (or its
   head) drags `revolverCylinderBone`'s **tail**, which changes that bone's rest
   orientation. Every keyframe on `revolverCylinderBone` is stored relative to
   its rest pose, so the whole cylinder animation now evaluates from a wrong
   basis and flies off.
   → **The new bone must be Disconnected.**

2. **Bone placed off the spin axis.** If the new bone's head/direction don't lie
   on the cylinder's barrel axis, rotating it *wobbles* the cylinder instead of
   spinning it cleanly.
   → **The new bone must be coincident with `revolverCylinderBone`** (same head,
   same direction and roll). Rotation about an axis is unaffected by sliding the
   pivot *along* that axis, so a bone that overlaps the parent is always safe.

3. **Renaming `revolverCylinderBone`.** Action channels are matched to bones by
   name string (`pose.bones["revolverCylinderBone"]`). Rename the bone and every
   one of those channels goes dead.
   → **Never rename `revolverCylinderBone`. Only add a child.**

4. **Partial re-weighting.** If a mesh vertex has weight on *both* the old and the
   new bone, it follows a blend of the two and looks wrong.
   → **Fully transfer:** every vertex weight `1.0` on the new group, old group
   removed.

5. **New bone has Deform OFF.** The Armature modifier ignores non-deform bones
   entirely, so a mesh weighted to it doesn't move at all.
   → **Deform must be ON** on `revolverCylinderRotationBone`.

6. **Armature in Rest Position.** Nothing deforms in the viewport and it looks
   like the rig is broken.
   → **Armature Properties ▸ Skeleton ▸ Pose Position.**

7. **Actions show "Unassigned".** Blender 4.4+ stores an action's channels in a
   *slot* that has to be *bound* to the armature object. Editing the rig can
   leave the binding unresolved, so the pose never applies — which reads as
   "the animations broke".
   → **After any rig edit, confirm each action's slot is still bound; re-bind if
   not** (bulk snippet in step 3.5).

## Step by step

### 0. Safety net

Save, and make sure `git status` is clean (you're at `fc41633`, which is clean).
If anything goes sideways you can always run:

```
git checkout -- Assets/Blender/Arms.blend
```

to get back to this baseline and restart at step 1.

### 1. Create the bone (duplicate-in-place — fewest failure modes)

1. Select `RigArmature`, press `Tab` for Edit Mode.
2. Click the body of **`revolverCylinderBone`** so it is the only bone selected.
3. Press `Shift+D`, then **immediately** `Esc` (or right-click). This drops a
   copy, `revolverCylinderBone.001`, exactly on top of the original. A duplicated
   bone keeps the source's parent (`revolverRootBone`) and is created
   **disconnected** — both are what we want.
4. Re-parent the copy under `revolverCylinderBone`:
   - With `revolverCylinderBone.001` selected, `Shift`-click `revolverCylinderBone`
     so it becomes the **active** (last-selected, brighter) bone.
   - `Ctrl+P` ▸ **Keep Offset**. (Do *not* pick "Connected".)
5. Rename `revolverCylinderBone.001` → `revolverCylinderRotationBone`
   (`F2`, type, Enter — or the name field in Bone Properties).
6. With `revolverCylinderRotationBone` selected, open **Bone Properties** (bone
   icon) and confirm:
   - **Deform** = ON.
   - **Relations ▸ Parent** = `revolverCylinderBone`, **Connected** = OFF.
   - **Relations ▸ Inherit Rotation** = ON, **Inherit Scale** = Full.
7. `Tab` back to Object Mode.

Target hierarchy:

```
revolverRootBone
├─ revolverCylinderBone              ← animations keep driving this
│  └─ revolverCylinderRotationBone   ← coincident child; Godot spins this
├─ revolverTriggerBone
└─ revolverHammerBone
```

#### Alternative: extrude (only if you follow the guard rails)

1. Edit Mode, select `revolverCylinderBone` (or just its tail).
2. `E`, then `Esc` immediately — **do not drag**. The new bone points the same
   direction, along the barrel axis.
3. `Alt+P` ▸ **Disconnect Bone** so it can never drag the parent's tail.
4. `Ctrl+P` ▸ **Keep Offset** onto `revolverCylinderBone` if it isn't already the
   parent.
5. Rename, and check **Deform** ON, as above.

A tail-extruded bone that points the same way still spins the cylinder correctly,
because the pivot only slid *along* the axis. The only danger is lateral
movement — never grab it sideways.

### 2. Move the meshes onto the new bone

The 13 meshes below are each 100 % weighted to a single group named
`revolverCylinderBone`:

`revolverCylinderMesh`, `bulletCasing1`–`bulletCasing6`,
`bulletWithCasing1`–`bulletWithCasing6`.

Because the new bone is coincident with the old one at rest, you do **not** need
to re-paint anything — just **rename the vertex group** so the Armature modifier
binds those verts to the new bone.

**Scripting workspace** — paste in the Text Editor and press Run:

```python
import bpy

targets = ["revolverCylinderMesh"] \
    + [f"bulletCasing{i}" for i in range(1, 7)] \
    + [f"bulletWithCasing{i}" for i in range(1, 7)]
OLD, NEW = "revolverCylinderBone", "revolverCylinderRotationBone"

for n in targets:
    o = bpy.data.objects.get(n)
    if not o:
        print("MISSING", n)
        continue
    g_old = o.vertex_groups.get(OLD)
    g_new = o.vertex_groups.get(NEW)
    if g_old and not g_new:
        g_old.name = NEW
        print("renamed:", n)
    elif g_old and g_new:
        for v in o.data.vertices:
            for ge in v.groups:
                if ge.group == g_old.index:
                    g_new.add([v.index], ge.weight, 'REPLACE')
        o.vertex_groups.remove(g_old)
        print("merged:", n)
    else:
        print("check manually:", n, [vg.name for vg in o.vertex_groups])
```

If pasting keeps mis-indenting, run this single line in the **Python Console**
instead:

```python
import bpy; [setattr(o.vertex_groups["revolverCylinderBone"], "name", "revolverCylinderRotationBone") for o in (bpy.data.objects.get(n) for n in (["revolverCylinderMesh"]+[f"bulletCasing{i}" for i in range(1,7)]+[f"bulletWithCasing{i}" for i in range(1,7)])) if o and o.vertex_groups.get("revolverCylinderBone") and not o.vertex_groups.get("revolverCylinderRotationBone")]
```

**Manual equivalent (per mesh):** select it → Object Data Properties (green
triangle) → Vertex Groups → double-click `revolverCylinderBone` → rename to
`revolverCylinderRotationBone`.

Leave these alone: `revolverMesh` (`revolverRootBone`), `revolverHammerMesh`
(`revolverHammerBone`), `revolverTriggerMesh` (`revolverTriggerBone`).

### 3. Verify — run through all of these

1. **Armature Properties ▸ Skeleton ▸ Pose Position** is active (not Rest
   Position).
2. **Pose Mode**, select `revolverCylinderBone`, press `R` `Y` and drag: the
   cylinder **and every bullet** swing with it; the gun body does not. `Alt+R` to
   clear.
3. Still in Pose Mode, select `revolverCylinderRotationBone`, `R` `Y` and drag:
   the cylinder and bullets spin in place around the barrel axis; nothing else
   moves. `Alt+R` to clear.
4. Dope Sheet ▸ **Action Editor**: load `RevolverCylinderOpen` and scrub from
   frame 0 to the end. The cylinder should swing out **exactly as it did before**
   this change — it is still driven by `revolverCylinderBone`, and the child
   follows. Spot-check `RevolverRigReloadTurnCylinder` and `RevolverRigDefault`
   too.
5. If any action's header shows **"Unassigned"** beside the slot selector, click
   the slot dropdown and choose `Armature`. Bulk fix — Python Console:

   ```python
   import bpy; ad=(bpy.data.objects["RigArmature"].animation_data or bpy.data.objects["RigArmature"].animation_data_create()); keep=ad.action; [setattr(ad,'action_slot',([s for s in a.slots if s.target_id_type=='OBJECT'] or list(a.slots))[0]) for a in bpy.data.actions if (setattr(ad,'action',a) or a.slots)]; ad.action=keep or bpy.data.actions.get("RevolverRigHipIdle")
   ```

6. Troubleshooting step 3:
   - Mesh doesn't move **at all** → the group name doesn't exactly match the bone
     name, or **Deform** is off on `revolverCylinderRotationBone`.
   - Mesh moves **halfway / lags** → it still has weight in the old
     `revolverCylinderBone` group. Remove that group from the mesh.
   - Cylinder **wobbles** instead of spinning → the new bone isn't coincident /
     on-axis. Delete it and redo step 1 with duplicate-in-place.

### 4. Save & export to Godot

1. `Ctrl+S`.
2. Re-export / let Godot re-import. In the imported scene confirm the skeleton
   has **both** `revolverCylinderBone` and `revolverCylinderRotationBone`, spelled
   exactly. Godot's glTF importer normally keeps bone names verbatim, but it will
   sanitise `.` and spaces if present.
3. The cylinder-spin code targets **`revolverCylinderRotationBone`**.
   `revolverCylinderBone` stays owned by the AnimationPlayer.

### 5. Godot side (recap)

Drive `revolverCylinderRotationBone` *after* the animation each frame so the spin
composites on top of the reload swing-out:

- A `SkeletonModifier3D` subclass with `_ProcessModification()`: look up
  `revolverCylinderRotationBone`, read its current pose rotation with
  `GetBonePoseRotation(idx)`, multiply in the spin quaternion about the bone's
  local axis, write it back with `SetBonePoseRotation`.
- Because `revolverCylinderRotationBone` is a child of the animated
  `revolverCylinderBone`, the eject/scoop motion comes through for free; the code
  only adds the 60°-per-shot indexing (shortest-path tween or constant-speed
  `RotateToward`).

## If it all goes wrong

```
git checkout -- Assets/Blender/Arms.blend
```

restores the `fc41633` baseline. Start again at step 1.

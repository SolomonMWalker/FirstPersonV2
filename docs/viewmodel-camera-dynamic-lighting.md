# Viewmodel camera & dynamic lighting

## The bug

`Player/ViewmodelCamera.cs` renders the player rig with a second `Camera3D` inside a
`SubViewport` that shares the main `World3D` (`own_world_3d` off). The world camera skips
the viewmodel visual layer via its cull mask; the viewmodel camera draws only that layer.

The problem: the scene's `DirectionalLight3D` (sun) and `WorldEnvironment` lighting in
`test_level` don't light the rig. This is a known Godot limitation, not a bug in our code.
A `SubViewport` viewmodel camera does not reliably pick up the scene's directional light /
environment contribution, even with `own_world_3d = false`.

**There is no drop-in plugin/addon that fixes this.** The community has two approaches.

---

## Option A (recommended): projection-matrix shader, drop the SubViewport

Riordan/Kastor's "First Person View Model Shader" (updated for Godot 4.3+). It rewrites
`PROJECTION_MATRIX` in `vertex()` so the viewmodel gets its own FOV, and scales `DEPTH`
so it doesn't clip into walls. The mesh stays in the **world camera's render pass**, so
the sun and environment light it exactly like everything else — the bug can't occur.

```glsl
shader_type spatial;
render_mode depth_draw_opaque, cull_back;

uniform float fov : hint_range(20, 120) = 50.0;
const float M_PI = 3.14159265359;

void vertex() {
    float s = 1.0 / tan(fov * 0.5 * M_PI / 180.0);
    PROJECTION_MATRIX[0][0] = s / (-VIEWPORT_SIZE.x / VIEWPORT_SIZE.y);
    PROJECTION_MATRIX[1][1] = s;
}

void fragment() {
    // Godot 4.3+ reverse-Z form; pulls the model forward in depth to stop wall clipping
    DEPTH = 1.0 - (1.0 - FRAGCOORD.z) * 0.7;
}
```

**Trade-offs**

- It's a `ShaderMaterial`, so you lose the `StandardMaterial3D` inspector unless you build
  a fuller version that passes through albedo / normal / metallic / roughness / AO.
- The depth-scale trick isn't perfect at extreme angles.
- Disable shadow-casting on the viewmodel — its real geometry isn't where the raster
  projection puts it, so cast shadows land in the wrong place.

**Fit with our code:** matches the existing "assign to the whole rig from code" pattern in
`ViewmodelCamera.cs`. Instead of `MoveToLayer` setting `VisualInstance3D.Layers`, recurse
over `GeometryInstance3D` children setting `MaterialOverride` (or a next-pass) to the
shader material. The second camera / SubViewport goes away entirely.

---

## Option B: keep the SubViewport, feed it lights

1. **Duplicate the `DirectionalLight3D`** as a child of the `SubViewport` — matched
   direction and energy, or a dimmer fill. Put that light and the viewmodel on the
   viewmodel layer (layer 2).
2. Any point/spot lights that should touch the rig need layer 2 added to their
   **Cull Mask** (`light_cull_mask`). Automate this with a `_Ready` sweep like the
   existing `MoveToLayer`, OR-ing the viewmodel bit into every `Light3D` under the level.
3. Ambient / sky from `WorldEnvironment` is shared through `World3D` and should already
   apply. If the rig looks fully unlit, it's the direct light that's missing, not ambient.

More maintenance (every relevant light has to be tracked), which is why Option A is
preferred.

---

## Sources

- David Szymanski (duskdev) on the viewmodel lighting problem + layer/light workaround —
  <https://mastodon.gamedev.place/@duskdev/111375275829587976>
- First Person View Model Shader (updated for Godot 4.3+) — Godot Shaders —
  <https://godotshaders.com/shader/first-person-view-model-shader-updated-for-godot-4-3/>
- First Person View Model Shader (original) — Godot Shaders —
  <https://godotshaders.com/shader/first-person-view-model-shader/>
- godot#121518 — SubViewport + WorldEnvironment/DirectionalLight3D lighting issues —
  <https://github.com/godotengine/godot/issues/121518>
- Godot Forum — Need help with viewports (subviewport viewmodel; poster ends up moving to
  shaders) — <https://forum.godotengine.org/t/need-help-with-viewports/68350>
- Godot Forum — Shadows do not cast on different render layers —
  <https://forum.godotengine.org/t/shadows-do-not-cast-on-different-render-layers/101672>

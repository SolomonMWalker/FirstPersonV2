# Working in this repo

Conventions that aren't derivable from reading the code, and traps that have already cost time.

## Tests live in `Tests/`

Both halves of a test go there: the `*Tests.cs` script and the one-node `test_*.tscn` harness that
runs it. Run one with `godot --headless --path . res://Tests/test_<name>.tscn`; exit 0 is a pass.

`test_level.tscn` is **not** a test despite the name — it is the greybox level, it is the project's
main scene, and most tests instantiate it to run against real geometry. It stays at the root.

## Exported variables must carry a description

Every `[Export]` gets a comment saying what it means, in the units it is measured in, and what a
value of 0 does if 0 is meaningful. A `//` comment directly above the field, or trailing it when one
short clause covers it — the pattern already used throughout `CameraController.cs`.

```csharp
[Export] public float BobStride = 1.5f;    // metres travelled per full bob cycle

// Metres of lag per radian-per-second of look movement. A fast flick is several radians a second
// and would throw the gun off the side of the screen uncapped, hence SwayMax.
[Export] public float SwayAmount = 0.015f;
```

An export is the surface someone tunes against months later, with no memory of why the number is
what it is. `Speed = 5.0f` is not self-documenting: metres per second, or units per tick? Does 0 mean
"stopped" or "disabled"? Say so. Where a knob is a deliberate off-switch (the camera juice channels,
the presentation channels on `HitscanComponent`), say that too — it is the part most likely to be
removed by someone who thinks it is dead code.

Group related exports under one comment rather than repeating yourself per field, and put the *why*
there: the reason `LoseRange` is larger than `SightRange` matters more than either number.

These are code comments. They are not visible in the Godot inspector — C# has no equivalent of
GDScript's `##` doc comments — so they serve the next person reading the file, not someone tuning
in the editor.

## Traps

**Never put a `#` comment inside a `.tscn` node block.** It silently swallows the one property line
directly after it. This shipped a real bug: the world camera's `cull_mask` was never applied, so the
viewmodel gun rendered at world scale in the world pass for as long as the comment above it existed.
Godot also strips `.tscn` comments on editor save, so they are not durable anyway — put the
explanation in the C# that reads the property.

**A test that passes before your change is not a test.** Frame-scripted tests here assert an end
state after enough elapsed time that the correct and broken paths have converged, which makes it
easy to write an assertion that cannot fail. Run any new assertion against the old behaviour, or
against the feature disabled, and confirm it fails, before believing it.

**A test must not assert against a tuning knob's authored value.** Feel values — bob amplitudes,
sway, recoil, ranges — get tuned in the editor between runs, and a threshold in absolute units
against one of them fails on a tuning decision rather than on a regression. Have the test write the
exports it depends on at setup, then assert against those. `ViewmodelTests` pins `BobAmount` and
`BobScale` for exactly this reason, after breaking twice on values that had moved.

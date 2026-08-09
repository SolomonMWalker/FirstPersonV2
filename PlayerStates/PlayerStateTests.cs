using System.Collections.Generic;
using FirstPerson.CustomTypes.StateMachine;
using FirstPerson.Helpers;
using Godot;

namespace FirstPerson.PlayerStates;

// Run headless:  godot --headless --path . res://test_player_states.tscn
// Exits 0 on pass, 1 on failure.
//
// Drives the real test_level scene with injected input and asserts on the machine's active
// configuration, so it covers the scene wiring (defaults resolving without node-path exports)
// as well as the transition graph.
public partial class PlayerStateTests : Node
{
    private const string Locomoting = "Player(Locomoting(AirState(Grounded), MovementState(Walking)))";
    // Movement-region fragments: matched as substrings, so they assert the walk/sprint/crouch choice
    // without caring whether the player happens to be grounded or airborne at the time.
    private const string Sprinting = "MovementState(Sprinting)";
    private const string Crouching = "MovementState(Crouching)";
    private const string Clambering = "Player(Clambering)";

    private readonly List<string> _failures = [];
    private readonly List<string> _seen = [];
    private int _frame;
    private PlayerController _body;
    private StateMachine _sm;
    private float _ledgeTop, _clamberEndY;
    private float _standCamY, _standHeight, _crouchDip, _crouchHeight;
    private float _maxBob, _strafeRoll, _straightRoll;
    private float _punchMax, _punchMin, _smallDropPunch, _fallSpeedAfterLanding, _rampTopY;
    private float _clamberLag, _crouchLag, _restLag, _maxStepLag, _descentLag, _stairTopY;
    private float _maxCamDrop, _maxBodyDrop, _lastCamY, _lastBodyY;
    private string _last = "", _blockedState = "", _freedState = "", _afterClamberState = "";

    // The Overhang slab's underside sits 1.6m over the floor: the 1.5m crouched capsule fits under
    // it, the 2m standing one does not. Clear is the same ground, out from under the slab. Both are
    // feet-on-floor positions -- floor top is y=0 and the capsule's centre is 1m above its feet.
    private static readonly Vector3 UnderOverhang = new(8, 1f, 0);
    private static readonly Vector3 Clear = new(8, 1f, 5);

    private CameraController Cam() => _body.GetNode<CameraController>("Camera3D");
    private CapsuleShape3D Capsule() =>
        (CapsuleShape3D)_body.GetNode<CollisionShape3D>("CollisionShape3D").Shape;

    public override void _Ready()
    {
        // Every frame number below is really a duration -- crouch easing, clamber flight and the
        // camera smoothing all run on wall-clock seconds. Pinned to 60Hz so the schedule means the
        // same thing regardless of what the project's physics tick rate is set to.
        Engine.PhysicsTicksPerSecond = 60;
        AddChild(GD.Load<PackedScene>("res://test_level.tscn").Instantiate());
    }

    private static void Press(Key key, bool down) =>
        Input.ParseInputEvent(new InputEventKey { PhysicalKeycode = key, Pressed = down });

    // Park the player over the jump platform at a given height and let go. The platform's top is at
    // y=5 and the capsule's centre rides 1m above its feet, so y=6 is resting on it.
    private void Drop(float y)
    {
        _body.GlobalPosition = new Vector3(-9, y, 9);
        _body.Velocity = Vector3.Zero;
    }

    private static void Space(bool down) => Press(Key.Space, down);

    public override void _PhysicsProcess(double delta)
    {
        _frame++;
        if (_frame < 40) return;

        if (_frame == 40)
        {
            _body = GetNode<PlayerController>("LevelSkeleton/CharacterBody3D");
            _sm = _body.GetNode<StateMachine>("StateMachine");
            var q = PhysicsRayQueryParameters3D.Create(new Vector3(0, 10, -5), new Vector3(0, -5, -5));
            _ledgeTop = ((Vector3)_body.GetWorld3D().DirectSpaceState.IntersectRay(q)["position"]).Y;
            _standCamY = Cam().Position.Y;
            _standHeight = Capsule().Height;
            Is(Locomoting, _sm.GetStateMachineString(), "initial configuration");
            return;
        }

        Record();

        // --- jump on open ground: Grounded -> InAir -> Grounded ---
        if (_frame == 42) { Space(true); return; }
        if (_frame == 44) { Space(false); return; }

        // --- movement states. S, so the player walks away from the ledge, not into it. ---
        if (_frame == 50) { Press(Key.S, true); Press(Key.Shift, true); return; }   // -> Sprinting
        // Shift released first: crouch must stick rather than bouncing back to sprint.
        if (_frame == 60) { Press(Key.Shift, false); Press(Key.C, true); return; }  // -> Crouching
        if (_frame == 62) { Press(Key.C, false); return; }
        // Walking straight back, so none of the velocity is sideways and the view must stay level.
        if (_frame == 90) { _straightRoll = Cam().Rotation.Z; return; }
        // Sampled deep into the crouch, once the camera has finished easing down. CrouchOffset, not
        // Position.Y: the player is still moving here, so the node carries head bob on top.
        if (_frame == 98) { _crouchDip = Cam().CrouchOffset; _crouchHeight = Capsule().Height; return; }
        if (_frame == 100) { Press(Key.C, true); return; }                          // -> Walking
        if (_frame == 102) { Press(Key.C, false); return; }
        if (_frame == 110) { Press(Key.S, false); return; }

        // --- clamber at the ledge: Locomoting -> Clambering -> Locomoting ---
        // Sprinting into it, because the clamber must not cost the player their movement state.
        if (_frame == 140) { _body.GlobalPosition = new Vector3(0, 1f, -2.8f); Press(Key.W, true); Press(Key.Shift, true); return; }
        if (_frame == 145) { Space(true); return; }
        // A 1.6m mantle is not a step. Without the IsClambering guard the rise clamps to MaxStepLag
        // and the eye sinks through the ledge halfway up.
        if (_frame is > 145 and < 200) { _clamberLag = Mathf.Max(_clamberLag, Mathf.Abs(Cam().StepLag)); return; }
        if (_frame == 200) { _afterClamberState = _sm.GetStateMachineString(); return; }
        if (_frame == 205) { Press(Key.W, false); Press(Key.Shift, false); return; }
        if (_frame == 250) { Space(false); return; }

        // --- headroom: crouch, slide under the overhang, and fail to stand back up ---
        // Clear ground beside the slab, so the crouch happens at full height.
        if (_frame == 270) { _body.GlobalPosition = Clear; Press(Key.C, true); return; }
        if (_frame == 272) { Press(Key.C, false); return; }
        // Under it only once the capsule has actually shrunk.
        if (_frame == 300) { _body.GlobalPosition = UnderOverhang; return; }
        if (_frame == 310) { Press(Key.C, true); return; }   // asks to stand; ceiling says no
        if (_frame == 312) { Press(Key.C, false); return; }
        if (_frame == 330) { _blockedState = _sm.GetStateMachineString(); return; }
        // Back into the open, with nothing re-pressing C: the refused stand-up must be gone, not
        // waiting to fire the moment there is room.
        if (_frame == 335) { _body.GlobalPosition = Clear; return; }
        if (_frame == 360) { _freedState = _sm.GetStateMachineString(); return; }
        if (_frame == 365) { Press(Key.C, true); return; }    // a fresh press does stand up
        if (_frame == 367) { Press(Key.C, false); return; }

        // --- camera juice: strafe right on open ground, standing and un-bobbed at the start ---
        if (_frame == 370) { Press(Key.D, true); return; }
        if (_frame is > 371 and < 390)
        {
            // Bob measured against the crouch height, not against stand height: the stand-up from
            // frame 365 is still easing here, and that ease is not bob.
            var eye = _standCamY - Cam().CrouchOffset;
            _maxBob = Mathf.Max(_maxBob, Mathf.Abs(Cam().Position.Y - eye));
            return;
        }
        if (_frame == 390) { _strafeRoll = Cam().Rotation.Z; Press(Key.D, false); return; }

        // --- landing punch: a 6m drop onto the jump platform, which lands hard enough to clamp ---
        if (_frame == 395) { Drop(12f); return; }
        if (_frame is > 395 and < 560)
        {
            // Punch measured against LookPitch, not against zero: the camera's X is the sum of the
            // two, and measuring the absolute would silently be measuring mouse-look.
            var punch = Cam().Rotation.X - _body.LookPitch;
            _punchMax = Mathf.Max(_punchMax, punch);
            _punchMin = Mathf.Min(_punchMin, punch);
            if (_frame == 559) _fallSpeedAfterLanding = _body.FallSpeed;
            return;
        }

        // --- a 0.25m step off, well under LandPunchThreshold, must produce nothing at all ---
        // Staged this late so the previous punch has fully rung out: the spring's envelope still
        // carries ~0.4 degrees half a second after landing, which would be read as a false positive.
        if (_frame == 560) { Drop(6.25f); return; }
        if (_frame is > 560 and < 600)
        {
            _smallDropPunch = Mathf.Max(_smallDropPunch, Mathf.Abs(Cam().Rotation.X - _body.LookPitch));
            return;
        }

        // --- the ramp is actually walkable: hold S (which drives +Z) from its foot to the top ---
        if (_frame == 605) { _body.GlobalPosition = new Vector3(-9, 1.5f, -3); Press(Key.S, true); return; }
        if (_frame is > 605 and < 760) { _rampTopY = Mathf.Max(_rampTopY, _body.GlobalPosition.Y); return; }
        if (_frame == 760) { Press(Key.S, false); return; }

        // --- step smoothing ---
        // Flat open ground at the foot of the stairs, standing still, everything settled.
        if (_frame == 775) { _body.GlobalPosition = new Vector3(8, 1f, 2f); _body.Velocity = Vector3.Zero; return; }
        // Crouch, standing still. The eye drops 0.5m and the body does not move at all, so any step
        // lag here means the dip is being absorbed as a step -- Source's 2004 bug.
        if (_frame == 790) { Press(Key.C, true); return; }
        if (_frame == 792) { Press(Key.C, false); return; }
        if (_frame is > 792 and < 815) { _crouchLag = Mathf.Max(_crouchLag, Mathf.Abs(Cam().StepLag)); return; }
        if (_frame == 815) { Press(Key.C, true); return; }    // back up
        if (_frame == 817) { Press(Key.C, false); return; }

        // Walk up the six 0.15m treads. S drives +Z, which is up the stairs.
        // Ascent is measured for the bound only, not for lag. Godot has no step-up teleport: the
        // capsule *rolls* over a tread over four or so ticks at ~2.5 m/s, which is already the
        // catch-up rate, so the eye keeps pace exactly and no lag ever accumulates. Nothing to
        // absorb, because nothing popped. Verified by dropping StepSmoothRate to 0.5, at which the
        // same climb does produce lag. When step-up traversal lands and the body starts teleporting
        // up treads, this is the assertion that will need to become a `> 0.02` like the descent one.
        if (_frame == 840) { Press(Key.S, true); return; }
        if (_frame is > 840 and < 930)
        {
            _maxStepLag = Mathf.Max(_maxStepLag, -Cam().StepLag);   // negative going up
            _stairTopY = Mathf.Max(_stairTopY, _body.GlobalPosition.Y);
            return;
        }
        // Straight back down the same stairs, and this half does pop: floor snap pulls the body down
        // the full tread in one tick. Descending is what Quake never did and Source added, and it
        // needs floor_snap_length long enough to keep the body grounded over the drop -- otherwise
        // the step reads as a fall and the guards correctly refuse to smooth it.
        if (_frame == 930)
        {
            Press(Key.S, false);
            Press(Key.W, true);
            _lastCamY = Cam().GlobalPosition.Y;
            _lastBodyY = _body.GlobalPosition.Y;
            return;
        }
        if (_frame is > 930 and < 1030)
        {
            // Global Y on the camera, not local: the whole point is that the body's world-space pop
            // must not reach the eye in one frame. Local Position.Y would only show the lag term.
            var camY = Cam().GlobalPosition.Y;
            var bodyY = _body.GlobalPosition.Y;
            _maxCamDrop = Mathf.Max(_maxCamDrop, _lastCamY - camY);
            _maxBodyDrop = Mathf.Max(_maxBodyDrop, _lastBodyY - bodyY);
            _lastCamY = camY;
            _lastBodyY = bodyY;
            _descentLag = Mathf.Max(_descentLag, Cam().StepLag);   // positive going down
            return;
        }
        if (_frame == 1030) { Press(Key.W, false); return; }
        // Well past the ~0.14s convergence, so the lag must be exactly zero and not merely small.
        if (_frame == 1060) { _restLag = Cam().StepLag; return; }
        if (_frame < 1070) return;

        Contains("Player(Locomoting(AirState(InAir), MovementState(Walking)))", "jump reaches InAir");
        Is(Locomoting, _sm.GetStateMachineString(), "final configuration is back to Locomoting");
        Contains(Sprinting, "moving with shift sprints");
        Contains(Crouching, "C crouches, and it sticks");
        Contains(Clambering, "clamber suspends both parallel regions");
        Has(_afterClamberState, Sprinting, "clambering does not cancel the sprint");

        // Crouch dips the camera by CrouchDrop and shortens the capsule by the same amount, and
        // both undo themselves on stand-up.
        var drop = Cam().CrouchDrop;
        Near(drop, _crouchDip, "crouch lowers the camera");
        Near(_standHeight - drop, _crouchHeight, "crouch shortens the capsule to match");
        Near(_standCamY, Cam().Position.Y, "standing up restores the camera");
        Near(_standHeight, Capsule().Height, "standing up restores the capsule");

        // Head bob rises while walking and settles back out once the player stops. The upper bound
        // is the guard that matters: bob must never grow past the amplitude it was told to use.
        True(_maxBob > 0.005f, $"walking bobs the camera (peak {_maxBob:F4}m)");
        True(_maxBob <= Cam().BobAmount, $"bob stays within BobAmount (peak {_maxBob:F4}m)");
        // Strafing right rolls the view; walking straight does not roll it at all.
        True(_strafeRoll < -Mathf.DegToRad(0.5f), $"strafing rolls the view ({Mathf.RadToDeg(_strafeRoll):F2} deg)");
        True(Mathf.Abs(_straightRoll) < Mathf.DegToRad(0.1f),
            $"walking straight keeps the view level ({Mathf.RadToDeg(_straightRoll):F2} deg)");

        // Landing punches the view, clamped to MaxPunch, and the spring overshoots back past level
        // on the way out -- that rebound is what separates a spring from a plain decay, and it is
        // exactly what a careless "simplify this to a lerp" would silently remove.
        // Direction matters and is easy to get backwards: Godot counts positive pitch as looking up,
        // while Quake and Source both count it as looking down, so their constants invert on import.
        // A landing must drive the view DOWN, which is negative here.
        var maxPunch = Mathf.DegToRad(Cam().MaxPunch);
        True(_punchMin < -Mathf.DegToRad(1f), $"landing pitches the view down ({Mathf.RadToDeg(_punchMin):F2} deg)");
        True(_punchMin >= -maxPunch - 0.001f, $"punch respects MaxPunch ({Mathf.RadToDeg(_punchMin):F2} deg)");
        // Crossing zero at all is what separates a spring from a plain decay, so the bar is "any
        // rebound" rather than a magnitude -- how big it gets is a tuning choice, and PunchDamping
        // near critical shrinks it to almost nothing. Only fails if the spring is gone entirely.
        True(_punchMax > Mathf.DegToRad(0.005f), $"punch rebounds back past level ({Mathf.RadToDeg(_punchMax):F3} deg)");
        True(_fallSpeedAfterLanding == 0f, $"FallSpeed is consumed on landing (got {_fallSpeedAfterLanding:F2})");
        True(_smallDropPunch < Mathf.DegToRad(0.1f),
            $"a drop under the threshold does not punch ({Mathf.RadToDeg(_smallDropPunch):F3} deg)");

        // The ramp is walkable at 30 degrees: holding S from its foot reaches the platform, whose
        // top is y=5 and so puts the capsule's centre at y=6.
        True(_rampTopY > 5.9f, $"walking up the ramp reaches the platform (got y={_rampTopY:F2})");

        // Step smoothing. The stairs must be climbable at all -- nothing in this project implements
        // step-up traversal, so the treads live or die on what the capsule rolls over unaided, and
        // a regression there would silently turn every assertion below into a test of flat ground.
        True(_stairTopY > 1.85f, $"the 0.15m treads are walkable (reached y={_stairTopY:F2}, landing is 1.9)");
        True(_maxStepLag <= Cam().MaxStepLag + 0.001f, $"ascent lag respects MaxStepLag ({_maxStepLag:F3}m)");
        True(_descentLag > 0.02f, $"descending lags the eye above the body ({_descentLag:F3}m)");
        True(_descentLag <= Cam().MaxStepLag + 0.001f, $"descent lag respects MaxStepLag ({_descentLag:F3}m)");
        // The measurement that matters: whatever the body did in one tick, the eye did less.
        True(_maxCamDrop < _maxBodyDrop,
            $"the eye drops slower than the body ({_maxCamDrop:F4}m vs {_maxBodyDrop:F4}m per tick)");
        True(_restLag == 0f, $"lag settles on exactly zero, no permanent eye-height error (got {_restLag:F6})");
        // The two guards that would regress silently, because both fail as "the camera feels a bit
        // off" rather than as anything that throws.
        True(_crouchLag < 0.001f, $"crouching is not absorbed as a step (lag {_crouchLag:F4}m)");
        True(_clamberLag < 0.001f, $"clambering is not absorbed as a step (lag {_clamberLag:F4}m)");

        Has(_blockedState, Crouching, "stand-up under the overhang is refused");
        Has(_freedState, Crouching, "the refused stand-up is dropped, not queued for later");
        // That the machine is back to Walking at the end is the final-configuration check above:
        // only the fresh press at frame 365 could have got it there.
        Near(_ledgeTop + 1.0f, _clamberEndY, "clamber leaves the player standing on the ledge");
        // A flapping guard pair trips the machine's loop cap, which only shows up as a pushed
        // error the assertions would otherwise sail straight past. One jump plus one clamber is
        // four changes, the sprint/crouch/walk trip is three more, and the headroom phase two --
        // plus room for a teleport to blip through InAir. Anything much larger means states are
        // churning; a real guard flap runs to the machine's 64-transition cap.
        // ... and the two staged drops onto the jump platform are two more round trips each.
        // ... and the step phase adds a crouch round trip plus room for the stairs to blip InAir.
        if (_seen.Count > 32)
            _failures.Add($"configuration churn: {_seen.Count} changes. Saw: {string.Join(" -> ", _seen)}");

        if (_failures.Count == 0) GD.Print("player state tests: all passed");
        else foreach (var f in _failures) GD.PrintErr(f);
        GetTree().Quit(_failures.Count == 0 ? 0 : 1);
    }

    // Configuration history, deduped to transitions only.
    private void Record()
    {
        var s = _sm.GetStateMachineString();
        if (s == _last) return;
        if (_last == Clambering) _clamberEndY = _body.GlobalPosition.Y;
        _last = s;
        _seen.Add(s);
    }

    private void Is(string expected, string actual, string what)
    {
        if (expected != actual) _failures.Add($"{what}: expected '{expected}', got '{actual}'");
    }

    private void Contains(string expected, string what)
    {
        if (!_seen.Exists(s => s.Contains(expected))) _failures.Add($"{what}: never saw '{expected}'. Saw: {string.Join(" -> ", _seen)}");
    }

    private void Has(string actual, string expected, string what)
    {
        if (!actual.Contains(expected)) _failures.Add($"{what}: expected '{expected}' in '{actual}'");
    }

    private void True(bool ok, string what)
    {
        if (!ok) _failures.Add(what);
    }

    private void Near(float expected, float actual, string what)
    {
        if (Mathf.Abs(expected - actual) > 0.05f)
            _failures.Add($"{what}: expected y={expected:F2}, got y={actual:F3}");
    }
}

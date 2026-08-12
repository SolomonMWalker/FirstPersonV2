using FirstPerson.CustomTypes.StateMachine;
using Godot;

namespace FirstPerson.PlayerStates;

// Run headless:  godot --headless --path . res://test_coyote.tscn
// Exits 0 on pass, 1 on failure.
//
// Drives the real test_level scene. "Walking off a ledge" is simulated by teleporting the body
// straight up past floor_snap_length (0.35, set on the scene's CharacterBody3D) with zero
// velocity, rather than routing through real ledge geometry -- from PlayerController's
// perspective the two are identical: IsOnFloor() goes false, JumpedThisAirborne stays false.
//
// Between scenarios the body is hard-reset to a resting position rather than left to actually
// land: waiting out a real jump arc under gravity is exactly the kind of timing arithmetic that
// produced a scheduling bug elsewhere in this project's tests, and there's nothing left to learn
// from watching it fall.
public partial class CoyoteTests : Node
{
    private readonly System.Collections.Generic.List<string> _failures = [];
    private int _frame;
    private PlayerController _body;
    private StateMachine _sm;

    private const string Grounded = "AirState(Grounded)";
    private const string Coyote = "AirState(Coyote)";
    private const string InAir = "AirState(InAir)";
    private static readonly Vector3 Resting = new(0, 1f, 0);   // feet at y=0, floor top is y=0

    public override void _Ready()
    {
        Engine.PhysicsTicksPerSecond = 60;
        AddChild(GD.Load<PackedScene>("res://test_level.tscn").Instantiate());
    }

    private static void Space(bool down) =>
        Input.ParseInputEvent(new InputEventKey { PhysicalKeycode = Key.Space, Pressed = down });

    // Leaves the floor without jumping: teleports up past the snap length so MoveAndSlide can't
    // re-snap it, with zero velocity so nothing here looks like a jump.
    private void StepOffLedge()
    {
        _body.GlobalPosition = _body.GlobalPosition with { Y = _body.GlobalPosition.Y + 0.5f };
        _body.Velocity = Vector3.Zero;
    }

    private void ResetToGround()
    {
        _body.GlobalPosition = Resting;
        _body.Velocity = Vector3.Zero;
    }

    public override void _PhysicsProcess(double delta)
    {
        _frame++;
        if (_frame < 10) return;

        if (_frame == 10)
        {
            _body = GetNode<PlayerController>("LevelSkeleton/Player");
            _sm = _body.GetNode<StateMachine>("StateMachine");
            Has(_sm.GetStateMachineString(), Grounded, "starts grounded");
            return;
        }

        // --- scenario A: a jump inside the grace window still fires ---
        if (_frame == 12) { StepOffLedge(); return; }
        if (_frame == 16)
        {
            // Still well inside CoyoteTime (0.15s / 9 ticks at 60Hz) -- 4 ticks elapsed.
            Has(_sm.GetStateMachineString(), Coyote, "leaving the floor unjumped enters Coyote");
            Space(true);
            return;
        }
        if (_frame == 17) { Space(false); return; }
        if (_frame == 20)
        {
            // Loosely bounded, not matched to JumpVelocity exactly: a couple of ticks of gravity
            // have already eaten into it by the time this runs. Comfortably positive is enough to
            // prove Jump() fired -- nothing else could have put upward velocity here.
            True(_body.Velocity.Y > 2.5f, $"a jump inside the grace window still fires (Velocity.Y={_body.Velocity.Y:F2})");
            ResetToGround();
            return;
        }
        if (_frame == 30) { Has(_sm.GetStateMachineString(), Grounded, "re-grounds cleanly after scenario A"); return; }

        // --- scenario B: an unused grace window closes, and jump stops working ---
        if (_frame == 35) { StepOffLedge(); return; }
        if (_frame == 50)
        {
            // 15 ticks since stepping off -- well past the 9-tick window, so this must be InAir.
            Has(_sm.GetStateMachineString(), InAir, "an unused grace window closes into InAir");
            Space(true);
            return;
        }
        if (_frame == 51) { Space(false); return; }
        if (_frame == 54)
        {
            True(_body.Velocity.Y < 0f,
                $"a jump after the window closes is a no-op (Velocity.Y={_body.Velocity.Y:F2})");
            ResetToGround();
            return;
        }
        if (_frame == 64) { Has(_sm.GetStateMachineString(), Grounded, "re-grounds cleanly after scenario B"); return; }

        // --- scenario C: the requirement that matters most -- a real jump never grants a second one ---
        if (_frame == 70) { Space(true); return; }
        if (_frame == 71) { Space(false); return; }
        if (_frame == 75)
        {
            // Only 5 ticks since the jump -- if this had gone through Coyote instead of straight to
            // InAir, the window (9 ticks) would not have closed yet and this would still read Coyote.
            Has(_sm.GetStateMachineString(), InAir, "a real jump goes straight to InAir, never Coyote");
            return;
        }
        // JumpVelocity (4.5) / gravity (~9.8) is an ~0.46s (28-tick) rise to apex; wait well past it
        // so the body is unambiguously falling before the second press. Jump() unconditionally SETS
        // Velocity.Y to a fixed positive value, so a still-negative reading right after the press is
        // an airtight no -- checking any earlier, while still rising from the first jump, a blocked
        // second press and a granted one would briefly look the same.
        if (_frame == 105) { Space(true); return; }
        if (_frame == 106) { Space(false); return; }
        if (_frame == 108)
        {
            True(_body.Velocity.Y < 0f,
                $"pressing jump again mid-air does not grant a second jump (Velocity.Y={_body.Velocity.Y:F2})");
            return;
        }
        if (_frame < 115) return;

        if (_failures.Count == 0) GD.Print("coyote tests: all passed");
        else foreach (var f in _failures) GD.PrintErr(f);
        GetTree().Quit(_failures.Count == 0 ? 0 : 1);
    }

    private void True(bool ok, string what)
    {
        if (!ok) _failures.Add(what);
    }

    private void Has(string actual, string expected, string what)
    {
        if (!actual.Contains(expected)) _failures.Add($"{what}: expected '{expected}' in '{actual}'");
    }
}

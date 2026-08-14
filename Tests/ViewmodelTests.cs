using System.Collections.Generic;
using Godot;

namespace FirstPerson.Tests;

// Run headless:  godot --headless --path . res://Tests/test_viewmodel.tscn
// Exits 0 on pass, 1 on failure.
//
// Two channels, tested the same way: drive the thing that should move the gun, sample the peak
// offset across every frame of it, then stop driving and assert the gun comes all the way back.
// The return is the half worth testing -- an offset that never fully decays is a gun permanently a
// few millimetres off centre, which looks like nothing at all until someone tunes against it.
public partial class ViewmodelTests : Node
{
    private static readonly Vector3 Clear = new(8, 1f, 5);

    private readonly List<string> _failures = [];
    private int _frame;
    private PlayerController _body;
    private CameraController _cam;
    private ViewmodelSway _gun;
    private float _peakSwayX, _restSwayX;
    private float _peakBobY, _restBobY;

    public override void _Ready()
    {
        // Same reason as PlayerStateTests: every frame number below is really a duration, and the
        // smoothing all runs on wall-clock seconds.
        Engine.PhysicsTicksPerSecond = 60;
        AddChild(GD.Load<PackedScene>("res://test_level.tscn").Instantiate());
    }

    private static void Press(Key key, bool down) =>
        Input.ParseInputEvent(new InputEventKey { PhysicalKeycode = key, Pressed = down });

    public override void _PhysicsProcess(double delta)
    {
        _frame++;
        if (_frame < 10) return;

        if (_frame == 10)
        {
            _body = GetNode<PlayerController>("LevelSkeleton/Player");
            _cam = _body.GetNode<CameraController>("Camera3D");
            _gun = _body.GetNodeOrNull<ViewmodelSway>("Camera3D/TestGun");
            True(_gun is not null, "the viewmodel gun has no ViewmodelSway script on it");
            if (_gun is null) { _frame = 1000; return; }

            // The test owns its own amplitudes rather than reading whatever the scene is tuned to
            // today. These are feel knobs under active tuning -- BobScale has already been 1.4, 0.7
            // and 0.05 -- and a threshold in absolute metres against an authored value fails the day
            // someone dials it down, which is a tuning decision and not a regression. Pinning them
            // here keeps the assertions about "is the channel connected", which is the only thing
            // this can actually check.
            _cam.BobAmount = 0.045f;
            _gun.BobScale = 1f;
            _gun.SwayAmount = 0.015f;
            _gun.SwayMax = 0.035f;

            // Open ground, standing still, so the sway phase below is measuring the turn and nothing
            // else -- no velocity means no bob to contaminate it.
            _body.GlobalPosition = Clear;
            _body.Velocity = Vector3.Zero;
            return;
        }

        // --- sway: turn the body the way the mouse would, and watch the gun trail it ---
        // Rotation is written directly rather than through injected mouse motion: _UnhandledInput
        // does not run for synthesised InputEventMouseMotion in a headless tree, and yaw on the body
        // is the whole of what ViewmodelSway actually reads.
        if (_frame is >= 20 and < 45)
        {
            _body.Rotation = _body.Rotation with { Y = _body.Rotation.Y + 0.06f };
            _peakSwayX = Mathf.Max(_peakSwayX, Mathf.Abs(_gun.Offset.X));
            return;
        }

        // Stopped turning. ~0.6s is many multiples of the 1/12s chase constant.
        if (_frame == 80) { _restSwayX = Mathf.Abs(_gun.Offset.X); return; }

        // --- bob: strafe on the flat and watch the gun walk ---
        // Heading reset first, and not merely position: the sway phase above left the body turned
        // 1.5rad, and strafing from there walks into level geometry. CameraController reads
        // post-slide velocity on purpose, so a blocked strafe correctly produces almost no bob --
        // which reads here as the feature being broken rather than as the test aiming at a wall.
        if (_frame == 82)
        {
            _body.Rotation = Vector3.Zero;
            _body.GlobalPosition = Clear;
            _body.Velocity = Vector3.Zero;
            Press(Key.D, true);
            return;
        }

        // Grounded frames only. Strafing from Clear leaves the flat after about a second, and
        // CameraController zeroes bob amplitude in the air by design -- sampling those frames would
        // measure the fade-out rather than the bob.
        if (_frame is > 82 and < 140)
        {
            if (_body.IsOnFloor()) _peakBobY = Mathf.Max(_peakBobY, Mathf.Abs(_gun.Offset.Y));
            return;
        }

        if (_frame == 140) { Press(Key.D, false); _body.Velocity = Vector3.Zero; return; }

        // Standing again. Bob amplitude chases speed at Smoothing=8, so ~0.75s puts it at e^-6 of
        // where it was; anything left here is a channel that does not actually return to rest.
        if (_frame == 185) { _restBobY = Mathf.Abs(_gun.Offset.Y); return; }

        if (_frame < 190) return;

        True(_peakSwayX > 0.01f, $"turning produced no viewmodel sway (peak X offset {_peakSwayX:F4})");
        True(_restSwayX < 0.002f, $"the viewmodel never returned to rest after the turn (X offset {_restSwayX:F4})");
        // Against the amplitudes pinned at frame 10 (0.045 * 1.0), not against whatever the scene is
        // tuned to, so this measures the channel rather than the tuning. A disabled channel is 0.0000.
        True(_peakBobY > 0.01f, $"walking produced no viewmodel bob (peak Y offset {_peakBobY:F4})");
        True(_restBobY < 0.002f, $"the viewmodel never settled after walking (Y offset {_restBobY:F4})");

        if (_failures.Count == 0) GD.Print("viewmodel tests: all passed");
        else foreach (var f in _failures) GD.PrintErr(f);
        GetTree().Quit(_failures.Count == 0 ? 0 : 1);
    }

    private void True(bool ok, string what)
    {
        if (!ok) _failures.Add(what);
    }
}

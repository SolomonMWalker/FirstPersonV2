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
    private float _standCamY, _standHeight, _crouchCamY, _crouchHeight;
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
        AddChild(GD.Load<PackedScene>("res://test_level.tscn").Instantiate());
    }

    private static void Press(Key key, bool down) =>
        Input.ParseInputEvent(new InputEventKey { PhysicalKeycode = key, Pressed = down });

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
        // Sampled deep into the crouch, once the camera has finished easing down.
        if (_frame == 98) { _crouchCamY = Cam().Position.Y; _crouchHeight = Capsule().Height; return; }
        if (_frame == 100) { Press(Key.C, true); return; }                          // -> Walking
        if (_frame == 102) { Press(Key.C, false); return; }
        if (_frame == 110) { Press(Key.S, false); return; }

        // --- clamber at the ledge: Locomoting -> Clambering -> Locomoting ---
        // Sprinting into it, because the clamber must not cost the player their movement state.
        if (_frame == 140) { _body.GlobalPosition = new Vector3(0, 1f, -2.8f); Press(Key.W, true); Press(Key.Shift, true); return; }
        if (_frame == 145) { Space(true); return; }
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
        if (_frame < 390) return;

        Contains("Player(Locomoting(AirState(InAir), MovementState(Walking)))", "jump reaches InAir");
        Is(Locomoting, _sm.GetStateMachineString(), "final configuration is back to Locomoting");
        Contains(Sprinting, "moving with shift sprints");
        Contains(Crouching, "C crouches, and it sticks");
        Contains(Clambering, "clamber suspends both parallel regions");
        Has(_afterClamberState, Sprinting, "clambering does not cancel the sprint");

        // Crouch dips the camera by CrouchDrop and shortens the capsule by the same amount, and
        // both undo themselves on stand-up.
        var drop = Cam().CrouchDrop;
        Near(_standCamY - drop, _crouchCamY, "crouch lowers the camera");
        Near(_standHeight - drop, _crouchHeight, "crouch shortens the capsule to match");
        Near(_standCamY, Cam().Position.Y, "standing up restores the camera");
        Near(_standHeight, Capsule().Height, "standing up restores the capsule");

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
        if (_seen.Count > 16)
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

    private void Near(float expected, float actual, string what)
    {
        if (Mathf.Abs(expected - actual) > 0.05f)
            _failures.Add($"{what}: expected y={expected:F2}, got y={actual:F3}");
    }
}

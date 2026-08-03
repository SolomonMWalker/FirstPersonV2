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
    private const string Clambering = "Player(Clambering)";

    private readonly List<string> _failures = [];
    private readonly List<string> _seen = [];
    private int _frame;
    private PlayerController _body;
    private StateMachine _sm;
    private float _ledgeTop, _clamberEndY;
    private string _last = "";

    public override void _Ready()
    {
        AddChild(GD.Load<PackedScene>("res://test_level.tscn").Instantiate());
    }

    private static void Space(bool down) =>
        Input.ParseInputEvent(new InputEventKey { PhysicalKeycode = Key.Space, Pressed = down });

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
            Is(Locomoting, _sm.GetStateMachineString(), "initial configuration");
            return;
        }

        Record();

        // --- jump on open ground: Grounded -> InAir -> Grounded ---
        if (_frame == 42) { Space(true); return; }
        if (_frame == 44) { Space(false); return; }

        // --- clamber at the ledge: Locomoting -> Clambering -> Locomoting ---
        if (_frame == 140) { _body.GlobalPosition = new Vector3(0, 1f, -2.8f); return; }
        if (_frame == 145) { Space(true); return; }
        if (_frame == 250) { Space(false); return; }
        if (_frame < 280) return;

        Contains("Player(Locomoting(AirState(InAir), MovementState(Walking)))", "jump reaches InAir");
        Is(Locomoting, _sm.GetStateMachineString(), "final configuration is back to Locomoting");
        Contains(Clambering, "clamber suspends both parallel regions");
        Near(_ledgeTop + 1.0f, _clamberEndY, "clamber leaves the player standing on the ledge");
        // A flapping guard pair trips the machine's loop cap, which only shows up as a pushed
        // error the assertions would otherwise sail straight past. One jump plus one clamber is
        // four changes; anything much larger means states are churning.
        if (_seen.Count > 8)
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
        if (!_seen.Contains(expected)) _failures.Add($"{what}: never saw '{expected}'. Saw: {string.Join(" -> ", _seen)}");
    }

    private void Near(float expected, float actual, string what)
    {
        if (Mathf.Abs(expected - actual) > 0.05f)
            _failures.Add($"{what}: expected y={expected:F2}, got y={actual:F3}");
    }
}

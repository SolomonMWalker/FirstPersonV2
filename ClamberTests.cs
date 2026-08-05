using System;
using System.Collections.Generic;
using Godot;

namespace FirstPerson.Helpers;

// Run headless:  godot --headless --path . res://test_clamber.tscn
// Exits 0 on pass, 1 on failure.
//
// Each case builds its own slice of world 40m apart so the static geometry cannot interfere,
// and gets its own ClamberController so cooldown/IsClambering never leak between cases.
// Assertions run after a couple of physics frames because TestMove needs the bodies registered.
public partial class ClamberTests : Node
{
    private readonly List<(string name, bool expectAccept, float expectY, ClamberController c)> _cases = [];
    private readonly List<string> _failures = [];
    private int _frame;
    private float _x;

    public override void _Ready()
    {
        // Open ground: the sweep always finds the floor, so this is the false-positive guard.
        Case("flat ground rejects", false, 0f, _ => { });

        // The height must come back exact, not quantised to some ray spacing.
        Case("1.0m ledge accepts at exactly 2.0", true, 2.0f,
            s => Box(s, new Vector3(4, 1, 4), new Vector3(0, 0.5f, -2.6f)));

        // A ledge of exactly MaxClamberHeight must clear its own lip, so the sweep rises past it
        // by Clearance. Without that the export is off-by-Clearance from what its name promises.
        Case("ledge at exactly MaxClamberHeight accepts", true, 2.6f,
            s => Box(s, new Vector3(4, 1.6f, 4), new Vector3(0, 0.8f, -2.6f)));

        // Same ledge, crouched: reach scales with body height (1.6 * 0.75 = 1.2), so it is now out
        // of range. The landing is unchanged where it is still reachable -- a crouched capsule's
        // feet sit at the same offset from the body origin as a standing one's.
        Case("ledge at MaxClamberHeight rejects while crouched", false, 0f,
            s => Box(s, new Vector3(4, 1.6f, 4), new Vector3(0, 0.8f, -2.6f)), crouched: true);

        Case("1.0m ledge still accepts while crouched", true, 2.0f,
            s => Box(s, new Vector3(4, 1, 4), new Vector3(0, 0.5f, -2.6f)), crouched: true);

        Case("0.2m curb rejects as a step", false, 0f,
            s => Box(s, new Vector3(4, 0.2f, 4), new Vector3(0, 0.1f, -2.6f)));

        Case("3m wall rejects", false, 0f,
            s => Box(s, new Vector3(4, 3, 4), new Vector3(0, 1.5f, -2.6f)));

        // Ceiling 0.2m above the player's head: cannot rise far enough to clear anything.
        Case("low ceiling rejects", false, 0f,
            s => Box(s, new Vector3(4, 1, 4), new Vector3(0, 2.7f, 0)));

        // 60 degrees, past the default FloorMaxAngle of 45. Kept thin and low so it sits inside
        // the sweep's landing zone without blocking the forward sweep — otherwise the slope gets
        // rejected for lack of room and the normal check is never exercised.
        // The old ray ladder had no normal check at all and would happily target this.
        Case("60deg slope rejects", false, 0f,
            s => Box(s, new Vector3(4, 0.4f, 1.6f), new Vector3(0, 0.4f, -0.923f),
                Basis.FromEuler(new Vector3(Mathf.DegToRad(60f), 0, 0))));
    }

    public override void _PhysicsProcess(double delta)
    {
        if (_frame++ < 2) return;

        foreach (var (name, expectAccept, expectY, c) in _cases)
        {
            var accepted = c.TryStartClamber();
            if (accepted != expectAccept)
                _failures.Add($"{name}: expected {(expectAccept ? "accept" : "reject")}, got "
                    + (accepted ? $"accept (landing Y {c.ClamberTarget.Y:F3})" : "reject"));
            else if (expectAccept && Math.Abs(c.ClamberTarget.Y - expectY) > 0.02f)
                _failures.Add($"{name}: landing Y {c.ClamberTarget.Y:F3}, expected {expectY:F3}");
        }

        if (_failures.Count == 0) GD.Print("clamber tests: all passed");
        else foreach (var f in _failures) GD.PrintErr(f);

        GetTree().Quit(_failures.Count == 0 ? 0 : 1);
    }

    private const float StandHeight = 2.0f;
    private const float CrouchHeight = 1.5f;   // PlayerController's 0.5m CrouchDrop

    private void Case(string name, bool expectAccept, float expectY, Action<Node3D> geometry,
        bool crouched = false)
    {
        _x += 40f;
        var slice = new Node3D { Position = new Vector3(_x, 0, 0) };
        AddChild(slice);

        Box(slice, new Vector3(10, 1, 10), new Vector3(0, -0.5f, 0));   // floor, top at y=0
        geometry(slice);

        var body = new CharacterBody3D { Position = new Vector3(0, 1.0f, 0) };  // feet at y=0, faces -Z
        // Shaped the way PlayerController.ApplyCrouchHeight does it: shorter capsule, dropped by
        // half the difference so the feet stay at the same offset from the body origin.
        var height = crouched ? CrouchHeight : StandHeight;
        body.AddChild(new CollisionShape3D
        {
            Shape = new CapsuleShape3D { Radius = 0.5f, Height = height },
            Position = new Vector3(0, (height - StandHeight) / 2f, 0),
        });
        slice.AddChild(body);

        var c = new ClamberController { Player = body, DebugLog = true, HeightScale = height / StandHeight };
        body.AddChild(c);
        _cases.Add((name, expectAccept, expectY, c));
    }

    private static void Box(Node parent, Vector3 size, Vector3 pos) => Box(parent, size, pos, Basis.Identity);

    private static void Box(Node parent, Vector3 size, Vector3 pos, Basis basis)
    {
        var body = new StaticBody3D { Transform = new Transform3D(basis, pos) };
        body.AddChild(new CollisionShape3D { Shape = new BoxShape3D { Size = size } });
        parent.AddChild(body);
    }
}

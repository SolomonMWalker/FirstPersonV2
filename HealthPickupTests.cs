using System.Collections.Generic;
using Godot;

namespace FirstPerson;

// Run headless:  godot --headless --path . res://test_pickup.tscn
// Exits 0 on pass, 1 on failure.
//
// Drives the real test_level, because the thing worth testing is the overlap: HealthComponent.Heal
// is covered synchronously in HealthComponentTests, and what is left is whether a body standing in
// the volume is actually seen. The player is teleported onto the pack rather than walked there --
// from the Area3D's point of view those are the same thing, and walking would mean steering.
//
// RespawnDelay is cut to 0.4s at runtime so the whole schedule finishes before the turret's first
// shot lands (~frame 139) and starts editing the health this test is asserting on.
public partial class HealthPickupTests : Node
{
    private static readonly Vector3 OnPack = new(4f, 1f, 2f);   // pack sits at (4, 0.6, 2)
    private static readonly Vector3 Away = new(0f, 1f, 0f);     // the player's own spawn

    private readonly List<string> _failures = [];
    private int _frame;
    private PlayerController _player;
    private HealthComponent _health;
    private HealthPickup _pack;

    public override void _Ready()
    {
        Engine.PhysicsTicksPerSecond = 60;
        AddChild(GD.Load<PackedScene>("res://test_level.tscn").Instantiate());
    }

    private void MoveTo(Vector3 where)
    {
        _player.GlobalPosition = where;
        _player.Velocity = Vector3.Zero;
    }

    public override void _PhysicsProcess(double delta)
    {
        _frame++;

        if (_frame == 10)
        {
            _player = GetNode<PlayerController>("LevelSkeleton/Player");
            _pack = GetNode<HealthPickup>("LevelSkeleton/HealthPickup");
            _health = Component.Get<HealthComponent>(_player);
            _pack.RespawnDelay = 0.4f;

            True(_pack.Visible, "the pack starts hidden");

            // Two hits: the shield absorbs the first whole however big it is, so the one that is
            // supposed to reach health has to land on a shield that is already down.
            _health.TakeDamage(9999f, Vector3.Right);
            _health.TakeDamage(60f, Vector3.Right);
            Near(_health.Current, 40f, "health after a 60 hit");

            MoveTo(OnPack);
            return;
        }

        if (_frame == 20)
        {
            Near(_health.Current, 90f, "health after picking up a 50 pack at 40");
            True(!_pack.Visible, "the pack did not disappear when taken");
            MoveTo(Away);
            return;
        }

        // 0.4s is 24 ticks; 40 is comfortably past it.
        if (_frame == 60)
        {
            True(_pack.Visible, "the pack never respawned");
            MoveTo(OnPack);
            return;
        }

        if (_frame == 70)
        {
            // 90 + 50 clamped to Max, not 140.
            Near(_health.Current, 100f, "overheal from a pack did not clamp to Max");
            True(!_pack.Visible, "the pack did not disappear on the second pickup");
            return;
        }

        // Left standing on it at full health through a respawn. It must come back and stay put:
        // an entered signal would never re-fire here, and a pack that vanishes on contact with a
        // full-health player is one the player can waste by walking over it.
        if (_frame == 110)
        {
            True(_pack.Visible, "the pack did not respawn under a player standing on it");
            Near(_health.Current, 100f, "health changed while standing on a pack at full health");
            return;
        }

        if (_frame < 125) return;

        True(_pack.Visible, "a full-health player consumed the pack anyway");

        if (_failures.Count == 0) GD.Print("health pickup tests: all passed");
        else foreach (var f in _failures) GD.PrintErr(f);
        GetTree().Quit(_failures.Count == 0 ? 0 : 1);
    }

    private void True(bool ok, string what)
    {
        if (!ok) _failures.Add(what);
    }

    private void Near(float actual, float expected, string what)
    {
        if (!Mathf.IsEqualApprox(actual, expected)) _failures.Add($"{what}: expected {expected}, got {actual}");
    }
}

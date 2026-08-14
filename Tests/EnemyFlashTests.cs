using System.Collections.Generic;
using Godot;

namespace FirstPerson.Tests;

// Run headless:  godot --headless --path . res://Tests/test_enemy_flash.tscn
// Exits 0 on pass, 1 on failure.
//
// Damage is applied directly (TakeDamage), not by waiting on a real shot to travel and land --
// that would make the flash window's timing depend on projectile flight time too, which is a
// separate concern this test has no business coupling to.
public partial class EnemyFlashTests : Node
{
    private readonly List<string> _failures = [];
    private int _frame;
    private HealthComponent _health;
    private MeshInstance3D _mesh;

    public override void _Ready()
    {
        Engine.PhysicsTicksPerSecond = 60;
        AddChild(GD.Load<PackedScene>("res://test_level.tscn").Instantiate());
    }

    public override void _PhysicsProcess(double delta)
    {
        _frame++;
        if (_frame < 10) return;

        if (_frame == 10)
        {
            var walker = GetNode<EnemyController>("LevelSkeleton/Walker");
            _health = Component.Get<HealthComponent>(walker);
            _mesh = walker.GetNode<MeshInstance3D>("MeshInstance3D");

            True(_mesh.MaterialOverride is null, "the enemy starts flashing before ever being hit");
            _health.TakeDamage(10f);
            return;
        }

        // Damaged fires synchronously out of TakeDamage, so the override is already set the very
        // next tick.
        if (_frame == 11)
        {
            True(_mesh.MaterialOverride is not null, "taking damage did not start the flash");
            return;
        }

        // FlashDuration defaults to 0.1s (6 ticks at 60Hz); comfortably past it but nowhere near
        // long enough to be catching the settle of some other timer.
        if (_frame == 25)
        {
            True(_mesh.MaterialOverride is null, "the flash never clears once FlashDuration is up");

            if (_failures.Count == 0) GD.Print("enemy flash tests: all passed");
            else foreach (var f in _failures) GD.PrintErr(f);
            GetTree().Quit(_failures.Count == 0 ? 0 : 1);
        }
    }

    private void True(bool ok, string what)
    {
        if (!ok) _failures.Add(what);
    }
}

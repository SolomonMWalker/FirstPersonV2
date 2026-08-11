using System.Collections.Generic;
using Godot;

namespace FirstPerson;

// Run headless:  godot --headless --path . res://test_turret.tscn
// Exits 0 on pass, 1 on failure.
//
// Drives the real test_level and just waits, because the whole point is the chain nothing else
// tests: turret fires -> projectile flies and collides for real -> it asks whatever it hit for a
// HealthComponent -> that routes through the shield's AbsorbDamage hook. Every link is unit-tested
// on its own; this is the only thing that proves they are actually connected in the scene.
//
// The player is left standing still at its spawn, which is what the barrel is aimed at in the scene
// -- the turret fires down a fixed line and hits whatever is standing on it. The muzzle is ~4.8m
// away and the shot travels at 14 m/s from Interval (2s), so it lands around frame 139 at 60Hz.
public partial class TurretComponentTests : Node
{
    private readonly List<string> _failures = [];
    private int _frame;
    private ShieldComponent _shield;
    private HealthComponent _health;

    public override void _Ready()
    {
        Engine.PhysicsTicksPerSecond = 60;
        AddChild(GD.Load<PackedScene>("res://test_level.tscn").Instantiate());
    }

    public override void _PhysicsProcess(double delta)
    {
        _frame++;

        if (_frame == 10)
        {
            var player = GetNode<PlayerController>("LevelSkeleton/CharacterBody3D");
            var enemy = GetNode<Node3D>("LevelSkeleton/Enemy");

            _shield = Component.Get<ShieldComponent>(player);
            _health = Component.Get<HealthComponent>(player);
            True(_shield is not null, "player has no ShieldComponent");
            True(_health is not null, "player has no HealthComponent");

            // Invulnerability is the absence of a capability, not a flag on one.
            True(Component.Get<HealthComponent>(enemy) is null,
                "the turret carries a HealthComponent; it is supposed to be undamageable");
            True(Component.Get<TurretComponent>(enemy) is not null, "the enemy has no TurretComponent");
            return;
        }

        // 3s in: one shot has landed, and nothing has recharged yet (RechargeDelay is 3s from the hit).
        if (_frame == 180)
        {
            if (_shield is not null)
                True(_shield.Current < _shield.Max,
                    $"the shield never took a hit ({_shield.Current}/{_shield.Max}) -- nothing reached the player");
            if (_health is not null)
                True(Mathf.IsEqualApprox(_health.Current, _health.Max),
                    $"health took damage through a shield that had plenty left ({_health.Current}/{_health.Max})");

            // The HUD's failure mode is silent: if it cannot find the player its bars just sit at
            // full. Cheapest place to catch that is here, where a shield is known to be down.
            var bar = GetNode<ProgressBar>("LevelSkeleton/CharacterBody3D/Hud/Bars/Shield");
            if (_shield is not null)
                True(Mathf.IsEqualApprox((float)bar.Value, _shield.Current),
                    $"the HUD shield bar reads {bar.Value}, the component reads {_shield.Current}");
            return;
        }

        if (_frame < 190) return;

        if (_failures.Count == 0) GD.Print("turret component tests: all passed");
        else foreach (var f in _failures) GD.PrintErr(f);
        GetTree().Quit(_failures.Count == 0 ? 0 : 1);
    }

    private void True(bool ok, string what)
    {
        if (!ok) _failures.Add(what);
    }
}

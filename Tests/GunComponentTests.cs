using System.Collections.Generic;
using Godot;

namespace FirstPerson.Tests;

// Run headless:  godot --headless --path . res://Tests/test_gun.tscn
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
public partial class GunComponentTests : Node
{
    private readonly List<string> _failures = [];
    private int _frame;
    private ShieldComponent _shield;
    private HealthComponent _health;
    private CameraController _camera;
    // The punch is a spring, and it has decayed back to nothing well before the assertions run --
    // so the peak is sampled every frame rather than read once at the end.
    private Vector2 _peakPunch;
    private DamageResult? _shotResult;

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
            var player = GetNode<PlayerController>("LevelSkeleton/Player");
            var enemy = GetNode<Node3D>("LevelSkeleton/Enemy");

            _shield = Component.Get<ShieldComponent>(player);
            _health = Component.Get<HealthComponent>(player);
            _camera = player.Camera;
            True(_shield is not null, "player has no ShieldComponent");
            True(_health is not null, "player has no HealthComponent");

            // Invulnerability is the absence of a capability, not a flag on one.
            True(Component.Get<HealthComponent>(enemy) is null,
                "the turret carries a HealthComponent; it is supposed to be undamageable");
            var enemyGun = Component.Get<GunComponent>(enemy);
            True(enemyGun is not null, "the turret has no GunComponent");
            if (enemyGun is not null) enemyGun.ShotLanded += r => _shotResult = r;
            return;
        }

        if (_camera is not null && _camera.Punch.Length() > _peakPunch.Length()) _peakPunch = _camera.Punch;

        // 3s in: one shot has landed, and nothing has recharged yet (RechargeDelay is 3s from the hit).
        if (_frame == 180)
        {
            if (_shield is not null)
                True(_shield.Current < _shield.Max,
                    $"the shield never took a hit ({_shield.Current}/{_shield.Max}) -- nothing reached the player");
            if (_health is not null)
                True(Mathf.IsEqualApprox(_health.Current, _health.Max),
                    $"health took damage through a shield that had plenty left ({_health.Current}/{_health.Max})");

            // The signal a hitmarker would key off of: the shield ate the hit whole, so this must
            // report Shield, not Health -- the shooter never sees GunComponent.Landed at all unless
            // Projectile actually forwards HealthComponent's return value through.
            True(_shotResult == DamageResult.Shield,
                $"the turret's shot should have reported Shield (got {_shotResult})");

            // The HUD's failure mode is silent: if it cannot find the player its bars just sit at
            // full. Cheapest place to catch that is here, where a shield is known to be down.
            var bar = GetNode<ProgressBar>("LevelSkeleton/Player/Hud/Bars/Shield");
            if (_shield is not null)
                True(Mathf.IsEqualApprox((float)bar.Value, _shield.Current),
                    $"the HUD shield bar reads {bar.Value}, the component reads {_shield.Current}");

            // The kick is directional, and the whole point is that you can tell where a hit came
            // from without a HUD element. The player faces -Z and never turns; the turret sits
            // behind and to its left, so the view must pitch UP and roll toward the near side. A
            // punch of zero means the shield ate the hit without telling the camera.
            True(_peakPunch.Length() > 0.001f, "taking a hit produced no view punch at all");
            True(_peakPunch.X > 0f,
                $"the hit came from behind, so the view should pitch up; peak punch was {_peakPunch}");
            True(_peakPunch.Y > 0f,
                $"the hit came from the left, so the view should roll with it; peak punch was {_peakPunch}");
            return;
        }

        if (_frame < 190) return;

        if (_failures.Count == 0) GD.Print("gun component tests: all passed");
        else foreach (var f in _failures) GD.PrintErr(f);
        GetTree().Quit(_failures.Count == 0 ? 0 : 1);
    }

    private void True(bool ok, string what)
    {
        if (!ok) _failures.Add(what);
    }
}

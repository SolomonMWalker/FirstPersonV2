using System.Collections.Generic;
using Godot;

namespace FirstPerson;

// Run headless:  godot --headless --path . res://test_player_gun.tscn
// Exits 0 on pass, 1 on failure.
//
// Proves the player's own weapon is wired end to end: holding "fire" drives the player's
// GunComponent, a shot spawns from the viewmodel's muzzle and lands on a real target -- and, the
// regression this setup makes possible for the first time, the shooter never damages itself, even
// though the muzzle sits inside the player's own collision capsule (the viewmodel is mounted on
// the camera, a few centimetres off the body's own centre).
public partial class PlayerGunTests : Node
{
    private readonly List<string> _failures = [];
    private int _frame;
    private HealthComponent _playerHealth;
    private ShieldComponent _playerShield;
    private HealthComponent _walkerHealth;
    private DamageResult? _shotResult;

    public override void _Ready()
    {
        Engine.PhysicsTicksPerSecond = 60;
        AddChild(GD.Load<PackedScene>("res://test_level.tscn").Instantiate());
    }

    private static void Fire(bool down) =>
        Input.ParseInputEvent(new InputEventMouseButton { ButtonIndex = MouseButton.Left, Pressed = down });

    public override void _PhysicsProcess(double delta)
    {
        _frame++;
        if (_frame < 10) return;

        if (_frame == 10)
        {
            var player = GetNode<PlayerController>("LevelSkeleton/Player");
            var walker = GetNode<Node3D>("LevelSkeleton/Walker");

            // Both turrets fire on fixed schedules regardless of where the player stands; silence
            // them so nothing else can land a hit during this test and contaminate the self-damage
            // check below.
            foreach (var turret in new[] { "LevelSkeleton/Enemy", "LevelSkeleton/Enemy2" })
                Component.Get<GunComponent>(GetNode<Node3D>(turret)).Firing = false;

            _playerHealth = Component.Get<HealthComponent>(player);
            _playerShield = Component.Get<ShieldComponent>(player);
            _walkerHealth = Component.Get<HealthComponent>(walker);
            True(_walkerHealth is not null, "the Walker has no HealthComponent to shoot at");
            var playerGun = Component.Get<GunComponent>(player);
            True(playerGun is not null, "the player has no GunComponent");
            if (playerGun is not null) playerGun.ShotLanded += r => _shotResult = r;

            // Close range and aimed dead-on (Y matched, so this is pure yaw -- see EnemyTests for
            // why LookAt has to avoid pitching the body), so one shot is enough and travel time is
            // short and predictable. This is a wiring check, not a marksmanship one.
            player.GlobalPosition = walker.GlobalPosition + new Vector3(0f, 1f, 4f);
            player.LookAt(walker.GlobalPosition with { Y = player.GlobalPosition.Y });

            Fire(true);
            return;
        }

        // Interval defaults to 0.2s (12 ticks) plus the usual one-tick input-sampling latency, plus
        // travel time for a 4m shot at 14 m/s (~17 ticks): comfortably done by 60 ticks in.
        if (_frame == 70)
        {
            Fire(false);

            True(_walkerHealth.Current < _walkerHealth.Max,
                $"the player's shot never landed on the Walker ({_walkerHealth.Current}/{_walkerHealth.Max})");
            // The Walker carries no ShieldComponent, so the hitmarker this feeds must read Health,
            // not Shield -- the two-target split (this file's unshielded Walker, GunComponentTests'
            // shielded player) is what actually proves the color mapping picks the right one.
            True(_shotResult == DamageResult.Health,
                $"the player's shot on the (unshielded) Walker should have reported Health (got {_shotResult})");

            // The muzzle sits inside the player's own capsule. Without Projectile.Shooter (set by
            // GunComponent right after spawning), the very first shot would have hit the player
            // before it ever cleared its own muzzle.
            if (_playerShield is not null)
                True(Mathf.IsEqualApprox(_playerShield.Current, _playerShield.Max),
                    $"the player's own shot damaged their shield ({_playerShield.Current}/{_playerShield.Max})");
            else if (_playerHealth is not null)
                True(Mathf.IsEqualApprox(_playerHealth.Current, _playerHealth.Max),
                    $"the player's own shot damaged their health ({_playerHealth.Current}/{_playerHealth.Max})");
            return;
        }

        if (_frame < 80) return;

        if (_failures.Count == 0) GD.Print("player gun tests: all passed");
        else foreach (var f in _failures) GD.PrintErr(f);
        GetTree().Quit(_failures.Count == 0 ? 0 : 1);
    }

    private void True(bool ok, string what)
    {
        if (!ok) _failures.Add(what);
    }
}

using System.Collections.Generic;
using FirstPerson.StateMachines;
using Godot;

namespace FirstPerson.Tests;

// Run headless:  godot --headless --path . res://Tests/test_enemy.tscn
// Exits 0 on pass, 1 on failure.
//
// Drives the real test_level, because almost nothing here is testable in isolation: the navmesh has
// to bake off real geometry, the agent has to resolve a real path, and the shot has to physically
// arrive. The player is teleported to set up each range band rather than walked there -- to the
// brain, distance is distance.
//
// Both turrets are switched off first. They fire down fixed lanes across the arena and would
// otherwise be a second, unrelated source of damage in the middle of every assertion about who shot
// the player.
public partial class EnemyTests : Node
{
    private readonly List<string> _failures = [];
    private int _frame;
    private PlayerController _player;
    private EnemyController _walker;
    private StateMachine _brain;
    private GunComponent _gun;
    private HealthComponent _walkerHealth;
    private ShieldComponent _playerShield;
    private InteractableComponent _switch;
    private Node3D _switchObject;
    private float _chaseStartDistance;

    public override void _Ready()
    {
        Engine.PhysicsTicksPerSecond = 60;
        AddChild(GD.Load<PackedScene>("res://test_level.tscn").Instantiate());
    }

    // Puts the player a given flat distance from the enemy, on the +Z side of it, which is the side
    // it starts facing -- so no scenario depends on the enemy completing a 180.
    private void PlaceTargetAt(float distance) =>
        _player.GlobalPosition = _walker.GlobalPosition + new Vector3(0f, 1f, distance);

    private static void PressE(bool down) =>
        Input.ParseInputEvent(new InputEventKey { PhysicalKeycode = Key.E, Pressed = down });

    public override void _PhysicsProcess(double delta)
    {
        _frame++;

        if (_frame == 10)
        {
            _player = GetNode<PlayerController>("LevelSkeleton/Player");
            _walker = GetNode<EnemyController>("LevelSkeleton/Walker");
            _brain = _walker.GetNode<StateMachine>("StateMachine");
            _gun = Component.Get<GunComponent>(_walker);
            _walkerHealth = Component.Get<HealthComponent>(_walker);
            _playerShield = Component.Get<ShieldComponent>(_player);

            foreach (var turret in new[] { "LevelSkeleton/Enemy", "LevelSkeleton/Enemy2" })
                Component.Get<GunComponent>(GetNode<Node3D>(turret)).Firing = false;

            // The bake is the thing most likely to silently produce nothing, and an empty navmesh
            // looks exactly like a broken brain from the other end of the level.
            var region = GetNode<NavigationRegion3D>("LevelSkeleton/NavigationRegion3D");
            True(region.NavigationMesh.GetPolygonCount() > 0,
                "the navmesh baked empty -- nothing in the \"navmesh\" group produced geometry");

            True(_walker.Target == _player, "the enemy did not find the player by group");
            True(_gun is not null && _walkerHealth is not null, "the enemy is missing a component");

            _switchObject = GetNode<Node3D>("LevelSkeleton/EnemySwitch");
            _switch = Component.Get<InteractableComponent>(_switchObject);
            True(_switch is not null, "the switch cube has no InteractableComponent");
            True(_walker.Switch == _switch, "the enemy's Switch export did not resolve to the cube");
            True(!_walker.Active, "the enemy is supposed to start switched off");

            // Well inside SightRange (20). A switched-off enemy must ignore that completely, which
            // is the whole point: the level must not open with something already running at you.
            PlaceTargetAt(18f);
            return;
        }

        if (_frame == 20)
        {
            Has("Idle", "a switched-off enemy woke up anyway");
            True(_walker.Velocity with { Y = 0f } == Vector3.Zero, "a dormant enemy is moving");

            // Stand at the cube and look at it: 2.2m from its face, inside the interactor's 3m reach,
            // and on the east side, which is where you arrive from the player's spawn. That spot is
            // ~17m from the enemy -- inside SightRange (20) and well outside AttackRange (12), which
            // is the band the whole placement exists to put you in.
            _player.GlobalPosition = _switchObject.GlobalPosition + new Vector3(3f, 1f, 0f);
            _player.LookAt(_switchObject.GlobalPosition with { Y = _player.GlobalPosition.Y });
            return;
        }

        if (_frame == 30)
        {
            var prompt = GetNode<Label>("LevelSkeleton/Player/Hud/Prompt");
            True(prompt.Visible && prompt.Text == "Press E to turn the enemy on",
                $"the switch prompt reads '{prompt.Text}' (visible={prompt.Visible})");
            PressE(true);
            return;
        }
        if (_frame == 31) { PressE(false); return; }

        if (_frame == 40)
        {
            True(_walker.Active, "interacting with the cube did not switch the enemy on");
            True(_switch.Verb == "turn the enemy off", $"the verb did not flip: '{_switch.Verb}'");

            // No teleport here on purpose. The cube is placed so that standing at it puts you inside
            // SightRange (20) and outside AttackRange (12), so flipping the switch has to start a
            // chase from exactly where the player is standing. Put the cube next to the enemy
            // instead and this reads Attack -- it plants and shoots you at point blank, and the
            // chase never happens because there is no gap to close.
            Has("Chase", "flipping the switch did not start a chase from where the player is standing");
            _chaseStartDistance = _walker.FlatDistanceToTarget;
            True(_chaseStartDistance > _walker.AttackRange,
                $"the switch is inside AttackRange ({_chaseStartDistance:F1}m); there is no room to chase");
            return;
        }

        // A second of chasing. At Speed 3.5 that is ~3.5m, so 2m is a loose floor -- it proves the
        // navmesh, the agent and the state are all working without pinning down the exact path.
        if (_frame == 100)
        {
            True(_walker.FlatDistanceToTarget < _chaseStartDistance - 2f,
                $"the enemy barely closed: {_chaseStartDistance:F1}m -> {_walker.FlatDistanceToTarget:F1}m");

            PlaceTargetAt(8f);
            return;
        }

        if (_frame == 110)
        {
            Has("Attack", "an enemy 8m away should be attacking");
            True(_gun.Firing, "the Attack state did not switch the gun on");
            True(_playerShield.Current >= _playerShield.Max, "the player was hit before the gun could have fired");
            return;
        }

        // Interval is 1.5s (90 ticks) from entering Attack, plus ~0.6s of flight over 8m at 14 m/s.
        if (_frame == 250)
        {
            True(_playerShield.Current < _playerShield.Max,
                $"the enemy never landed a shot (shield {_playerShield.Current}/{_playerShield.Max})");

            _walkerHealth.TakeDamage(9999f, Vector3.Zero);
            return;
        }

        if (_frame == 260)
        {
            Has("Dead", "killing the enemy did not reach the Dead state");
            True(!_gun.Firing, "a dead enemy is still firing");
            return;
        }

        // RemoveAfter is 2s (120 ticks) from entering Dead.
        if (_frame < 400) return;

        True(!GodotObject.IsInstanceValid(_walker), "the corpse was never removed");

        if (_failures.Count == 0) GD.Print("enemy tests: all passed");
        else foreach (var f in _failures) GD.PrintErr(f);
        GetTree().Quit(_failures.Count == 0 ? 0 : 1);
    }

    private void True(bool ok, string what)
    {
        if (!ok) _failures.Add(what);
    }

    private void Has(string expected, string what)
    {
        var actual = _brain.GetStateMachineString();
        if (!actual.Contains(expected)) _failures.Add($"{what}: expected '{expected}' in '{actual}'");
    }
}

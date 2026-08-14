using System.Collections.Generic;
using Godot;

namespace FirstPerson.Tests;

// Run headless:  godot --headless --path . res://Tests/test_player_gun.tscn
// Exits 0 on pass, 1 on failure.
//
// Proves the player's own weapon is wired end to end: holding "fire" drives the player's
// HitscanComponent, the ray lands on a real target the instant Interval is up (no travel time to
// wait on) -- and, the regression the camera-origin ray makes possible, the shooter never damages
// itself, even though the ray starts from dead centre of its own collision capsule.
public partial class PlayerGunTests : Node
{
    private readonly List<string> _failures = [];
    private int _frame;
    private HealthComponent _playerHealth;
    private ShieldComponent _playerShield;
    private HealthComponent _walkerHealth;
    private DamageResult? _shotResult;
    private CameraController _camera;
    // The punch is a spring and has started decaying back toward level well before the assertion
    // runs, so the peak is sampled every frame rather than read once at the end -- same reasoning
    // as GunComponentTests' _peakPunch.
    private float _peakPunchX;
    // Same sampling reason as _peakPunchX: the flash is ~2 physics ticks long and is guaranteed to
    // be back off by the time any end-of-test assertion runs, so catching it means looking every frame.
    private Node3D _muzzleFlash;
    private bool _flashSeen;
    private float _healthBeforeSecondPull;

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
            var playerGun = Component.Get<HitscanComponent>(player);
            True(playerGun is not null, "the player has no HitscanComponent");
            if (playerGun is not null) playerGun.ShotLanded += r => _shotResult = r;
            _muzzleFlash = playerGun?.MuzzleFlash;
            // Before a single shot has been fired. The end-of-test "it went off again" check cannot
            // catch a flash that was lit from the moment the scene loaded -- that one passes either
            // way -- so the starting state has to be asserted separately, here.
            True(_muzzleFlash is null || !_muzzleFlash.Visible,
                "the muzzle flash is lit before the weapon has been fired");
            // The flare is drawn, unlike the light, so it has to be on the viewmodel layer or the
            // world camera renders it at world scale -- a metre-wide sheet hanging in the room.
            // Cheap to assert and impossible to notice headless, which is how cull_mask on the
            // world camera stayed wrong for as long as it did.
            var flare = _muzzleFlash?.GetNodeOrNull<VisualInstance3D>("Flare");
            True(flare is not null, "the muzzle flash has no Flare sprite under it");
            True(flare is null || flare.Layers == 2,
                $"the muzzle flare is not on the viewmodel render layer (layers={flare?.Layers})");
            _camera = player.Camera;

            // Close range and aimed dead-on (Y matched, so this is pure yaw -- see EnemyTests for
            // why LookAt has to avoid pitching the body). This is a wiring check, not a
            // marksmanship one; the range only has to comfortably clear HitscanComponent.Range.
            player.GlobalPosition = walker.GlobalPosition + new Vector3(0f, 1f, 4f);
            player.LookAt(walker.GlobalPosition with { Y = player.GlobalPosition.Y });

            Fire(true);
            return;
        }

        if (_camera is not null) _peakPunchX = Mathf.Max(_peakPunchX, _camera.Punch.X);
        if (_muzzleFlash is not null && _muzzleFlash.Visible) _flashSeen = true;

        // Interval defaults to 0.2s (12 ticks); input sampling and Firing-forwarding both add a
        // tick of their own latency on top (this test node is the tree root, so it and every
        // priority-0 node process before PlayerController's priority-1 SampleInput each tick --
        // measured empirically at 15 ticks total from Fire(true) to the shot actually landing).
        // There is no travel time to wait on beyond that; padded well past it regardless.
        // Fire-on-press, stated as behaviour rather than as a guard on any one line: the frame-30
        // block below holds the trigger for twenty ticks and so cannot tell a weapon that fires
        // instantly from one that telegraphs first. Note this does NOT pin _Ready's starting
        // cooldown -- with the countdown draining regardless of Firing, any starting value has
        // already run out by the time this presses. The pause-and-repress check at 44 is what
        // actually holds that line.
        if (_frame == 16)
            True(_walkerHealth.Current < _walkerHealth.Max,
                "the first trigger pull produced no shot within six ticks");

        if (_frame == 30)
        {
            Fire(false);

            True(_walkerHealth.Current < _walkerHealth.Max,
                $"the player's shot never landed on the Walker ({_walkerHealth.Current}/{_walkerHealth.Max})");
            // The Walker carries no ShieldComponent, so the hitmarker this feeds must read Health,
            // not Shield -- the two-target split (this file's unshielded Walker, GunComponentTests'
            // shielded player) is what actually proves the color mapping picks the right one.
            True(_shotResult == DamageResult.Health,
                $"the player's shot on the (unshielded) Walker should have reported Health (got {_shotResult})");
            // Placeholder recoil: a positive pitch kicks the view up (Godot's convention, opposite
            // Quake's -- see CameraController.AddPunch). Same spring landing and damage punch use,
            // so this only proves HitscanComponent actually calls it, not the spring itself.
            True(_peakPunchX > 0f, $"firing produced no upward recoil kick (peak Punch.X={_peakPunchX:F4})");

            // The ray starts at the camera's own GlobalPosition, inside the player's own capsule.
            // Godot does not report a hit on a shape the ray originates inside of, so query.Exclude
            // is not actually load-bearing for this dead-centre geometry -- but it is what
            // InteractorComponent's own ray relies on for the same reason, and this is the guard
            // against the day the muzzle or the camera's offset stops being dead centre.
            if (_playerShield is not null)
                True(Mathf.IsEqualApprox(_playerShield.Current, _playerShield.Max),
                    $"the player's own shot damaged their shield ({_playerShield.Current}/{_playerShield.Max})");
            else if (_playerHealth is not null)
                True(Mathf.IsEqualApprox(_playerHealth.Current, _playerHealth.Max),
                    $"the player's own shot damaged their health ({_playerHealth.Current}/{_playerHealth.Max})");
            return;
        }

        // Second pull, well past Interval since the last shot (the trigger came up at 30, and the
        // latest shot it could have fired clears its cooldown by ~41). This is the other half of the
        // same bug: a countdown that only drained while Firing froze its remainder at the moment of
        // release and charged it to this pull, so re-pressing after any pause was dead too.
        if (_frame == 44)
        {
            _healthBeforeSecondPull = _walkerHealth.Current;
            Fire(true);
            return;
        }

        if (_frame == 52)
        {
            Fire(false);
            True(_walkerHealth.Current < _healthBeforeSecondPull,
                "pressing fire again after a pause produced no shot within eight ticks");
            return;
        }

        if (_frame < 56) return;

        True(_muzzleFlash is not null, "the player's HitscanComponent has no MuzzleFlash wired");
        True(_flashSeen, "firing never made the muzzle flash visible");
        // Ten ticks after the trigger came up, far past FlashTime. This is the guard on the hide
        // running above the Firing guard rather than inside it -- release mid-flash used to leave
        // the muzzle lit until the next shot.
        True(_muzzleFlash is null || !_muzzleFlash.Visible,
            "the muzzle flash was still lit long after the trigger was released");

        if (_failures.Count == 0) GD.Print("player gun tests: all passed");
        else foreach (var f in _failures) GD.PrintErr(f);
        GetTree().Quit(_failures.Count == 0 ? 0 : 1);
    }

    private void True(bool ok, string what)
    {
        if (!ok) _failures.Add(what);
    }
}

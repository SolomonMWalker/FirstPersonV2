using Godot;

// Instant raycast damage from the player's camera, no travel time -- the standard first weapon
// type (PhysicsRayQueryParameters3D, no lead, no projectile pooling). GunComponent's projectiles
// stay the deliberately dodgeable, telegraphed kind for turrets and enemies; this is specifically
// the player's weapon, for the same reason InteractorComponent is specifically the player's
// interact ray -- aiming from a look direction only means anything for the thing with a camera.
[GlobalClass]
public partial class HitscanComponent : Component
{
	[Signal] public delegate void ShotLandedEventHandler(DamageResult result);

	[Export] public float Damage = 20f;
	[Export] public float Interval = 0.2f;
	[Export] public float Range = 100f;

	// Placeholder recoil, not a tuned weapon feel -- a flat vertical kick per shot until there's a
	// real weapon system to replace it with (per-weapon patterns, spray control, the works). Degrees,
	// positive kicks the view up (see CameraController.AddPunch). 0 disables it, same posture as
	// every other channel that feeds the same spring.
	[Export] public float RecoilPitch = 1.2f;

	// Authored per object, same seam GunComponent uses -- PlayerController flips this on the
	// "fire" action.
	[Export] public bool Firing;

	private PlayerController _player;
	private float _cooldown;

	public override void _Ready()
	{
		base._Ready();
		_player = PlayerController.Of(this);
		// Full interval before the first shot, same reasoning as GunComponent: firing the instant
		// Firing goes true would read as having no fire-rate at all.
		_cooldown = Interval;
	}

	public override void _PhysicsProcess(double delta)
	{
		if (!Firing) return;

		_cooldown -= (float)delta;
		if (_cooldown > 0f) return;
		_cooldown = Interval;

		Fire();
	}

	private void Fire()
	{
		// Camera, not this node's own transform: a component has to stay a direct child of
		// Components (see Components/README.md) and cannot inherit the camera's rotation from
		// there. Read live rather than cached, same as InteractorComponent's _player.Camera --
		// safe here because _PhysicsProcess only ever runs after the whole scene's _Ready
		// cascade has finished, unlike this component's own _Ready.
		var camera = _player.Camera;

		// Fires on every shot, hit or miss -- recoil is a property of pulling the trigger, not of
		// what the bullet did. Uses CameraController's existing punch spring (the same one landing
		// and damage feed), so sustained fire stacks and settles for free with no new code here.
		if (RecoilPitch != 0f) camera.AddPunch(RecoilPitch, 0f);

		var from = camera.GlobalPosition;
		var query = PhysicsRayQueryParameters3D.Create(from, from - camera.GlobalBasis.Z * Range);
		// The ray starts inside the player's own capsule -- without this it hits the shooter's
		// own body every time, same trap Projectile.Shooter exists to dodge for GunComponent.
		query.Exclude = [_player.GetRid()];

		var hit = GetWorld3D().DirectSpaceState.IntersectRay(query);
		if (hit.Count == 0) return;

		var result = Component.Get<HealthComponent>(hit["collider"].As<Node>())
			?.TakeDamage(Damage, (Vector3)hit["position"]) ?? DamageResult.None;
		if (result != DamageResult.None) EmitSignal(SignalName.ShotLanded, (int)result);
	}
}

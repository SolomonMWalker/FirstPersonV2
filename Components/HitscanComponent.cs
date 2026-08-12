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

	[Export] public float Damage = 20f;     // hit points per shot, dealt to anything with a HealthComponent
	// Seconds between shots -- the fire rate, and the only thing gating held fire. It does NOT delay
	// the first pull: the countdown starts at zero and runs whether or not the trigger is down, so a
	// click always fires immediately unless one landed less than Interval ago.
	[Export] public float Interval = 0.2f;
	[Export] public float Range = 100f;     // metres; past this the ray simply misses

	// Placeholder recoil, not a tuned weapon feel -- a flat vertical kick per shot until there's a
	// real weapon system to replace it with (per-weapon patterns, spray control, the works). Degrees,
	// positive kicks the view up (see CameraController.AddPunch). 0 disables it, same posture as
	// every other channel that feeds the same spring.
	[Export] public float RecoilPitch = 1.2f;

	// Authored per object, same seam GunComponent uses -- PlayerController flips this on the
	// "fire" action.
	[Export] public bool Firing;

	// Non-positional (AudioStreamPlayer, not 3D): this is the player's own gun next to their own
	// ears, so it has no position in the world to pan or attenuate from. Optional -- an unset
	// player is silent rather than broken, same posture as RecoilPitch = 0.
	[Export] public AudioStreamPlayer FireSound;

	// Shown for FlashTime seconds on each shot and hidden again. Anything parented under it comes
	// along, so growing this from a bare light into light + flare sprite is scene work, not code.
	// Optional, like the rest of the presentation exports.
	[Export] public Node3D MuzzleFlash;
	[Export] public float FlashTime = 0.04f;   // ~2 frames at 60fps

	private PlayerController _player;
	private float _cooldown;
	private float _flash;

	public override void _Ready()
	{
		base._Ready();
		_player = PlayerController.Of(this);
		// Deliberately NOT GunComponent's "wait a full Interval before the first shot". That delay is
		// a telegraph, and a telegraph is something you give an enemy so the player can react to it.
		// On the player's own trigger the same delay is just a click that does nothing, so the first
		// pull fires on the spot and Interval gates only the shots after it.
		_cooldown = 0f;
	}

	public override void _PhysicsProcess(double delta)
	{
		// Above the Firing guard on purpose: releasing the trigger inside the flash window would
		// otherwise skip the hide and leave the muzzle lit until the next shot.
		if (_flash > 0f)
		{
			_flash -= (float)delta;
			if (_flash <= 0f) MuzzleFlash.Visible = false;
		}

		// Drains whether or not the trigger is held. Draining it only while Firing would freeze the
		// remainder at the moment of release and charge it to the *next* pull, so a tap shorter than
		// Interval costs the player a shot and the click after it fires early.
		if (_cooldown > 0f) _cooldown -= (float)delta;

		if (!Firing) return;
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

		// Same reasoning as recoil: the bang belongs to the trigger pull, not to the hit. Play()
		// restarts from the top, so fire rates faster than the sample is long cut it off rather
		// than layering -- correct for a revolver, revisit if a weapon ever wants overlapping tails.
		FireSound?.Play();

		if (MuzzleFlash is not null)
		{
			MuzzleFlash.Visible = true;
			_flash = FlashTime;
			// Spun about the barrel axis so consecutive shots stop looking like the same stamp. Set
			// on the flash root rather than the flare itself: an omni light is rotation-invariant, so
			// this costs nothing here and reaches anything else hung under it later for free.
			MuzzleFlash.Rotation = MuzzleFlash.Rotation with { Z = GD.Randf() * Mathf.Tau };
		}

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

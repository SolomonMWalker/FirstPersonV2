using Godot;

// Spits a projectile down its own -Z every Interval seconds while Firing. It does not track, aim,
// lead, or check line of sight, and it has no idea the player exists -- where it points is wherever
// the object carrying it is facing, and that is the caller's problem, not this component's.
//
// Two very different things use it, which is why it is not called TurretComponent any more. A turret
// bolts it to something that never moves and leaves Firing on, so the shot goes down one fixed lane
// forever. An enemy bolts it to a body that yaws to face you and lets its Attack state switch Firing
// on and off, and the same fixed -Z becomes an aimed weapon for free.
//
// Firing is that seam. Because the countdown only runs while it is true, switching on always costs a
// full Interval before the first shot -- which is exactly the telegraph delay an enemy wants.
//
// The object carrying this is invulnerable purely by not having a HealthComponent, which is the
// component system's whole point: "can't be shot" is the absence of a capability, not a flag.
//
// This node's own position and rotation are the muzzle -- put it just past the end of the barrel.
[GlobalClass]
public partial class GunComponent : Component
{
	[Export] public PackedScene Projectile;
	[Export] public float Interval = 2f;

	// Authored per object. A sibling InteractableComponent, if there is one, becomes a switch; so does
	// a state machine that writes this directly. An object with neither is simply always on.
	[Export] public bool Firing = true;

	// Names this gun in the interact prompt: "turn the turret on", "turn the searchlight off". Unused
	// when there is no sibling interactable, which is the case on anything that shoots by itself.
	[Export] public string SwitchLabel = "turret";

	private float _cooldown;
	private InteractableComponent _switch;

	public override void _Ready()
	{
		base._Ready();
		// A full interval before the first shot: walking into a level and being hit on frame one
		// reads as a bug rather than as an enemy.
		_cooldown = Interval;

		// Loudly, and once. An unset scene makes the gun do nothing at all, which from the far end of
		// the room is indistinguishable from every other reason a shot might not arrive.
		if (Projectile is null) GD.PushError($"{Name}: no Projectile scene set; this gun will never fire.");

		// The specific behaviour attaches itself to the generic capability, never the reverse --
		// same direction as ShieldComponent installing itself into HealthComponent's hook.
		// InteractableComponent has no idea guns exist.
		_switch = Get<InteractableComponent>(GameObject);
		if (_switch is null) return;
		_switch.Interacted += Toggle;
		UpdateVerb();
	}

	private void Toggle()
	{
		Firing = !Firing;
		// Fresh countdown, so switching on does not fire instantly off a cooldown that has been
		// draining the whole time the gun was off.
		_cooldown = Interval;
		UpdateVerb();
	}

	// The prompt has to describe what pressing E will DO, not what the object currently is.
	private void UpdateVerb() => _switch.Verb = Firing ? $"turn the {SwitchLabel} off" : $"turn the {SwitchLabel} on";

	public override void _PhysicsProcess(double delta)
	{
		if (!Firing) return;

		_cooldown -= (float)delta;
		if (_cooldown > 0f) return;
		_cooldown = Interval;

		if (Projectile is null) return;

		var shot = Projectile.Instantiate<Node3D>();
		// Into the level, not under this node. A shot parented to its shooter would inherit the
		// shooter's transform and be freed along with it -- neither is true of a bullet in flight.
		(GetTree().CurrentScene ?? GameObject.GetParent()).AddChild(shot);
		// Straight out of the muzzle, down this node's own -Z, exactly where the barrel points.
		// Orthonormalized so a scaled shooter cannot smuggle a scale into the shot's basis, which
		// Projectile multiplies by Speed and would silently read as a different muzzle velocity.
		shot.GlobalTransform = GlobalTransform.Orthonormalized();
	}
}

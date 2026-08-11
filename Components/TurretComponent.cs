using Godot;

// Spits a projectile down its own -Z every Interval seconds, forever. A test dummy, not an enemy:
// it does not track, aim, lead, or check line of sight, and it has no idea the player exists. Where
// it points is set once in the scene by rotating the object it sits on.
//
// That makes it a fixed hazard rather than an opponent, which is what you want while tuning health
// and shields: the damage arrives on a schedule you can predict, and walking out of the line of fire
// stops it completely. Anything cleverer belongs to a real enemy, not to a rig for testing numbers.
//
// The object carrying this is invulnerable purely by not having a HealthComponent, which is the
// component system's whole point: "can't be shot" is the absence of a capability, not a flag.
//
// This node's own position and rotation are the muzzle -- put it just past the end of the barrel.
[GlobalClass]
public partial class TurretComponent : Component
{
	[Export] public PackedScene Projectile;
	[Export] public float Interval = 2f;

	private float _cooldown;

	public override void _Ready()
	{
		base._Ready();
		// A full interval before the first shot: walking into a level and being hit on frame one
		// reads as a bug rather than as an enemy.
		_cooldown = Interval;

		// Loudly, and once. An unset scene makes the turret do nothing at all, which from the far end
		// of the room is indistinguishable from every other reason a shot might not arrive.
		if (Projectile is null) GD.PushError($"{Name}: no Projectile scene set; this turret will never fire.");
	}

	public override void _PhysicsProcess(double delta)
	{
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

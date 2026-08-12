using Godot;

// A shot in flight. Flies straight down its own -Z, damages the first thing it touches that can be
// damaged, and dies either way. It knows nothing about who fired it or what it hit -- asking the
// thing it hit for a HealthComponent is the whole of its targeting logic, which is what lets the
// same projectile work against the player, an enemy, or a destructible crate that doesn't exist yet.
[GlobalClass]
public partial class Projectile : Area3D
{
	// Reports what happened, for a shooter that wants to react (a hitmarker) rather than watch the
	// target's own signals -- this fires once per shot regardless of who or what was hit, where
	// HealthComponent's Damaged only fires for the "reached hit points" case.
	[Signal] public delegate void LandedEventHandler(DamageResult result);

	[Export] public float Speed = 14f;
	[Export] public float Damage = 20f;
	[Export] public float Lifetime = 5f;   // seconds before it gives up, so strays don't accumulate

	// Set by GunComponent right after spawning, not authored in a scene. The one thing this needs
	// to know about its shooter: every existing muzzle sits well clear of its own shooter's
	// collider, but a muzzle overridden onto the shooter's own camera (the player's viewmodel) can
	// sit well inside it, and there is no other way to tell "the body I just grazed on the way out"
	// from a real target.
	public Node3D Shooter;

	private float _life;
	// Two bodies can enter on the same physics frame -- a wall corner and the player, say. Without
	// this the shot would deal its damage twice on the way out.
	private bool _spent;

	public override void _Ready()
	{
		_life = Lifetime;
		BodyEntered += OnHit;
	}

	public override void _PhysicsProcess(double delta)
	{
		GlobalPosition += -GlobalBasis.Z * Speed * (float)delta;

		_life -= (float)delta;
		if (_life <= 0f) QueueFree();
	}

	private void OnHit(Node3D body)
	{
		// Passes straight through, not spent: this is the shot clearing its own muzzle, not a shot
		// that hit something. It must still be able to hit a real target afterward.
		if (body == Shooter) return;
		if (_spent) return;
		_spent = true;

		// No HealthComponent is the normal case, not a failure: that's a wall, and hitting a wall is
		// how taking cover works without anything here having to know what cover is.
		var result = Component.Get<HealthComponent>(body)?.TakeDamage(Damage, GlobalPosition) ?? DamageResult.None;
		if (result != DamageResult.None) EmitSignal(SignalName.Landed, (int)result);
		QueueFree();
	}
}

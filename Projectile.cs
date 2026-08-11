using Godot;

// A shot in flight. Flies straight down its own -Z, damages the first thing it touches that can be
// damaged, and dies either way. It knows nothing about who fired it or what it hit -- asking the
// thing it hit for a HealthComponent is the whole of its targeting logic, which is what lets the
// same projectile work against the player, an enemy, or a destructible crate that doesn't exist yet.
[GlobalClass]
public partial class Projectile : Area3D
{
	[Export] public float Speed = 14f;
	[Export] public float Damage = 20f;
	[Export] public float Lifetime = 5f;   // seconds before it gives up, so strays don't accumulate

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
		if (_spent) return;
		_spent = true;

		// No HealthComponent is the normal case, not a failure: that's a wall, and hitting a wall is
		// how taking cover works without anything here having to know what cover is.
		Component.Get<HealthComponent>(body)?.TakeDamage(Damage, GlobalPosition);
		QueueFree();
	}
}

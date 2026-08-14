using Godot;

// A health pack: heals whatever touches it, vanishes, and comes back after a delay. Like Projectile
// it uses the component system without being part of it -- it asks whatever walked into it for a
// HealthComponent, so it heals the player, a future ally, or anything else with hit points, and does
// nothing at all to the things that haven't got any.
[GlobalClass]
public partial class HealthPickup : Area3D
{
	// A flat number of points, not a fraction of anything. Tuning a pack against the health bar it
	// refills is the whole design question; a percentage would silently retune itself every time
	// that bar changed size.
	[Export] public float Amount = 50f;
	// Seconds before it comes back after being taken. It hides rather than freeing itself, so the
	// node path stays valid across the whole cycle.
	[Export] public float RespawnDelay = 5f;

	// Decoration, and not optional decoration: a pickup that sits perfectly still reads as scenery,
	// and this object's entire job is to be spotted from across a room while something shoots at you.
	[Export] public float SpinSpeed = 90f;    // degrees per second
	[Export] public float BobHeight = 0.15f;  // metres, peak rise above the authored position
	[Export] public float BobPeriod = 2f;     // seconds per full up-and-down

	private float _cooldown;
	private float _bobPhase;
	private float _restY;

	public override void _Ready() => _restY = Position.Y;

	public override void _PhysicsProcess(double delta)
	{
		var d = (float)delta;

		// Visible is the state, not the timer: with the sign of a countdown as the flag, a
		// RespawnDelay of 0 would skip the frame that turns the pickup back on and it would never
		// come back at all.
		if (!Visible)
		{
			_cooldown -= d;
			if (_cooldown > 0f) return;
			Visible = true;
		}

		RotateY(Mathf.DegToRad(SpinSpeed) * d);
		_bobPhase = Mathf.Wrap(_bobPhase + Mathf.Tau * d / BobPeriod, 0f, Mathf.Tau);
		Position = Position with { Y = _restY + Mathf.Sin(_bobPhase) * BobHeight };

		// Polled rather than driven by body_entered. That signal fires on the way in and nothing
		// else, so a player still standing in the volume when the pack respawns would never pick it
		// up -- they never re-entered. Polling makes that case work with no second code path.
		foreach (var body in GetOverlappingBodies())
		{
			if (!TryGive(body)) continue;
			Visible = false;
			_cooldown = RespawnDelay;
			break;
		}
	}

	private bool TryGive(Node3D body)
	{
		if (Component.Get<HealthComponent>(body) is not { Alive: true } health) return false;

		// Full health leaves the pack sitting there rather than swallowing it. Standard shooter
		// behaviour, and it is what lets a pack be staged as a resource to come back to instead of
		// something you lose by walking the wrong way past it.
		if (health.Current >= health.Max) return false;

		health.Heal(Amount);
		return true;
	}
}

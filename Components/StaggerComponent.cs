using Godot;

// Tracks stagger damage separately from hit points and knocks the object into a Staggered state
// once enough of it lands. A sibling capability like HealthComponent and ShieldComponent, and just
// as optional -- the player carries no StaggerComponent, so a shot at the player simply has nowhere
// for its staggerDamage to go, the same "absence of a capability" rule that makes an unshootable
// turret possible.
[GlobalClass]
public partial class StaggerComponent : Component
{
	[Signal] public delegate void StaggeredEventHandler();
	[Signal] public delegate void RecoveredEventHandler();

	[Export] public float Threshold = 50f;         // stagger points that trip the Staggered state
	[Export] public float Duration = 2f;           // seconds spent staggered before recovering
	// Multiplies incoming (health) damage while Staggered. Read by whatever calls TakeDamage --
	// this component has no hook into HealthComponent, so it is on the caller to check Staggered
	// and scale its own damage before dealing it, the same way it already reads StaggerDamage.
	[Export] public float DamageMultiplier = 1.5f;

	// Seconds without a stagger hit before the meter starts recovering on its own. Any further
	// stagger damage restarts the wait -- same shape as ShieldComponent's RechargeDelay, and for the
	// same reason: without a restart, chip stagger during a sustained fight would be free.
	[Export] public float RefillDelay = 6f;
	// Fraction of Threshold recovered per second once RefillDelay has elapsed with no further hits.
	[Export] public float RefillRate = 0.5f;

	public bool IsStaggered { get; private set; }
	public float Current { get; private set; }

	private float _recoverTimer;
	private float _refillCooldown;
	private HealthComponent _health;

	public override void _Ready()
	{
		base._Ready();
		// Optional, same as ShieldComponent's: a corpse doesn't need to be told to stop recovering.
		_health = Get<HealthComponent>(GameObject);
	}

	// Called by whatever landed the hit (HitscanComponent, Projectile) alongside -- not instead of --
	// the matching call to HealthComponent.TakeDamage. The two travel together because every hit in
	// this game carries both a damage and a staggerDamage value.
	public void TakeStagger(float amount, Vector3 fromPosition = default)
	{
		if (amount <= 0f) return;
		if (_health is { Alive: false }) return;
		// Already down: more stagger damage while staggered doesn't stack or extend it. Revisit if a
		// heavy hit should ever refresh the timer instead of being wasted.
		if (IsStaggered) return;

		Current += amount;
		_refillCooldown = RefillDelay;
		if (Current < Threshold) return;

		Current = 0f;
		IsStaggered = true;
		_recoverTimer = Duration;
		EmitSignal(SignalName.Staggered);

		// TODO: play the Staggered animation here once one exists -- either directly (an
		// AnimationPlayer sibling this component reaches for) or by having a future
		// EnemyStates/StaggeredState listen for this Staggered signal and halt movement itself, the
		// same way BrainState's Dead transition listens to HealthComponent today.
	}

	public override void _PhysicsProcess(double delta)
	{
		if (IsStaggered)
		{
			_recoverTimer -= (float)delta;
			if (_recoverTimer > 0f) return;

			IsStaggered = false;
			EmitSignal(SignalName.Recovered);
			return;
		}

		// Nothing accumulated, or still waiting out RefillDelay from the last hit.
		if (Current <= 0f) return;
		if (_refillCooldown > 0f)
		{
			_refillCooldown -= (float)delta;
			return;
		}

		Current = Mathf.Max(Current - Threshold * RefillRate * (float)delta, 0f);
	}
}

using Godot;

namespace FirstPerson;

// A Halo-style regenerating shield: it soaks damage entirely until it breaks, then stays down until
// the object has gone long enough without being hit. Installs itself into HealthComponent's
// AbsorbDamage slot, so every existing damage source keeps calling TakeDamage and knows nothing
// about shields -- adding this component to an object is the whole integration.
[GlobalClass]
public partial class ShieldComponent : Component
{
	// Amount is what arrived, not what the shield could actually take, so a hit that overkills a
	// nearly-flat shield still reads as the big hit it was.
	[Signal] public delegate void DamagedEventHandler(float amount, Vector3 fromPosition);
	[Signal] public delegate void BrokenEventHandler();
	[Signal] public delegate void RechargedEventHandler();   // back to full

	[Export] public float Max = 70f;   // shield points, and the value it starts at

	// Two delays and two rates. Delays in seconds without taking damage, rates in points per second. A break costs you a much longer wait, and the fast refill afterwards
	// is what makes the wait read as recovery rather than as nothing happening. Any damage at all
	// restarts the wait, which is what stops chip damage from being free.
	[Export] public float RechargeDelay = 3f;          // chipped but still up
	[Export] public float BrokenRechargeDelay = 6f;    // after a break, and it latches until full
	[Export] public float RechargeRate = 25f;
	[Export] public float BrokenRechargeRate = 50f;    // held for the whole climb back, not just the start

	public float Current { get; private set; }
	public bool Up => Current > 0f;

	// The recharge cooldown, counted down in _PhysicsProcess. A float and not a Timer node: this
	// already ticks for the refill ramp, so the countdown is three lines with no child node to wire.
	private float _cooldown;

	// Latched at the break and cleared only on reaching full, so the fast rate survives the whole
	// climb instead of ending the instant Current leaves zero.
	private bool _broken;

	private HealthComponent _health;

	public override void _Ready()
	{
		base._Ready();
		Current = Max;

		// Null health is legal: a shield generator or a destructible barrier can be all shield.
		_health = Get<HealthComponent>(GameObject);
		if (_health is null) return;

		if (_health.AbsorbDamage is not null)
			GD.PushError($"{Name}: {GameObject?.Name} already has a damage absorber; only one may install itself.");
		else
			_health.AbsorbDamage = Absorb;
	}

	// Deliberately not a pure transform: it drains the shield, restarts the cooldown and emits.
	// Returns what gets through to hit points.
	private float Absorb(float amount, Vector3 fromPosition)
	{
		// Already down. The hit passes straight through, but still restarts the wait -- being shot
		// while your shield is broken has to keep it broken, or sustained fire would be survivable.
		if (!Up)
		{
			_cooldown = BrokenRechargeDelay;
			return amount;
		}

		Current = Mathf.Max(Current - amount, 0f);
		EmitSignal(SignalName.Damaged, amount, fromPosition);

		// The delay is picked by whether the shield is up *now*, the rate by the latch. A chip hit on
		// a shield part-way through a post-break refill therefore gets the short wait but keeps the
		// fast rate: it really is up again, and the break has already been paid for once.
		if (Up)
		{
			_cooldown = RechargeDelay;
		}
		else
		{
			_broken = true;
			_cooldown = BrokenRechargeDelay;
			EmitSignal(SignalName.Broken);
		}

		// No bleed-through: the hit that pops the shield is absorbed whole, so a one-point shield is
		// still a free hit. That free hit is the mechanic -- without it a shield is just more health.
		return 0f;
	}

	public override void _PhysicsProcess(double delta)
	{
		// Physics and not _Process: this is a gameplay quantity, and the default Pausable process
		// mode then stops it during pause for free.
		if (Current >= Max) return;
		// A corpse regenerating its shield is the kind of thing nobody notices until they do.
		if (_health is { Alive: false }) return;

		if (_cooldown > 0f)
		{
			_cooldown -= (float)delta;
			return;
		}

		Current = Mathf.Min(Current + (_broken ? BrokenRechargeRate : RechargeRate) * (float)delta, Max);

		if (Current < Max) return;
		_broken = false;
		EmitSignal(SignalName.Recharged);
	}
}

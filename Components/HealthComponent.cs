using System;
using Godot;

// What TakeDamage did with an incoming hit, for a caller that needs the answer synchronously (a
// hitmarker deciding what color to flash) rather than by listening to a signal. "Shield" is this
// component's own honest name for it: it does not know an absorber is a shield, only that
// something installed in AbsorbDamage consumed the hit whole. If armour ever chains onto the same
// slot (see AbsorbDamage below), this stops being an accurate name for that case.
public enum DamageResult { None, Shield, Health }

// Hit points and nothing else. The same component goes on the player and on every enemy -- there is
// no player health and enemy health, and no interface between them, because there is no difference.
//
// It does not know what damaged it, what should happen when it dies, or whether anything on screen
// is showing it. All of that hangs off the two signals.
[GlobalClass]
public partial class HealthComponent : Component
{
	// fromPosition is the damage source in world space, or Vector3.Zero for damage with no direction
	// (falling, poison, self-inflicted). Listeners that need a direction must handle the zero case.
	[Signal] public delegate void DamagedEventHandler(float amount, Vector3 fromPosition);
	[Signal] public delegate void DiedEventHandler();

	[Export] public float Max = 100f;

	public float Current { get; private set; }
	public bool Alive => Current > 0f;

	// Installed by a sibling that soaks damage before it reaches hit points (ShieldComponent). Takes
	// the incoming damage, returns what gets through. Health never learns what absorbed it, or that
	// anything did -- the newcomer attaches itself to the incumbent, never the reverse, which is what
	// keeps this file from growing a branch per absorber.
	// Not a [Signal]: Godot signals cannot return a value, and this has to hand an amount back.
	// ponytail: one slot, not a list -- if armour ever ships alongside shields, chain them here.
	// Nothing frees a component today; when something does, the absorber must clear this in
	// _ExitTree or this delegate holds a freed object and the next hit throws.
	public Func<float, Vector3, float> AbsorbDamage;

	public override void _Ready()
	{
		base._Ready();
		Current = Max;
	}

	public DamageResult TakeDamage(float amount, Vector3 fromPosition = default)
	{
		// Already dead absorbs nothing: two hits landing on the same frame must not fire Died twice,
		// or every death listener (ragdoll, score, respawn) runs twice.
		if (amount <= 0f || !Alive) return DamageResult.None;

		if (AbsorbDamage is not null) amount = AbsorbDamage(amount, fromPosition);
		// Fully soaked. Nothing lost hit points, so Damaged must not fire -- it means "took real
		// damage", and anything wanting "was hit at all" listens to the absorber's own signal too.
		if (amount <= 0f) return DamageResult.Shield;

		Current = Mathf.Max(Current - amount, 0f);
		EmitSignal(SignalName.Damaged, amount, fromPosition);
		if (!Alive) EmitSignal(SignalName.Died);
		return DamageResult.Health;
	}

	// Never resurrects: a health pack walked over by a corpse has to do nothing, or death stops
	// being final for the price of standing in the right spot. Coming back needs its own verb.
	public void Heal(float amount)
	{
		if (amount <= 0f || !Alive) return;
		Current = Mathf.Min(Current + amount, Max);
	}
}

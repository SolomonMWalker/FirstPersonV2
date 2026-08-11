using System;
using Godot;

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

	public void TakeDamage(float amount, Vector3 fromPosition = default)
	{
		// Already dead absorbs nothing: two hits landing on the same frame must not fire Died twice,
		// or every death listener (ragdoll, score, respawn) runs twice.
		if (amount <= 0f || !Alive) return;

		if (AbsorbDamage is not null) amount = AbsorbDamage(amount, fromPosition);
		// Fully soaked. Nothing lost hit points, so Damaged must not fire -- it means "took real
		// damage", and anything wanting "was hit at all" listens to the absorber's own signal too.
		if (amount <= 0f) return;

		Current = Mathf.Max(Current - amount, 0f);
		EmitSignal(SignalName.Damaged, amount, fromPosition);
		if (!Alive) EmitSignal(SignalName.Died);
	}
}

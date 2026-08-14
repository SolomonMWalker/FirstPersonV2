using Godot;

namespace FirstPerson.EnemyStates;

// Terminal. No outgoing edges at all, which is the statechart's way of saying death is final --
// there is no guard anywhere that could take it back out.
public partial class DeadState : EnemyState
{
	[Export] public float RemoveAfter = 2f;   // seconds the body lies there before it is freed

	private float _remaining;

	public override void StateEntered()
	{
		base.StateEntered();
		_remaining = RemoveAfter;

		// Stop shooting first: a corpse that gets one last shot off reads as a bug, not as a
		// last gasp.
		var gun = Component.Get<GunComponent>(Enemy);
		if (gun is not null) gun.Firing = false;

		// Deferred, because this runs inside the physics step and Godot will not let a body's shape
		// be disabled mid-flush. Dropping the collider is what stops the corpse blocking a doorway
		// for the two seconds before it disappears.
		Enemy.GetNode<CollisionShape3D>("CollisionShape3D").SetDeferred(CollisionShape3D.PropertyName.Disabled, true);
	}

	public override void StatePhysicsProcessing(double delta)
	{
		// Still falls while it waits, so something killed mid-air lands instead of hanging there.
		Enemy.Halt(delta);

		_remaining -= (float)delta;
		if (_remaining <= 0f) Enemy.QueueFree();
	}
}

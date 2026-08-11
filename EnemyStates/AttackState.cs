using FirstPerson.CustomTypes.StateMachine;

namespace FirstPerson.EnemyStates;

// Plants its feet, keeps facing you, and switches the gun on. That is the entire weapon: the body
// yaw does the aiming, and GunComponent's own countdown does the rate of fire.
//
// It stops to shoot rather than firing on the move. Standing still is the readable version -- it
// tells you which enemy is about to shoot -- and shooting while walking is a tuning decision worth
// making against a real weapon rather than guessing at now.
public partial class AttackState : EnemyState
{
	// Leaves a little past AttackRange rather than exactly at it, so backing off by a step does not
	// flip the state twice a second.
	private const float Hysteresis = 2f;

	private GunComponent _gun;

	public override void _Ready()
	{
		base._Ready();
		_gun = Component.Get<GunComponent>(Enemy);
	}

	protected override void AddTransitions() =>
		// Alive && excludes the parent's Dead edge; see BrainState.
		AddTransition("Chase", () => Enemy.Alive && Enemy.FlatDistanceToTarget > Enemy.AttackRange + Hysteresis);

	// Because the gun's countdown only runs while Firing, every entry into Attack costs a full
	// Interval before the first shot. That is the telegraph delay, for free.
	public override void StateEntered()
	{
		base.StateEntered();
		if (_gun is not null) _gun.Firing = true;
	}

	public override void StateExited()
	{
		base.StateExited();
		if (_gun is not null) _gun.Firing = false;
	}

	public override void StatePhysicsProcessing(double delta)
	{
		Enemy.FaceTarget(delta);
		Enemy.Halt(delta);
	}
}

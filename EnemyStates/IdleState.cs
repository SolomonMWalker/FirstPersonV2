namespace FirstPerson.EnemyStates;

// Asleep, not patrolling and not looking around. It stands there until it is switched on and you
// come close enough, which is all the level needs from an enemy you have not met yet.
public partial class IdleState : EnemyState
{
	protected override void AddTransitions() =>
		// Alive && is not noise: it excludes the parent's Dead edge, which a deeper edge like this
		// one would otherwise preempt forever. See BrainState. Active && is what a switched-off
		// enemy rests on -- nothing the player does will wake it.
		AddTransition("Chase", () => Enemy.Alive && Enemy.Active && Enemy.FlatDistanceToTarget < Enemy.SightRange);

	public override void StatePhysicsProcessing(double delta) => Enemy.Halt(delta);
}

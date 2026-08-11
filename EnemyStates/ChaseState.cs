namespace FirstPerson.EnemyStates;

// Walks the navmesh path toward the player, turning to face them as it goes.
public partial class ChaseState : EnemyState
{
	protected override void AddTransitions()
	{
		// Order matters: close enough to shoot is checked before far enough to give up, so an enemy
		// cannot lose interest on the frame it arrives. Alive && excludes the parent's Dead edge.
		AddTransition("Attack", () => Enemy.Alive && Enemy.FlatDistanceToTarget < Enemy.AttackRange);
		AddTransition("Idle", () => Enemy.Alive && Enemy.FlatDistanceToTarget > Enemy.LoseRange);
	}

	public override void StatePhysicsProcessing(double delta)
	{
		Enemy.FaceTarget(delta);
		Enemy.MoveAlongPath(delta);
	}
}

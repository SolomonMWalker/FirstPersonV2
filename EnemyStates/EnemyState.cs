using FirstPerson.CustomTypes.StateMachine;
using Godot;

namespace FirstPerson.EnemyStates;

// Shared base for the leaves of the enemy brain, shaped like PlayerStates/WalkingState: resolve the
// body by ancestor walk, then declare edges in a virtual so subclasses only write what differs.
public partial class EnemyState : AtomicState
{
	protected EnemyController Enemy;

	public override void _Ready()
	{
		Enemy = EnemyController.Of(this);
		AddTransitions();
	}

	protected virtual void AddTransitions() { }
}

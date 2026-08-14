using FirstPerson.CustomTypes.StateMachine;
using Godot;

namespace FirstPerson.EnemyStates;

// Holds the one edge that is true from everywhere: dead is dead, whatever you were doing. Written
// once here rather than three times on Idle, Chase and Attack -- the hoisting compound states exist
// for, and the reason adding a Patrol state later costs one node and no edits.
//
// The live states pay for it by excluding the condition in their own guards (see the note on
// Enemy.Alive in each). Deepest-source-wins means a child edge preempts this one, so without that
// exclusion a corpse would keep taking Chase -> Attack -> Chase and never reach Dead. That sharp
// edge, and this remedy, are both documented in StateMachine/README.md.
public partial class BrainState : CompoundState
{
	private EnemyController _enemy;
	private State _dead;
	private State _idle;

	public override void _Ready()
	{
		base._Ready();   // CompoundState collects children and validates DefaultState
		_enemy = EnemyController.Of(this);
		_dead = GetNode<State>("Dead");
		_idle = GetNode<State>("Idle");

		// !_dead.Enabled is what stops this being a self-transition. A compound's edges are evaluated
		// while ANY descendant is active, and being dead stays true forever -- so without it the
		// machine exits and re-enters Dead every single frame, which re-runs StateEntered and resets
		// the removal timer, and the corpse lies there permanently one frame from disappearing.
		AddTransition(_dead, () => !_enemy.Alive && !_dead.Enabled);

		// Switched off mid-fight, it goes back to sleep from wherever it was. Declared after Dead so
		// a corpse that is also switched off still dies -- insertion order breaks the tie. Same
		// self-transition exclusion as above, for the same reason.
		AddTransition(_idle, () => _enemy.Alive && !_enemy.Active && !_idle.Enabled);
	}
}

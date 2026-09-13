using Godot;
using FirstPerson.StateMachines;

namespace FirstPerson.GruntStates;

// StateMachine/Root/Alive/Active
//
// The two full-body overrides are declared here rather than on Alive because a descendant's
// transition preempts an ancestor's. When Dead is wired later it belongs here too -- first, so
// it wins over these -- and on Staggered and Falling alongside.
public partial class GruntActiveState : ParallelState
{
    [Export] public GruntAnimator Animator { get; set; }

    public override void _Ready()
    {
        base._Ready();
        AddTransition("Falling", () => Animator.TestFalling);
        AddTransition("Staggered", () => Animator.TestStagger, () => Animator.TestStagger = false);
    }
}

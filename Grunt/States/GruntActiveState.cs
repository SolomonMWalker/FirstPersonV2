using Godot;
using FirstPerson.StateMachines;

namespace FirstPerson.GruntStates;

// StateMachine/Root/Alive/Active
// Overrides live here, not on Alive, because descendant transitions preempt ancestors'.
public partial class GruntActiveState : ParallelState
{
    [Export] public GruntAnimator Animator { get; set; }

    public override void _Ready()
    {
        base._Ready();
        var grunt = (GruntEnemy)Owner;
        AddTransition("Falling", () => grunt.IsFalling);
        AddTransition("Staggered", () => Animator.TestStagger, () => Animator.TestStagger = false);
    }
}

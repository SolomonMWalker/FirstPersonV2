namespace FirstPerson.GruntStates;

// StateMachine/Root/Alive/Staggered
public partial class StaggeredState : GruntState
{
    public override void StateEntered()
    {
        base.StateEntered();
        Animator.Override = "stagger";
    }

    public override void StateExited()
    {
        base.StateExited();
        Animator.Override = null;
    }

    protected override void AddTransitions()
    {
        // Safe here: stagger's tree exits wait for code instead of auto-advancing.
        AddTransition("Active", () => Animator.IsCurrentAnimationFinished());
    }
}

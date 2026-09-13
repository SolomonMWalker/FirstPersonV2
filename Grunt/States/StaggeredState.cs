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
        // Unlike the aim/fire pair, stagger's exits are AtEnd + Enabled -- the tree parks on the
        // last frame and waits for code -- so the finished check is safe here.
        AddTransition("Active", () => Animator.IsCurrentAnimationFinished());
    }
}

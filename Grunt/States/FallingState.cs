namespace FirstPerson.GruntStates;

// StateMachine/Root/Alive/Falling
public partial class FallingState : GruntState
{
    public override void StateEntered()
    {
        base.StateEntered();
        Animator.Override = "falling";
    }

    public override void StateExited()
    {
        base.StateExited();
        Animator.Override = null;
    }

    protected override void AddTransitions()
    {
        AddTransition("Active", () => !Animator.TestFalling);
    }
}

namespace FirstPerson.GruntStates;

// StateMachine/Root/Alive/Active/Locomotion/Idle
public partial class IdleState : GruntState
{
    public override void StateEntered()
    {
        base.StateEntered();
        Animator.Motion = "idle";
    }

    protected override void AddTransitions()
    {
        AddTransition("Walking", () => Animator.TestWalking);
    }
}

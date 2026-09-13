namespace FirstPerson.GruntStates;

// StateMachine/Root/Alive/Active/Locomotion/Walking
public partial class WalkingState : GruntState
{
    public override void StateEntered()
    {
        base.StateEntered();
        Animator.Motion = "walk";
    }

    protected override void AddTransitions()
    {
        AddTransition("Idle", () => !Animator.TestWalking);
    }
}

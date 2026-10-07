namespace FirstPerson.GruntStates;

// StateMachine/Root/Alive/Active/Action/None
public partial class ActionNoneState : GruntState
{
    public override void StateEntered()
    {
        base.StateEntered();
        Animator.Action = GruntAction.None;
    }

    protected override void AddTransitions()
    {
        AddTransition("Firing", () => Animator.TestFire, () => Animator.TestFire = false);
    }
}

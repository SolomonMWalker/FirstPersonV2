namespace FirstPerson.GruntStates;

// StateMachine/Root/Alive/Active/Action/None
public partial class ActionNoneState : GruntState
{
    public override void StateEntered()
    {
        base.StateEntered();
        Animator.Action = null;
    }

    protected override void AddTransitions()
    {
        AddTransition("Aim", () => Animator.TestFire, () => Animator.TestFire = false);
    }
}

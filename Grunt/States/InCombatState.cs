namespace FirstPerson.GruntStates;

// StateMachine/Root/Alive/Active/Awareness/InCombat
public partial class InCombatState : GruntState
{
    public override void StateEntered()
    {
        base.StateEntered();
        Animator.Posture = "InCombat";
    }

    protected override void AddTransitions()
    {
        AddTransition("NotInCombat", () => !Animator.TestInCombat);
    }
}

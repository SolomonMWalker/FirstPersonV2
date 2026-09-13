namespace FirstPerson.GruntStates;

// StateMachine/Root/Alive/Active/Awareness/NotInCombat
public partial class NotInCombatState : GruntState
{
    public override void StateEntered()
    {
        base.StateEntered();
        Animator.Posture = "NotInCombat";
    }

    protected override void AddTransitions()
    {
        AddTransition("InCombat", () => Animator.TestInCombat);
    }
}

namespace FirstPerson.GruntStates;

// StateMachine/Root/Alive/Active/Awareness/InCombat
public partial class InCombatState : GruntState
{
    public override void StateEntered()
    {
        base.StateEntered();
        Animator.CombatState = GruntCombatState.InCombat;
    }

    protected override void AddTransitions()
    {
        AddTransition("NotInCombat", () => !Grunt.IsInCombat);
    }
}

namespace FirstPerson.GruntStates;

// StateMachine/Root/Alive/Active/Awareness/NotInCombat
public partial class NotInCombatState : GruntState
{
    public override void StateEntered()
    {
        base.StateEntered();
        Animator.CombatState = GruntCombatState.NotInCombat;
    }

    protected override void AddTransitions()
    {
        AddTransition("InCombat", () => Grunt.IsInCombat);
    }
}

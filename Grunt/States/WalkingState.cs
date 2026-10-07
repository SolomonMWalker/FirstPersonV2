namespace FirstPerson.GruntStates;

// StateMachine/Root/Alive/Active/Locomotion/Walking
public partial class WalkingState : GruntState
{
    public override void StateEntered()
    {
        base.StateEntered();
        Animator.Motion = GruntMotion.Walk;
    }

    protected override void AddTransitions()
    {
        AddTransition("Idle", () => !Grunt.IsMoving);
    }
}

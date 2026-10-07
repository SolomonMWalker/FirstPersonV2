namespace FirstPerson.GruntStates;

// StateMachine/Root/Alive/Falling
public partial class FallingState : GruntState
{
    public override void StateEntered()
    {
        base.StateEntered();
        Animator.Override = GruntOverride.Falling;
    }

    public override void StateExited()
    {
        base.StateExited();
        Animator.Override = GruntOverride.None;
    }

    protected override void AddTransitions()
    {
        AddTransition("Active", () => Grunt.IsOnFloor());
    }
}

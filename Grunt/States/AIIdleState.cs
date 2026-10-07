using Godot;

namespace FirstPerson.GruntStates;

// StateMachine/Root/Alive/Active/Behaviour/AIIdle
public partial class AIIdleState : GruntState
{
    public override void StateEntered()
    {
        base.StateEntered();
        Grunt.HorizontalVelocity = Vector3.Zero;
    }

    protected override void AddTransitions()
    {
        AddTransition("Chase", () => Grunt.DistanceToTarget > Grunt.ResumeDistance && Grunt.HasTarget);
    }
}

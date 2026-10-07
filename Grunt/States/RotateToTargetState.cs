namespace FirstPerson.GruntStates;

// StateMachine/Root/Alive/Active/Rotation/RotateToTarget
public partial class RotateToTargetState : GruntRotationState
{
    public override void StatePhysicsProcessing(double delta)
    {
        if (Grunt.HasTarget)
            Grunt.FaceTowards(Grunt.Target.GlobalPosition, delta);
    }
}

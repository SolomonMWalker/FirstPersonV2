namespace FirstPerson.GruntStates;

// StateMachine/Root/Alive/Active/Rotation/RotateToMovementDirection
public partial class RotateToMovementDirectionState : GruntRotationState
{
    // Standing still keeps the current facing.
    public override void StatePhysicsProcessing(double delta)
    {
        if (Grunt.IsMoving)
            Grunt.FaceDirection(Grunt.HorizontalVelocity, delta);
    }
}

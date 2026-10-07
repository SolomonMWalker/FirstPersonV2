using Godot;

namespace FirstPerson.GruntStates;

// StateMachine/Root/Alive/Active/Behaviour/Chase
public partial class ChaseState : GruntState
{
    public override void StateExited()
    {
        base.StateExited();
        Grunt.HorizontalVelocity = Vector3.Zero;
    }

    public override void StatePhysicsProcessing(double delta)
    {
        var agent = Grunt.NavAgent;
        agent.TargetPosition = Grunt.Target.GlobalPosition;

        // No path yet (or no navmesh): stand still rather than walk blind into walls.
        if (agent.IsNavigationFinished())
        {
            Grunt.HorizontalVelocity = Vector3.Zero;
            return;
        }

        var next = agent.GetNextPathPosition();
        var direction = (next - Grunt.GlobalPosition) with { Y = 0f };
        Grunt.HorizontalVelocity = direction.Normalized() * Grunt.MoveSpeed;
    }

    protected override void AddTransitions()
    {
        AddTransition("AIIdle", () => !Grunt.HasTarget || Grunt.DistanceToTarget <= Grunt.StopDistance);
    }
}

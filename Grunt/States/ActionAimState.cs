namespace FirstPerson.GruntStates;

// StateMachine/Root/Alive/Active/Action/Aim
public partial class ActionAimState : GruntState
{
    public override void StateEntered()
    {
        base.StateEntered();
        Animator.Action = "aimGun";
    }

    protected override void AddTransitions()
    {
        // aimGun -> fireGun is AtEnd + Auto in the AnimationTree, so the tree leads and the chart
        // follows it. Waiting on IsCurrentAnimationFinished() instead would deadlock: once the
        // tree advances on its own, its current node stops matching the travel target and the
        // travel never reads as complete. See AnimationTreeDriver.CurrentNode.
        AddTransition("Fire", () => Animator.CurrentNode.EndsWith("fireGun"));
    }
}

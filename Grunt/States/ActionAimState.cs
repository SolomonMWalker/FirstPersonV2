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
        // The tree auto-advances aimGun -> fireGun; IsCurrentAnimationFinished would deadlock.
        AddTransition("Fire", () => Animator.CurrentNode.EndsWith("fireGun"));
    }
}

namespace FirstPerson.GruntStates;

// StateMachine/Root/Alive/Active/Action/None
public partial class ActionNoneState : GruntState
{
    public override void StateEntered()
    {
        base.StateEntered();
        // Clears whatever Aim latched, so the path falls back to Locomotion's idle/walk clip.
        Animator.Action = null;
    }

    protected override void AddTransitions()
    {
        // The flag is consumed as the edge is taken, so it reads as a request rather than a hold.
        AddTransition("Aim", () => Animator.TestFire, () => Animator.TestFire = false);
    }
}

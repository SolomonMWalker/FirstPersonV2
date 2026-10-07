namespace FirstPerson.GruntStates;

// StateMachine/Root/Alive/Active/Action/Firing
public partial class ActionFiringState : GruntState
{
    // The tree reaches aimGun a frame or more after entry; don't exit before it has.
    private bool _sequenceStarted;

    public override void StateEntered()
    {
        base.StateEntered();
        _sequenceStarted = false;
        Animator.Action = GruntAction.Firing;
    }

    protected override void AddTransitions()
    {
        // The tree auto-advances aimGun -> fireGun -> idle; IsCurrentAnimationFinished would deadlock.
        AddTransition("None", IsSequenceFinished);
    }

    private bool IsSequenceFinished()
    {
        var current = Animator.CurrentNode;
        var inSequence = current.EndsWith("aimGun") || current.EndsWith("fireGun");
        if (inSequence) _sequenceStarted = true;
        return _sequenceStarted && !inSequence;
    }
}

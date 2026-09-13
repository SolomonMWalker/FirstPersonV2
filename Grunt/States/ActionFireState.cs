namespace FirstPerson.GruntStates;

// StateMachine/Root/Alive/Active/Action/Fire
public partial class ActionFireState : GruntState
{
    // Deliberately latches nothing: the tree auto-advanced itself onto fireGun, and travelling
    // to the clip it is already playing would restart it. Aim's "aimGun" stays latched until
    // None clears it, which composes the same path either way.
    protected override void AddTransitions()
    {
        // fireGun -> idleWithGunReady is also AtEnd + Auto, so again the tree leads.
        AddTransition("None", () => !Animator.CurrentNode.EndsWith("fireGun"));
    }
}

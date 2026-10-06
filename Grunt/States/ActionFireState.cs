namespace FirstPerson.GruntStates;

// StateMachine/Root/Alive/Active/Action/Fire
public partial class ActionFireState : GruntState
{
    // Sets no facet: the tree auto-advanced here, and travelling would restart the clip.
    protected override void AddTransitions()
    {
        AddTransition("None", () => !Animator.CurrentNode.EndsWith("fireGun"));
    }
}

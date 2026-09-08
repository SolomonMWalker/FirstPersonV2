using Godot;
using FirstPerson.StateMachines;

namespace FirstPerson.PlayerRigStates;

// StateMachine/Root/Revolver/Hammer/HammerDown
[GlobalClass]
public partial class HammerDownState : AtomicState
{
    public override void StateEntered()
    {
        base.StateEntered();
    }

    public override void StateExited()
    {
        base.StateExited();
    }

    public override void StateProcessing(double delta)
    {
    }

    public override void StatePhysicsProcessing(double delta)
    {
    }
}

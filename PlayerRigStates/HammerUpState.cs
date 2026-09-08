using Godot;
using FirstPerson.StateMachines;

namespace FirstPerson.PlayerRigStates;

// StateMachine/Root/Revolver/Hammer/HammerUp
[GlobalClass]
public partial class HammerUpState : AtomicState
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

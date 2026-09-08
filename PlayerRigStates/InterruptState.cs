using Godot;
using FirstPerson.StateMachines;

namespace FirstPerson.PlayerRigStates;

// StateMachine/Root/Revolver/Action/Reload/Interrupt
[GlobalClass]
public partial class InterruptState : AtomicState
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

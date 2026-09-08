using Godot;
using FirstPerson.StateMachines;

namespace FirstPerson.PlayerRigStates;

// StateMachine/Root/Weapon/Revolver
[GlobalClass]
public partial class WeaponRevolverState : AtomicState
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

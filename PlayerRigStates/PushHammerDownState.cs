using Godot;
using FirstPerson.StateMachines;

namespace FirstPerson.PlayerRigStates;

// StateMachine/Root/Revolver/Action/PushHammerDown
[GlobalClass]
public partial class PushHammerDownState : AtomicState
{
    [Export] public RevolverController RevolverController { get; set; }
    [Export] public RigController RigController { get; set; }
    
    public override void StateEntered()
    {
        base.StateEntered();
        RigController.Travel(RevolverController.aiming ? "RevolverRigHammerDownAim" : "RevolverRigHammerDownHip");
    }

    public override void StateExited()
    {
        RevolverController.isHammerDown = true;
        GD.Print("Leaving pushHammerDownState");
        base.StateExited();
    }

    public override void StateProcessing(double delta)
    {
    }

    public override void StatePhysicsProcessing(double delta)
    {
    }
    
}

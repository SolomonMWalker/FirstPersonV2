using Godot;
using FirstPerson.StateMachines;

namespace FirstPerson.PlayerRigStates;

// StateMachine/Root/Revolver/Action/Idle
[GlobalClass]
public partial class IdleState : AtomicState
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
        base.StateExited();
    }

    public override void StateProcessing(double delta)
    {
    }

    public override void StatePhysicsProcessing(double delta)
    {
    }
    
    protected virtual void AddTransitions()
    {
        AddTransition("PushHammerDown", 
            () => RigController.IsCurrentAnimationFinished() 
                  && RevolverController.pushHammerDown,
            () => RevolverController.pushHammerDown = false);
        AddTransition("Fire", 
            () => RigController.IsCurrentAnimationFinished()
                  && RevolverController.fire,
            () => RevolverController.fire = false);
    }
}

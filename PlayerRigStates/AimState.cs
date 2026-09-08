using Godot;
using FirstPerson.StateMachines;

namespace FirstPerson.PlayerRigStates;

// StateMachine/Root/AimOrHip/Aim
[GlobalClass]
public partial class AimState : AtomicState
{
    [Export] public RevolverController RevolverController { get; set; }
    [Export] public RigController RigController { get; set; }
    
    public override void StateEntered()
    {
        base.StateEntered();
        RigController.Travel("RevolverRigAimIdle"); 
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
        AddTransition("Hip", 
            () => RigController.IsCurrentAnimationFinished() && !RevolverController.aiming);
    }
}

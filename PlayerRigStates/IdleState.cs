using Godot;
using FirstPerson.StateMachines;

namespace FirstPerson.PlayerRigStates;

// StateMachine/Root/Revolver/Action/Idle
[GlobalClass]
public partial class IdleState : AtomicState
{
    [Export] public RevolverController RevolverController { get; set; }
    [Export] public RigController RigController { get; set; }

    private bool _animatedToAiming;
    
    public override void _Ready()
    {
        base._Ready();
        AddTransitions();
    }

    public override void StateEntered()
    {
        base.StateEntered();
    }

    public override void StateExited()
    {
        GD.Print("leaving idle state");
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
                  && RevolverController.pushHammerDownTrigger,
            () =>
            {
                RevolverController.pushHammerDownTrigger = false;
                GD.Print("transitioning to pushHammerDown");
            });
        AddTransition("Fire", 
            () => RigController.IsCurrentAnimationFinished()
                  && RevolverController.fireTrigger,
            () => RevolverController.fireTrigger = false);
    }
}

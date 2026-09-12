using Godot;
using FirstPerson.StateMachines;

namespace FirstPerson.PlayerRigStates;

// StateMachine/Root/RevolverRig/AimOrHip/Aim
[GlobalClass]
public partial class AimState : AtomicState
{
    [Export] public RevolverController RevolverController { get; set; }
    [Export] public RigController RigController { get; set; }

    // The Action region's Idle state. Stance may only change while it is active.
    [Export] public State Idle { get; set; }

    public override void _Ready()
    {
        base._Ready();
        AddTransitions();
    }

    public override void StateEntered()
    {
        base.StateEntered();
        RigController.Stance = "Aim";
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
            () => !RevolverController.aiming && Idle.Enabled && RigController.IsTravelComplete());
    }
}

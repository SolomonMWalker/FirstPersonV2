using Godot;
using FirstPerson.StateMachines;

namespace FirstPerson.PlayerRigStates;

// StateMachine/Root/RevolverRig/AimOrHip/Hip
public partial class HipState : AtomicState
{
    [Export] public RevolverController RevolverController { get; set; }
    [Export] public RigController RigController { get; set; }

    // Stance only changes while this is active.
    [Export] public State Idle { get; set; }

    public override void _Ready()
    {
        base._Ready();
        AddTransitions();
    }

    public override void StateEntered()
    {
        base.StateEntered();
        RigController.Stance = "Hip";
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
        AddTransition("Aim",
            () => RevolverController.aiming && Idle.Enabled && RigController.IsTravelComplete());
    }
}

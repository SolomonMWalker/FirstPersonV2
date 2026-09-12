using Godot;
using FirstPerson.StateMachines;

namespace FirstPerson.PlayerRigStates;

// StateMachine/Root/RevolverRig/AimOrHip/Hip
[GlobalClass]
public partial class HipState : AtomicState
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
        // IdleState owns the actual clip -- this region only says which branch of the tree
        // the rig is in.
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
        // Idle.Enabled is the "only from idle" rule: while firing, cocking or reloading the
        // rig stays in hip regardless of what the aim button is doing.
        AddTransition("Aim",
            () => RevolverController.aiming && Idle.Enabled && RigController.IsTravelComplete());
    }
}

using Godot;
using FirstPerson.StateMachines;

namespace FirstPerson.PlayerRigStates;

// StateMachine/Root/RevolverRig/Action/Idle
public partial class IdleState : AtomicState
{
    [Export] public RevolverController RevolverController { get; set; }
    [Export] public RigController RigController { get; set; }

    public override void _Ready()
    {
        base._Ready();
        AddTransitions();
    }

    public override void StateEntered()
    {
        base.StateEntered();
        TravelToIdle();
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
        // Travel de-dupes, so this only acts when stance or hammer changes.
        TravelToIdle();
    }

    private void TravelToIdle()
    {
        var stance = RigController.Stance;
        var hammer = RevolverController.isHammerDown ? "HammerDown" : "HammerUp";
        RigController.Travel($"{stance}/RevolverRig{stance}{hammer}Idle");
    }

    protected virtual void AddTransitions()
    {
        AddTransition("PushHammerDown",
            () => RigController.IsTravelComplete()
                  && RevolverController.pushHammerDownTrigger,
            () => RevolverController.pushHammerDownTrigger = false);
        AddTransition("Fire",
            () => RigController.IsTravelComplete()
                  && RevolverController.fireTrigger,
            () => RevolverController.fireTrigger = false);
        // Last, so fire and cock win over a same-frame reload.
        AddTransition("Reload",
            () => RigController.IsTravelComplete()
                  && RevolverController.reloadTrigger,
            () => RevolverController.reloadTrigger = false);
    }
}

using Godot;
using FirstPerson.StateMachines;

namespace FirstPerson.PlayerRigStates;

// StateMachine/Root/RevolverRig/Action/Idle
[GlobalClass]
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
        // The chart's initial configuration is entered from _Ready, before the first tick,
        // so the idle clip has to be picked here as well as in the per-frame poll.
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
        // AnimationTree runs on the physics callback. Travel de-dupes, so this only does
        // anything on the frame the chosen clip actually changes -- which is how a Hip->Aim
        // transition, or the hammer going up or down, re-triggers the idle animation.
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
        // IsTravelComplete, not IsCurrentAnimationFinished: the precondition for accepting an
        // action is that the idle pose is actually on screen, not that its clip ran out. The
        // idles are single-frame holds today, but this still holds if they are ever made to loop.
        AddTransition("PushHammerDown",
            () => RigController.IsTravelComplete()
                  && RevolverController.pushHammerDownTrigger,
            () => RevolverController.pushHammerDownTrigger = false);
        AddTransition("Fire",
            () => RigController.IsTravelComplete()
                  && RevolverController.fireTrigger,
            () => RevolverController.fireTrigger = false);
        // Last, so a queued shot or cock wins over a reload press latched on the same frame.
        AddTransition("Reload",
            () => RigController.IsTravelComplete()
                  && RevolverController.reloadTrigger,
            () => RevolverController.reloadTrigger = false);
    }
}

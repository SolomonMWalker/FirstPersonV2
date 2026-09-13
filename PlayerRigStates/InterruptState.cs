using Godot;
using FirstPerson.StateMachines;

namespace FirstPerson.PlayerRigStates;

// StateMachine/Root/RevolverRig/Action/Reload/Interrupt
public partial class InterruptState : AtomicState
{
    [Export] public RevolverController RevolverController { get; set; }
    [Export] public RigController RigController { get; set; }

    // The abort reach, then the same close clip the normal path uses.
    private int _phase;

    public override void _Ready()
    {
        base._Ready();
        AddTransitions();
    }

    public override void StateEntered()
    {
        base.StateEntered();
        // Consumed here, not in the guard: the guard is polled on both ticks, and clearing it
        // there would drop the flag before this state got to act on it.
        RevolverController.reloadInterrupted = false;
        _phase = 0;
        // The nested node is RevolverReloadInterrupt -- the one nested state without a short name.
        RigController.Travel("Reload/RevolverReloadInterrupt");
        GD.Print($"[reload] {Name} tree={RigController.CurrentNode} ammo={RevolverController.ammoInCylinder} reserve={RevolverController.reserveAmmo} left={RevolverController.reloadRemaining}");
    }

    public override void StatePhysicsProcessing(double delta)
    {
        if (_phase != 0 || !RigController.IsCurrentAnimationFinished()) return;
        _phase = 1;
        RigController.Travel("Reload/Close");
    }

    protected virtual void AddTransitions()
    {
        AddTransition("Idle", () => _phase == 1 && RigController.IsCurrentAnimationFinished());
    }
}

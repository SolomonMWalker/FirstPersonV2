using Godot;
using FirstPerson.StateMachines;

namespace FirstPerson.PlayerRigStates;

// StateMachine/Root/RevolverRig/Action/Reload/Interrupt
public partial class InterruptState : AtomicState
{
    [Export] public RevolverController RevolverController { get; set; }
    [Export] public RigController RigController { get; set; }

    private bool _closing;

    public override void _Ready()
    {
        base._Ready();
        AddTransitions();
    }

    public override void StateEntered()
    {
        base.StateEntered();
        // Consumed here, not in the guard, which is polled on both ticks.
        RevolverController.reloadInterrupted = false;
        _closing = false;
        RigController.Travel("Reload/RevolverReloadInterrupt");
        GD.Print($"[reload] {Name} tree={RigController.CurrentNode} ammo={RevolverController.ammoInCylinder} reserve={RevolverController.reserveAmmo} left={RevolverController.reloadRemaining}");
    }

    public override void StatePhysicsProcessing(double delta)
    {
        if (_closing || !RigController.IsCurrentAnimationFinished()) return;
        _closing = true;
        RigController.Travel("Reload/Close");
    }

    protected virtual void AddTransitions()
    {
        AddTransition("Idle", () => _closing && RigController.IsCurrentAnimationFinished());
    }
}

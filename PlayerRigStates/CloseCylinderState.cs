using Godot;
using FirstPerson.StateMachines;

namespace FirstPerson.PlayerRigStates;

// StateMachine/Root/RevolverRig/Action/Reload/CloseCylinder
[GlobalClass]
public partial class CloseCylinderState : AtomicState
{
    [Export] public RevolverController RevolverController { get; set; }
    [Export] public RigController RigController { get; set; }

    // Reach-to-close, then the close itself.
    private int _phase;

    public override void _Ready()
    {
        base._Ready();
        AddTransitions();
    }

    public override void StateEntered()
    {
        base.StateEntered();
        _phase = 0;
        RigController.Travel("Reload/ToClose");
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
        // No Interrupt edge, deliberately: a fire press while the cylinder is shutting is ignored.
        // The player presses again once the gun is back in idle.
        AddTransition("Idle", () => _phase == 1 && RigController.IsCurrentAnimationFinished());
    }
}

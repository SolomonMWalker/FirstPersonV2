using Godot;
using FirstPerson.StateMachines;

namespace FirstPerson.PlayerRigStates;

// StateMachine/Root/RevolverRig/Action/Reload/InsertNextBullet
public partial class InsertBulletState : AtomicState
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
        RevolverController.reloadRemaining--;
        RigController.Travel("Reload/InsertNext");
        GD.Print($"[reload] {Name} tree={RigController.CurrentNode} ammo={RevolverController.ammoInCylinder} reserve={RevolverController.reserveAmmo} left={RevolverController.reloadRemaining}");
    }

    protected virtual void AddTransitions()
    {
        // Can't abort a travel still in flight.
        AddTransition("Interrupt",
            () => RevolverController.reloadInterrupted && RigController.IsTravelComplete());
        AddTransition("TurnCylinder",
            () => RigController.IsCurrentAnimationFinished() && RevolverController.reloadRemaining > 0);
        AddTransition("CloseCylinder", () => RigController.IsCurrentAnimationFinished());
    }
}

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
        // This state seats one round; claim it before the guards read what is left.
        RevolverController.reloadRemaining--;
        RigController.Travel("Reload/InsertNext");
        GD.Print($"[reload] {Name} tree={RigController.CurrentNode} ammo={RevolverController.ammoInCylinder} reserve={RevolverController.reserveAmmo} left={RevolverController.reloadRemaining}");
    }

    protected virtual void AddTransitions()
    {
        // Not until the clip is on screen: a travel still in flight has nowhere to abort from.
        AddTransition("Interrupt",
            () => RevolverController.reloadInterrupted && RigController.IsTravelComplete());
        AddTransition("TurnCylinder",
            () => RigController.IsCurrentAnimationFinished() && RevolverController.reloadRemaining > 0);
        AddTransition("CloseCylinder", () => RigController.IsCurrentAnimationFinished());
    }
}

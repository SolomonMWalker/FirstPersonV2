using Godot;
using FirstPerson.StateMachines;

namespace FirstPerson.PlayerRigStates;

// StateMachine/Root/RevolverRig/Action/Reload/TurnCylinder
public partial class TurnCylinderState : AtomicState
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
        RigController.Travel("Reload/Turn");
        GD.Print($"[reload] {Name} tree={RigController.CurrentNode} ammo={RevolverController.ammoInCylinder} reserve={RevolverController.reserveAmmo} left={RevolverController.reloadRemaining}");
    }

    protected virtual void AddTransitions()
    {
        // Not until the clip is on screen: a travel still in flight has nowhere to abort from.
        AddTransition("Interrupt",
            () => RevolverController.reloadInterrupted && RigController.IsTravelComplete());
        AddTransition("InsertNextBullet", () => RigController.IsCurrentAnimationFinished());
    }
}

using Godot;
using FirstPerson.StateMachines;

namespace FirstPerson.PlayerRigStates;

// StateMachine/Root/RevolverRig/Action/Reload/IntroInsertBullet
public partial class IntroInsertBulletState : AtomicState
{
    [Export] public RevolverController RevolverController { get; set; }
    [Export] public RigController RigController { get; set; }

    // Opens the cylinder, then plays the first insert.
    private bool _inserting;

    public override void _Ready()
    {
        base._Ready();
        AddTransitions();
    }

    public override void StateEntered()
    {
        base.StateEntered();
        // Ignore fire presses latched before the reload started.
        RevolverController.reloadInterrupted = false;
        RevolverController.BeginReload();
        RevolverController.reloadRemaining--;
        _inserting = false;
        RigController.Travel($"RevolverRigReloadStartOpenCylinder{RigController.Stance}");
        GD.Print($"[reload] {Name} tree={RigController.CurrentNode} ammo={RevolverController.ammoInCylinder} reserve={RevolverController.reserveAmmo} left={RevolverController.reloadRemaining}");
    }

    public override void StatePhysicsProcessing(double delta)
    {
        if (_inserting || !RigController.IsCurrentAnimationFinished()) return;
        _inserting = true;
        RigController.Travel("Reload/InsertFirst");
    }

    private bool InsertFinished => _inserting && RigController.IsCurrentAnimationFinished();

    protected virtual void AddTransitions()
    {
        // The tree can only reach Interrupt from the insert clips.
        AddTransition("Interrupt",
            () => RevolverController.reloadInterrupted && _inserting
                  && RigController.IsTravelComplete());
        AddTransition("TurnCylinder", () => InsertFinished && RevolverController.reloadRemaining > 0);
        AddTransition("CloseCylinder", () => InsertFinished);
    }
}

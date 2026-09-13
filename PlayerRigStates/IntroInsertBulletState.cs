using Godot;
using FirstPerson.StateMachines;

namespace FirstPerson.PlayerRigStates;

// StateMachine/Root/RevolverRig/Action/Reload/IntroInsertBullet
[GlobalClass]
public partial class IntroInsertBulletState : AtomicState
{
    [Export] public RevolverController RevolverController { get; set; }
    [Export] public RigController RigController { get; set; }

    // Two clips: the stance-specific open-cylinder leaf at the root of the tree, then the first
    // insert inside the nested Reload machine. One state owns both so the cylinder is never left
    // hanging open as a resting configuration.
    private int _phase;

    public override void _Ready()
    {
        base._Ready();
        AddTransitions();
    }

    public override void StateEntered()
    {
        base.StateEntered();
        // A fire press latched while some other action was playing must not abort the reload it
        // is only now starting.
        RevolverController.reloadInterrupted = false;
        // How many rounds this reload will seat, decided once, here. This state seats the first
        // of them, so claim it now: the guards below then read what is still to come.
        RevolverController.BeginReload();
        RevolverController.reloadRemaining--;
        _phase = 0;
        RigController.Travel($"RevolverRigReloadStartOpenCylinder{RigController.Stance}");
        GD.Print($"[reload] {Name} tree={RigController.CurrentNode} ammo={RevolverController.ammoInCylinder} reserve={RevolverController.reserveAmmo} left={RevolverController.reloadRemaining}");
    }

    public override void StatePhysicsProcessing(double delta)
    {
        if (_phase != 0 || !RigController.IsCurrentAnimationFinished()) return;
        _phase = 1;
        RigController.Travel("Reload/InsertFirst");
    }

    private bool InsertFinished => _phase == 1 && RigController.IsCurrentAnimationFinished();

    protected virtual void AddTransitions()
    {
        // Only once the insert clip is genuinely on screen. The interrupt edges in the tree leave
        // InsertFirst / Turn / InsertNext, so aborting before the tree has arrived there would
        // travel to a node the nested machine cannot reach yet. The press stays latched in
        // reloadInterrupted until then, so nothing is lost.
        AddTransition("Interrupt",
            () => RevolverController.reloadInterrupted && _phase == 1
                  && RigController.IsTravelComplete());
        AddTransition("TurnCylinder", () => InsertFinished && RevolverController.reloadRemaining > 0);
        AddTransition("CloseCylinder", () => InsertFinished);
    }
}

using Godot;
using FirstPerson.StateMachines;

namespace FirstPerson.PlayerRigStates;

// StateMachine/Root/RevolverRig/Action/Fire
[GlobalClass]
public partial class FireState : AtomicState
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
        var stance = RigController.Stance;
        RigController.Travel($"{stance}/RevolverRig{stance}Fire");
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
    }

    protected virtual void AddTransitions()
    {
        // RevolverController.Fire() already cleared isHammerDown, so Idle lands on the
        // hammer-up clip when we get back.
        AddTransition("Idle", () => RigController.IsCurrentAnimationFinished());
    }
}

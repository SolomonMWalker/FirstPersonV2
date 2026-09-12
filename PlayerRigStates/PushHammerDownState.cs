using Godot;
using FirstPerson.StateMachines;

namespace FirstPerson.PlayerRigStates;

// StateMachine/Root/RevolverRig/Action/PushHammerDown
[GlobalClass]
public partial class PushHammerDownState : AtomicState
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
        RigController.Travel($"{stance}/RevolverRigPushHammerDown{stance}");
    }

    public override void StateExited()
    {
        RevolverController.isHammerDown = true;
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
        AddTransition("Idle", () => RigController.IsCurrentAnimationFinished());
    }
}

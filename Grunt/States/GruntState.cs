using Godot;
using FirstPerson.StateMachines;

namespace FirstPerson.GruntStates;

public abstract partial class GruntState : AtomicState
{
    [Export] public GruntAnimator Animator { get; set; }

    public override void _Ready()
    {
        base._Ready();
        AddTransitions();
    }

    protected abstract void AddTransitions();
}

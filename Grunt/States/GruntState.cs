using Godot;
using FirstPerson.StateMachines;

namespace FirstPerson.GruntStates;

public abstract partial class GruntState : AtomicState
{
    [Export] public GruntAnimator Animator { get; set; }

    protected GruntEnemy Grunt { get; private set; }

    public override void _Ready()
    {
        base._Ready();
        Grunt = (GruntEnemy)Owner;
        AddTransitions();
    }

    protected abstract void AddTransitions();
}

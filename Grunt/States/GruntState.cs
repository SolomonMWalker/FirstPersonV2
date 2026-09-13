using Godot;
using FirstPerson.StateMachines;

namespace FirstPerson.GruntStates;

// Base for the grunt's leaf states. Each one latches a single facet on the animator and reads
// its test drivers for guards; nothing here knows about any other region.
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

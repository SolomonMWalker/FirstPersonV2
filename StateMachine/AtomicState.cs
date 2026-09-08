using Godot;

namespace FirstPerson.StateMachines;

// A leaf state. Processing is delivered by the StateMachine, not polled per node.
[GlobalClass]
[Icon("res://StateMachine/Icons/atomic_state.svg")]
public partial class AtomicState : State
{
    public override string GetFullStateString()
    {
        return Name;
    }
}

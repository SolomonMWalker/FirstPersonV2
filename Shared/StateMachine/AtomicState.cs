using Godot;

namespace FirstPerson.StateMachines;

[GlobalClass]
[Icon("res://StateMachine/Icons/atomic_state.svg")]
public partial class AtomicState : State
{
    public override string GetFullStateString()
    {
        return Name;
    }
}

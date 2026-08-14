using System.Collections.Generic;
using System.Linq;
using Godot;

namespace FirstPerson.StateMachines;

// All children are active whenever this state is active.
[GlobalClass]
public partial class ParallelState : State
{
    public List<State> ChildrenStates { get; private set; } = [];

    public override void _Ready()
    {
        ChildrenStates = GetChildren().OfType<State>().ToList();
    }

    public override List<State> GetAllStates()
    {
        List<State> states = [this];
        foreach (var child in ChildrenStates)
        {
            states.AddRange(child.GetAllStates());
        }

        return states;
    }

    public override string GetFullStateString()
    {
        var children = string.Join(", ", ChildrenStates.Select(c => c.GetFullStateString()));
        return $"{Name}({children})";
    }
}

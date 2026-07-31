using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace FirstPerson.CustomTypes.StateMachine;

// Exactly one child is active at a time. The StateMachine owns ActiveState; it is updated as part
// of computing the entry set, so this class no longer decides anything about transitions itself.
[GlobalClass]
public partial class CompoundState : State
{
    [Export] public State DefaultState;

    public List<State> ChildrenStates { get; private set; } = [];
    public State ActiveState { get; internal set; }

    public override void _Ready()
    {
        ChildrenStates = GetChildren().OfType<State>().ToList();
        if (ChildrenStates.Count == 0)
        {
            throw new Exception("Compound state has no children states");
        }

        DefaultState ??= ChildrenStates.First();

        if (!ChildrenStates.Contains(DefaultState))
        {
            throw new Exception($"Default state {DefaultState.Name} is not a child of {Name}");
        }

        ActiveState = DefaultState;
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
        return $"{Name}({ActiveState?.GetFullStateString()})";
    }
}

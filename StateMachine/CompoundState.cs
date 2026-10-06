using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace FirstPerson.StateMachines;

// Exactly one child active at a time; ActiveState is set by the StateMachine.
[GlobalClass]
[Icon("res://StateMachine/Icons/compound_state.svg")]
public partial class CompoundState : State
{
    // Defaults to the first State child.
    [Export] public State DefaultState;

    // Shallow history: re-entry resumes the last active child instead of DefaultState.
    [Export] public bool RememberActiveState;

    public State EntryState => RememberActiveState && ActiveState is not null ? ActiveState : DefaultState;

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

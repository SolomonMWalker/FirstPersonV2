using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace FirstPerson.StateMachines;

// Exactly one child is active at a time. The StateMachine owns ActiveState; it is updated as part
// of computing the entry set, so this class no longer decides anything about transitions itself.
[GlobalClass]
public partial class CompoundState : State
{
    // Which child this region enters when nothing says otherwise. Unset falls back to the first
    // State child, so a region with an obvious starting state needs no wiring.
    [Export] public State DefaultState;

    // Shallow history (SCXML's <history type="shallow">). Off by default: re-entering resolves to
    // DefaultState. On, the region resumes whichever child it was in when it last exited, so a
    // transition that suspends it -- one taken by an ancestor, or by a parallel sibling region --
    // gives it back unchanged instead of resetting it.
    [Export] public bool RememberActiveState;

    // Where default entry lands: the remembered child, or the declared default.
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

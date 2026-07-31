using System;
using System.Collections.Generic;
using Godot;

namespace FirstPerson.CustomTypes.StateMachine;

[GlobalClass]
public partial class State : Node
{
    public event EventHandler<ChangeStateEventArgs> StateChangeRequired;
    protected void OnStateChangeRequired(ChangeStateEventArgs e)
    {
        var handler = StateChangeRequired;
        handler?.Invoke(this, e);
    }

    public virtual List<State> GetAllStates()
    {
        return [this];
    }

    // Declarative outgoing transitions, evaluated by the StateMachine while this state is active.
    // Transitions on a compound/parallel state are evaluated while ANY descendant is active, and a
    // descendant's own transition preempts them (deepest source wins). Evaluated in insertion order;
    // first match wins. Optional: states can still transition imperatively via OnStateChangeRequired.
    public List<Transition> Transitions { get; } = [];

    public Transition AddTransition(string toStateName, Func<bool> guard = null, Action onTransition = null)
    {
        var transition = new Transition(toStateName, guard, onTransition);
        Transitions.Add(transition);
        return transition;
    }

    // Preferred overload: rename-safe, needs no lookup or startup validation.
    public Transition AddTransition(State toState, Func<bool> guard = null, Action onTransition = null)
    {
        var transition = new Transition(toState, guard, onTransition);
        Transitions.Add(transition);
        return transition;
    }

    // The first transition whose guard currently passes, or null if none are eligible.
    public Transition GetEligibleTransition()
    {
        foreach (var transition in Transitions)
        {
            if (transition.GuardPasses()) return transition;
        }

        return null;
    }

    public bool Enabled { get; private set; }

    // Plain flag setters. They do NOT cascade -- the StateMachine computes the exact exit and entry
    // sets from the least common ancestor and enters/exits each state itself.
    public void Enable() => Enabled = true;

    public void Disable() => Enabled = false;

    // The enter/exit hooks. Override these; never Enable/Disable.
    public virtual void StateEntered()
    {
        Enable();
    }

    public virtual void StateExited()
    {
        Disable();
    }

    public virtual void StatePhysicsProcessing(double delta) {}
    public virtual void StateProcessing(double delta) {}

    public virtual string GetFullStateString() => "";

    // Strict ancestors that are States, outermost first. Excludes this state.
    public List<State> GetAncestorChain()
    {
        List<State> chain = [];
        for (var parent = GetParent() as State; parent is not null; parent = parent.GetParent() as State)
        {
            chain.Add(parent);
        }

        chain.Reverse();
        return chain;
    }
}

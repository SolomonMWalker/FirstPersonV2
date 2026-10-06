using System;
using System.Collections.Generic;
using Godot;

namespace FirstPerson.StateMachines;

[GlobalClass]
public abstract partial class State : Node
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

    // Evaluated in order while this state or any descendant is active; first passing guard wins.
    public List<Transition> Transitions { get; } = [];

    public Transition AddTransition(string toStateName, Func<bool> guard = null, Action onTransition = null)
    {
        var transition = new Transition(toStateName, guard, onTransition);
        Transitions.Add(transition);
        return transition;
    }

    // Preferred: rename-safe.
    public Transition AddTransition(State toState, Func<bool> guard = null, Action onTransition = null)
    {
        var transition = new Transition(toState, guard, onTransition);
        Transitions.Add(transition);
        return transition;
    }

    public Transition GetEligibleTransition()
    {
        foreach (var transition in Transitions)
        {
            if (transition.GuardPasses()) return transition;
        }

        return null;
    }

    public bool Enabled { get; private set; }

    // Non-cascading; the StateMachine enters and exits each state itself.
    public void Enable() => Enabled = true;

    public void Disable() => Enabled = false;

    // Override these, not Enable/Disable.
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

    // Outermost first, excluding this state.
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

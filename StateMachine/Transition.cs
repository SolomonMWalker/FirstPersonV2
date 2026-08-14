using System;

namespace FirstPerson.StateMachines;

// A declarative transition: where to go, an optional guard that decides when it's
// eligible, and an optional effect that fires as the edge is taken.
public class Transition
{
    // Set when the transition was declared by name; resolved to ToState at startup.
    public string ToStateName { get; }

    // The resolved target. Set directly, or filled in by the StateMachine -- at startup for
    // transitions declared by then, lazily for ones added later.
    public State ToState { get; internal set; }

    // Set once the machine has reported an unresolvable target, so it isn't logged every frame.
    internal bool ResolutionReported;

    private readonly Func<bool> _guard;
    private readonly Action _onTransition;

    public Transition(string toStateName, Func<bool> guard = null, Action onTransition = null)
    {
        ToStateName = toStateName;
        _guard = guard;
        _onTransition = onTransition;
    }

    public Transition(State toState, Func<bool> guard = null, Action onTransition = null)
    {
        ToState = toState;
        ToStateName = toState?.Name;
        _guard = guard;
        _onTransition = onTransition;
    }

    // True when this transition is eligible to be taken. A null guard is always eligible.
    // Guards must be side-effect free: they are polled every frame while the source is active.
    public bool GuardPasses() => _guard is null || _guard();

    // Effect fired as the transition is taken, after the source exits and before the target
    // enters (SCXML order). Override for class-based transitions, or pass an action to the ctor.
    public virtual void OnTransition() => _onTransition?.Invoke();
}

using System;

namespace FirstPerson.StateMachines;

public class Transition
{
    public string ToStateName { get; }

    // Resolved from ToStateName by the StateMachine when not set directly.
    public State ToState { get; internal set; }

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

    // Polled every frame while the source is active; keep guards side-effect free.
    public bool GuardPasses() => _guard is null || _guard();

    // Runs after the source exits, before the target enters.
    public virtual void OnTransition() => _onTransition?.Invoke();
}

using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace FirstPerson.CustomTypes.StateMachine;

public class ChangeStateEventArgs(string stateName) : EventArgs
{
    public string StateName { get; set; } = stateName;
}

[GlobalClass]
public partial class StateMachine : Node
{
    [Export] public State RootState { get; set; }
    public List<State> States { get; private set; } = [];

    // Safety cap: if more than this many transitions resolve in a single frame we assume a
    // transition loop (e.g. two states that keep targeting each other) and bail out.
    private const int MaxTransitionsPerFrame = 64;

    // Path relative to the root ("Grounded/Idle"), plus a bare-name index that only resolves when
    // the name is unambiguous. Godot only enforces name uniqueness among siblings.
    private readonly Dictionary<string, State> _statesByPath = [];
    private readonly Dictionary<string, State> _statesByName = [];
    private readonly HashSet<string> _ambiguousNames = [];

    private readonly Queue<PendingTransition> _pendingTransitions = new();

    private readonly struct PendingTransition(State source, State target, Transition transition = null)
    {
        public State Source { get; } = source;
        public State Target { get; } = target;
        public Transition Transition { get; } = transition;
    }

    public override void _Ready()
    {
        base._Ready();

        // Mirrors CompoundState.DefaultState: fall back to the first State child, so a machine
        // built in a scene needs no node-path export to wire itself up.
        RootState ??= GetChildren().OfType<State>().FirstOrDefault();

        if (RootState is null)
        {
            throw new Exception("No Root state present as a child of state machine");
        }

        States = RootState.GetAllStates();
        BuildLookup();
        ResolveTransitions();
        SubscribeToStates();
        EnterInitialConfiguration();
    }

    public override void _ExitTree()
    {
        base._ExitTree();
        States.ForEach(s => s.StateChangeRequired -= HandleChangeStateEvent);
    }

    private void BuildLookup()
    {
        foreach (var state in States)
        {
            var path = state == RootState ? state.Name.ToString() : RootState.GetPathTo(state).ToString();
            if (!_statesByPath.TryAdd(path, state))
            {
                GD.PushError($"StateMachine: duplicate state path '{path}'.");
            }

            var name = state.Name.ToString();
            if (!_statesByName.TryAdd(name, state))
            {
                _ambiguousNames.Add(name);
            }
        }
    }

    private void ResolveTransitions()
    {
        foreach (var state in States)
        {
            foreach (var transition in state.Transitions)
            {
                ResolveTarget(state, transition);
            }
        }
    }

    // Resolves lazily as well as at startup: transitions wired after the machine is ready would
    // otherwise stay dead forever. Reports an unresolvable target once, not once per frame.
    private State ResolveTarget(State source, Transition transition)
    {
        if (transition.ToState is not null) return transition.ToState;

        if (TryResolve(transition.ToStateName, out var target))
        {
            transition.ToState = target;
            return target;
        }

        if (!transition.ResolutionReported)
        {
            transition.ResolutionReported = true;
            GD.PushError($"StateMachine: transition on '{source.Name}' targets " +
                         $"'{transition.ToStateName}', which does not resolve to a state " +
                         "in this machine (missing, or an ambiguous bare name).");
        }

        return null;
    }

    private bool TryResolve(string nameOrPath, out State state)
    {
        state = null;
        if (string.IsNullOrEmpty(nameOrPath)) return false;
        if (_statesByPath.TryGetValue(nameOrPath, out state)) return true;
        if (_ambiguousNames.Contains(nameOrPath)) return false;
        return _statesByName.TryGetValue(nameOrPath, out state);
    }

    private void SubscribeToStates()
    {
        States.ForEach(s => s.StateChangeRequired += HandleChangeStateEvent);
    }

    // Imperative path: a state raised OnStateChangeRequired. We only enqueue here; the change is
    // applied from the tick. This is what keeps a transition raised inside StateEntered from
    // re-entering the machine mid-transition.
    public void HandleChangeStateEvent(object sender, ChangeStateEventArgs args)
    {
        if (sender is not State source) return;

        if (!TryResolve(args.StateName, out var target))
        {
            GD.PushError($"StateMachine: '{source.Name}' requested state '{args.StateName}', " +
                         "which does not resolve to a state in this machine.");
            return;
        }

        Enqueue(new PendingTransition(source, target));
    }

    public override void _Process(double delta) => Tick(delta, physics: false);

    public override void _PhysicsProcess(double delta) => Tick(delta, physics: true);

    // Transitions resolve on BOTH ticks. Guards that read physics facts (IsOnFloor, velocity) would
    // otherwise be sampled at render cadence and miss inputs.
    private void Tick(double delta, bool physics)
    {
        EvaluateTransitions();
        ProcessPendingTransitions();

        foreach (var state in ActiveConfiguration())
        {
            if (physics) state.StatePhysicsProcessing(delta);
            else state.StateProcessing(delta);
        }
    }

    // ---- transition selection -------------------------------------------------------------

    // Walk up from every active leaf, deepest source first, so a child's transition preempts an
    // ancestor's. One transition per source per microstep.
    private void EvaluateTransitions()
    {
        foreach (var leaf in ActiveLeaves())
        {
            for (State source = leaf; source is not null; source = source.GetParent() as State)
            {
                var transition = source.GetEligibleTransition();
                if (transition is null) continue;

                var target = ResolveTarget(source, transition);
                if (target is null) continue;

                Enqueue(new PendingTransition(source, target, transition));
                break;
            }
        }
    }

    private void Enqueue(PendingTransition pending)
    {
        // Conflict rule: at most one transition per source state in flight. Deduping on the target
        // instead would silently discard another source's transition effect.
        foreach (var queued in _pendingTransitions)
        {
            if (queued.Source == pending.Source) return;
        }

        _pendingTransitions.Enqueue(pending);
    }

    // Drain the queue fully each tick, so chains of transient states resolve within one frame
    // instead of one hop per frame.
    private void ProcessPendingTransitions()
    {
        var processed = 0;
        while (_pendingTransitions.Count > 0)
        {
            if (++processed > MaxTransitionsPerFrame)
            {
                GD.PushError($"StateMachine exceeded {MaxTransitionsPerFrame} transitions in a single " +
                             "frame - likely a transition loop. Clearing pending transitions.");
                _pendingTransitions.Clear();
                return;
            }

            var pending = _pendingTransitions.Dequeue();

            // The source may have been exited by an earlier transition in this same drain.
            if (!pending.Source.Enabled) continue;

            ApplyTransition(pending.Source, pending.Target, pending.Transition);

            // Catch on-enter transitions of the states we just activated so they chain this tick.
            EvaluateTransitions();
        }
    }

    // ---- the core algorithm ---------------------------------------------------------------

    // Exit innermost-first, run the transition effect, then enter outermost-first (SCXML order).
    private void ApplyTransition(State source, State target, Transition transition)
    {
        var domain = LeastCommonAncestor(source, target);

        // A null domain means source and target share no proper ancestor (e.g. targeting the root),
        // so the entire configuration leaves, root included.
        List<State> exiting = [];
        if (domain is null) exiting.AddRange(ActiveConfiguration());
        else CollectActiveDescendants(domain, exiting);
        exiting.Reverse();
        foreach (var state in exiting) state.StateExited();

        transition?.OnTransition();

        List<State> entering = [];
        HashSet<State> onPath = [target, .. target.GetAncestorChain()];
        CollectEntry(domain is null ? RootState : ChildOnPath(domain, onPath), onPath, target, entering);

        foreach (var state in entering)
        {
            if (state.GetParent() is CompoundState parent) parent.ActiveState = state;
            state.StateEntered();
        }
    }

    // The transition domain: the deepest state that is a PROPER ancestor of both source and target.
    // Using proper ancestors is what makes an external self-transition exit and re-enter its state.
    private static State LeastCommonAncestor(State source, State target)
    {
        var a = source.GetAncestorChain();
        var b = target.GetAncestorChain();

        State lca = null;
        for (var i = 0; i < Math.Min(a.Count, b.Count) && a[i] == b[i]; i++)
        {
            lca = a[i];
        }

        return lca;
    }

    private static State ChildOnPath(State domain, HashSet<State> onPath)
    {
        return domain switch
        {
            CompoundState c => c.ChildrenStates.First(onPath.Contains),
            ParallelState p => p.ChildrenStates.First(onPath.Contains),
            _ => throw new Exception($"State {domain.Name} has children on a transition path but is " +
                                     "neither compound nor parallel"),
        };
    }

    // Active descendants of state, outermost first. Excludes state itself -- the domain does not exit.
    private static void CollectActiveDescendants(State state, List<State> outList)
    {
        switch (state)
        {
            case null:
                // No common ancestor: the whole machine is leaving, root included.
                return;
            case CompoundState c when c.ActiveState is { Enabled: true }:
                outList.Add(c.ActiveState);
                CollectActiveDescendants(c.ActiveState, outList);
                break;
            case ParallelState p:
                foreach (var child in p.ChildrenStates.Where(child => child.Enabled))
                {
                    outList.Add(child);
                    CollectActiveDescendants(child, outList);
                }

                break;
        }
    }

    // Entry set from state down to target, then default entry below it. Parallel siblings that are
    // not on the path get their own default entry.
    private static void CollectEntry(State state, HashSet<State> onPath, State target, List<State> outList)
    {
        outList.Add(state);

        if (state == target)
        {
            CollectDefaultEntry(state, outList);
            return;
        }

        switch (state)
        {
            case CompoundState c:
                CollectEntry(c.ChildrenStates.First(onPath.Contains), onPath, target, outList);
                break;
            case ParallelState p:
                foreach (var child in p.ChildrenStates)
                {
                    if (onPath.Contains(child)) CollectEntry(child, onPath, target, outList);
                    else
                    {
                        outList.Add(child);
                        CollectDefaultEntry(child, outList);
                    }
                }

                break;
        }
    }

    // Default entry: a compound resolves to its DefaultState, a parallel enters every region.
    private static void CollectDefaultEntry(State state, List<State> outList)
    {
        switch (state)
        {
            case CompoundState c:
                outList.Add(c.DefaultState);
                CollectDefaultEntry(c.DefaultState, outList);
                break;
            case ParallelState p:
                foreach (var child in p.ChildrenStates)
                {
                    outList.Add(child);
                    CollectDefaultEntry(child, outList);
                }

                break;
        }
    }

    private void EnterInitialConfiguration()
    {
        List<State> entering = [RootState];
        CollectDefaultEntry(RootState, entering);

        foreach (var state in entering)
        {
            if (state.GetParent() is CompoundState parent) parent.ActiveState = state;
            state.StateEntered();
        }
    }

    // ---- queries --------------------------------------------------------------------------

    // The active states, outermost first. Materialised because transitions mutate the tree.
    public List<State> ActiveConfiguration()
    {
        if (RootState is null || !RootState.Enabled) return [];

        List<State> active = [RootState];
        CollectActiveDescendants(RootState, active);
        return active;
    }

    public List<AtomicState> ActiveLeaves()
    {
        return ActiveConfiguration().OfType<AtomicState>().ToList();
    }

    public string GetStateMachineString()
    {
        return RootState.GetFullStateString();
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace FirstPerson.CustomTypes.StateMachine;

// Run headless:  godot --headless --path . res://Tests/test_state_machine.tscn
// Exits 0 on pass, 1 on failure. Covers the checklist in REFACTOR_PLAN.md section 7.
public partial class StateMachineTests : Node
{
    public static readonly List<string> Log = [];

    // Off by default so the per-tick processing calls don't pollute the enter/exit order assertions.
    public static bool LogProcessing;

    private readonly List<string> _failures = [];
    private string _test = "";

    public override void _Ready()
    {
        Run(nameof(DeepTransitionExitsInnermostFirstAndEntersOutermostFirst),
            DeepTransitionExitsInnermostFirstAndEntersOutermostFirst);
        Run(nameof(StaleSiblingIsNotEntered), StaleSiblingIsNotEntered);
        Run(nameof(DefaultChildGetsStateEntered), DefaultChildGetsStateEntered);
        Run(nameof(EffectRunsBetweenExitAndEntry), EffectRunsBetweenExitAndEntry);
        Run(nameof(DuplicateLeafNamesResolveByPath), DuplicateLeafNamesResolveByPath);
        Run(nameof(ParentTransitionFiresAndChildPreemptsIt), ParentTransitionFiresAndChildPreemptsIt);
        Run(nameof(EnteringParallelEntersEveryRegion), EnteringParallelEntersEveryRegion);
        Run(nameof(SelfTransitionReExits), SelfTransitionReExits);
        Run(nameof(RunawayGuardTripsTheCap), RunawayGuardTripsTheCap);
        Run(nameof(PhysicsTickReachesActiveStates), PhysicsTickReachesActiveStates);

        if (_failures.Count == 0) GD.Print($"state machine tests: all passed");
        else foreach (var f in _failures) GD.PrintErr(f);

        GetTree().Quit(_failures.Count == 0 ? 0 : 1);
    }

    // ---- tests ----------------------------------------------------------------------------

    // 1 + 2: exit runs C,B,A (innermost first, including descendants of the abandoned branch);
    // entry runs X,Y,Z (outermost first).
    private void DeepTransitionExitsInnermostFirstAndEntersOutermostFirst()
    {
        var c = Leaf("C");
        var z = Leaf("Z");
        var a = Compound("A", Compound("B", c));
        var x = Compound("X", Compound("Y", z));
        var sm = Machine(Compound("Root", a, x));

        c.AddTransition(z, Once());
        Log.Clear();
        sm._Process(0.0);

        AreEqual("-C,-B,-A,+X,+Y,+Z", string.Join(",", Log));
    }

    // 3: transitioning into A from outside must not enter A's previously-active child.
    private void StaleSiblingIsNotEntered()
    {
        var b = Leaf("B");
        var b2 = Leaf("B2");
        var far = Leaf("Far");
        var a = Compound("A", b, b2);
        var sm = Machine(Compound("Root", a, far));

        b.AddTransition(far, Once());
        sm._Process(0.0);

        Log.Clear();
        far.AddTransition(b2, Once());
        sm._Process(0.0);

        AreEqual("-Far,+A,+B2", string.Join(",", Log));
    }

    // 4 + 9 + 17: the initial configuration fires entry hooks, and transitioning to a compound
    // state enters its default child.
    private void DefaultChildGetsStateEntered()
    {
        var b = Leaf("B");
        var far = Leaf("Far");
        var a = Compound("A", b);

        Log.Clear();
        var sm = Machine(Compound("Root", a, far));
        AreEqual("+Root,+A,+B", string.Join(",", Log));

        b.AddTransition(far, Once());
        sm._Process(0.0);

        Log.Clear();
        far.AddTransition(a, Once()); // target the compound, not the leaf
        sm._Process(0.0);
        AreEqual("-Far,+A,+B", string.Join(",", Log));
    }

    // 5: the effect sees the source already exited and the target not yet entered.
    private void EffectRunsBetweenExitAndEntry()
    {
        var from = Leaf("From");
        var to = Leaf("To");
        var sm = Machine(Compound("Root", from, to));

        bool? sourceEnabled = null;
        bool? targetEnabled = null;
        from.AddTransition(to, Once(), () =>
        {
            sourceEnabled = from.Enabled;
            targetEnabled = to.Enabled;
            Log.Add("effect");
        });

        Log.Clear();
        sm._Process(0.0);

        IsTrue(sourceEnabled == false, "source should already be exited");
        IsTrue(targetEnabled == false, "target should not be entered yet");
        AreEqual("-From,effect,+To", string.Join(",", Log));
    }

    // 6: two leaves named Idle under different parents. Bare names are ambiguous; paths resolve.
    private void DuplicateLeafNamesResolveByPath()
    {
        var idle1 = Leaf("Idle");
        var idle2 = Leaf("Idle");
        var p1 = Compound("P1", idle1);
        var p2 = Compound("P2", idle2);
        var sm = Machine(Compound("Root", p1, p2));

        idle1.AddTransition("P2/Idle", Once());
        sm._Process(0.0);

        IsTrue(idle2.Enabled, "P2/Idle should be active");
        IsTrue(!idle1.Enabled, "P1/Idle should have exited");
    }

    // 7: a transition on a compound state fires while a descendant is active, but the descendant's
    // own transition preempts it.
    private void ParentTransitionFiresAndChildPreemptsIt()
    {
        var b = Leaf("B");
        var t1 = Leaf("T1");
        var t2 = Leaf("T2");
        var a = Compound("A", b);
        var sm = Machine(Compound("Root", a, t1, t2));

        var childGuardOpen = true;
        a.AddTransition(t1, () => true);
        b.AddTransition(t2, () => childGuardOpen);

        sm._Process(0.0);
        IsTrue(t2.Enabled, "deepest source should win");

        // Back into A, this time with the child's guard shut.
        childGuardOpen = false;
        t2.AddTransition(a, Once());
        sm._Process(0.0);
        IsTrue(t1.Enabled, "parent transition should fire when the child's guard is shut");
    }

    // 8: entering one region of a parallel state default-enters the others.
    private void EnteringParallelEntersEveryRegion()
    {
        var r1A = Leaf("R1a");
        var r2A = Leaf("R2a");
        var r2B = Leaf("R2b");
        var start = Leaf("Start");
        var parallel = Parallel("P", Compound("R1", r1A), Compound("R2", r2A, r2B));
        var sm = Machine(Compound("Root", start, parallel));

        start.AddTransition(r2B, Once()); // aim at one region's non-default leaf
        sm._Process(0.0);

        IsTrue(r2B.Enabled, "targeted leaf should be active");
        IsTrue(r1A.Enabled, "the other region should have default-entered");
        IsTrue(!r2A.Enabled, "the targeted region should not use its default");
    }

    // 11: an external self-transition exits and re-enters.
    private void SelfTransitionReExits()
    {
        var s = Leaf("S");
        var sm = Machine(Compound("Root", s));

        s.AddTransition(s, Once());
        Log.Clear();
        sm._Process(0.0);

        AreEqual("-S,+S", string.Join(",", Log));
    }

    // 10: a permanently-true guard must terminate the frame, not hang.
    private void RunawayGuardTripsTheCap()
    {
        var a = Leaf("A");
        var b = Leaf("B");
        var sm = Machine(Compound("Root", a, b));

        a.AddTransition(b);
        b.AddTransition(a);

        sm._Process(0.0); // logs an error and clears the queue; must return
        IsTrue(a.Enabled ^ b.Enabled, "exactly one leaf should be active after the cap trips");
    }

    // 12: physics processing reaches the active configuration.
    private void PhysicsTickReachesActiveStates()
    {
        var leaf = Leaf("Leaf");
        var sm = Machine(Compound("Root", leaf));

        Log.Clear();
        LogProcessing = true;
        sm._PhysicsProcess(0.016);
        LogProcessing = false;

        IsTrue(Log.Contains("physics:Leaf"), "active leaf should get StatePhysicsProcessing");
        IsTrue(!Log.Contains("process:Leaf"), "physics tick should not fire StateProcessing");
    }

    // ---- harness --------------------------------------------------------------------------

    private void Run(string name, Action test)
    {
        _test = name;
        try
        {
            test();
        }
        catch (Exception e)
        {
            _failures.Add($"{name}: threw {e.GetType().Name}: {e.Message}");
        }

        // Tear the machine down, or it keeps ticking (and re-reporting) until the tree quits.
        foreach (var child in GetChildren())
        {
            RemoveChild(child);
            child.Free();
        }
    }

    private void AreEqual(string expected, string actual)
    {
        if (expected != actual) _failures.Add($"{_test}: expected [{expected}] but got [{actual}]");
    }

    private void IsTrue(bool condition, string because)
    {
        if (!condition) _failures.Add($"{_test}: {because}");
    }

    // A guard that passes exactly once, so a test tick resolves a single transition.
    private static Func<bool> Once()
    {
        var fired = false;
        return () =>
        {
            if (fired) return false;
            fired = true;
            return true;
        };
    }

    // ---- tree building --------------------------------------------------------------------

    private static TrackedAtomic Leaf(string name) => new() { Name = name };

    private static TrackedCompound Compound(string name, params State[] children)
    {
        var c = new TrackedCompound { Name = name };
        foreach (var child in children) c.AddChild(child);
        return c;
    }

    private static TrackedParallel Parallel(string name, params State[] children)
    {
        var p = new TrackedParallel { Name = name };
        foreach (var child in children) p.AddChild(child);
        return p;
    }

    // Assembles the machine and adds it to the tree, which fires _Ready bottom-up.
    private StateMachine Machine(State root)
    {
        var sm = new StateMachine { Name = $"SM{GetChildCount()}", RootState = root };
        sm.AddChild(root);
        AddChild(sm);
        return sm;
    }
}

public partial class TrackedAtomic : AtomicState
{
    public override void StateEntered() { base.StateEntered(); StateMachineTests.Log.Add($"+{Name}"); }
    public override void StateExited() { base.StateExited(); StateMachineTests.Log.Add($"-{Name}"); }
    public override void StateProcessing(double d)
    {
        if (StateMachineTests.LogProcessing) StateMachineTests.Log.Add($"process:{Name}");
    }

    public override void StatePhysicsProcessing(double d)
    {
        if (StateMachineTests.LogProcessing) StateMachineTests.Log.Add($"physics:{Name}");
    }
}

public partial class TrackedCompound : CompoundState
{
    public override void StateEntered() { base.StateEntered(); StateMachineTests.Log.Add($"+{Name}"); }
    public override void StateExited() { base.StateExited(); StateMachineTests.Log.Add($"-{Name}"); }
}

public partial class TrackedParallel : ParallelState
{
    public override void StateEntered() { base.StateEntered(); StateMachineTests.Log.Add($"+{Name}"); }
    public override void StateExited() { base.StateExited(); StateMachineTests.Log.Add($"-{Name}"); }
}

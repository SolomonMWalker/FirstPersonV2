using System.Collections.Generic;
using Godot;

namespace FirstPerson.Tests;

// Run headless:  godot --headless --path . res://Tests/test_stagger.tscn
// Exits 0 on pass, 1 on failure. Same fixed-delta-driven-by-hand approach as ShieldComponentTests.
public partial class StaggerComponentTests : Node
{
    private const float Dt = 0.1f;

    private readonly List<string> _failures = [];
    private HealthComponent _health;
    private StaggerComponent _stagger;

    public override void _Ready()
    {
        var gameObject = new Node3D { Name = "Dummy" };
        var container = new Node3D { Name = "Components" };
        _health = new HealthComponent { Name = "HealthComponent", Max = 100f };
        _stagger = new StaggerComponent
        {
            Name = "StaggerComponent",
            Threshold = 30f,
            Duration = 2f,
            DamageMultiplier = 2f,
            RefillDelay = 6f,
            RefillRate = 0.5f,
        };
        container.AddChild(_health);
        container.AddChild(_stagger);
        gameObject.AddChild(container);
        AddChild(gameObject);

        var staggers = 0;
        var recoveries = 0;
        _stagger.Staggered += () => staggers++;
        _stagger.Recovered += () => recoveries++;

        // 1. Chip stagger under the threshold does nothing yet.
        _stagger.TakeStagger(20f);
        Near(_stagger.Current, 20f, 0f, "stagger did not accumulate");
        True(!_stagger.IsStaggered, "staggered before reaching Threshold");

        // 2. The hit that crosses Threshold trips it and resets the meter.
        _stagger.TakeStagger(20f);
        True(_stagger.IsStaggered, "did not enter Staggered at/over Threshold");
        True(staggers == 1, $"expected 1 Staggered signal, got {staggers}");
        Near(_stagger.Current, 0f, 0f, "meter did not reset on triggering");

        // 3. More stagger damage while already staggered is wasted, not stacked.
        _stagger.TakeStagger(999f);
        Near(_stagger.Current, 0f, 0f, "stagger damage accumulated while already staggered");
        True(staggers == 1, $"Staggered fired again while already staggered ({staggers})");

        // 4. Recovers after Duration, not a tick before.
        Step(1.9f);
        True(_stagger.IsStaggered, "recovered before Duration elapsed");
        Step(0.2f);
        True(!_stagger.IsStaggered, "still staggered after Duration elapsed");
        True(recoveries == 1, $"expected 1 Recovered signal, got {recoveries}");

        // 5. Refill: quiet for RefillDelay, then climbs back down at Threshold * RefillRate per
        //    second, and a hit during the wait restarts the delay instead of layering onto a decay
        //    that never started.
        _stagger.TakeStagger(20f);
        Near(_stagger.Current, 20f, 0f, "stagger did not accumulate after recovering");
        Step(5.9f);
        Near(_stagger.Current, 20f, 0f, "meter decayed before RefillDelay elapsed");

        _stagger.TakeStagger(5f);
        Near(_stagger.Current, 25f, 0f, "hit during the wait did not add to the meter");
        Step(5.9f);
        Near(_stagger.Current, 25f, 0f, "a hit during the wait did not restart RefillDelay");

        // RefillRate is a fraction of Threshold per second, so 30 * 0.5 = 15/s -- three seconds of
        // that clears the whole 25 and clamps at zero rather than going negative.
        Step(3f);
        Near(_stagger.Current, 0f, 0f, "meter did not decay back to zero at Threshold * RefillRate");
        True(!_stagger.IsStaggered, "decaying meter crossed into Staggered on its own");
        True(staggers == 1, $"decay re-triggered Staggered ({staggers})");

        // 6. Can be triggered again after recovering.
        _stagger.TakeStagger(30f);
        True(_stagger.IsStaggered, "did not re-trigger after recovering");
        True(staggers == 2, $"expected 2 Staggered signals, got {staggers}");

        // 7. Corpses don't stagger.
        Step(2.1f);
        _health.TakeDamage(9999f);
        _stagger.TakeStagger(999f);
        True(!_stagger.IsStaggered, "a dead object entered Staggered");

        if (_failures.Count == 0) GD.Print("stagger component tests: all passed");
        else foreach (var f in _failures) GD.PrintErr(f);
        GetTree().Quit(_failures.Count == 0 ? 0 : 1);
    }

    private void Step(float seconds)
    {
        for (var i = 0; i < Mathf.RoundToInt(seconds / Dt); i++) _stagger._PhysicsProcess(Dt);
    }

    private void True(bool ok, string what)
    {
        if (!ok) _failures.Add(what);
    }

    private void Near(float actual, float expected, float tolerance, string what)
    {
        if (Mathf.Abs(actual - expected) > Mathf.Max(tolerance, 1e-4f))
            _failures.Add($"{what}: expected {expected}, got {actual}");
    }
}

using System.Collections.Generic;
using Godot;

namespace FirstPerson.Tests;

// Run headless:  godot --headless --path . res://Tests/test_shield.tscn
// Exits 0 on pass, 1 on failure.
//
// Time is driven by calling _PhysicsProcess directly with a fixed delta rather than by waiting real
// frames. Nothing here needs the engine's tick -- the shield only reads its own delta -- and the
// alternative is the frame-counting arithmetic CoyoteTests.cs opens by warning about. Everything
// runs inside _Ready, so no engine tick can interleave and add time behind the assertions' back.
//
// Rate assertions carry a one-tick tolerance, because where the cooldown lands inside a tick is not
// worth pinning down and would make these fail on a delta change alone.
public partial class ShieldComponentTests : Node
{
    private const float Dt = 0.1f;

    private readonly List<string> _failures = [];
    private HealthComponent _health;
    private ShieldComponent _shield;

    public override void _Ready()
    {
        // Built the way a scene builds it: object -> Components -> the components.
        var gameObject = new Node3D { Name = "Dummy" };
        var container = new Node3D { Name = "Components" };
        _health = new HealthComponent { Name = "HealthComponent", Max = 100f };
        // Round numbers, not the shipping defaults, so the expected values below are readable.
        _shield = new ShieldComponent
        {
            Name = "ShieldComponent",
            Max = 50f,
            RechargeDelay = 3f,
            BrokenRechargeDelay = 6f,
            RechargeRate = 10f,
            BrokenRechargeRate = 25f,
        };
        container.AddChild(_health);
        container.AddChild(_shield);
        gameObject.AddChild(container);
        AddChild(gameObject);   // _Ready fires here, and the shield installs itself

        var shieldHits = 0;
        var healthHits = 0;
        var breaks = 0;
        var recharges = 0;
        _shield.Damaged += (_, _) => shieldHits++;
        _shield.Broken += () => breaks++;
        _shield.Recharged += () => recharges++;
        _health.Damaged += (_, _) => healthHits++;

        // 1. Installed itself into the health it shares an object with.
        True(_health.AbsorbDamage is not null, "shield did not install itself into Health.AbsorbDamage");
        Near(_shield.Current, 50f, 0f, "shield does not start full");

        // 2. A hit inside capacity is taken entirely by the shield.
        _health.TakeDamage(20f, Vector3.Right);
        Near(_shield.Current, 30f, 0f, "20 damage to a 50 shield");
        Near(_health.Current, 100f, 0f, "health took damage through an intact shield");
        True(shieldHits == 1, $"expected 1 shield Damaged signal, got {shieldHits}");
        True(healthHits == 0, $"health emitted Damaged through an intact shield ({healthHits})");

        // 3. No bleed-through: the hit that pops the shield is absorbed whole, however big it was.
        _health.TakeDamage(999f, Vector3.Right);
        Near(_shield.Current, 0f, 0f, "999 damage to a 30 shield");
        Near(_health.Current, 100f, 0f, "the breaking hit bled through to health");
        True(breaks == 1, $"expected 1 Broken signal, got {breaks}");
        True(!_shield.Up, "shield reads as up at 0");

        // 4. The next hit is the one that hurts.
        _health.TakeDamage(10f, Vector3.Right);
        Near(_health.Current, 90f, 0f, "damage through a broken shield");
        True(breaks == 1, $"Broken fired again on an already-broken shield ({breaks})");
        True(healthHits == 1, $"expected 1 health Damaged signal, got {healthHits}");

        // 5. Broken shields wait out the long delay -- and that last hit restarted it.
        Step(5.5f);
        Near(_shield.Current, 0f, 0f, "shield recharged before BrokenRechargeDelay elapsed");

        // 6. Then refill at the broken rate: 0.5s of leftover cooldown, then ~0.5s x 25/s.
        Step(1f);
        Near(_shield.Current, 12.5f, 2.5f, "recharge rate after a break");

        // 7. A hit part-way through a refill drops it again and restarts the wait. Still up
        //    afterwards, so this takes the short delay -- 2.5s of it is not enough to recharge.
        var before = _shield.Current;
        _health.TakeDamage(5f, Vector3.Right);
        Near(_shield.Current, before - 5f, 0f, "chip hit during a recharge");
        Step(2.5f);
        Near(_shield.Current, before - 5f, 0f, "recharge resumed without waiting out the reset timer");

        // 8. The broken latch survives the whole climb: still the fast rate, all the way to full.
        Step(4f);
        Near(_shield.Current, 50f, 0f, "shield did not reach full at the broken rate");
        True(recharges == 1, $"expected 1 Recharged signal, got {recharges}");

        // 9. Full again clears the latch, so the next chip uses the slow rate: 3s of delay, then
        //    ~1s x 10/s. At the broken rate this would have clamped back to full and re-fired
        //    Recharged, so the ceiling check is the real assertion here.
        _health.TakeDamage(20f, Vector3.Right);
        Step(4f);
        True(_shield.Current < 50f, "a chip hit recharged at the broken rate; the latch did not clear");
        Near(_shield.Current, 40f, 1f, "recharge rate on an unbroken shield");
        True(recharges == 1, $"Recharged fired again without reaching full ({recharges})");

        // 10. Corpses do not regenerate. First hit pops the shield, second one kills.
        _health.TakeDamage(999f, Vector3.Right);
        _health.TakeDamage(999f, Vector3.Right);
        True(!_health.Alive, "health survived two 999 hits");
        Step(30f);
        Near(_shield.Current, 0f, 0f, "a dead object recharged its shield");

        if (_failures.Count == 0) GD.Print("shield component tests: all passed");
        else foreach (var f in _failures) GD.PrintErr(f);
        GetTree().Quit(_failures.Count == 0 ? 0 : 1);
    }

    // Whole ticks, counted as an int: summing a float clock would drift against the shield's own
    // accumulation and make the tolerances lie.
    private void Step(float seconds)
    {
        for (var i = 0; i < Mathf.RoundToInt(seconds / Dt); i++) _shield._PhysicsProcess(Dt);
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

using System.Collections.Generic;
using Godot;

namespace FirstPerson;

// Run headless:  godot --headless --path . res://test_health.tscn
// Exits 0 on pass, 1 on failure.
public partial class HealthComponentTests : Node
{
    public override void _Ready()
    {
        var failures = new List<string>();

        // A GameObject the way a scene builds one: object -> Components -> the component.
        var gameObject = new Node3D { Name = "Dummy" };
        var container = new Node3D { Name = "Components" };
        var health = new HealthComponent { Name = "HealthComponent", Max = 50f };
        container.AddChild(health);
        gameObject.AddChild(container);
        AddChild(gameObject);   // _Ready fires here

        if (health.GameObject != gameObject) failures.Add("GameObject did not resolve to the owner");
        if (Component.Get<HealthComponent>(gameObject) != health) failures.Add("Get<HealthComponent> did not find the component");
        if (Component.Get<HealthComponent>(this) is not null) failures.Add("Get found a component on an object with no Components node");
        if (!Mathf.IsEqualApprox(health.Current, 50f)) failures.Add($"expected Current=50 at spawn, got {health.Current}");

        var damaged = 0;
        var died = 0;
        var lastFrom = Vector3.Zero;
        health.Damaged += (_, from) => { damaged++; lastFrom = from; };
        health.Died += () => died++;

        health.TakeDamage(20f, new Vector3(1f, 0f, 0f));
        if (!Mathf.IsEqualApprox(health.Current, 30f)) failures.Add($"expected Current=30 after 20 damage, got {health.Current}");
        if (damaged != 1) failures.Add($"expected 1 Damaged signal, got {damaged}");
        if (lastFrom != new Vector3(1f, 0f, 0f)) failures.Add($"Damaged carried the wrong position: {lastFrom}");
        if (!health.Alive) failures.Add("died at 30/50");

        // Zero and negative are no-ops, not heals.
        health.TakeDamage(0f);
        health.TakeDamage(-10f);
        if (!Mathf.IsEqualApprox(health.Current, 30f)) failures.Add($"non-positive damage changed Current to {health.Current}");
        if (damaged != 1) failures.Add("non-positive damage emitted Damaged");

        // Overkill clamps at zero rather than going negative, and kills exactly once.
        health.TakeDamage(999f);
        if (!Mathf.IsEqualApprox(health.Current, 0f)) failures.Add($"expected Current=0 after overkill, got {health.Current}");
        if (health.Alive) failures.Add("still alive at 0 health");
        if (died != 1) failures.Add($"expected 1 Died signal, got {died}");

        // The corpse absorbs nothing: a second hit on the same frame must not fire Died again.
        health.TakeDamage(999f);
        if (died != 1) failures.Add($"Died fired {died} times; damage after death must be ignored");

        // ...and it does not get back up for a health pack either.
        health.Heal(25f);
        if (!Mathf.IsEqualApprox(health.Current, 0f)) failures.Add($"healing resurrected the dead: {health.Current}");
        if (health.Alive) failures.Add("healing a corpse brought it back to life");

        // Healing on a live one: tops up, clamps at Max, and ignores non-positive amounts rather
        // than quietly running TakeDamage backwards.
        var live = new HealthComponent { Name = "Live", Max = 50f };
        container.AddChild(live);
        live.TakeDamage(30f);
        live.Heal(10f);
        if (!Mathf.IsEqualApprox(live.Current, 30f)) failures.Add($"expected Current=30 after healing 10, got {live.Current}");
        live.Heal(0f);
        live.Heal(-10f);
        if (!Mathf.IsEqualApprox(live.Current, 30f)) failures.Add($"non-positive heal changed Current to {live.Current}");
        live.Heal(999f);
        if (!Mathf.IsEqualApprox(live.Current, 50f)) failures.Add($"overheal did not clamp to Max: {live.Current}");

        if (failures.Count == 0) GD.Print("health component tests: all passed");
        else foreach (var f in failures) GD.PrintErr(f);
        GetTree().Quit(failures.Count == 0 ? 0 : 1);
    }
}

using Godot;

namespace FirstPerson.Tests;

// Run headless:  godot --headless --path . res://Tests/test_grunt_wiring.tscn
// Exits 0 on pass, 1 on failure. Config, not logic, but the kind of config that breaks silently: a
// bone losing its collision_layer override, or Component.Get failing to walk up through the
// skeleton to find Health/Stagger on the object that actually owns them.
public partial class GruntWiringTests : Node
{
    public override void _Ready()
    {
        var failures = 0;

        // EnemyController._Ready looks this up via GetFirstNodeInGroup("player") and pushes an error
        // if it's missing -- present so the rest of _Ready runs exactly like it would in the level.
        var player = new Node3D { Name = "Player" };
        player.AddToGroup("player");
        AddChild(player);

        var scene = GD.Load<PackedScene>("res://Enemy/grunt.tscn").Instantiate<Node3D>();
        AddChild(scene);
        var grunt = (EnemyController)scene;

        var capsule = grunt.GetNode<CollisionShape3D>("CollisionShape3D");
        var head = grunt.GetNode<PhysicalBone3D>("GruntRig/Skeleton3D/PhysicalBoneSimulator3D/Head");

        // The movement-only capsule and a sample bone hitbox land on the two layers HitscanComponent
        // and Projectile rely on to tell them apart (query.CollisionMask = 0b011 in both).
        failures += Check(((CollisionObject3D)grunt).CollisionLayer == 4, "Grunt's capsule is not on the movement-only layer (4)");
        failures += Check(head.CollisionLayer == 2, "Head hitbox is not on the hitbox layer (2)");

        // Component.Get must resolve Health/Stagger from a hit on either collider -- the bone hitbox
        // several levels deep under Skeleton3D/PhysicalBoneSimulator3D, and the capsule itself.
        failures += Check(Component.Get<HealthComponent>(head) is not null, "HealthComponent did not resolve from a bone hitbox");
        failures += Check(Component.Get<StaggerComponent>(head) is not null, "StaggerComponent did not resolve from a bone hitbox");
        failures += Check(Component.Get<HealthComponent>(capsule) is not null, "HealthComponent did not resolve from the capsule");

        // The controller itself: EnemyController._Ready must find its Skeleton-nested mesh (via
        // FlashMesh, not the hardcoded top-level "MeshInstance3D" the placeholder capsule enemy
        // uses), its NavigationAgent3D, its Health, and a GunComponent to drive Attack's Firing flag.
        failures += Check(grunt.Health is not null, "EnemyController did not resolve its HealthComponent");
        failures += Check(grunt.Agent is not null, "EnemyController did not resolve its NavigationAgent3D");
        failures += Check(Component.Get<GunComponent>(grunt) is not null, "GunComponent did not resolve on Grunt");

        // The state machine BrainState hard-requires by name, plus the two states that give this
        // enemy its chase-and-shoot behaviour.
        foreach (var stateName in new[] { "Idle", "Chase", "Attack", "Dead" })
            failures += Check(grunt.GetNodeOrNull($"StateMachine/Brain/{stateName}") is not null,
                $"StateMachine/Brain/{stateName} is missing");

        if (failures == 0) GD.Print("grunt wiring tests: all passed");
        GetTree().Quit(failures == 0 ? 0 : 1);
    }

    private int Check(bool ok, string what)
    {
        if (ok) return 0;
        GD.PrintErr(what);
        return 1;
    }
}

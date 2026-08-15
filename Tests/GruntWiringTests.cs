using Godot;

namespace FirstPerson.Tests;

// Run headless:  godot --headless --path . res://Tests/test_grunt_wiring.tscn
// Exits 0 on pass, 1 on failure. Config, not logic, but the kind of config that breaks silently: a
// bone losing its collision_layer override puts it back on the world's layer, where the movement
// capsule shadows it and no shot ever registers on a limb again.
//
// This covers the collision layers only. It used to also assert the whole gameplay stack --
// EnemyController, Health, Stagger, the NavigationAgent3D, GunComponent, and StateMachine/Brain's
// four states -- but grunt_basic_enemy.tscn is a rebuild that has not been rewired yet: it is the
// imported model, a movement capsule and the 19 bone hitboxes, with no script on it. Those
// assertions come back from git (HEAD:Enemy/grunt.tscn) when the Grunt is wired up again.
public partial class GruntWiringTests : Node
{
    private const string GruntScene = "res://Enemy/Grunt/grunt_basic_enemy.tscn";

    public override void _Ready()
    {
        var failures = 0;

        // Load defensively. The previous version of this test pointed at a scene that had been
        // deleted, and GD.Load returning null meant _Ready threw before ever reaching Quit -- so
        // headless Godot sat there forever instead of failing. A hang is a much worse failure mode
        // than a red exit code, because nothing in CI or a terminal tells you which test it was.
        var packed = GD.Load<PackedScene>(GruntScene);
        if (packed is null)
        {
            GD.PrintErr($"could not load {GruntScene}");
            GetTree().Quit(1);
            return;
        }

        var grunt = packed.Instantiate<CharacterBody3D>();
        AddChild(grunt);

        // The movement capsule and the bone hitboxes must land on different layers, which is what
        // lets HitscanComponent's query mask (Layers.PlayerShot) see the bones and not the capsule.
        failures += Check(grunt.CollisionLayer == Layers.CharacterPhysics,
            $"Grunt's capsule is on layer {grunt.CollisionLayer}, expected CharacterPhysics ({Layers.CharacterPhysics})");
        failures += Check(grunt.CollisionMask == Layers.Movement,
            $"Grunt's capsule masks {grunt.CollisionMask}, expected Movement ({Layers.Movement})");

        var simulator = grunt.GetNodeOrNull("GruntRig/Skeleton3D/PhysicalBoneSimulator3D");
        failures += Check(simulator is not null, "PhysicalBoneSimulator3D is missing");

        var bones = 0;
        foreach (var child in simulator?.GetChildren() ?? [])
        {
            if (child is not PhysicalBone3D bone) continue;
            bones++;
            failures += Check(bone.CollisionLayer == Layers.Enemy,
                $"bone {bone.Name} is on layer {bone.CollisionLayer}, expected Enemy ({Layers.Enemy})");
            // Mask 0 is the half that is easiest to lose and hardest to notice: a hitbox only ever
            // needs to be *found* by a query, never to collide. Leave it at the default 1 and the
            // bones shove themselves around against the floor.
            failures += Check(bone.CollisionMask == 0,
                $"bone {bone.Name} masks {bone.CollisionMask}, expected 0 -- hitboxes must not collide");
        }

        failures += Check(bones == 19, $"found {bones} bone hitboxes, expected 19");

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

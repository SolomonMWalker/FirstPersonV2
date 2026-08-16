using System.Linq;
using Godot;

namespace FirstPerson;

// "This object's skeleton can go limp and fall like a body." Turns the hitbox skeleton built per
// Docs/RAGDOLL_JOINT_GUIDE.md -- a Skeleton3D whose PhysicalBone3D children kinematically track the
// animated pose with collision_mask 0 -- into an actual physics-driven ragdoll.
//
// Two ways in, one place that does the work: HealthComponent.Died (the combat case) and an optional
// interact Switch (for testing, or a scripted death with no HealthComponent involved). Both call
// ActivateRagdoll directly, same shape as EnemyController's Switch -- the specific attaches itself to
// the generic, never the reverse.
[GlobalClass]
public partial class RagdollComponent : Component
{
	// Path to the skeleton carrying the PhysicalBone3D hitboxes, relative to GameObject. Grunt's mesh
	// and bones live under GruntRig/Skeleton3D rather than at the top level -- same reason
	// EnemyController.FlashMesh exists as an override point instead of a hardcoded lookup.
	[Export] public NodePath SkeletonPath = "GruntRig/Skeleton3D";

	// The movement capsule to drop once the ragdoll takes over. Left alone, it and the now-simulated
	// bones occupy the same space and fight -- the same layer-3-vs-layer-4 collision problem the
	// hitboxes already solve, wearing a different symptom.
	[Export] public NodePath CapsulePath = "CollisionShape3D";

	// What the ragdoll bones collide with once active: world geometry only, never CharacterPhysics.
	// The capsule above is disabled, but every *other* live character's capsule is still on that
	// layer, and a corpse that can shove or be shoved by them is exactly the "stuck to its own body"
	// bug the ragdoll guide warns about, one layer over.
	[Export] public uint RagdollMask = Layers.WorldSolid;

	// Optional. Wired the same way EnemyController wires its Switch: a nearby InteractableComponent
	// can trigger the ragdoll directly, for testing or a scripted death with no HealthComponent
	// involved at all.
	[Export] public InteractableComponent Switch;

	private bool _active;

	public override void _Ready()
	{
		base._Ready();

		var health = Get<HealthComponent>(GameObject);
		if (health is not null) health.Died += ActivateRagdoll;

		if (Switch is not null) Switch.Interacted += ActivateRagdoll;
	}

	// Idempotent: a Switch pressed twice, or a Died that arrives after a manual trigger already ran,
	// must not restart a simulation that is already going.
	public void ActivateRagdoll()
	{
		if (_active) return;
		_active = true;

		// Deferred: this can run from inside a physics step (a hitscan's Died firing mid-query), and
		// Godot will not let a body's shape be disabled mid-flush. Same reasoning as DeadState's own
		// capsule drop.
		if (GameObject.GetNodeOrNull(CapsulePath) is CollisionShape3D capsule)
			capsule.SetDeferred(CollisionShape3D.PropertyName.Disabled, true);

		// Stops move_and_slide from fighting the simulated bones for the same space. A no-op today --
		// Grunt carries no movement script yet -- and free once one lands.
		if (GameObject is CharacterBody3D body) body.SetPhysicsProcess(false);

		if (GameObject.GetNodeOrNull("AnimationTree") is AnimationTree tree) tree.Active = false;
		if (GameObject.GetNodeOrNull("AnimationPlayer") is AnimationPlayer player) player.Stop();

		if (GameObject.GetNodeOrNull(SkeletonPath) is not Skeleton3D skeleton) return;
		if (skeleton.GetNodeOrNull<PhysicalBoneSimulator3D>("PhysicalBoneSimulator3D") is not { } simulator) return;

		foreach (var bone in simulator.GetChildren().OfType<PhysicalBone3D>())
			bone.CollisionMask = RagdollMask;

		simulator.PhysicalBonesStartSimulation();
	}
}

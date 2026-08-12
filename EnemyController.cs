using Godot;

// The body half of an enemy: movement, facing, and the handful of facts its brain asks about.
// Deliberately shaped like PlayerController -- it owns the single MoveAndSlide and runs at
// ProcessPhysicsPriority 1 so the states have already written Velocity by the time it moves.
//
// It contains no decisions. Whether to chase, shoot or stand still lives entirely in EnemyStates/,
// driven by the same statechart the player uses; this class only knows how to do the things a state
// asks for. That split is what keeps "add a Patrol state" from meaning "edit the enemy".
public partial class EnemyController : CharacterBody3D
{
	[Export] public float Speed = 3.5f;

	// Wakes at SightRange, gives up at the larger LoseRange. Two numbers rather than one because a
	// single threshold makes the enemy stutter in and out of Chase while you stand exactly on it.
	[Export] public float SightRange = 20f;
	[Export] public float LoseRange = 26f;
	[Export] public float AttackRange = 12f;

	[Export] public float TurnSpeed = 6f;   // radians per second of yaw toward the target

	// Visual-only: no gameplay reads this. A brief unshaded white flash on the body mesh so a hit
	// registers at a glance while tuning damage/fire-rate numbers, without needing a hitmarker or
	// combat log to confirm a shot landed.
	[Export] public float FlashDuration = 0.1f;

	// Switched off, an enemy never wakes however close you get, and one already awake goes back to
	// Idle. Defaults on, so an enemy you just drop into a level behaves; the ones that should start
	// dormant say so in the scene.
	[Export] public bool Active = true;

	// Optional, and pointedly not required to be on this object -- a console across the room is the
	// whole point. Interacting with it toggles Active. The specific behaviour attaches itself to the
	// generic capability, the same direction as everything else in Components/.
	[Export] public InteractableComponent Switch;
	[Export] public string SwitchLabel = "the enemy";

	// The player. By group, so nothing has to be wired per scene and it survives the player being
	// respawned into a different node.
	public Node3D Target { get; private set; }

	public NavigationAgent3D Agent { get; private set; }

	// Its own hit points, if it has any. No HealthComponent means it cannot die, and Alive is then
	// permanently true -- the same "absence of a capability" rule the turrets rely on.
	public HealthComponent Health { get; private set; }
	public bool Alive => Health?.Alive ?? true;

	// Flat, because ranges are a fact about the floor plan. Measuring through Y would make an enemy
	// at the bottom of a ramp think you are further away than one standing next to you.
	public float FlatDistanceToTarget =>
		Target is null ? float.MaxValue : ((Target.GlobalPosition - GlobalPosition) with { Y = 0f }).Length();

	private MeshInstance3D _mesh;
	private StandardMaterial3D _flashMaterial;
	private float _flashTimer;

	// Nearest EnemyController ancestor, for the state nodes hanging several levels below the body.
	// A near-copy of PlayerController.Of on purpose: two copies is not yet a pattern, and hoisting it
	// into a shared generic would mean editing every file in PlayerStates/ to prove a point.
	public static EnemyController Of(Node node)
	{
		for (var parent = node.GetParent(); parent is not null; parent = parent.GetParent())
		{
			if (parent is EnemyController enemy) return enemy;
		}

		GD.PushError($"{node.Name}: no EnemyController ancestor.");
		return null;
	}

	public override void _Ready()
	{
		Agent = GetNode<NavigationAgent3D>("NavigationAgent3D");
		Health = Component.Get<HealthComponent>(this);
		Target = GetTree().GetFirstNodeInGroup("player") as Node3D;
		if (Target is null) GD.PushError($"{Name}: nothing in the \"player\" group; this enemy has no target.");

		if (Switch is not null)
		{
			Switch.Interacted += ToggleActive;
			UpdateVerb();
		}

		// The body flashes itself rather than being wired up by something else, same reasoning as
		// CameraController subscribing to the player's own Damaged: the thing owning the visual is
		// what should own the subscription.
		_mesh = GetNode<MeshInstance3D>("MeshInstance3D");
		if (Health is not null) Health.Damaged += OnDamaged;

		// After the StateMachine child, so MoveAndSlide applies the velocity the states just wrote.
		// Same arrangement, and the same reason, as PlayerController.
		ProcessPhysicsPriority = 1;
	}

	private void OnDamaged(float amount, Vector3 fromPosition)
	{
		_flashTimer = FlashDuration;
		// MaterialOverride, not editing the mesh's own material: that StandardMaterial3D is one
		// shared resource across every enemy instance (and the barrel), same trap as an inline
		// CollisionShape3D -- writing into it would flash every enemy in the level at once.
		_mesh.MaterialOverride ??= _flashMaterial ??= new StandardMaterial3D
		{
			ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
			AlbedoColor = Colors.White,
		};
	}

	private void ToggleActive()
	{
		Active = !Active;
		UpdateVerb();
	}

	// What pressing the key will DO, not what the enemy currently is -- same rule as GunComponent's.
	private void UpdateVerb() => Switch.Verb = Active ? $"turn {SwitchLabel} off" : $"turn {SwitchLabel} on";

	public override void _PhysicsProcess(double delta)
	{
		MoveAndSlide();

		if (_flashTimer > 0f)
		{
			_flashTimer -= (float)delta;
			if (_flashTimer <= 0f) _mesh.MaterialOverride = null;
		}
	}

	// Yaw only. A capsule has no business leaning, and pitching the body would tip the gun with it.
	public void FaceTarget(double delta)
	{
		if (Target is null) return;

		var flat = (Target.GlobalPosition - GlobalPosition) with { Y = 0f };
		if (flat.LengthSquared() < 0.0001f) return;

		// LerpAngle rather than lerping the vector: it takes the short way round, so an enemy you run
		// past spins the way it would actually turn instead of unwinding the long way.
		var wanted = Mathf.Atan2(-flat.X, -flat.Z);   // -Z is forward
		Rotation = Rotation with { Y = Mathf.LerpAngle(Rotation.Y, wanted, (float)delta * TurnSpeed) };
	}

	// The whole navigation integration. The agent owns the path; this only walks toward whichever
	// corner of it comes next, which is what makes the difference between the enemy rounding the
	// ramp and grinding along its side.
	public void MoveAlongPath(double delta)
	{
		if (Target is null || Agent is null)
		{
			Halt(delta);
			return;
		}

		Agent.TargetPosition = Target.GlobalPosition;

		// Arrived, or the path never resolved. Standing still beats sprinting at a stale waypoint.
		if (Agent.IsNavigationFinished())
		{
			Halt(delta);
			return;
		}

		var step = (Agent.GetNextPathPosition() - GlobalPosition) with { Y = 0f };
		var direction = step.Normalized();
		Velocity = new Vector3(direction.X * Speed, Velocity.Y, direction.Z * Speed) + Gravity(delta);
	}

	// Gravity and nothing else. Horizontal velocity is zeroed outright rather than eased: these are
	// hard stops driven by a state change, and a slide-out would look like indecision.
	public void Halt(double delta) => Velocity = new Vector3(0f, Velocity.Y, 0f) + Gravity(delta);

	private Vector3 Gravity(double delta) => IsOnFloor() ? Vector3.Zero : GetGravity() * (float)delta;
}

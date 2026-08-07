using FirstPerson.Helpers;
using Godot;

public partial class PlayerController : CharacterBody3D
{
	[Export] public float Speed = 5.0f;
	[Export] public float JumpVelocity = 4.5f;
	[Export] public float MouseSensitivity = 0.003f;
	[Export] public ClamberController Clamber;

	// Sampled once per physics tick; the states read these rather than polling input themselves.
	public Vector2 MoveInput { get; private set; }
	public bool JumpPressed { get; private set; }
	// Latches, not one-frame edges: guards are polled on _Process too, so a flag that goes false on
	// its own the next physics tick makes edges fire against a stale value. These stay set until a
	// transition consumes them.

	// Shift is down and hasn't started a sprint yet. Consumed on entering Sprinting and re-armed
	// only by releasing shift — otherwise crouching mid-sprint would bounce straight back to sprint.
	public bool SprintArmed { get; set; }

	// Crouch is a toggle; C flips it, and the crouch->sprint edge clears it.
	public bool CrouchToggled { get; set; }

	// Extra camera motion (the crouch dip). Mouse-look stays in this class; see CameraController.
	public CameraController Camera { get; private set; }

	// Where the player is looking, in radians, kept here rather than read back off the camera node.
	// CameraController composes it with the impact punch — if mouse-look accumulated from the node
	// instead, it would read back a punched pitch and integrate every landing into the player's aim.
	public float LookPitch { get; private set; }

	// Fastest downward speed reached during the current fall, in metres per second. Accumulated by
	// InAirState and consumed by GroundedState on landing: MoveAndSlide has already zeroed Velocity.Y
	// by the time the landing is detectable, so the impact speed has to be remembered on the way down.
	public float FallSpeed { get; set; }

	private CollisionShape3D _collider;
	private CapsuleShape3D _capsule;
	private float _standHeight;
	private bool _jumpHeld;
	private bool _sprintHeld;
	private bool _crouchHeld;

	// Nearest PlayerController ancestor. State nodes hang several levels below the body, and
	// hand-written NodePath exports in a .tscn do not reliably resolve into typed node references.
	public static PlayerController Of(Node node)
	{
		for (var parent = node.GetParent(); parent is not null; parent = parent.GetParent())
		{
			if (parent is PlayerController player) return player;
		}

		GD.PushError($"{node.Name}: no PlayerController ancestor.");
		return null;
	}

	public override void _Ready()
	{
		Camera = GetNode<CameraController>("Camera3D");

		_collider = GetNode<CollisionShape3D>("CollisionShape3D");
		// Duplicated: a shape declared inline in a .tscn is shared by every instance of that scene,
		// so resizing it in place would crouch every player at once.
		_capsule = (CapsuleShape3D)_collider.Shape.Duplicate();
		_collider.Shape = _capsule;
		_standHeight = _capsule.Height;

		// Fall back to a child node so clamber works without inspector wiring.
		Clamber ??= GetNodeOrNull<ClamberController>("ClamberController");
		// Run after the StateMachine child, so MoveAndSlide applies the velocity the states just
		// wrote. Input sampled here is consumed by the states on the next tick — one frame of
		// latency, invisible for movement and harmless for the latched jump edge.
		ProcessPhysicsPriority = 1;
		Input.MouseMode = Input.MouseModeEnum.Captured;
	}

	public override void _UnhandledInput(InputEvent @event)
	{
		if (@event is InputEventMouseMotion motion && Input.MouseMode == Input.MouseModeEnum.Captured)
		{
			RotateY(-motion.Relative.X * MouseSensitivity);
			// Body yaw is applied to this node; pitch is only recorded. CameraController is the sole
			// writer of the camera's rotation, so look, roll and punch compose in one place.
			LookPitch = Mathf.Clamp(LookPitch - motion.Relative.Y * MouseSensitivity, -1.5f, 1.5f);
		}

	}

	public void Jump() => Velocity = Velocity with { Y = JumpVelocity };

	public override void _PhysicsProcess(double delta)
	{
		SampleInput();
		ApplyCrouchHeight();
		MoveAndSlide();
	}

	// The capsule shortens by exactly as much as the eye dropped, and slides down half that so its
	// bottom -- the feet -- stays put. Shrinking around the centre instead would lift the player off
	// the floor at the start of every crouch.
	private void ApplyCrouchHeight()
	{
		var drop = Camera.CrouchOffset;
		_capsule.Height = _standHeight - drop;
		_collider.Position = _collider.Position with { Y = -drop / 2f };
		// Clamber reach follows the same height, so the ledges in range shrink with the crouch.
		if (Clamber is not null) Clamber.HeightScale = _capsule.Height / _standHeight;
	}

	private void SampleInput()
	{
		var jump = Input.IsPhysicalKeyPressed(Key.Space);
		// ponytail: edge detected by polling, so a tap shorter than one physics frame (~16ms)
		// is lost. Swap for an InputMap action and Input.IsActionJustPressed if that ever bites.
		JumpPressed = jump && !_jumpHeld;
		_jumpHeld = jump;

		// ponytail: raw key reads, no InputMap actions. Swap for Input.GetVector
		// with named actions when you want rebindable controls or gamepad support.
		var sprint = Input.IsPhysicalKeyPressed(Key.Shift);
		if (!sprint) SprintArmed = false;
		else if (!_sprintHeld) SprintArmed = true;
		_sprintHeld = sprint;

		var crouch = Input.IsPhysicalKeyPressed(Key.C);
		if (crouch && !_crouchHeld) CrouchToggled = !CrouchToggled;
		_crouchHeld = crouch;

		MoveInput = new Vector2(
			(Input.IsPhysicalKeyPressed(Key.D) ? 1 : 0) - (Input.IsPhysicalKeyPressed(Key.A) ? 1 : 0),
			(Input.IsPhysicalKeyPressed(Key.S) ? 1 : 0) - (Input.IsPhysicalKeyPressed(Key.W) ? 1 : 0));

		// Started here, not in a transition guard: guards must be side-effect free and
		// TryStartClamber commits. A fresh press always tries; while airborne a held key keeps
		// trying, so you can jump into a ledge and mantle the moment it comes in reach.
		if (Clamber is { IsClambering: false } && (JumpPressed || (jump && !IsOnFloor())))
			Clamber.TryStartClamber();
	}
}

using FirstPerson.Helpers;
using Godot;

public partial class PlayerController : CharacterBody3D
{
	// Metres per second on the ground at a walk. The movement states scale off this rather than
	// carrying their own absolute speeds, so sprint and crouch stay in proportion when it is tuned.
	[Export] public float Speed = 5.0f;
	[Export] public float JumpVelocity = 4.5f;        // metres per second, upward, applied on the jump frame
	[Export] public float MouseSensitivity = 0.003f;  // radians of look per pixel of mouse movement

	// Both optional and both fall back to a lookup in _Ready -- Clamber to a child node, Weapon to a
	// HitscanComponent on this GameObject. Wire them only to override that.
	[Export] public ClamberController Clamber;
	[Export] public HitscanComponent Weapon;

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

	// Set by Jump(), reset by GroundedState on landing. Lets GroundedState tell a jump-caused
	// departure from the floor apart from an unjumped one -- CoyoteState's grace window only
	// exists on the second kind, and this is what makes it structurally unreachable after a real
	// jump (no double jump).
	public bool JumpedThisAirborne { get; set; }

	private CollisionShape3D _collider;
	private CapsuleShape3D _capsule;
	private float _standHeight;

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
		Weapon ??= Component.Get<HitscanComponent>(this);
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

	public void Jump()
	{
		Velocity = Velocity with { Y = JumpVelocity };
		JumpedThisAirborne = true;
	}

	public override void _PhysicsProcess(double delta)
	{
		SampleInput();
		ApplyCrouchHeight();
		// Before MoveAndSlide, not after: this lifts the body above a step so the horizontal
		// velocity already sampled this tick glides onto it, rather than reacting to a move that
		// already happened.
		Clamber?.TryStepUp(delta);
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
		JumpPressed = Input.IsActionJustPressed("jump");
		var jumpHeld = Input.IsActionPressed("jump");

		var sprint = Input.IsActionPressed("sprint");
		if (!sprint) SprintArmed = false;
		else if (Input.IsActionJustPressed("sprint")) SprintArmed = true;

		if (Input.IsActionJustPressed("crouch")) CrouchToggled = !CrouchToggled;

		if (Weapon is not null) Weapon.Firing = Input.IsActionPressed("fire");

		MoveInput = Input.GetVector("move_left", "move_right", "move_forward", "move_back");

		// Started here, not in a transition guard: guards must be side-effect free and
		// TryStartClamber commits. A fresh press always tries; while airborne a held key keeps
		// trying, so you can jump into a ledge and mantle the moment it comes in reach.
		if (Clamber is { IsClambering: false } && (JumpPressed || (jumpHeld && !IsOnFloor())))
			Clamber.TryStartClamber();
	}
}

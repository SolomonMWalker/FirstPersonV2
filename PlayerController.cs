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

	private Camera3D _camera;
	private bool _jumpHeld;

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
		_camera = GetNode<Camera3D>("Camera3D");
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
			_camera.Rotation = _camera.Rotation with
			{
				X = Mathf.Clamp(_camera.Rotation.X - motion.Relative.Y * MouseSensitivity, -1.5f, 1.5f)
			};
		}

	}

	public void Jump() => Velocity = Velocity with { Y = JumpVelocity };

	public override void _PhysicsProcess(double delta)
	{
		SampleInput();
		MoveAndSlide();
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

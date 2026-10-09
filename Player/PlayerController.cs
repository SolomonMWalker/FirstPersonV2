using Godot;

namespace FirstPerson;

public partial class PlayerController : CharacterBody3D
{
	// Walk speed; other movement states scale off this.
	[Export] public float Speed = 5.0f;
	[Export] public float JumpVelocity = 4.5f;
	[Export] public float MouseSensitivity = 0.003f;  // radians per pixel

	// Defaults to the child ClamberController.
	[Export] public ClamberController Clamber;

	[Export] public RigController Rig;

	[Export] public HitscanComponent HitscanComponent { get; set; }

	public Vector2 MoveInput { get; private set; }
	public bool JumpPressed { get; private set; }
	// Latched until consumed by entering Sprinting; re-armed only by releasing shift.
	public bool SprintArmed { get; set; }

	public bool CrouchToggled { get; set; }

	public CameraController Camera { get; private set; }

	// Kept here, not read off the camera, so impact punch never leaks into aim.
	public float LookPitch { get; private set; }

	// Peak fall speed; MoveAndSlide zeroes Velocity.Y before landing is detected.
	public float FallSpeed { get; set; }

	// Coyote time only applies when the player walked off a ledge rather than jumped.
	public bool JumpedThisAirborne { get; set; }

	private CollisionShape3D _collider;
	private CapsuleShape3D _capsule;
	private float _standHeight;

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
		// Inline .tscn shapes are shared across instances; duplicate before resizing.
		_capsule = (CapsuleShape3D)_collider.Shape.Duplicate();
		_collider.Shape = _capsule;
		_standHeight = _capsule.Height;

		Clamber ??= GetNodeOrNull<ClamberController>("ClamberController");
		Rig.Revolver.OnFired += OnRevolverFired;
		// After the StateMachine, so MoveAndSlide uses the velocity the states just wrote.
		ProcessPhysicsPriority = 1;
		Input.MouseMode = Input.MouseModeEnum.Captured;
	}

	public override void _UnhandledInput(InputEvent @event)
	{
		if (@event is InputEventMouseMotion motion && Input.MouseMode == Input.MouseModeEnum.Captured)
		{
			RotateY(-motion.Relative.X * MouseSensitivity);
			// Pitch is only recorded; CameraController applies it.
			LookPitch = Mathf.Clamp(LookPitch - motion.Relative.Y * MouseSensitivity, -1.5f, 1.5f);
		}
	}

	public void Jump()
	{
		Velocity = Velocity with { Y = JumpVelocity };
		JumpedThisAirborne = true;
	}

	private void OnRevolverFired(float spreadDegrees, int pellets, int damagePerPellet)
	{
		HitscanComponent.FireHitscan(Camera.GlobalTransform, spreadDegrees, pellets, damagePerPellet);
	}

	public override void _PhysicsProcess(double delta)
	{
		SampleInput();
		ApplyCrouchHeight();
		Clamber?.TryStepUp(delta);
		MoveAndSlide();
	}

	// Shrink by the eye drop and shift down half that, so the feet stay planted.
	private void ApplyCrouchHeight()
	{
		var drop = Camera.CrouchOffset;
		_capsule.Height = _standHeight - drop;
		_collider.Position = _collider.Position with { Y = -drop / 2f };
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

		MoveInput = Input.GetVector("move_left", "move_right", "move_forward", "move_back");

		if (Clamber is { IsClambering: false } && (JumpPressed || (jumpHeld && !IsOnFloor())))
			Clamber.TryStartClamber();

		var canUseWeapon = !Clamber.IsClambering;
		Rig.GetInput(
			canUseWeapon && Input.IsActionJustPressed("fire"),
			canUseWeapon && Input.IsActionPressed("aim"),
			canUseWeapon && Input.IsActionJustPressed("reload"));
	}
}

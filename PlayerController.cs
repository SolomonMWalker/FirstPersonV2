using Godot;

public partial class PlayerController : CharacterBody3D
{
	[Export] public float Speed = 5.0f;
	[Export] public float JumpVelocity = 4.5f;
	[Export] public float MouseSensitivity = 0.003f;

	private Camera3D _camera;

	public override void _Ready()
	{
		_camera = GetNode<Camera3D>("Camera3D");
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

	public override void _PhysicsProcess(double delta)
	{
		Vector3 velocity = Velocity;

		if (!IsOnFloor())
			velocity += GetGravity() * (float)delta;
		else if (Input.IsPhysicalKeyPressed(Key.Space))
			velocity.Y = JumpVelocity;

		// ponytail: raw key reads, no InputMap actions. Swap for Input.GetVector
		// with named actions when you want rebindable controls or gamepad support.
		var input = new Vector2(
			(Input.IsPhysicalKeyPressed(Key.D) ? 1 : 0) - (Input.IsPhysicalKeyPressed(Key.A) ? 1 : 0),
			(Input.IsPhysicalKeyPressed(Key.S) ? 1 : 0) - (Input.IsPhysicalKeyPressed(Key.W) ? 1 : 0));

		Vector3 direction = (Transform.Basis * new Vector3(input.X, 0, input.Y)).Normalized();
		velocity.X = direction.X * Speed;
		velocity.Z = direction.Z * Speed;

		Velocity = velocity;
		MoveAndSlide();
	}
}

using Godot;

// Camera movement beyond mouse-look, which still lives in PlayerController._UnhandledInput and only
// touches Rotation. This one only touches Position, so the two never fight.
public partial class CameraController : Camera3D
{
	[Export] public float CrouchDrop = 0.5f;   // how far below stand height the crouched eye sits
	[Export] public float CrouchSpeed = 2.5f;  // metres per second, so the drop takes ~0.2s

	// Set by CrouchingState on enter/exit. The state machine is the source of truth for crouching;
	// this is just the view of it.
	public bool Crouched { get; set; }

	// How far the eye currently is below stand height. PlayerController resizes the collider from
	// this, so the capsule tracks the camera exactly rather than re-deriving the same easing.
	public float CrouchOffset => _standY - Position.Y;

	private float _standY;

	public override void _Ready() => _standY = Position.Y;

	public override void _PhysicsProcess(double delta)
	{
		var target = Crouched ? _standY - CrouchDrop : _standY;
		// MoveToward, not Lerp: constant speed and it actually arrives, so CrouchOffset settles on
		// exactly 0 and the capsule returns to its authored height.
		Position = Position with { Y = Mathf.MoveToward(Position.Y, target, CrouchSpeed * (float)delta) };
	}
}

using Godot;

namespace FirstPerson;

// Owns all camera motion: look pitch, crouch, bob, roll, step smoothing and impact punch.
public partial class CameraController : Camera3D
{
	[Export] public float CrouchDrop = 0.5f;
	[Export] public float CrouchSpeed = 2.5f;  // metres/second

	// Set any of these to 0 to disable that effect.
	[Export] public float BobAmount = 0.02f;  // metres
	[Export] public float BobStride = 3f;     // metres per bob cycle
	[Export] public float BobSway = 0.6f;     // lateral bob as a fraction of vertical
	[Export] public float RollAngle = 0.75f;  // degrees at full strafe speed
	[Export] public float Smoothing = 10f;

	// Landing/recoil kick. Underdamped on purpose so it overshoots and settles.
	[Export] public float PunchSpring = 65f;
	[Export] public float PunchDamping = 9f;
	[Export] public float MaxPunch = 8f;  // degrees

	// Eases out the one-tick Y pop from stairs and kerbs. Rate 0 disables.
	[Export] public float StepSmoothRate = 2.5f;  // metres/second
	[Export] public float MaxStepLag = 0.35f;

	public bool Crouched { get; set; }

	// Excludes bob and step lag so the collider doesn't resize while walking.
	public float CrouchOffset => _standY - _eyeY;

	private PlayerController _player;
	private float _standX, _standY;
	// Eye height after crouch easing, before bob and step lag.
	private float _eyeY;
	private float _bobPhase;
	private float _bobAmp;
	private float _roll;
	// Radians; X = pitch, Y = roll.
	private Vector2 _punch;
	private Vector2 _punchVelocity;

	// Kept out of _eyeY so steps never resize the collider.
	private float _stepLag;
	private float _lastBodyY;
	private float _lastEyeY;
	private bool _wasGrounded;

	// Degrees. Positive pitch tips the view up (opposite of Quake/Source).
	public void AddPunch(float pitchDegrees, float rollDegrees) =>
		_punch += new Vector2(Mathf.DegToRad(pitchDegrees), Mathf.DegToRad(rollDegrees));

	public override void _Ready()
	{
		_player = PlayerController.Of(this);
		_standX = Position.X;
		_standY = _eyeY = _lastEyeY = Position.Y;
		_lastBodyY = _player.GlobalPosition.Y;

		// After PlayerController's MoveAndSlide (priority 1), so steps are seen the tick they happen.
		ProcessPhysicsPriority = 2;
	}

	public override void _PhysicsProcess(double delta)
	{
		var d = (float)delta;

		var crouchTarget = Crouched ? _standY - CrouchDrop : _standY;
		_eyeY = Mathf.MoveToward(_eyeY, crouchTarget, CrouchSpeed * d);

		var velocity = _player.Velocity with { Y = 0 };
		var speed = velocity.Length();
		var grounded = _player.IsOnFloor();

		// Phase follows distance travelled, not time.
		if (grounded) _bobPhase = Mathf.Wrap(_bobPhase + speed * d / BobStride * Mathf.Tau, 0f, Mathf.Tau);

		var smoothingWeight = 1f - Mathf.Exp(-Smoothing * d);

		var targetBobAmp = grounded ? BobAmount * Mathf.Min(speed / _player.Speed, 1f) : 0f;
		_bobAmp = Mathf.Lerp(_bobAmp, targetBobAmp, smoothingWeight);

		StepSmooth(d, grounded);

		// Vertical at twice the lateral frequency traces a walking figure-8.
		Position = Position with
		{
			X = _standX + Mathf.Sin(_bobPhase) * _bobAmp * BobSway,
			Y = _eyeY + Mathf.Sin(_bobPhase * 2f) * _bobAmp + _stepLag,
		};

		// Roll tracked in _roll, not read from Rotation.Z, which jitters near gimbal lock.
		var sidewaysFraction = Mathf.Clamp(velocity.Dot(_player.GlobalBasis.X) / _player.Speed, -1f, 1f);
		_roll = Mathf.Lerp(_roll, Mathf.DegToRad(-RollAngle) * sidewaysFraction, smoothingWeight);

		StepPunch(d);

		Rotation = new Vector3(_player.LookPitch + _punch.X, 0f, _roll + _punch.Y);
	}

	// Port of Quake's stair smoothing, with Source's step-down support.
	private void StepSmooth(float d, bool grounded)
	{
		var bodyY = _player.GlobalPosition.Y;
		var rise = bodyY - _lastBodyY;
		_lastBodyY = bodyY;

		// A crouch is deliberate eye movement, not a step to absorb.
		var eyeMoved = !Mathf.IsEqualApprox(_eyeY, _lastEyeY);
		_lastEyeY = _eyeY;

		// Grounded on both ticks, so the last tick of a fall isn't absorbed as a step.
		var smoothing = grounded && _wasGrounded && !eyeMoved && StepSmoothRate > 0f
		                && _player.Clamber is not { IsClambering: true };
		_wasGrounded = grounded;

		if (!smoothing)
		{
			_stepLag = 0f;
			return;
		}

		// Clamped so a teleport can't drag the eye underground.
		_stepLag = Mathf.Clamp(_stepLag - rise, -MaxStepLag, MaxStepLag);
		_stepLag = Mathf.MoveToward(_stepLag, 0f, StepSmoothRate * d);
	}

	// Damped spring, snapped to zero once negligible.
	private void StepPunch(float d)
	{
		if (_punch.LengthSquared() < 1e-8f && _punchVelocity.LengthSquared() < 1e-8f)
		{
			_punch = _punchVelocity = Vector2.Zero;
			return;
		}

		_punchVelocity -= _punch * Mathf.Clamp(PunchSpring * d, 0f, 2f);
		_punchVelocity *= Mathf.Max(1f - PunchDamping * d, 0f);
		_punch += _punchVelocity * d;

		var limit = Mathf.DegToRad(MaxPunch);
		_punch = new Vector2(Mathf.Clamp(_punch.X, -limit, limit), Mathf.Clamp(_punch.Y, -limit, limit));
	}
}

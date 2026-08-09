using Godot;

// Every bit of camera motion. PlayerController handles input and turns the body's yaw; it records
// look pitch but never writes this node, so look, strafe roll and impact punch all compose here and
// cannot fight each other.
public partial class CameraController : Camera3D
{
	[Export] public float CrouchDrop = 0.5f;   // how far below stand height the crouched eye sits
	[Export] public float CrouchSpeed = 2.5f;  // metres per second, so the drop takes ~0.2s

	// Head bob and the roll that leans into a strafe. Every one of these disables its channel at 0
	// — motion sickness is the most common complaint about this effect, so it stays switchable.
	[Export] public float BobAmount = 0.045f;  // metres, peak vertical rise at full speed
	[Export] public float BobStride = 1.5f;    // metres travelled per full bob cycle
	[Export] public float BobSway = 0.6f;      // lateral bob as a fraction of the vertical
	[Export] public float RollAngle = 2.0f;    // degrees of roll at full sideways speed
	[Export] public float Smoothing = 8f;      // how fast roll and bob amplitude chase their targets

	// Impact punch — the angular kick from landing and (later) taking damage. Source's constants,
	// which are per-second against a delta multiply and so port over with no unit conversion.
	// PunchDamping is low enough that the spring overshoots on purpose: the head dips, rebounds a
	// little past level and settles, and that rebound is what makes a landing feel like one.
	[Export] public float PunchSpring = 65f;
	[Export] public float PunchDamping = 9f;
	[Export] public float MaxPunch = 8f;       // degrees, hard clamp so a long fall cannot flip the view

	// Step smoothing. The body's Y jumps in a single tick when the capsule rolls over a kerb or a
	// stair tread; the eye is held where it was in world space and reeled back in at a constant rate,
	// turning a one-frame discontinuity into ~0.14s of motion. Quake's `oldz` from V_CalcRefdef,
	// expressed as a lag rather than an absolute because this node is a child of the body.
	// Rate 0 disables the channel, same posture as the bob knobs and as Source's `smoothstairs`.
	[Export] public float StepSmoothRate = 2.5f;  // metres per second the eye catches up
	[Export] public float MaxStepLag = 0.35f;     // furthest the eye may ever trail the body

	// Set by CrouchingState on enter/exit. The state machine is the source of truth for crouching;
	// this is just the view of it.
	public bool Crouched { get; set; }

	// How far the eye currently is below stand height. PlayerController resizes the collider from
	// this, so the capsule tracks the camera exactly rather than re-deriving the same easing.
	// Derived from the eye height alone, never from the live node: bob must not reach the collision
	// shape, or the capsule would resize a few times a second and drag clamber reach along with it.
	public float CrouchOffset => _standY - _eyeY;

	private PlayerController _player;
	private float _standX, _standY;
	// Where the eye sits once the crouch easing has been applied and before bob is added — the
	// standing height, the crouched height, or anywhere between while easing between the two.
	private float _eyeY;
	private float _bobPhase;
	private float _bobAmp;
	private float _roll;
	// Punch displacement and its velocity, both in radians, X = pitch and Y = roll.
	private Vector2 _punch;
	private Vector2 _punchVel;

	// How far the eye currently trails the body: negative while catching up from a step UP (the eye
	// sits below where the body already is), positive after a step down. Added to Position.Y at the
	// last moment and deliberately never folded into _eyeY -- CrouchOffset is derived from _eyeY and
	// resizes the collision capsule, so walking over a kerb must not touch the collider.
	private float _stepLag;
	private float _lastBodyY;
	private float _lastEyeY;
	private bool _wasGrounded;

	// For tests. The guards are the part of this that regresses silently.
	public float StepLag => _stepLag;

	// One impulse channel for every impact. Landing calls it with pitch; damage adds roll from the
	// hit direction; weapon recoil, when there is a weapon, calls the same thing. Kept as a single
	// system on purpose — separate landing and damage effects would fight over the view.
	//
	// Angles are in Godot's convention, NOT the reference engines': positive pitch tips the view UP
	// (recoil), negative tips it DOWN (a landing nod). Quake and Source both count positive pitch as
	// looking down, so their constants invert on the way in — see GroundedState.
	public void AddPunch(float pitchDegrees, float rollDegrees) =>
		_punch += new Vector2(Mathf.DegToRad(pitchDegrees), Mathf.DegToRad(rollDegrees));

	// Direction is player -> attacker in world space, as in Quake's V_ParseDamage. Pitch comes from
	// the forward component and roll from the sideways one, so the kick points away from the hit and
	// the player can feel where it came from without looking at a HUD.
	public void AddDamagePunch(Vector3 toAttacker, float pitchScale, float rollScale)
	{
		var flat = (toAttacker with { Y = 0 }).Normalized();
		// Both negated out of Quake's convention: it counts positive pitch as down, and its positive
		// roll is the opposite hand from Rotation.Z. A literal port reverses both.
		AddPunch(-flat.Dot(-_player.GlobalBasis.Z) * pitchScale, -flat.Dot(_player.GlobalBasis.X) * rollScale);
	}

	public override void _Ready()
	{
		_player = PlayerController.Of(this);
		_standX = Position.X;
		_standY = _eyeY = _lastEyeY = Position.Y;
		_lastBodyY = _player.GlobalPosition.Y;
		// After PlayerController's MoveAndSlide, which runs at priority 1. Step smoothing has to read
		// GlobalPosition once the body has already moved, or every step is detected a tick late and
		// the one frame that actually shows the pop is the frame rendered unsmoothed.
		// Side effect, deliberate: bob and roll now read post-slide velocity, so walking into a wall
		// stops the bob instead of bobbing on a velocity the slide threw away.
		ProcessPhysicsPriority = 2;
	}

	public override void _PhysicsProcess(double delta)
	{
		var d = (float)delta;

		var crouchTarget = Crouched ? _standY - CrouchDrop : _standY;
		// MoveToward, not Lerp: constant speed and it actually arrives, so CrouchOffset settles on
		// exactly 0 and the capsule returns to its authored height.
		_eyeY = Mathf.MoveToward(_eyeY, crouchTarget, CrouchSpeed * d);

		// Y dropped so falling does not inflate the bob, exactly as Quake's V_CalcBob does.
		var velocity = _player.Velocity with { Y = 0 };
		var speed = velocity.Length();
		var grounded = _player.IsOnFloor();

		// Phase advances with distance travelled rather than with time, so steps stay locked to the
		// ground actually covered: speeding up shortens the stride instead of jumping the sine.
		if (grounded) _bobPhase = Mathf.Wrap(_bobPhase + speed * d / BobStride * Mathf.Tau, 0f, Mathf.Tau);

		// Exponential, not Lerp(a, b, rate * delta): this one is genuinely framerate-independent.
		var chase = 1f - Mathf.Exp(-Smoothing * d);

		// Amplitude rides on speed, so bob fades to nothing when standing still and scales with the
		// sprint multiplier for free — nothing here has to ask the state machine anything. Smoothed
		// because the movement states write velocity straight from input with no acceleration, so
		// speed is a step function and the raw amplitude would pop on every key release.
		var wanted = grounded ? BobAmount * Mathf.Min(speed / _player.Speed, 1f) : 0f;
		_bobAmp = Mathf.Lerp(_bobAmp, wanted, chase);

		StepSmooth(d, grounded);

		// Vertical at twice the lateral rate: two footfalls per stride, tracing the figure-8 that
		// makes this read as walking. A single sine on Y alone reads as floating.
		Position = Position with
		{
			X = _standX + Mathf.Sin(_bobPhase) * _bobAmp * BobSway,
			Y = _eyeY + Mathf.Sin(_bobPhase * 2f) * _bobAmp + _stepLag,
		};

		// Quake's V_CalcRoll: how much of the velocity points sideways, ramped to a clamp. Driven by
		// velocity and not by the movement keys, so air control, diagonals and anything else that
		// pushes the body around all lean correctly without knowing this exists.
		// Rolled state is kept here rather than read back off Rotation.Z: getting it decomposes the
		// basis to Euler angles, and mouse-look pitches this node to within a few degrees of gimbal
		// lock, where that decomposition is ill-conditioned enough to jitter the roll.
		var side = Mathf.Clamp(velocity.Dot(_player.GlobalBasis.X) / _player.Speed, -1f, 1f);
		_roll = Mathf.Lerp(_roll, Mathf.DegToRad(-RollAngle) * side, chase);

		StepPunch(d);

		// The one place the camera's rotation is written. Look pitch comes from PlayerController,
		// which only records it, so the punch can never be integrated back into the player's aim.
		Rotation = new Vector3(_player.LookPitch + _punch.X, 0f, _roll + _punch.Y);
	}

	// Quake's stair block, ported. Every guard here maps to one in the original or in Source's
	// SmoothViewOnStairs, and each prevents a specific visible bug rather than being defensive.
	private void StepSmooth(float d, bool grounded)
	{
		// The body's world Y, tracked across ticks. Quake reads ent->origin[2] directly because its
		// view lives in world space; this camera is a child of the body, so a step moves its global
		// Y while its local Position.Y does not change at all. Hence a lag, not an absolute — and a
		// lag of zero is the correct resting state, so nothing needs initialising against the parent.
		var bodyY = _player.GlobalPosition.Y;
		var rise = bodyY - _lastBodyY;
		_lastBodyY = bodyY;

		// Source's m_flOldPlayerViewOffsetZ guard. A crouch is a deliberate eye movement, not a step
		// to be absorbed — without this, pressing C would make the camera sit still.
		var eyeMoved = !Mathf.IsEqualApprox(_eyeY, _lastEyeY);
		_lastEyeY = _eyeY;

		// Grounded on BOTH ticks, not just this one. On the frame a fall ends, `rise` is the whole
		// last tick of the drop, and absorbing that floats the eye ABOVE the body at exactly the
		// moment the landing punch fires. Quake dodges this by only ever smoothing upward; testing
		// two ticks instead also lets a step DOWN smooth, which Quake never did and Source added.
		var smoothing = grounded && _wasGrounded && !eyeMoved && StepSmoothRate > 0f
		                && _player.Clamber is not { IsClambering: true };
		_wasGrounded = grounded;

		// Quake's `else oldz = origin[2]`. Any guard failing clears the lag outright, so stale lag
		// from before a jump can never reappear on landing.
		if (!smoothing)
		{
			_stepLag = 0f;
			return;
		}

		// Hold the eye where it was, clamped so a teleport or a respawn cannot drag it underground,
		// then reel it in. MoveToward and not exponential decay: a residual lag is a permanent
		// eye-height error, and there is nothing anywhere that would clear it.
		_stepLag = Mathf.Clamp(_stepLag - rise, -MaxStepLag, MaxStepLag);
		_stepLag = Mathf.MoveToward(_stepLag, 0f, StepSmoothRate * d);
	}

	// Source's damped torsional spring: a restoring force proportional to displacement, damping that
	// bleeds velocity, and a hard zero at the bottom so the punch actually arrives instead of
	// crawling toward centre forever.
	private void StepPunch(float d)
	{
		if (_punch.LengthSquared() < 1e-8f && _punchVel.LengthSquared() < 1e-8f)
		{
			_punch = _punchVel = Vector2.Zero;
			return;
		}

		_punchVel -= _punch * Mathf.Clamp(PunchSpring * d, 0f, 2f);
		_punchVel *= Mathf.Max(1f - PunchDamping * d, 0f);
		_punch += _punchVel * d;

		var limit = Mathf.DegToRad(MaxPunch);
		_punch = new Vector2(Mathf.Clamp(_punch.X, -limit, limit), Mathf.Clamp(_punch.Y, -limit, limit));
	}
}

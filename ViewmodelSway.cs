using Godot;

// The viewmodel's own motion, on top of everything it already inherits. It sits on the gun mesh,
// which is a real child of the world Camera3D, so look, roll, punch and the camera's own bob arrive
// for free -- this writes nothing but a local offset from the authored rest pose.
//
// Two channels, both switchable at 0 like every channel in CameraController:
//
//   Bob   -- the gun walking the same walk as the eye. It does NOT run its own oscillator; it reads
//            CameraController.BobPhase, which already advances on distance travelled. HL2's move was
//            to take bob off the camera and put it on the gun, and that only reads as one motion if
//            both ends share one phase. Dial CameraController.BobAmount down and BobScale up to go
//            the whole way; leave both and the gun simply bobs a little more than the eye.
//   Sway  -- the gun trailing a turn and catching back up, which is what sells it as a held object
//            rather than a decal on the screen.
public partial class ViewmodelSway : Node3D
{
	// Gun bob as a multiple of the camera's own. Riding CameraController's amplitude rather than a
	// metres value of our own means the speed ramp and the fade to nothing when standing still come
	// along with it -- there is no second set of those knobs to keep in sync.
	[Export] public float BobScale = 1.4f;

	// Metres of lag per radian-per-second of look movement, and the hard clamp on it. A fast flick
	// is several radians a second and would throw the gun off the side of the screen uncapped.
	[Export] public float SwayAmount = 0.015f;
	[Export] public float SwayMax = 0.035f;
	[Export] public float SwaySmoothing = 12f;   // how fast the gun catches back up

	private PlayerController _player;
	private CameraController _camera;
	private Vector3 _rest;
	private Vector3 _sway;
	private float _lastYaw, _lastPitch;

	// For tests, and for the same reason CameraController exposes StepLag: the decay back to rest is
	// the part that regresses silently, because a gun stuck slightly off-centre still looks like a gun.
	public Vector3 Offset => Position - _rest;

	public override void _Ready()
	{
		_player = PlayerController.Of(this);
		_camera = _player.GetNode<CameraController>("Camera3D");

		// The authored pose is the rest pose. Everything below is an offset from it, so moving the
		// gun in the editor stays the ordinary way to move the gun.
		_rest = Position;
		_lastYaw = _player.Rotation.Y;
		_lastPitch = _player.LookPitch;

		// After CameraController at priority 2, which advances the phase this reads. Reading it at
		// priority 0 would bob the gun off last tick's phase -- invisible, but it would put the gun
		// permanently one tick out of step with the eye, which is the one thing sharing a phase is for.
		ProcessPhysicsPriority = 3;
	}

	public override void _PhysicsProcess(double delta)
	{
		var d = (float)delta;

		// AngleDifference, not subtraction: body yaw accumulates through RotateY and wraps past +/-pi,
		// where a raw difference is a full-turn spike and the gun would jump clean off the screen once
		// per revolution. Pitch is clamped to +/-1.5 rad and needs no such care.
		var yaw = _player.Rotation.Y;
		var dYaw = Mathf.AngleDifference(_lastYaw, yaw);
		var dPitch = _player.LookPitch - _lastPitch;
		_lastYaw = yaw;
		_lastPitch = _player.LookPitch;

		// Opposite the turn, because the gun is what gets left behind by it: turning right (yaw
		// falling) drifts the gun to screen-left, looking down lifts it. Divided by delta so the lag
		// measures how fast you turned rather than how long the tick happened to be.
		var wanted = new Vector3(
			Mathf.Clamp(dYaw / d * SwayAmount, -SwayMax, SwayMax),
			Mathf.Clamp(-dPitch / d * SwayAmount, -SwayMax, SwayMax),
			0f);

		// The same exponential chase CameraController uses for roll and bob amplitude, and genuinely
		// framerate-independent for the same reason.
		_sway = _sway.Lerp(wanted, 1f - Mathf.Exp(-SwaySmoothing * d));

		// The camera's figure-8 at the gun's own scale: lateral at the stride rate, vertical at twice
		// it, two footfalls per stride. Reusing the shape and not merely the phase is what keeps the
		// gun reading as part of the same walk instead of as a second thing that happens to wobble.
		var amp = _camera.BobAmp * BobScale;
		var phase = _camera.BobPhase;
		var bob = new Vector3(Mathf.Sin(phase) * amp, Mathf.Sin(phase * 2f) * amp, 0f);

		Position = _rest + _sway + bob;
	}
}



using FirstPerson.CustomTypes.StateMachine;
using Godot;

namespace FirstPerson.PlayerStates;

public partial class GroundedState : AtomicState
{
    // Below this the landing is a hop, not an impact, and punching the view for it would turn the
    // effect into constant noise. Source's own threshold works out at 6.15 m/s here -- a 1.9m fall --
    // which would leave both an ordinary jump (4.5 m/s) and a max-height clamber drop (5.6 m/s)
    // completely silent. Lowered, because this controller is built around jumping and clambering.
    [Export] public float LandPunchThreshold { get; set; } = 4.0f;

    // Degrees the view nods DOWN per metre/second of impact above the threshold. 1.2 puts a 20-foot
    // fall at roughly the 8 degrees Source clamps to. 0 disables the effect.
    [Export] public float LandPunch { get; set; } = 1.2f;

    private PlayerController _player;

    public override void _Ready()
    {
        _player = PlayerController.Of(this);
        // Order matters: a jump-caused departure goes straight to InAir -- JumpedThisAirborne is
        // already true by the time !IsOnFloor() passes, one tick after Jump() sets it (see Jump's
        // comment). Only an unjumped fall falls through to the second guard and gets the Coyote
        // grace window. This is also what keeps the pair from ever flapping: at any instant at
        // most one of the two guards passes.
        AddTransition("InAir", () => !_player.IsOnFloor() && _player.JumpedThisAirborne);
        AddTransition("Coyote", () => !_player.IsOnFloor());
    }

    // Landing. FallSpeed is cleared unconditionally, whether or not it was big enough to punch, so a
    // fall that ended too gently can never leak into the next landing.
    public override void StateEntered()
    {
        base.StateEntered();
        var speed = _player.FallSpeed;
        _player.FallSpeed = 0f;
        // Negative: hitting the ground drives the head down, and Godot counts positive pitch as
        // looking up. Source's constant is positive only because its pitch axis runs the other way.
        if (speed > LandPunchThreshold)
            _player.Camera.AddPunch(-(speed - LandPunchThreshold) * LandPunch, 0f);
        // Every landing starts the next airborne trip clean, whether it came back from InAir or
        // from an unused/expired Coyote window.
        _player.JumpedThisAirborne = false;
    }

    // Jump is applied here rather than as a transition effect. Guards are polled on _Process too,
    // and JumpPressed stays set until the next physics sample — so a jump edge declared as a
    // transition fires repeatedly against a not-yet-updated IsOnFloor() and trips the loop cap.
    // Applying velocity here lets the plain !IsOnFloor() edge move the state one frame later.
    public override void StatePhysicsProcessing(double delta)
    {
        if (_player.JumpPressed) _player.Jump();
    }
}

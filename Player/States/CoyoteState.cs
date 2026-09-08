using FirstPerson.StateMachines;
using Godot;

namespace FirstPerson.PlayerStates;

// The grace window after walking off a ledge, unjumped, during which a jump still fires. Only
// ever entered with PlayerController.JumpedThisAirborne already false -- GroundedState routes a
// jump-caused departure straight to InAir instead -- so this never has to tell the two cases
// apart itself; it just closes the moment either a jump happens here or the timer runs out.
public partial class CoyoteState : AtomicState
{
    // Classic coyote-time window. Short enough not to read as a delayed jump, long enough to
    // forgive the input lag of walking off an edge at speed.
    [Export] public float CoyoteTime { get; set; } = 0.15f;

    private PlayerController _player;
    private float _timer;

    public override void _Ready()
    {
        _player = PlayerController.Of(this);
        AddTransition("Grounded", () => _player.IsOnFloor());
        // Once the grace jump is used, or the window closes, this is plain InAir from here --
        // JumpPressed is ignored there today, which is exactly "no double jump."
        AddTransition("InAir", () => _player.JumpedThisAirborne || _timer <= 0f);
    }

    public override void StateEntered()
    {
        base.StateEntered();
        _timer = CoyoteTime;
    }

    public override void StatePhysicsProcessing(double delta)
    {
        // Same accumulation InAirState does -- coyote time does not pause gravity, it only keeps
        // the jump window open a little past the ledge. Duplicated rather than inherited: every
        // other leaf state resolves PlayerController.Of(this) independently rather than sharing a
        // base beyond AtomicState, and two lines isn't worth being the first exception.
        _player.Velocity += _player.GetGravity() * (float)delta;
        _player.FallSpeed = Mathf.Max(_player.FallSpeed, -_player.Velocity.Y);

        _timer -= (float)delta;
        if (_player.JumpPressed) _player.Jump();
    }
}

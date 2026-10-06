using FirstPerson.StateMachines;
using Godot;

namespace FirstPerson.PlayerStates;

// Grace window after walking off a ledge during which a jump still fires.
public partial class CoyoteState : AtomicState
{
    [Export] public float CoyoteTime { get; set; } = 0.15f;

    private PlayerController _player;
    private float _timer;

    public override void _Ready()
    {
        _player = PlayerController.Of(this);
        AddTransition("Grounded", () => _player.IsOnFloor());
        AddTransition("InAir", () => _player.JumpedThisAirborne || _timer <= 0f);
    }

    public override void StateEntered()
    {
        base.StateEntered();
        _timer = CoyoteTime;
    }

    public override void StatePhysicsProcessing(double delta)
    {
        _player.Velocity += _player.GetGravity() * (float)delta;
        _player.FallSpeed = Mathf.Max(_player.FallSpeed, -_player.Velocity.Y);

        _timer -= (float)delta;
        if (_player.JumpPressed) _player.Jump();
    }
}

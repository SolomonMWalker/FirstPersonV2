using FirstPerson.StateMachines;
using Godot;

namespace FirstPerson.PlayerStates;

public partial class InAirState : AtomicState
{
    private PlayerController _player;

    public override void _Ready()
    {
        _player = PlayerController.Of(this);
        AddTransition("Grounded", () => _player.IsOnFloor());
    }

    public override void StatePhysicsProcessing(double delta)
    {
        _player.Velocity += _player.GetGravity() * (float)delta;
        // Remembered on the way down because it cannot be measured at the bottom: the landing is
        // only detectable after MoveAndSlide has resolved the floor and zeroed Velocity.Y.
        _player.FallSpeed = Mathf.Max(_player.FallSpeed, -_player.Velocity.Y);
    }
}

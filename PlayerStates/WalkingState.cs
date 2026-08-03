using FirstPerson.CustomTypes.StateMachine;
using Godot;

namespace FirstPerson.PlayerStates;

// Horizontal movement. Lives in the MovementState region, so it runs whether grounded or airborne
// — that parallelism is what gives air control for free. Sprinting/Crouching become siblings.
public partial class WalkingState : AtomicState
{
    private PlayerController _player;

    public override void _Ready() => _player = PlayerController.Of(this);

    public override void StatePhysicsProcessing(double delta)
    {
        var input = _player.MoveInput;
        var direction = (_player.Transform.Basis * new Vector3(input.X, 0, input.Y)).Normalized();
        _player.Velocity = _player.Velocity with
        {
            X = direction.X * _player.Speed,
            Z = direction.Z * _player.Speed,
        };
    }
}

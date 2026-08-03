using FirstPerson.CustomTypes.StateMachine;
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
    }
}

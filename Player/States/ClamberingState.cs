using FirstPerson.StateMachines;
using Godot;

namespace FirstPerson.PlayerStates;

// Started by PlayerController; this state only follows IsClambering.
public partial class ClamberingState : AtomicState
{
    private PlayerController _player;

    public override void _Ready()
    {
        _player = PlayerController.Of(this);
        AddTransition("Locomoting", () => !_player.Clamber.IsClambering);
    }

    public override void StateEntered()
    {
        base.StateEntered();
        // The mantle caught the fall; don't punch the view on the next landing.
        _player.FallSpeed = 0f;
    }

    public override void StatePhysicsProcessing(double delta)
    {
        _player.Velocity = _player.Clamber.GetClamberVelocity(delta);
    }

    public override void StateExited()
    {
        base.StateExited();
        // Otherwise we launch off the ledge.
        _player.Velocity = _player.Velocity with { Y = 0f };
    }
}

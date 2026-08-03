using FirstPerson.CustomTypes.StateMachine;
using Godot;

namespace FirstPerson.PlayerStates;

// Sibling of the whole Locomoting region, not a member of it: a clamber suspends gravity, WASD and
// jump together, so entering here must exit both parallel regions rather than one of them.
//
// The clamber is *started* by PlayerController reading input, not by this transition — guards must
// be side-effect free and TryStartClamber commits. This state observes the resulting flag.
public partial class ClamberingState : AtomicState
{
    private PlayerController _player;

    public override void _Ready()
    {
        _player = PlayerController.Of(this);
        AddTransition("Locomoting", () => !_player.Clamber.IsClambering);
    }

    public override void StatePhysicsProcessing(double delta)
    {
        _player.Velocity = _player.Clamber.GetClamberVelocity(delta);
    }

    public override void StateExited()
    {
        base.StateExited();
        // Handoff: drop the clamber's vertical velocity or we launch off the ledge.
        _player.Velocity = _player.Velocity with { Y = 0f };
    }
}

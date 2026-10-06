using FirstPerson.StateMachines;

namespace FirstPerson.PlayerStates;

// Not on the root: it stays active while clambering, so the edge would refire every tick.
public partial class LocomotingState : ParallelState
{
    private PlayerController _player;

    public override void _Ready()
    {
        base._Ready();
        _player = PlayerController.Of(this);
        AddTransition("Clambering", () => _player.Clamber.IsClambering);
    }
}

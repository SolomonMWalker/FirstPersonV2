using FirstPerson.StateMachines;

namespace FirstPerson.PlayerStates;

// The two parallel regions: AirState and MovementState. The edge into Clambering is declared here
// rather than on each leaf — that hoisting is the whole reason for the hierarchy. It cannot live on
// the Player root instead: the root stays active while clambering, so its guard would keep firing
// and self-transition until the machine's loop cap trips.
public partial class LocomotingState : ParallelState
{
    private PlayerController _player;

    public override void _Ready()
    {
        base._Ready();   // ParallelState collects its regions here
        _player = PlayerController.Of(this);
        AddTransition("Clambering", () => _player.Clamber.IsClambering);
    }
}

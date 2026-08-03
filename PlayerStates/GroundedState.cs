using FirstPerson.CustomTypes.StateMachine;
using Godot;

namespace FirstPerson.PlayerStates;

public partial class GroundedState : AtomicState
{
    private PlayerController _player;

    public override void _Ready()
    {
        _player = PlayerController.Of(this);
        // The only edge out. Its guard is the exact complement of InAir's, so the pair can never
        // flap: at any instant exactly one of them passes.
        AddTransition("InAir", () => !_player.IsOnFloor());
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

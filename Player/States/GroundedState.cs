using FirstPerson.StateMachines;
using Godot;

namespace FirstPerson.PlayerStates;

public partial class GroundedState : AtomicState
{
    // Metres/second. Lower than Source's 6.15 so jumps and clamber drops still register.
    [Export] public float LandPunchThreshold { get; set; } = 4.0f;

    // Degrees of downward nod per m/s above the threshold. 0 disables.
    [Export] public float LandPunch { get; set; } = 1.2f;

    private PlayerController _player;

    public override void _Ready()
    {
        _player = PlayerController.Of(this);
        // Order matters: only an unjumped departure falls through to Coyote.
        AddTransition("InAir", () => !_player.IsOnFloor() && _player.JumpedThisAirborne);
        AddTransition("Coyote", () => !_player.IsOnFloor());
    }

    public override void StateEntered()
    {
        base.StateEntered();
        var fallSpeed = _player.FallSpeed;
        _player.FallSpeed = 0f;
        if (fallSpeed > LandPunchThreshold)
            _player.Camera.AddPunch(-(fallSpeed - LandPunchThreshold) * LandPunch, 0f);
        _player.JumpedThisAirborne = false;
    }

    // Not a transition effect: guards also poll on _Process, so it would refire before IsOnFloor updates.
    public override void StatePhysicsProcessing(double delta)
    {
        if (_player.JumpPressed) _player.Jump();
    }
}

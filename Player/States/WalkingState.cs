using FirstPerson.StateMachines;
using Godot;

namespace FirstPerson.PlayerStates;

// Base movement state. Runs in parallel with air states, which gives air control.
public partial class WalkingState : AtomicState
{
    protected PlayerController Player;
    protected virtual float SpeedMultiplier => 1f;

    protected bool WantsSprint => Player.SprintArmed && Player.MoveInput != Vector2.Zero;

    public override void _Ready()
    {
        Player = PlayerController.Of(this);
        AddTransitions();
    }

    protected virtual void AddTransitions()
    {
        AddTransition("Sprinting", () => WantsSprint, () => Player.SprintArmed = false);
        AddTransition("Crouching", () => Player.CrouchToggled);
    }

    public override void StatePhysicsProcessing(double delta)
    {
        var input = Player.MoveInput;
        var direction = (Player.Transform.Basis * new Vector3(input.X, 0, input.Y)).Normalized();
        var speed = Player.Speed * SpeedMultiplier;
        Player.Velocity = Player.Velocity with
        {
            X = direction.X * speed,
            Z = direction.Z * speed,
        };
    }
}

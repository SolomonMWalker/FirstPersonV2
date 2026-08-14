using FirstPerson.CustomTypes.StateMachine;
using Godot;

namespace FirstPerson.PlayerStates;

// Horizontal movement, and the base for the other movement states — they differ only in speed
// multiplier and outgoing edges. Lives in the MovementState region, so it runs whether grounded or
// airborne; that parallelism is what gives air control for free, and it is why jumping and
// clambering leave the walk/sprint/crouch choice alone.
public partial class WalkingState : AtomicState
{
    protected PlayerController Player;
    protected virtual float SpeedMultiplier => 1f;

    // True when sprinting should begin: moving *and* an unconsumed shift press.
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

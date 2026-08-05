using Godot;

namespace FirstPerson.PlayerStates;

// Walking, faster. Ends when the player stops moving or crouches — releasing shift alone does not
// end it, per spec.
public partial class SprintingState : WalkingState
{
    protected override float SpeedMultiplier => 1.4f;

    protected override void AddTransitions()
    {
        AddTransition("Crouching", () => Player.CrouchToggled);
        AddTransition("Walking", () => Player.MoveInput == Vector2.Zero);
    }
}
